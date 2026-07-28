using HarmonyLib;
using RimWorld;
using Verse;

namespace Landworks
{
    [HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.SetTerrain))]
    public static class Patch_TerrainGrid_SetTerrain
    {
        // Cached field accessor — avoids reflection/Traverse on every terrain change.
        private static readonly AccessTools.FieldRef<TerrainGrid, Map> MapField =
            AccessTools.FieldRefAccess<TerrainGrid, Map>("map");

        public static void Postfix(TerrainGrid __instance, IntVec3 c, TerrainDef newTerr)
        {
            // Hottest path in terraforming mods: bail ASAP.
            if (TerrainUtility.SuppressTerrainHooks > 0 || newTerr == null)
            {
                return;
            }
            if (!TerrainUtility.NeedsTracking(newTerr))
            {
                return;
            }

            Map map = MapField(__instance);
            if (map == null)
            {
                return;
            }
            MapComponent_Landworks.For(map)?.RegisterPlacedTerrain(c, newTerr);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.ChanceFactorNow))]
    public static class Patch_IncidentWorker_ChanceFactorNow
    {
        public static void Postfix(IncidentWorker __instance, IIncidentTarget target, ref float __result)
        {
            if (__result <= 0f)
            {
                return;
            }

            IncidentDef def = __instance.def;
            if (def != LandworksDefOf.LW_GeologicalBacklash && def != LandworksDefOf.LW_SoilCollapse)
            {
                return;
            }

            if (!(target is Map map))
            {
                return;
            }

            MapComponent_Landworks comp = MapComponent_Landworks.For(map);
            if (comp == null)
            {
                __result = 0f;
                return;
            }

            float factor = comp.StressChanceFactor();
            if (factor <= 0f)
            {
                __result = 0f;
                return;
            }
            __result *= 0.25f + factor * 3.5f;
        }
    }
}
