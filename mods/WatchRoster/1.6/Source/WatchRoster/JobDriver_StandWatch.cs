using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WatchRoster
{
    public class JobDriver_StandWatch : JobDriver
    {
        private const TargetIndex PostInd = TargetIndex.A;
        private const TargetIndex SpotInd = TargetIndex.B;
        private const int WatchDurationTicks = 2500; // ~1 hour pulse; job re-issued while night

        private Building_WatchPost Post => job.GetTarget(PostInd).Thing as Building_WatchPost;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(job.GetTarget(PostInd), job, 1, -1, null, errorOnFailed))
            {
                return false;
            }

            // Spot may equal post cell; reserve cell soft via standable.
            return pawn.ReserveSittableOrSpot(job.GetTarget(SpotInd).Cell, job, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(PostInd);
            this.FailOn(() =>
            {
                WatchRosterSettings s = WatchRosterMod.Settings;
                if (s == null || !s.masterEnabled)
                {
                    return true;
                }

                if (!s.allowDayWatch && !WatchUtility.IsWatchNight(Map))
                {
                    return true;
                }

                return false;
            });

            yield return Toils_Goto.GotoCell(SpotInd, PathEndMode.OnCell);

            Toil watch = ToilMaker.MakeToil("StandWatch");
            watch.defaultCompleteMode = ToilCompleteMode.Delay;
            watch.defaultDuration = WatchDurationTicks;
            watch.handlingFacing = true;
            watch.initAction = () =>
            {
                Building_WatchPost post = Post;
                if (post != null)
                {
                    pawn.rotationTracker.FaceCell(post.Position);
                }
            };
            watch.tickIntervalAction = delta =>
            {
                Building_WatchPost post = Post;
                if (post != null)
                {
                    pawn.rotationTracker.FaceCell(post.Position);
                }

                pawn.GainComfortFromCellIfPossible(delta);

                // Light rest drain: standing watch is tiring but not a mood farm.
                if (pawn.needs?.rest != null && Find.TickManager.TicksGame % 60 < delta)
                {
                    pawn.needs.rest.CurLevel -= 0.00035f * delta;
                }
            };
            watch.AddFinishAction(() =>
            {
                // No auto-doors, no raid points — finish is a no-op beyond job end.
            });
            yield return watch;
        }
    }
}
