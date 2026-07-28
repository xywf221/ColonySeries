using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Landworks
{
    /// <summary>
    /// Tracks only engineered cells. Never scans map.AllCells in steady state.
    /// Rare pulse processes a bounded batch of degrade timers.
    /// </summary>
    public class MapComponent_Landworks : MapComponent
    {
        public float geologicalStress;

        // Remaining ticks until degrade for amended/tilled cells.
        private Dictionary<IntVec3, int> degradeTicks = new Dictionary<IntVec3, int>();

        // All engineered landworks cells that can be hit by backlash (O(1) membership, no full-map scan).
        private HashSet<IntVec3> engineeredCells = new HashSet<IntVec3>();

        // Reused buffers to avoid per-pulse GC.
        private readonly List<IntVec3> keyBuffer = new List<IntVec3>(64);
        private readonly List<StabilizerCache> stabilizerBuffer = new List<StabilizerCache>(8);

        private int nextRareTick;
        private int lastStressLetterTick = -99999;
        private int degradeCursor;

        private struct StabilizerCache
        {
            public IntVec3 center;
            public float radiusSq;
            public float reliefPerPulse;
        }

        public MapComponent_Landworks(Map map) : base(map)
        {
        }

        public static MapComponent_Landworks For(Map map)
        {
            return map?.GetComponent<MapComponent_Landworks>();
        }

        public int EngineeredCount => engineeredCells.Count;
        public int DegradableCount => degradeTicks.Count;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref geologicalStress, "geologicalStress", 0f);
            Scribe_Values.Look(ref nextRareTick, "nextRareTick", 0);
            Scribe_Values.Look(ref lastStressLetterTick, "lastStressLetterTick", -99999);
            Scribe_Values.Look(ref degradeCursor, "degradeCursor", 0);
            Scribe_Collections.Look(ref degradeTicks, "degradeTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref engineeredCells, "engineeredCells", LookMode.Value);
            if (degradeTicks == null)
            {
                degradeTicks = new Dictionary<IntVec3, int>();
            }
            if (engineeredCells == null)
            {
                engineeredCells = new HashSet<IntVec3>();
            }
        }

        public override void MapComponentTick()
        {
            // Fast path: one integer compare most ticks.
            int now = Find.TickManager.TicksGame;
            if (now < nextRareTick)
            {
                return;
            }

            LandworksSettings settings = LandworksMod.Settings;
            int interval = settings != null ? settings.RareTickIntervalTicks : 2500;
            nextRareTick = now + interval;
            TickRare(interval, settings);
        }

        private void TickRare(int intervalTicks, LandworksSettings settings)
        {
            float easy = settings != null ? settings.EasyMult : 1f;
            float decay = (settings != null ? settings.stressDecayPerHour : 0.35f) * easy;
            // interval is in ticks; 2500 ticks ~= 1 in-game hour.
            float hours = intervalTicks / 2500f;
            geologicalStress = Mathf.Max(0f, geologicalStress - decay * hours);

            // Cache active stabilizers once per pulse (tiny list).
            RebuildStabilizerCache(intervalTicks, settings);

            if (degradeTicks.Count == 0)
            {
                MaybeWarnStress();
                return;
            }

            // Snapshot keys into reusable buffer.
            keyBuffer.Clear();
            foreach (IntVec3 cell in degradeTicks.Keys)
            {
                keyBuffer.Add(cell);
            }

            int maxProcess = settings != null ? settings.MaxDegradesPerPulse : 64;
            if (keyBuffer.Count <= maxProcess)
            {
                degradeCursor = 0;
                for (int i = 0; i < keyBuffer.Count; i++)
                {
                    ProcessDegradeCell(keyBuffer[i], intervalTicks);
                }
            }
            else
            {
                // Round-robin large farms so cost stays bounded.
                if (degradeCursor >= keyBuffer.Count)
                {
                    degradeCursor = 0;
                }
                int processed = 0;
                while (processed < maxProcess && keyBuffer.Count > 0)
                {
                    if (degradeCursor >= keyBuffer.Count)
                    {
                        degradeCursor = 0;
                    }
                    ProcessDegradeCell(keyBuffer[degradeCursor], intervalTicks);
                    degradeCursor++;
                    processed++;
                    // keyBuffer is a snapshot; removals from dict are fine, next pulse refreshes.
                    if (processed >= keyBuffer.Count)
                    {
                        break;
                    }
                }
            }

            MaybeWarnStress();
        }

        private void RebuildStabilizerCache(int intervalTicks, LandworksSettings settings)
        {
            stabilizerBuffer.Clear();
            if (LandworksDefOf.LW_SoilStabilizer == null)
            {
                return;
            }

            List<Thing> buildings = map.listerThings.ThingsOfDef(LandworksDefOf.LW_SoilStabilizer);
            if (buildings == null || buildings.Count == 0)
            {
                return;
            }

            float radius = settings != null ? settings.stabilizerRadius : 9.9f;
            float reliefPerDay = settings != null ? settings.stabilizerStressReliefPerDay : 4f;
            float hours = intervalTicks / 2500f;
            float reliefPerPulse = reliefPerDay * (hours / 24f);

            for (int i = 0; i < buildings.Count; i++)
            {
                Thing b = buildings[i];
                CompSoilStabilizer comp = b.TryGetComp<CompSoilStabilizer>();
                if (comp == null || !comp.Active)
                {
                    continue;
                }

                geologicalStress = Mathf.Max(0f, geologicalStress - reliefPerPulse);

                StabilizerCache cache;
                cache.center = b.Position;
                cache.radiusSq = radius * radius;
                cache.reliefPerPulse = reliefPerPulse;
                stabilizerBuffer.Add(cache);
            }
        }

        private bool IsStabilized(IntVec3 cell)
        {
            int n = stabilizerBuffer.Count;
            if (n == 0)
            {
                return false;
            }
            for (int i = 0; i < n; i++)
            {
                StabilizerCache s = stabilizerBuffer[i];
                // LengthHorizontalSquared avoids sqrt.
                if ((cell - s.center).LengthHorizontalSquared <= s.radiusSq)
                {
                    return true;
                }
            }
            return false;
        }

        private void ProcessDegradeCell(IntVec3 cell, int intervalTicks)
        {
            if (!cell.InBounds(map))
            {
                degradeTicks.Remove(cell);
                engineeredCells.Remove(cell);
                return;
            }

            if (!degradeTicks.TryGetValue(cell, out int ticksLeft))
            {
                return;
            }

            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            if (!TerrainUtility.IsDegradable(terrain))
            {
                degradeTicks.Remove(cell);
                SyncEngineeredMembership(cell, terrain);
                return;
            }

            if (IsStabilized(cell))
            {
                return; // pause timer while covered
            }

            ticksLeft -= intervalTicks;
            if (ticksLeft > 0)
            {
                degradeTicks[cell] = ticksLeft;
                return;
            }

            TerrainDef next = TerrainUtility.DegradeTarget(terrain, geologicalStress);
            if (next == null)
            {
                degradeTicks.Remove(cell);
                return;
            }

            TerrainUtility.SetTerrainSilent(map, cell, next);
            SyncEngineeredMembership(cell, next);

            if (TerrainUtility.IsDegradable(next))
            {
                RegisterDegradable(cell, next);
            }
            else
            {
                degradeTicks.Remove(cell);
            }
        }

        public void RegisterPlacedTerrain(IntVec3 cell, TerrainDef def)
        {
            LandworksSettings settings = LandworksMod.Settings;
            float gain = TerrainUtility.StressForTerrain(def);
            if (gain > 0f)
            {
                float mult = (settings != null ? settings.stressGainMultiplier : 1f) * (settings != null ? settings.EasyMult : 1f);
                geologicalStress += gain * mult;
            }

            SyncEngineeredMembership(cell, def);

            if (TerrainUtility.IsDegradable(def))
            {
                RegisterDegradable(cell, def);
            }
            else
            {
                degradeTicks.Remove(cell);
            }
        }

        public void RegisterExcavation(IntVec3 cell)
        {
            LandworksSettings settings = LandworksMod.Settings;
            float amount = settings != null ? settings.excavateStress : 0.6f;
            float mult = (settings != null ? settings.stressGainMultiplier : 1f) * (settings != null ? settings.EasyMult : 1f);
            geologicalStress += amount * mult;
            degradeTicks.Remove(cell);
            // Excavation scar is tracked so backlash can still find scars if desired; scars have 0 stress weight.
            TerrainDef after = map.terrainGrid.TerrainAt(cell);
            SyncEngineeredMembership(cell, after);
        }

        private void SyncEngineeredMembership(IntVec3 cell, TerrainDef def)
        {
            if (TerrainUtility.IsEngineeredTracked(def))
            {
                engineeredCells.Add(cell);
            }
            else
            {
                engineeredCells.Remove(cell);
            }
        }

        private void RegisterDegradable(IntVec3 cell, TerrainDef def)
        {
            LandworksSettings settings = LandworksMod.Settings;
            float days = def == LandworksDefOf.LW_TilledSoil
                ? (settings != null ? settings.tilledSoilLifetimeDays : 5f)
                : (settings != null ? settings.amendedSoilLifetimeDays : 18f);
            days *= settings != null ? settings.EasyLifetimeMult : 1f;
            int ticks = Mathf.Max(1, Mathf.RoundToInt(days * GenDate.TicksPerDay));
            degradeTicks[cell] = ticks;
            engineeredCells.Add(cell);
        }

        public void ForceDegradeRandom(int count, bool toHardpan)
        {
            if (count <= 0 || engineeredCells.Count == 0)
            {
                return;
            }

            keyBuffer.Clear();
            foreach (IntVec3 c in engineeredCells)
            {
                keyBuffer.Add(c);
            }
            TerrainUtility.Shuffle(keyBuffer);

            int changed = 0;
            for (int i = 0; i < keyBuffer.Count && changed < count; i++)
            {
                IntVec3 c = keyBuffer[i];
                if (!c.InBounds(map))
                {
                    engineeredCells.Remove(c);
                    degradeTicks.Remove(c);
                    continue;
                }

                TerrainDef cur = map.terrainGrid.TerrainAt(c);
                if (!TerrainUtility.IsEngineeredTracked(cur))
                {
                    engineeredCells.Remove(c);
                    degradeTicks.Remove(c);
                    continue;
                }

                TerrainDef next = PickBacklashResult(cur, toHardpan);
                TerrainUtility.SetTerrainSilent(map, c, next);
                degradeTicks.Remove(c);
                SyncEngineeredMembership(c, next);
                changed++;
            }

            geologicalStress = Mathf.Max(0f, geologicalStress - changed * 1.5f);
        }

        private static TerrainDef PickBacklashResult(TerrainDef cur, bool toHardpan)
        {
            if (toHardpan)
            {
                return LandworksDefOf.LW_ExhaustedSoil;
            }
            if (cur == LandworksDefOf.LW_HeavyEmbankment || cur == LandworksDefOf.LW_EmbankmentFill)
            {
                return TerrainDefOf.Mud;
            }
            if (cur == LandworksDefOf.LW_DrainedMarsh)
            {
                return TerrainUtility.MarshyTerrain ?? TerrainDefOf.Mud;
            }
            if (cur == LandworksDefOf.LW_AmendedSoil || cur == LandworksDefOf.LW_TilledSoil)
            {
                return LandworksDefOf.LW_ExcavationScar;
            }
            return LandworksDefOf.LW_ExhaustedSoil;
        }

        private void MaybeWarnStress()
        {
            if (geologicalStress < 35f)
            {
                return;
            }
            if (Find.TickManager.TicksGame - lastStressLetterTick < GenDate.TicksPerDay * 5)
            {
                return;
            }
            lastStressLetterTick = Find.TickManager.TicksGame;
            string severity = geologicalStress >= 70f
                ? "LW_StressLetter_Critical".Translate()
                : geologicalStress >= 50f
                    ? "LW_StressLetter_High".Translate()
                    : "LW_StressLetter_Medium".Translate();
            Find.LetterStack.ReceiveLetter(
                "LW_StressLetter_Label".Translate(),
                "LW_StressLetter_Text".Translate(geologicalStress.ToString("F0"), severity),
                geologicalStress >= 50f ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent);
        }

        public float StressChanceFactor()
        {
            float min = LandworksMod.Settings != null ? LandworksMod.Settings.backlashMinStress : 30f;
            // Start ramping a bit below fire threshold.
            float start = Mathf.Max(5f, min - 5f);
            if (geologicalStress < start)
            {
                return 0f;
            }
            return Mathf.Clamp01((geologicalStress - start) / 80f);
        }
    }
}
