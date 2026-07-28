using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SalvageAtlas
{
    /// <summary>
    /// Haul mech fragments to the salvage bench / analyze spot and bank them as atlas progress.
    /// Never grants ResearchManager points — parallel to vanilla research.
    /// </summary>
    public class JobDriver_AnalyzeFragments : JobDriver
    {
        private float workLeft;
        private float totalWork = 500f;
        private int analyzeCount = 1;

        private Thing Bench => job.targetA.Thing;
        private Thing Fragments => job.targetB.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed))
            {
                return false;
            }
            int count = job.count > 0 ? job.count : 1;
            if (job.targetB.HasThing && !pawn.Reserve(job.targetB.Thing, job, 1, count, null, errorOnFailed))
            {
                return false;
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !SalvageUtility.ModActive || !SalvageUtility.ResearchDone);

            // Pick up fragments if not already carrying.
            yield return Toils_General.Do(() =>
            {
                if (job.targetB.HasThing)
                {
                    return;
                }
                Thing frag = FindFragments();
                if (frag == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                job.SetTarget(TargetIndex.B, frag);
                job.count = Mathf.Min(job.count > 0 ? job.count : 5, frag.stackCount);
                if (!pawn.Reserve(frag, job, 1, job.count))
                {
                    EndJobWith(JobCondition.Incompletable);
                }
            });

            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(TargetIndex.B)
                .FailOn(() => !pawn.CanReserve(job.targetB.Thing));

            yield return Toils_Haul.StartCarryThing(TargetIndex.B, putRemainderInQueue: false, subtractNumTakenFromJobCount: true);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil work = new Toil();
            work.initAction = () =>
            {
                Thing carried = pawn.carryTracker?.CarriedThing;
                analyzeCount = carried != null ? Mathf.Clamp(carried.stackCount, 1, 10) : Mathf.Max(1, job.count);
                float mult = SalvageAtlasMod.Settings?.AnalyzeWorkMult ?? 1f;
                totalWork = (180f + analyzeCount * 90f) * mult;
                workLeft = totalWork;
            };
            work.tickAction = () =>
            {
                float speed = pawn.GetStatValue(StatDefOf.ResearchSpeed);
                if (speed < 0.15f)
                {
                    speed = pawn.GetStatValue(StatDefOf.GeneralLaborSpeed);
                }
                if (speed < 0.15f)
                {
                    speed = 0.15f;
                }
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Intellectual, 0.14f);
                }
                workLeft -= speed;
                if (workLeft <= 0f)
                {
                    FinishAnalyze();
                    ReadyForNextToil();
                }
            };
            work.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            work.FailOn(() =>
            {
                Thing carried = pawn.carryTracker?.CarriedThing;
                return carried == null || carried.def != SalvageAtlasDefOf.SA_MechFragment;
            });
            work.WithProgressBar(TargetIndex.A, () => 1f - workLeft / Mathf.Max(1f, totalWork));
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithEffect(EffecterDefOf.Research, TargetIndex.A);
            yield return work;
        }

        private Thing FindFragments()
        {
            if (SalvageAtlasDefOf.SA_MechFragment == null)
            {
                return null;
            }
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                Map,
                ThingRequest.ForDef(SalvageAtlasDefOf.SA_MechFragment),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                x => !x.IsForbidden(pawn) && pawn.CanReserve(x) && x.stackCount > 0);
        }

        private void FinishAnalyze()
        {
            Thing carried = pawn.carryTracker?.CarriedThing;
            if (carried == null || carried.def != SalvageAtlasDefOf.SA_MechFragment)
            {
                return;
            }
            int take = Mathf.Clamp(analyzeCount, 1, carried.stackCount);
            if (take >= carried.stackCount)
            {
                carried.Destroy(DestroyMode.Vanish);
            }
            else
            {
                carried.SplitOff(take).Destroy(DestroyMode.Vanish);
            }

            // 1 physical fragment → 1 analyzed bank unit (settings can boost via yield mult on salvage only).
            GameComponent_SalvageAtlas atlas = GameComponent_SalvageAtlas.Get();
            atlas?.AddAnalyzed(take);

            Messages.Message(
                "SA_Msg_Analyzed".Translate(pawn.LabelShort, take, atlas?.analyzedFragments ?? 0),
                Bench,
                MessageTypeDefOf.TaskCompletion);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workLeft, "workLeft", 0f);
            Scribe_Values.Look(ref totalWork, "totalWork", 500f);
            Scribe_Values.Look(ref analyzeCount, "analyzeCount", 1);
        }
    }
}
