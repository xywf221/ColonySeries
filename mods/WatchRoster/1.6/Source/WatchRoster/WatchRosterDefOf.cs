using RimWorld;
using Verse;

namespace WatchRoster
{
    [DefOf]
    public static class WatchRosterDefOf
    {
        public static ThingDef WR_WatchPost;
        public static JobDef WR_StandWatch;

        static WatchRosterDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WatchRosterDefOf));
        }
    }
}
