using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Fieldcraft
{
    /// <summary>
    /// Soil budget ledger. Only tracks cells that need it — no map.AllCells scans.
    /// Phase 2: fallow recharge, mono-crop memory, paddy-aware inspect.
    /// </summary>
    public class MapComponent_Fieldcraft : MapComponent
    {
        private Dictionary<IntVec3, float> budgets = new Dictionary<IntVec3, float>();
        private Dictionary<IntVec3, int> zeroBudgetTicks = new Dictionary<IntVec3, int>();
        private HashSet<IntVec3> fallowCells = new HashSet<IntVec3>();
        // Last harvested crop defName for simple mono-crop penalty.
        private Dictionary<IntVec3, string> lastCrop = new Dictionary<IntVec3, string>();

        private readonly List<IntVec3> keyBuffer = new List<IntVec3>(64);
        private readonly List<IntVec3> fallowBuffer = new List<IntVec3>(32);
        private int nextRareTick;
        private int pulseCursor;

        public MapComponent_Fieldcraft(Map map) : base(map)
        {
        }

        public static MapComponent_Fieldcraft For(Map map)
        {
            return map?.GetComponent<MapComponent_Fieldcraft>();
        }

        public int TrackedCount => budgets.Count;
        public int FallowCount => fallowCells.Count;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref nextRareTick, "nextRareTick", 0);
            Scribe_Values.Look(ref pulseCursor, "pulseCursor", 0);
            Scribe_Collections.Look(ref budgets, "budgets", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref zeroBudgetTicks, "zeroBudgetTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref fallowCells, "fallowCells", LookMode.Value);
            Scribe_Collections.Look(ref lastCrop, "lastCrop", LookMode.Value, LookMode.Value);
            if (budgets == null)
            {
                budgets = new Dictionary<IntVec3, float>();
            }
            if (zeroBudgetTicks == null)
            {
                zeroBudgetTicks = new Dictionary<IntVec3, int>();
            }
            if (fallowCells == null)
            {
                fallowCells = new HashSet<IntVec3>();
            }
            if (lastCrop == null)
            {
                lastCrop = new Dictionary<IntVec3, string>();
            }
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now < nextRareTick)
            {
                return;
            }

            FieldcraftSettings settings = FieldcraftMod.Settings;
            int interval = settings != null ? settings.RareTickIntervalTicks : 2500;
            nextRareTick = now + interval;
            TickRare(interval, settings);
        }

        private void TickRare(int intervalTicks, FieldcraftSettings settings)
        {
            // Fallow cells must be processed even if not yet in budgets.
            if (fallowCells.Count > 0)
            {
                fallowBuffer.Clear();
                foreach (IntVec3 c in fallowCells)
                {
                    fallowBuffer.Add(c);
                }
                for (int i = 0; i < fallowBuffer.Count; i++)
                {
                    IntVec3 c = fallowBuffer[i];
                    if (!c.InBounds(map))
                    {
                        fallowCells.Remove(c);
                        continue;
                    }
                    EnsureTracked(c);
                }
            }

            if (budgets.Count == 0)
            {
                return;
            }

            float leakPerDay = (settings != null ? settings.naturalLeakPerDay : 0.8f)
                               * (settings != null ? settings.EasyLeakMult : 1f);
            float fallowPerDay = settings != null ? settings.fallowRechargePerDay : 4f;
            if (settings != null && settings.easyMode)
            {
                fallowPerDay *= 1.35f;
            }
            float days = intervalTicks / (float)GenDate.TicksPerDay;
            float leak = leakPerDay * days;
            float fallowGain = fallowPerDay * days;

            keyBuffer.Clear();
            foreach (IntVec3 c in budgets.Keys)
            {
                keyBuffer.Add(c);
            }

            int maxProcess = settings != null ? settings.MaxCellsPerPulse : 64;
            int count = keyBuffer.Count;
            if (count <= maxProcess)
            {
                pulseCursor = 0;
                for (int i = 0; i < count; i++)
                {
                    ProcessCell(keyBuffer[i], leak, fallowGain, intervalTicks, settings);
                }
            }
            else
            {
                if (pulseCursor >= count)
                {
                    pulseCursor = 0;
                }
                int processed = 0;
                while (processed < maxProcess && processed < count)
                {
                    if (pulseCursor >= keyBuffer.Count)
                    {
                        pulseCursor = 0;
                    }
                    ProcessCell(keyBuffer[pulseCursor], leak, fallowGain, intervalTicks, settings);
                    pulseCursor++;
                    processed++;
                }
            }
        }

        private void ProcessCell(IntVec3 cell, float leak, float fallowGain, int intervalTicks, FieldcraftSettings settings)
        {
            if (!cell.InBounds(map))
            {
                budgets.Remove(cell);
                zeroBudgetTicks.Remove(cell);
                fallowCells.Remove(cell);
                lastCrop.Remove(cell);
                return;
            }

            TerrainDef terrain = map.terrainGrid.TerrainAt(cell);
            bool trackNatural = settings == null || settings.trackNaturalSoil;
            if (!LandworksBridge.IsBudgetEligible(terrain, trackNatural))
            {
                budgets.Remove(cell);
                zeroBudgetTicks.Remove(cell);
                fallowCells.Remove(cell);
                lastCrop.Remove(cell);
                return;
            }

            if (!budgets.TryGetValue(cell, out float budget))
            {
                return;
            }

            bool fallow = fallowCells.Contains(cell);
            bool hasPlant = cell.GetPlant(map) != null;

            // Marked fallow but still has a plant: no recharge until cleared.
            if (fallow && !hasPlant && fallowGain > 0f)
            {
                budget = Mathf.Min(SoilBudgetUtility.MaxBudget, budget + fallowGain);
                budgets[cell] = budget;
                zeroBudgetTicks.Remove(cell);
                return;
            }

            if (leak > 0f && budget > 0f)
            {
                float cellLeak = hasPlant ? leak : leak * 0.35f;
                // Flooded paddy leaks slightly less (water holds organics).
                if (terrain == FieldcraftDefOf.FC_PaddyFlooded)
                {
                    cellLeak *= 0.7f;
                }
                if (LandworksBridge.Active && LandworksBridge.IsLandworksEngineered(terrain))
                {
                    float stress = LandworksBridge.GetGeologicalStress(map);
                    if (stress > 40f)
                    {
                        cellLeak *= 1f + (stress - 40f) / 100f;
                    }
                }
                budget = Mathf.Max(0f, budget - cellLeak);
                budgets[cell] = budget;
            }

            // Unmaintained flooded paddy slowly dries out when budget very low.
            if (terrain == FieldcraftDefOf.FC_PaddyFlooded && budget < 15f && FieldcraftDefOf.FC_PaddyDry != null)
            {
                if (Rand.Chance(0.08f))
                {
                    LandworksBridge.SetTerrainSilent(map, cell, FieldcraftDefOf.FC_PaddyDry);
                }
            }

            if (budget <= 0.01f)
            {
                int z = 0;
                zeroBudgetTicks.TryGetValue(cell, out z);
                z += intervalTicks;
                zeroBudgetTicks[cell] = z;

                float exhaustDays = settings != null ? settings.zeroBudgetExhaustDays : 3f;
                int need = Mathf.RoundToInt(exhaustDays * GenDate.TicksPerDay);

                if (LandworksBridge.IsDegradableLandworks(terrain))
                {
                    LandworksBridge.AccelerateDegrade(map, cell, 2.5f);
                }

                if (z >= need)
                {
                    TryExhaustCell(cell, terrain);
                    zeroBudgetTicks.Remove(cell);
                }
            }
            else
            {
                zeroBudgetTicks.Remove(cell);
            }
        }

        private void TryExhaustCell(IntVec3 cell, TerrainDef terrain)
        {
            // Flooded/dry paddy collapses to scar or hardpan if available.
            if (terrain == FieldcraftDefOf.FC_PaddyFlooded || terrain == FieldcraftDefOf.FC_PaddyDry)
            {
                TerrainDef hardpan = LandworksBridge.ExhaustedTerrain;
                if (hardpan != null)
                {
                    LandworksBridge.SetTerrainSilent(map, cell, hardpan);
                    budgets[cell] = 5f;
                    return;
                }
                if (FieldcraftDefOf.FC_PaddyDry != null && terrain == FieldcraftDefOf.FC_PaddyFlooded)
                {
                    LandworksBridge.SetTerrainSilent(map, cell, FieldcraftDefOf.FC_PaddyDry);
                    budgets[cell] = 10f;
                    return;
                }
            }

            TerrainDef lwHardpan = LandworksBridge.ExhaustedTerrain;
            if (lwHardpan != null && (LandworksBridge.IsDegradableLandworks(terrain)
                                    || terrain == LandworksBridge.ReclaimedSoil
                                    || terrain == TerrainDefOf.SoilRich))
            {
                LandworksBridge.SetTerrainSilent(map, cell, lwHardpan);
                budgets[cell] = 5f;
                return;
            }

            budgets[cell] = 0f;
        }

        public float GetBudget(IntVec3 cell)
        {
            if (budgets.TryGetValue(cell, out float b))
            {
                return b;
            }
            return -1f;
        }

        public bool TryGetBudget(IntVec3 cell, out float budget)
        {
            return budgets.TryGetValue(cell, out budget);
        }

        public bool IsFallow(IntVec3 cell)
        {
            return fallowCells.Contains(cell);
        }

        public void SetFallow(IntVec3 cell, bool fallow)
        {
            if (!cell.InBounds(map))
            {
                return;
            }
            if (fallow)
            {
                fallowCells.Add(cell);
                EnsureTracked(cell);
            }
            else
            {
                fallowCells.Remove(cell);
            }
        }

        public void ToggleFallow(IntVec3 cell)
        {
            SetFallow(cell, !IsFallow(cell));
        }

        public string GetLastCrop(IntVec3 cell)
        {
            if (lastCrop.TryGetValue(cell, out string name))
            {
                return name;
            }
            return null;
        }

        public void EnsureTracked(IntVec3 cell, TerrainDef terrain = null)
        {
            if (!cell.InBounds(map))
            {
                return;
            }
            if (budgets.ContainsKey(cell))
            {
                return;
            }
            terrain = terrain ?? map.terrainGrid.TerrainAt(cell);
            bool trackNatural = FieldcraftMod.Settings == null || FieldcraftMod.Settings.trackNaturalSoil;
            if (!LandworksBridge.IsBudgetEligible(terrain, trackNatural))
            {
                return;
            }
            budgets[cell] = LandworksBridge.InitialBudgetFor(terrain);
        }

        public void OnTerrainChanged(IntVec3 cell, TerrainDef newTerr)
        {
            bool trackNatural = FieldcraftMod.Settings == null || FieldcraftMod.Settings.trackNaturalSoil;
            if (!LandworksBridge.IsBudgetEligible(newTerr, trackNatural))
            {
                budgets.Remove(cell);
                zeroBudgetTicks.Remove(cell);
                fallowCells.Remove(cell);
                lastCrop.Remove(cell);
                return;
            }

            float initial = LandworksBridge.InitialBudgetFor(newTerr);
            if (budgets.TryGetValue(cell, out float old))
            {
                budgets[cell] = Mathf.Clamp(Mathf.Max(old * 0.5f, initial), 0f, SoilBudgetUtility.MaxBudget);
            }
            else
            {
                budgets[cell] = initial;
            }
            zeroBudgetTicks.Remove(cell);
        }

        public void ApplyHarvestDrain(IntVec3 cell, ThingDef plantDef)
        {
            EnsureTracked(cell);
            if (!budgets.TryGetValue(cell, out float budget))
            {
                return;
            }

            // Green manure / plow-in crops restore instead of drain.
            if (SoilBudgetUtility.IsGreenManure(plantDef))
            {
                float restore = SoilBudgetUtility.GreenManureRestoreFor(plantDef);
                budgets[cell] = Mathf.Min(SoilBudgetUtility.MaxBudget, budget + restore);
                zeroBudgetTicks.Remove(cell);
                fallowCells.Remove(cell);
                lastCrop[cell] = plantDef.defName;
                return;
            }

            float drain = SoilBudgetUtility.HarvestDrainFor(plantDef);

            // Mono-crop penalty: same crop as last harvest.
            FieldcraftSettings settings = FieldcraftMod.Settings;
            bool mono = settings == null || settings.monoCropPenalty;
            if (mono && lastCrop.TryGetValue(cell, out string prev) && prev == plantDef.defName)
            {
                float penalty = settings != null ? settings.monoCropDrainBonus : 0.35f;
                drain *= 1f + penalty;
            }

            budget = Mathf.Max(0f, budget - drain);
            budgets[cell] = budget;
            lastCrop[cell] = plantDef.defName;
            // Harvesting clears fallow intent.
            fallowCells.Remove(cell);

            float chance = settings != null ? settings.residueReturnChance : 0.35f;
            if (Rand.Chance(chance))
            {
                float ret = settings != null ? settings.residueBudgetReturn : 6f;
                budgets[cell] = Mathf.Min(SoilBudgetUtility.MaxBudget, budgets[cell] + ret);

                if (settings != null && settings.residueMaySpawnCompost && Rand.Chance(0.2f))
                {
                    ThingDef compost = LandworksBridge.CompostDef;
                    if (compost != null)
                    {
                        Thing t = ThingMaker.MakeThing(compost);
                        t.stackCount = 1;
                        GenPlace.TryPlaceThing(t, cell, map, ThingPlaceMode.Near);
                    }
                }
            }
        }

        public bool TryTopdress(IntVec3 cell, float amount)
        {
            EnsureTracked(cell);
            if (!budgets.TryGetValue(cell, out float budget))
            {
                return false;
            }
            budgets[cell] = Mathf.Min(SoilBudgetUtility.MaxBudget, budget + amount);
            zeroBudgetTicks.Remove(cell);

            TerrainDef t = map.terrainGrid.TerrainAt(cell);
            if (LandworksBridge.IsDegradableLandworks(t))
            {
                int ticks = Mathf.RoundToInt(10f * GenDate.TicksPerDay);
                LandworksBridge.RefreshDegradeTimer(map, cell, ticks);
            }

            if (t == LandworksBridge.ExhaustedTerrain && LandworksBridge.ReclaimedSoil != null && budgets[cell] >= 60f)
            {
                LandworksBridge.SetTerrainSilent(map, cell, LandworksBridge.ReclaimedSoil);
                budgets[cell] = 55f;
            }

            return true;
        }

        public bool TryPlowIn(IntVec3 cell, Thing thing)
        {
            Plant plant = thing as Plant;
            if (plant?.def == null)
            {
                return false;
            }
            EnsureTracked(cell);
            if (!budgets.TryGetValue(cell, out float budget))
            {
                return false;
            }

            float restore = SoilBudgetUtility.PlowInRestoreFor(plant.def, plant.Growth);
            budgets[cell] = Mathf.Min(SoilBudgetUtility.MaxBudget, budget + restore);
            zeroBudgetTicks.Remove(cell);
            fallowCells.Remove(cell);
            lastCrop[cell] = plant.def.defName;

            // Destroy plant without normal harvest product.
            if (!plant.Destroyed)
            {
                plant.Destroy(DestroyMode.KillFinalize);
            }

            if (Rand.Chance(0.35f))
            {
                FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Dirt, 1);
            }
            return true;
        }

        public float GrowthFactorAt(IntVec3 cell)
        {
            if (!budgets.TryGetValue(cell, out float budget))
            {
                return 1f;
            }
            return SoilBudgetUtility.GrowthFactorFromBudget(budget);
        }

        public string InspectExtra(IntVec3 cell)
        {
            if (!TryGetBudget(cell, out float budget))
            {
                return null;
            }
            string s = "FC_Inspect_Budget".Translate(budget.ToString("F0"), SoilBudgetUtility.BudgetLabel(budget));
            if (IsFallow(cell))
            {
                s += "\n" + "FC_Inspect_Fallow".Translate();
            }
            if (lastCrop.TryGetValue(cell, out string crop) && !crop.NullOrEmpty())
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(crop);
                string label = def != null ? def.label : crop;
                s += "\n" + "FC_Inspect_LastCrop".Translate(label);
            }
            TerrainDef t = map.terrainGrid.TerrainAt(cell);
            if (t == FieldcraftDefOf.FC_PaddyFlooded)
            {
                s += "\n" + "FC_Inspect_PaddyFlooded".Translate();
            }
            else if (t == FieldcraftDefOf.FC_PaddyDry)
            {
                s += "\n" + "FC_Inspect_PaddyDry".Translate();
            }
            return s;
        }
    }
}
