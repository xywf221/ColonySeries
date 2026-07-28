using RimWorld;
using Verse;

namespace FordRights
{
    [DefOf]
    public static class FordRightsDefOf
    {
        public static ThingDef FR_FordCrossing;
        public static ThingDef FR_RiverFish;
        public static JobDef FR_FishAtFord;
        public static ResearchProjectDef FR_RiverRights;
        public static IncidentDef FR_FordFlood;

        static FordRightsDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FordRightsDefOf));
        }
    }
}
