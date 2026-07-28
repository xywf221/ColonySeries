using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DeadDrop
{
    public class WorkGiver_ServiceDeadDrop : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForDef(DeadDropDefOf.DD_DeadDropCairn);

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            DeadDropSettings settings = DeadDropMod.Settings;
            if (settings != null && !settings.masterEnabled)
            {
                return true;
            }

            MapComponent_DeadDrop comp = MapComponent_DeadDrop.For(pawn.Map);
            if (comp == null || !comp.pendingService || comp.activeOrder == null ||
                comp.activeOrder.state != DeadDropOrderState.Accepted)
            {
                return true;
            }

            if (settings != null && settings.honestPawnsRefuse &&
                pawn.story?.traits != null &&
                pawn.story.traits.HasTrait(TraitDefOf.Kind) &&
                !forced)
            {
                return true;
            }

            return false;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            List<Thing> list = pawn.Map.listerThings.ThingsOfDef(DeadDropDefOf.DD_DeadDropCairn);
            if (list == null)
            {
                yield break;
            }
            for (int i = 0; i < list.Count; i++)
            {
                yield return list[i];
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_DeadDrop) || !t.Spawned)
            {
                return false;
            }

            MapComponent_DeadDrop comp = MapComponent_DeadDrop.For(pawn.Map);
            if (comp == null || !comp.pendingService || comp.activeOrder == null ||
                comp.activeOrder.state != DeadDropOrderState.Accepted)
            {
                return false;
            }

            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            if (!pawn.CanReach(t, PathEndMode.Touch, Danger.Deadly))
            {
                return false;
            }

            if (!MapComponent_DeadDrop.CanFulfill(comp.activeOrder, pawn.Map, out string reason))
            {
                JobFailReason.Is(reason);
                return false;
            }

            DeadDropOrder order = comp.activeOrder;
            if (order.NeedsGoodsDelivery)
            {
                Thing goods = FindReachable(pawn, order.DeliverDef);
                if (goods == null)
                {
                    JobFailReason.Is("DD_Fail_NeedGoods".Translate(order.deliverCount, order.DeliverDef.label, 0));
                    return false;
                }
            }
            if (order.NeedsSilverPayment)
            {
                Thing silver = FindReachable(pawn, ThingDefOf.Silver);
                if (silver == null)
                {
                    JobFailReason.Is("DD_Fail_NeedSilver".Translate(order.SilverToPay, 0));
                    return false;
                }
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            MapComponent_DeadDrop comp = MapComponent_DeadDrop.For(pawn.Map);
            DeadDropOrder order = comp?.activeOrder;
            Job job = JobMaker.MakeJob(DeadDropDefOf.DD_ServiceDeadDrop, t);

            if (order != null && order.NeedsGoodsDelivery && order.DeliverDef != null)
            {
                Thing goods = FindReachable(pawn, order.DeliverDef);
                if (goods != null)
                {
                    job.targetB = goods;
                    job.count = Mathf.Min(order.deliverCount, goods.stackCount);
                }
            }

            if (order != null && order.NeedsSilverPayment)
            {
                Thing silver = FindReachable(pawn, ThingDefOf.Silver);
                if (silver != null)
                {
                    job.targetC = silver;
                }
            }

            return job;
        }

        private static Thing FindReachable(Pawn pawn, ThingDef def)
        {
            if (def == null)
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
                x => !x.IsForbidden(pawn) && pawn.CanReserve(x) && x.stackCount > 0);
        }
    }
}
