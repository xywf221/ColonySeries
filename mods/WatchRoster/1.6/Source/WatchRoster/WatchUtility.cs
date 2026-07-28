using RimWorld;
using Verse;
using Verse.AI;

namespace WatchRoster
{
    public static class WatchUtility
    {
        public const string PrewarnedCustomKey = "WatchRoster_Prewarned";

        public static bool IsWatchNight(Map map)
        {
            if (map == null)
            {
                return false;
            }

            WatchRosterSettings s = WatchRosterMod.Settings;
            if (s != null && s.allowDayWatch)
            {
                return true;
            }

            int hour = GenLocalDate.HourInteger(map);
            int start = s?.NightStartHour ?? 20;
            int end = s?.NightEndHour ?? 5;
            if (start == end)
            {
                return true;
            }

            // Night wraps past midnight, e.g. 20..5
            if (start > end)
            {
                return hour >= start || hour < end;
            }

            return hour >= start && hour < end;
        }

        public static bool IsStandingWatch(Pawn pawn)
        {
            if (pawn?.CurJob == null)
            {
                return false;
            }

            return pawn.CurJob.def == WatchRosterDefOf.WR_StandWatch;
        }

        public static bool IsMannedPost(Building_WatchPost post)
        {
            if (post == null || !post.Spawned)
            {
                return false;
            }

            IntVec3 spot = post.WatchSpot;
            foreach (Thing t in spot.GetThingList(post.Map))
            {
                if (t is Pawn p && p.IsColonistPlayerControlled && !p.Dead && !p.Downed && IsStandingWatch(p))
                {
                    if (p.CurJob.targetA.Thing == post)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static int CountMannedPosts(Map map)
        {
            if (map == null || WatchRosterDefOf.WR_WatchPost == null)
            {
                return 0;
            }

            int count = 0;
            var list = map.listerThings.ThingsOfDef(WatchRosterDefOf.WR_WatchPost);
            if (list == null)
            {
                return 0;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Building_WatchPost post && IsMannedPost(post))
                {
                    count++;
                }
            }

            return count;
        }

        public static int CountPosts(Map map)
        {
            if (map == null || WatchRosterDefOf.WR_WatchPost == null)
            {
                return 0;
            }

            var list = map.listerThings.ThingsOfDef(WatchRosterDefOf.WR_WatchPost);
            return list?.Count ?? 0;
        }

        public static bool HasActiveWatch(Map map)
        {
            WatchRosterSettings s = WatchRosterMod.Settings;
            if (s == null || !s.masterEnabled)
            {
                return false;
            }

            if (!IsWatchNight(map))
            {
                return false;
            }

            return CountMannedPosts(map) > 0;
        }

        public static bool HasEmptyPostsAtNight(Map map)
        {
            WatchRosterSettings s = WatchRosterMod.Settings;
            if (s == null || !s.masterEnabled)
            {
                return false;
            }

            if (!IsWatchNight(map))
            {
                return false;
            }

            int posts = CountPosts(map);
            if (posts <= 0)
            {
                return false;
            }

            return CountMannedPosts(map) <= 0;
        }

        /// <summary>
        /// FireWatcher interval multiplier. Lower = faster fire observation refresh.
        /// Manned: faster. Empty posts at night: slower. Otherwise 1.
        /// </summary>
        public static float FireObserveIntervalMult(Map map)
        {
            WatchRosterSettings s = WatchRosterMod.Settings;
            if (s == null || !s.masterEnabled || !s.enableFireWatch || map == null)
            {
                return 1f;
            }

            if (!IsWatchNight(map) && !s.allowDayWatch)
            {
                return 1f;
            }

            int manned = CountMannedPosts(map);
            if (manned > 0)
            {
                // Extra manned posts slightly help, soft cap.
                float mult = s.FireMannedMult;
                if (manned >= 2)
                {
                    mult *= 0.85f;
                }

                return mult;
            }

            if (CountPosts(map) > 0)
            {
                return s.FireEmptyMult;
            }

            return 1f;
        }
    }
}
