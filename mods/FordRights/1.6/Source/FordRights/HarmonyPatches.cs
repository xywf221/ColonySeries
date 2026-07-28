using HarmonyLib;
using RimWorld;
using Verse;

namespace FordRights
{
    /// <summary>
    /// Passage tolls on peaceful arrivals when an operational riverside ford exists.
    /// Flood is IncidentDef-driven. Placement requires adjacent water.
    /// </summary>
    public class PlaceWorker_FordNearWater : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            if (!loc.InBounds(map))
            {
                return false;
            }

            if (!FordUtility.IsNearWater(map, loc, FordUtility.WaterAdjacencyRadius))
            {
                return "FR_Place_NeedWater".Translate();
            }

            return true;
        }
    }

    public static class FordArrivalTolls
    {
        public static void Collect(IncidentParms parms, string fallbackLabel)
        {
            FordRightsSettings s = FordRightsMod.Settings;
            if (s == null || !s.modEnabled || !s.tollsEnabled)
            {
                return;
            }

            Map map = parms?.target as Map;
            if (map == null)
            {
                return;
            }

            if (!FordUtility.ResearchDone())
            {
                return;
            }

            MapComponent_FordRights comp = MapComponent_FordRights.For(map);
            if (comp == null)
            {
                return;
            }

            if (!comp.cachedHasWater)
            {
                comp.RecheckWater(force: true);
                if (!comp.cachedHasWater)
                {
                    return;
                }
            }

            Building_FordCrossing ford = FordUtility.FindBestOperationalFord(map);
            if (ford == null)
            {
                return;
            }

            string name = fallbackLabel;
            if (parms.faction != null)
            {
                name = parms.faction.Name;
            }

            comp.TryCollectToll(ford, name, parms.faction);
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "TryExecuteWorker")]
    public static class Patch_TraderCaravanArrival_Toll
    {
        public static void Postfix(IncidentParms parms, bool __result)
        {
            if (__result)
            {
                FordArrivalTolls.Collect(parms, "FR_Traveler_Trader".Translate());
            }
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_VisitorGroup), "TryExecuteWorker")]
    public static class Patch_VisitorGroup_Toll
    {
        public static void Postfix(IncidentParms parms, bool __result)
        {
            if (__result)
            {
                FordArrivalTolls.Collect(parms, "FR_Traveler_Visitor".Translate());
            }
        }
    }

    [HarmonyPatch(typeof(IncidentWorker_TravelerGroup), "TryExecuteWorker")]
    public static class Patch_TravelerGroup_Toll
    {
        public static void Postfix(IncidentParms parms, bool __result)
        {
            if (__result)
            {
                FordArrivalTolls.Collect(parms, "FR_Traveler_Traveler".Translate());
            }
        }
    }
}
