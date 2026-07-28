using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WorkshopWear
{
    public static class WorkshopWearUtility
    {
        public const int SlapdashSteelCost = 5;
        public const int ProperSteelCost = 8;
        public const int ProperComponentCost = 2;

        public static bool MapHasEnough(Map map, ThingDef def, int count)
        {
            if (map == null || def == null || count <= 0)
            {
                return false;
            }
            return map.resourceCounter.GetCount(def) >= count;
        }

        public static bool CanDoSlapdash(Map map)
        {
            return MapHasEnough(map, ThingDefOf.Steel, SlapdashSteelCost);
        }

        public static bool CanDoProper(Map map)
        {
            return MapHasEnough(map, ThingDefOf.Steel, ProperSteelCost)
                   && MapHasEnough(map, ThingDefOf.ComponentIndustrial, ProperComponentCost);
        }

        /// <summary>
        /// Find nearest reachable stack of def for hauling into the repair job.
        /// </summary>
        public static Thing FindNearestResource(Pawn pawn, ThingDef def, int needed)
        {
            if (pawn?.Map == null || def == null || needed <= 0)
            {
                return null;
            }

            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(def),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                t => !t.IsForbidden(pawn)
                     && pawn.CanReserve(t)
                     && t.stackCount > 0);
        }

        public static int ConsumeFromMap(Map map, ThingDef def, int count)
        {
            if (map == null || def == null || count <= 0)
            {
                return 0;
            }

            int left = count;
            List<Thing> things = map.listerThings.ThingsOfDef(def);
            // Copy indices carefully — destroying mutates the list
            for (int i = things.Count - 1; i >= 0 && left > 0; i--)
            {
                Thing t = things[i];
                if (t == null || t.Destroyed || t.stackCount <= 0)
                {
                    continue;
                }
                // Prefer stockpiles / non-forbidden
                if (t.IsForbidden(Faction.OfPlayer))
                {
                    continue;
                }

                int take = UnityEngine.Mathf.Min(left, t.stackCount);
                t.SplitOff(take).Destroy(DestroyMode.Vanish);
                left -= take;
            }

            return count - left;
        }

        public static int ConsumeCarriedOrMap(Pawn pawn, ThingDef def, int count)
        {
            int left = count;
            Thing carried = pawn?.carryTracker?.CarriedThing;
            if (carried != null && carried.def == def)
            {
                int take = UnityEngine.Mathf.Min(left, carried.stackCount);
                if (take >= carried.stackCount)
                {
                    carried.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    carried.SplitOff(take).Destroy(DestroyMode.Vanish);
                }
                left -= take;
            }

            if (left > 0 && pawn?.Map != null)
            {
                left -= ConsumeFromMap(pawn.Map, def, left);
            }

            return count - left;
        }
    }
}
