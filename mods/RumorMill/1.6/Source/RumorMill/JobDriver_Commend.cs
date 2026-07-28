using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RumorMill
{
    public class JobDriver_Commend : JobDriver
    {
        private Pawn Target => (Pawn)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Target, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Target == null || !Target.IsColonist);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil commend = ToilMaker.MakeToil("RM_Commend");
            commend.initAction = () =>
            {
                Pawn target = Target;
                if (target == null || target.Dead)
                {
                    return;
                }
                // Face each other and play social effect
                if (pawn.interactions != null && RumorMillDefOf.RM_RumorChat != null)
                {
                    pawn.interactions.TryInteractWith(target, RumorMillDefOf.RM_RumorChat);
                }
                RumorUtility.ApplyCommend(pawn, target);
            };
            commend.socialMode = RandomSocialMode.SuperActive;
            commend.defaultCompleteMode = ToilCompleteMode.Delay;
            commend.defaultDuration = 90;
            yield return commend;
        }
    }
}
