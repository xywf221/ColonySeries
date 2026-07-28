using HarmonyLib;
using RimWorld;
using Verse;

namespace Fieldcraft
{
    [HarmonyPatch(typeof(TerrainGrid), nameof(TerrainGrid.SetTerrain))]
    public static class Patch_TerrainGrid_SetTerrain
    {
        private static readonly AccessTools.FieldRef<TerrainGrid, Map> MapField =
            AccessTools.FieldRefAccess<TerrainGrid, Map>("map");

        public static void Postfix(TerrainGrid __instance, IntVec3 c, TerrainDef newTerr)
        {
            if (newTerr == null)
            {
                return;
            }
            bool trackNatural = FieldcraftMod.Settings == null || FieldcraftMod.Settings.trackNaturalSoil;
            if (!LandworksBridge.IsBudgetEligible(newTerr, trackNatural)
                && !LandworksBridge.IsLandworksEngineered(newTerr)
                && !LandworksBridge.IsPaddy(newTerr))
            {
                return;
            }

            Map map = MapField(__instance);
            if (map == null)
            {
                return;
            }
            MapComponent_Fieldcraft.For(map)?.OnTerrainChanged(c, newTerr);
        }
    }

    [HarmonyPatch(typeof(Plant), "PlantCollected")]
    public static class Patch_Plant_PlantCollected
    {
        public static void Prefix(Plant __instance, Pawn by)
        {
            if (__instance?.Map == null || __instance.Destroyed)
            {
                return;
            }
            if (__instance.def?.plant == null || !__instance.def.plant.Harvestable)
            {
                return;
            }
            if (__instance.def.plant.IsTree)
            {
                return;
            }

            MapComponent_Fieldcraft comp = MapComponent_Fieldcraft.For(__instance.Map);
            if (comp == null)
            {
                return;
            }
            TerrainDef terrain = __instance.Position.GetTerrain(__instance.Map);
            bool trackNatural = FieldcraftMod.Settings == null || FieldcraftMod.Settings.trackNaturalSoil;
            if (!LandworksBridge.IsBudgetEligible(terrain, trackNatural))
            {
                return;
            }
            comp.ApplyHarvestDrain(__instance.Position, __instance.def);
        }
    }

    [HarmonyPatch(typeof(Plant), "GrowthRateFactor_Fertility", MethodType.Getter)]
    public static class Patch_Plant_GrowthRateFactor_Fertility
    {
        public static void Postfix(Plant __instance, ref float __result)
        {
            if (__result <= 0f || __instance?.Map == null)
            {
                return;
            }
            if (__instance.def?.plant == null || __instance.def.plant.IsTree)
            {
                return;
            }
            MapComponent_Fieldcraft comp = MapComponent_Fieldcraft.For(__instance.Map);
            if (comp == null)
            {
                return;
            }

            float factor = comp.GrowthFactorAt(__instance.Position);
            TerrainDef terrain = __instance.Position.GetTerrain(__instance.Map);
            factor *= SoilBudgetUtility.PaddyGrowthFactor(__instance.def, terrain);

            if (factor >= 0.999f && factor <= 1.001f)
            {
                return;
            }
            __result *= factor;
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.GetInspectString))]
    public static class Patch_Plant_GetInspectString
    {
        public static void Postfix(Plant __instance, ref string __result)
        {
            if (__instance?.Map == null)
            {
                return;
            }
            MapComponent_Fieldcraft comp = MapComponent_Fieldcraft.For(__instance.Map);
            if (comp == null)
            {
                return;
            }
            string extra = comp.InspectExtra(__instance.Position);
            if (extra.NullOrEmpty())
            {
                return;
            }
            if (string.IsNullOrEmpty(__result))
            {
                __result = extra;
            }
            else
            {
                __result = __result + "\n" + extra;
            }
        }
    }
}
