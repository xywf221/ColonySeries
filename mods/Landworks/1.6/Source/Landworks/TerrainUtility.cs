using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Landworks
{
    public static class TerrainUtility
    {
        // Suppress stress/degrade registration while our own systems change terrain.
        public static int SuppressTerrainHooks;

        private static TerrainDef marshyTerrain;
        private static TerrainDef mossyTerrain;
        private static TerrainAffordanceDef diggableAffordance;
        private static bool resolved;

        private static void EnsureResolved()
        {
            if (resolved)
            {
                return;
            }
            resolved = true;
            marshyTerrain = DefDatabase<TerrainDef>.GetNamedSilentFail("MarshyTerrain");
            mossyTerrain = DefDatabase<TerrainDef>.GetNamedSilentFail("MossyTerrain");
            diggableAffordance = DefDatabase<TerrainAffordanceDef>.GetNamedSilentFail("Diggable");
        }

        public static TerrainDef MarshyTerrain
        {
            get { EnsureResolved(); return marshyTerrain; }
        }

        public static bool IsEngineeredTracked(TerrainDef def)
        {
            if (def == null)
            {
                return false;
            }
            return def == LandworksDefOf.LW_ReclaimedSoil
                || def == LandworksDefOf.LW_AmendedSoil
                || def == LandworksDefOf.LW_TilledSoil
                || def == LandworksDefOf.LW_DrainedMarsh
                || def == LandworksDefOf.LW_EmbankmentFill
                || def == LandworksDefOf.LW_HeavyEmbankment
                || def == LandworksDefOf.LW_ExhaustedSoil
                || def == LandworksDefOf.LW_ExcavationScar;
        }

        public static bool IsDegradable(TerrainDef def)
        {
            return def == LandworksDefOf.LW_AmendedSoil || def == LandworksDefOf.LW_TilledSoil;
        }

        /// <summary>Cheap filter for Harmony hot path before any map lookup.</summary>
        public static bool NeedsTracking(TerrainDef def)
        {
            return IsDegradable(def) || StressForTerrain(def) > 0f;
        }

        public static float StressForTerrain(TerrainDef def)
        {
            if (def == null)
            {
                return 0f;
            }
            if (def == LandworksDefOf.LW_ReclaimedSoil) return 1f;
            if (def == LandworksDefOf.LW_AmendedSoil) return 2.5f;
            if (def == LandworksDefOf.LW_TilledSoil) return 1.2f;
            if (def == LandworksDefOf.LW_DrainedMarsh) return 3f;
            if (def == LandworksDefOf.LW_EmbankmentFill) return 4f;
            if (def == LandworksDefOf.LW_HeavyEmbankment) return 6.5f;
            return 0f;
        }

        public static bool CanExcavate(TerrainDef def)
        {
            EnsureResolved();
            if (def == null || def.IsWater)
            {
                return false;
            }
            if (def == TerrainDefOf.Ice)
            {
                return false;
            }
            if (def == TerrainDefOf.Soil
                || def == TerrainDefOf.SoilRich
                || def == TerrainDefOf.Gravel
                || def == TerrainDefOf.Sand
                || def == TerrainDefOf.SoftSand
                || def == TerrainDefOf.Mud
                || def == TerrainDefOf.PackedDirt
                || def == TerrainDefOf.Riverbank
                || def == marshyTerrain
                || def == mossyTerrain
                || def == LandworksDefOf.LW_ReclaimedSoil
                || def == LandworksDefOf.LW_AmendedSoil
                || def == LandworksDefOf.LW_TilledSoil
                || def == LandworksDefOf.LW_DrainedMarsh
                || def == LandworksDefOf.LW_EmbankmentFill
                || def == LandworksDefOf.LW_HeavyEmbankment
                || def == LandworksDefOf.LW_ExhaustedSoil)
            {
                return true;
            }
            if (diggableAffordance != null && def.affordances != null && def.affordances.Contains(diggableAffordance) && def.fertility > 0.05f)
            {
                return true;
            }
            return false;
        }

        public static int FillYield(TerrainDef def)
        {
            EnsureResolved();
            int raw;
            if (def == TerrainDefOf.SoilRich || def == LandworksDefOf.LW_AmendedSoil || def == LandworksDefOf.LW_TilledSoil)
            {
                raw = 5;
            }
            else if (def == TerrainDefOf.Soil || def == mossyTerrain || def == LandworksDefOf.LW_ReclaimedSoil || def == TerrainDefOf.Riverbank)
            {
                raw = 4;
            }
            else if (def == marshyTerrain || def == LandworksDefOf.LW_DrainedMarsh || def == TerrainDefOf.Mud)
            {
                raw = 3;
            }
            else if (def == TerrainDefOf.Gravel || def == LandworksDefOf.LW_EmbankmentFill || def == LandworksDefOf.LW_HeavyEmbankment)
            {
                raw = 2;
            }
            else if (def == TerrainDefOf.Sand || def == TerrainDefOf.SoftSand || def == TerrainDefOf.PackedDirt || def == LandworksDefOf.LW_ExhaustedSoil)
            {
                raw = 1;
            }
            else
            {
                raw = 2;
            }

            float mult = LandworksMod.Settings != null ? LandworksMod.Settings.fillYieldMultiplier : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(raw * mult));
        }

        public static TerrainDef ResultAfterExcavation(TerrainDef def)
        {
            EnsureResolved();
            if (def == TerrainDefOf.SoilRich || def == TerrainDefOf.Soil || def == mossyTerrain || def == TerrainDefOf.Riverbank
                || def == LandworksDefOf.LW_ReclaimedSoil || def == LandworksDefOf.LW_AmendedSoil || def == LandworksDefOf.LW_TilledSoil)
            {
                return LandworksDefOf.LW_ExcavationScar;
            }
            if (def == marshyTerrain || def == LandworksDefOf.LW_DrainedMarsh)
            {
                return TerrainDefOf.Mud;
            }
            if (def == TerrainDefOf.Mud)
            {
                return LandworksDefOf.LW_ExcavationScar;
            }
            if (def == TerrainDefOf.Gravel || def == LandworksDefOf.LW_EmbankmentFill || def == LandworksDefOf.LW_HeavyEmbankment)
            {
                return TerrainDefOf.Sand;
            }
            if (def == TerrainDefOf.Sand || def == TerrainDefOf.PackedDirt || def == LandworksDefOf.LW_ExhaustedSoil)
            {
                return TerrainDefOf.SoftSand;
            }
            return LandworksDefOf.LW_ExcavationScar;
        }

        public static int WorkToExcavate(TerrainDef def)
        {
            int yield = FillYield(def);
            float mult = LandworksMod.Settings != null ? LandworksMod.Settings.excavateWorkMultiplier : 1f;
            // Undo fill-yield mult for work base so yield slider doesn't secretly change work.
            float yieldBase = LandworksMod.Settings != null && LandworksMod.Settings.fillYieldMultiplier > 0.01f
                ? yield / LandworksMod.Settings.fillYieldMultiplier
                : yield;
            return Mathf.Max(100, Mathf.RoundToInt((400f + yieldBase * 220f) * mult));
        }

        public static TerrainDef DegradeTarget(TerrainDef def, float geologicalStress)
        {
            if (def == LandworksDefOf.LW_TilledSoil)
            {
                return geologicalStress >= 40f ? LandworksDefOf.LW_ExhaustedSoil : LandworksDefOf.LW_ReclaimedSoil;
            }
            if (def == LandworksDefOf.LW_AmendedSoil)
            {
                return geologicalStress >= 55f ? LandworksDefOf.LW_ExhaustedSoil : LandworksDefOf.LW_ReclaimedSoil;
            }
            if (def == LandworksDefOf.LW_ReclaimedSoil && geologicalStress >= 70f)
            {
                return LandworksDefOf.LW_ExhaustedSoil;
            }
            return null;
        }

        public static void SetTerrainSilent(Map map, IntVec3 cell, TerrainDef def)
        {
            SuppressTerrainHooks++;
            try
            {
                map.terrainGrid.SetTerrain(cell, def);
            }
            finally
            {
                SuppressTerrainHooks--;
            }
        }

        public static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Rand.RangeInclusive(0, i);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
