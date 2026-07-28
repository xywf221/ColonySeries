using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WatchRoster
{
    public class WorkGiver_StandWatch : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.OnCell;

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForDef(WatchRosterDefOf.WR_WatchPost);

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            WatchRosterSettings s = WatchRosterMod.Settings;
            if (s == null || !s.masterEnabled)
            {
                return true;
            }

            if (!forced && !WatchUtility.IsWatchNight(pawn.Map) && !s.allowDayWatch)
            {
                return true;
            }

            if (WatchRosterDefOf.WR_WatchPost == null)
            {
                return true;
            }

            return pawn.Map.listerThings.ThingsOfDef(WatchRosterDefOf.WR_WatchPost).Count == 0;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            var list = pawn.Map.listerThings.ThingsOfDef(WatchRosterDefOf.WR_WatchPost);
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
            if (!(t is Building_WatchPost post) || !post.Spawned)
            {
                return false;
            }

            WatchRosterSettings s = WatchRosterMod.Settings;
            if (s == null || !s.masterEnabled)
            {
                return false;
            }

            if (!forced && !WatchUtility.IsWatchNight(pawn.Map) && !s.allowDayWatch)
            {
                return false;
            }

            AcceptanceReport can = post.CanPawnWatch(pawn, forced);
            if (!can.Accepted)
            {
                JobFailReason.Is(can.Reason);
                return false;
            }

            // One watcher per post.
            if (WatchUtility.IsMannedPost(post) && !forced)
            {
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Building_WatchPost post = t as Building_WatchPost;
            if (post == null)
            {
                return null;
            }

            Job job = JobMaker.MakeJob(WatchRosterDefOf.WR_StandWatch, post, post.WatchSpot);
            job.expiryInterval = 0;
            return job;
        }
    }
}
