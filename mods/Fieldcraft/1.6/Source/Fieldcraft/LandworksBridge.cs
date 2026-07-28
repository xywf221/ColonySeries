using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Fieldcraft
{
    /// <summary>
    /// Soft-link to Landworks via reflection. No assembly reference.
    /// When Landworks is absent, Fieldcraft runs in solo natural-soil mode.
    /// </summary>
    public static class LandworksBridge
    {
        public static bool Active { get; private set; }

        private static Type mapCompType;
        private static MethodInfo forMethod;
        private static FieldInfo engineeredCellsField;
        private static FieldInfo geologicalStressField;
        private static FieldInfo degradeTicksField;
        private static MethodInfo setTerrainSilentMethod;

        private static TerrainDef lwReclaimed;
        private static TerrainDef lwAmended;
        private static TerrainDef lwTilled;
        private static TerrainDef lwExhausted;
        private static TerrainDef lwDrainedMarsh;
        private static TerrainDef lwEmbankment;
        private static TerrainDef lwHeavyEmbankment;
        private static TerrainDef lwScar;

        private static ThingDef lwCompost;
        private static ResearchProjectDef lwDrainage;

        private static bool initTried;

        public static void Init()
        {
            if (initTried)
            {
                return;
            }
            initTried = true;

            try
            {
                mapCompType = AccessTools.TypeByName("Landworks.MapComponent_Landworks");
                if (mapCompType == null)
                {
                    Active = false;
                    ResolveTerrainDefs();
                    return;
                }

                forMethod = AccessTools.Method(mapCompType, "For", new[] { typeof(Map) });
                engineeredCellsField = AccessTools.Field(mapCompType, "engineeredCells");
                geologicalStressField = AccessTools.Field(mapCompType, "geologicalStress");
                degradeTicksField = AccessTools.Field(mapCompType, "degradeTicks");

                Type terrainUtil = AccessTools.TypeByName("Landworks.TerrainUtility");
                if (terrainUtil != null)
                {
                    setTerrainSilentMethod = AccessTools.Method(terrainUtil, "SetTerrainSilent",
                        new[] { typeof(Map), typeof(IntVec3), typeof(TerrainDef) });
                }

                ResolveTerrainDefs();
                lwCompost = DefDatabase<ThingDef>.GetNamedSilentFail("LW_Compost");
                lwDrainage = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("LW_Drainage");

                Active = forMethod != null && engineeredCellsField != null;
                if (Active)
                {
                    Log.Message("[Fieldcraft] Landworks soft-link active.");
                }
            }
            catch (Exception e)
            {
                Active = false;
                Log.Warning("[Fieldcraft] Landworks soft-link failed: " + e.Message);
            }
        }

        private static void ResolveTerrainDefs()
        {
            lwReclaimed = DefDatabase<TerrainDef>.GetNamedSilentFail("LW_ReclaimedSoil");
            lwAmended = DefDatabase<TerrainDef>.GetNamedSilentFail("LW_AmendedSoil");
            lwTilled = DefDatabase<TerrainDef>.GetNamedSilentFail("LW_TilledSoil");
            lwExhausted = DefDatabase<TerrainDef>.GetNamedSilentFail("LW_ExhaustedSoil");
            lwDrainedMarsh = DefDatabase<TerrainDef>.GetNamedSilentFail("LW_DrainedMarsh");
            lwEmbankment = DefDatabase<TerrainDef>.GetNamedSilentFail("LW_EmbankmentFill");
            lwHeavyEmbankment = DefDatabase<TerrainDef>.GetNamedSilentFail("LW_HeavyEmbankment");
            lwScar = DefDatabase<TerrainDef>.GetNamedSilentFail("LW_ExcavationScar");
            lwDrainage = DefDatabase<ResearchProjectDef>.GetNamedSilentFail("LW_Drainage");
        }

        public static ThingDef CompostDef => FieldcraftDefOf.FC_Compost ?? lwCompost;

        public static bool LandworksDrainageFinished
        {
            get
            {
                if (lwDrainage == null)
                {
                    return true; // no Landworks drainage research → don't hard-block
                }
                return lwDrainage.IsFinished;
            }
        }

        public static bool IsLandworksEngineered(TerrainDef def)
        {
            if (def == null)
            {
                return false;
            }
            return def == lwReclaimed || def == lwAmended || def == lwTilled
                || def == lwExhausted || def == lwDrainedMarsh
                || def == lwEmbankment || def == lwHeavyEmbankment || def == lwScar;
        }

        public static bool IsDegradableLandworks(TerrainDef def)
        {
            return def == lwAmended || def == lwTilled;
        }

        public static bool IsPaddy(TerrainDef def)
        {
            return def == FieldcraftDefOf.FC_PaddyDry || def == FieldcraftDefOf.FC_PaddyFlooded;
        }

        public static float InitialBudgetFor(TerrainDef def)
        {
            if (def == null)
            {
                return 0f;
            }
            if (def == FieldcraftDefOf.FC_PaddyFlooded)
            {
                return 85f;
            }
            if (def == FieldcraftDefOf.FC_PaddyDry)
            {
                return 65f;
            }
            if (def == lwTilled)
            {
                return 100f;
            }
            if (def == lwAmended)
            {
                return 90f;
            }
            if (def == lwReclaimed || def == TerrainDefOf.SoilRich)
            {
                return 70f;
            }
            if (def == lwDrainedMarsh)
            {
                return 55f;
            }
            if (def == TerrainDefOf.Soil)
            {
                return 50f;
            }
            if (def == lwEmbankment)
            {
                return 30f;
            }
            if (def == TerrainDefOf.Gravel || def == TerrainDefOf.Sand || def == lwScar)
            {
                return 15f;
            }
            if (def == lwExhausted || def == lwHeavyEmbankment || def == TerrainDefOf.PackedDirt)
            {
                return 5f;
            }
            if (def.fertility >= 1.0f)
            {
                return 65f;
            }
            if (def.fertility >= 0.7f)
            {
                return 45f;
            }
            if (def.fertility >= 0.3f)
            {
                return 25f;
            }
            return 0f;
        }

        public static bool IsBudgetEligible(TerrainDef def, bool trackNatural)
        {
            if (def == null || def.IsWater)
            {
                return false;
            }
            if (IsPaddy(def))
            {
                return true;
            }
            if (IsLandworksEngineered(def))
            {
                return def != lwScar || trackNatural;
            }
            if (!trackNatural)
            {
                return false;
            }
            if (def.fertility < 0.2f)
            {
                return false;
            }
            TerrainAffordanceDef growSoil = DefDatabase<TerrainAffordanceDef>.GetNamedSilentFail("GrowSoil");
            if (growSoil != null && def.affordances != null && def.affordances.Contains(growSoil))
            {
                return true;
            }
            return def == TerrainDefOf.Soil || def == TerrainDefOf.SoilRich || def == TerrainDefOf.Gravel;
        }

        public static bool CanFormPaddy(TerrainDef def)
        {
            if (def == null || def.IsWater)
            {
                return false;
            }
            if (IsPaddy(def))
            {
                return false;
            }
            if (def == lwExhausted || def == lwScar)
            {
                return false;
            }
            if (IsLandworksEngineered(def))
            {
                return def == lwReclaimed || def == lwAmended || def == lwTilled || def == lwDrainedMarsh
                       || def == lwEmbankment;
            }
            TerrainAffordanceDef growSoil = DefDatabase<TerrainAffordanceDef>.GetNamedSilentFail("GrowSoil");
            if (growSoil != null && def.affordances != null && def.affordances.Contains(growSoil))
            {
                return true;
            }
            return def == TerrainDefOf.Soil || def == TerrainDefOf.SoilRich || def == TerrainDefOf.Mud
                   || def == TerrainDefOf.Gravel;
        }

        public static object GetLandworksComp(Map map)
        {
            if (!Active || forMethod == null || map == null)
            {
                return null;
            }
            try
            {
                return forMethod.Invoke(null, new object[] { map });
            }
            catch
            {
                return null;
            }
        }

        public static float GetGeologicalStress(Map map)
        {
            object comp = GetLandworksComp(map);
            if (comp == null || geologicalStressField == null)
            {
                return 0f;
            }
            try
            {
                return (float)geologicalStressField.GetValue(comp);
            }
            catch
            {
                return 0f;
            }
        }

        public static bool IsEngineeredCell(Map map, IntVec3 cell)
        {
            object comp = GetLandworksComp(map);
            if (comp == null || engineeredCellsField == null)
            {
                return IsLandworksEngineered(map.terrainGrid.TerrainAt(cell));
            }
            try
            {
                object raw = engineeredCellsField.GetValue(comp);
                if (raw is HashSet<IntVec3> typed)
                {
                    return typed.Contains(cell);
                }
                if (raw is ICollection)
                {
                    MethodInfo contains = raw.GetType().GetMethod("Contains", new[] { typeof(IntVec3) });
                    if (contains != null)
                    {
                        return (bool)contains.Invoke(raw, new object[] { cell });
                    }
                }
            }
            catch
            {
                // fall through
            }
            return IsLandworksEngineered(map.terrainGrid.TerrainAt(cell));
        }

        public static void AccelerateDegrade(Map map, IntVec3 cell, float factor)
        {
            if (!Active || factor <= 1f)
            {
                return;
            }
            object comp = GetLandworksComp(map);
            if (comp == null || degradeTicksField == null)
            {
                return;
            }
            try
            {
                if (!(degradeTicksField.GetValue(comp) is Dictionary<IntVec3, int> dict))
                {
                    return;
                }
                if (!dict.TryGetValue(cell, out int ticks) || ticks <= 0)
                {
                    return;
                }
                int reduced = Mathf.Max(1, Mathf.RoundToInt(ticks / factor));
                dict[cell] = reduced;
            }
            catch
            {
                // ignore
            }
        }

        public static void RefreshDegradeTimer(Map map, IntVec3 cell, int ticks)
        {
            if (!Active || ticks <= 0)
            {
                return;
            }
            object comp = GetLandworksComp(map);
            if (comp == null || degradeTicksField == null)
            {
                return;
            }
            try
            {
                if (!(degradeTicksField.GetValue(comp) is Dictionary<IntVec3, int> dict))
                {
                    return;
                }
                TerrainDef t = map.terrainGrid.TerrainAt(cell);
                if (IsDegradableLandworks(t))
                {
                    dict[cell] = ticks;
                }
            }
            catch
            {
                // ignore
            }
        }

        public static void SetTerrainSilent(Map map, IntVec3 cell, TerrainDef def)
        {
            if (def == null)
            {
                return;
            }
            if (setTerrainSilentMethod != null)
            {
                try
                {
                    setTerrainSilentMethod.Invoke(null, new object[] { map, cell, def });
                    return;
                }
                catch
                {
                    // fall through
                }
            }
            map.terrainGrid.SetTerrain(cell, def);
        }

        public static TerrainDef ExhaustedTerrain => lwExhausted;

        public static TerrainDef AmendedSoil => lwAmended;

        public static TerrainDef ReclaimedSoil => lwReclaimed;
    }
}
