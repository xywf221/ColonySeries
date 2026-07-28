using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DeadDrop
{
    /// <summary>
    /// Walk goods/silver to the edge cairn, then exchange. Not remote AFK income.
    /// </summary>
    public class JobDriver_ServiceDeadDrop : JobDriver
    {
        private float workLeft;
        private const float BaseWork = 360f;
        private bool deliveredGoods;
        private bool deliveredSilver;

        private Building_DeadDrop Cairn => job.targetA.Thing as Building_DeadDrop;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed))
            {
                return false;
            }
            // targetB = goods to deliver (optional)
            if (job.targetB.HasThing && !pawn.Reserve(job.targetB.Thing, job, 1, job.count > 0 ? job.count : 1, null, errorOnFailed))
            {
                return false;
            }
            // targetC = silver to pay (optional)
            if (job.targetC.HasThing && !pawn.Reserve(job.targetC.Thing, job, 1, job.countQueue != null && job.countQueue.Count > 0 ? job.countQueue[0] : 1, null, errorOnFailed))
            {
                return false;
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() =>
            {
                MapComponent_DeadDrop comp = MapComponent_DeadDrop.For(Map);
                return comp == null ||
                       comp.activeOrder == null ||
                       comp.activeOrder.state != DeadDropOrderState.Accepted;
            });

            MapComponent_DeadDrop mapComp = MapComponent_DeadDrop.For(Map);
            DeadDropOrder order = mapComp?.activeOrder;

            // --- Haul delivery goods if required ---
            if (order != null && order.NeedsGoodsDelivery)
            {
                yield return Toils_General.Do(() =>
                {
                    if (job.targetB.HasThing)
                    {
                        return;
                    }
                    Thing goods = FindStack(order.DeliverDef, order.deliverCount);
                    if (goods == null)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    job.SetTarget(TargetIndex.B, goods);
                    job.count = Mathf.Min(order.deliverCount, goods.stackCount);
                    if (!pawn.Reserve(goods, job, 1, job.count))
                    {
                        EndJobWith(JobCondition.Incompletable);
                    }
                });

                Toil failGoods = Toils_General.Do(() => { });
                failGoods.FailOnDespawnedNullOrForbidden(TargetIndex.B);

                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                    .FailOnDespawnedNullOrForbidden(TargetIndex.B)
                    .FailOn(() => !pawn.CanReserve(job.targetB.Thing));

                yield return Toils_Haul.StartCarryThing(TargetIndex.B, putRemainderInQueue: false, subtractNumTakenFromJobCount: true);

                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

                yield return Toils_General.Do(() =>
                {
                    // Drop carried goods at cairn; CompleteService will consume from map (including dropped).
                    Thing carried = pawn.carryTracker?.CarriedThing;
                    if (carried != null)
                    {
                        pawn.carryTracker.TryDropCarriedThing(Cairn.Position, ThingPlaceMode.Near, out _);
                    }
                    deliveredGoods = true;
                });
            }

            // --- Haul silver payment if required ---
            if (order != null && order.NeedsSilverPayment)
            {
                yield return Toils_General.Do(() =>
                {
                    if (job.targetC.HasThing)
                    {
                        return;
                    }
                    Thing silver = FindStack(ThingDefOf.Silver, order.SilverToPay);
                    if (silver == null)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    job.SetTarget(TargetIndex.C, silver);
                    int need = order.SilverToPay;
                    if (!pawn.Reserve(silver, job, 1, need))
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    // encode pay count in job.count for carry
                    job.count = Mathf.Min(need, silver.stackCount);
                });

                yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.ClosestTouch)
                    .FailOnDespawnedNullOrForbidden(TargetIndex.C);

                // Carry silver using targetC via manual toil (StartCarryThing is B-oriented helpers mostly).
                Toil pickSilver = new Toil();
                pickSilver.initAction = () =>
                {
                    Thing silver = job.targetC.Thing;
                    if (silver == null || silver.Destroyed)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    int take = Mathf.Min(order.SilverToPay, silver.stackCount);
                    pawn.carryTracker.TryStartCarry(silver.SplitOff(take));
                };
                pickSilver.defaultCompleteMode = ToilCompleteMode.Instant;
                yield return pickSilver;

                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

                yield return Toils_General.Do(() =>
                {
                    Thing carried = pawn.carryTracker?.CarriedThing;
                    if (carried != null)
                    {
                        pawn.carryTracker.TryDropCarriedThing(Cairn.Position, ThingPlaceMode.Near, out _);
                    }
                    deliveredSilver = true;
                });
            }

            // Always end at cairn for the exchange ritual.
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil work = new Toil();
            work.initAction = () =>
            {
                workLeft = BaseWork;
                if (pawn.story?.traits != null && pawn.story.traits.HasTrait(TraitDefOf.Kind))
                {
                    workLeft *= 1.25f;
                }
            };
            work.tickAction = () =>
            {
                float speed = pawn.GetStatValue(StatDefOf.GeneralLaborSpeed);
                if (speed < 0.25f)
                {
                    speed = 0.25f;
                }
                workLeft -= speed;
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Social, 0.06f);
                }
                if (workLeft <= 0f)
                {
                    FinishExchange();
                    ReadyForNextToil();
                }
            };
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            work.WithProgressBar(TargetIndex.A, () => 1f - workLeft / BaseWork);
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.PlaySustainerOrSound(SoundDefOf.Interact_ConstructDirt);
            yield return work;
        }

        private Thing FindStack(ThingDef def, int minCount)
        {
            if (def == null)
            {
                return null;
            }
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                Map,
                ThingRequest.ForDef(def),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                t => !t.IsForbidden(pawn) &&
                     pawn.CanReserve(t) &&
                     t.stackCount > 0 &&
                     (t.Faction == null || t.Faction == Faction.OfPlayer || !t.def.CanHaveFaction));
        }

        private void FinishExchange()
        {
            Building_DeadDrop cairn = Cairn;
            if (cairn == null || !cairn.Spawned)
            {
                return;
            }
            MapComponent_DeadDrop.For(Map)?.CompleteService(pawn, cairn);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workLeft, "workLeft", 0f);
            Scribe_Values.Look(ref deliveredGoods, "deliveredGoods", false);
            Scribe_Values.Look(ref deliveredSilver, "deliveredSilver", false);
        }
    }
}
