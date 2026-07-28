using RimWorld;
using Verse;

namespace DeadDrop
{
    [DefOf]
    public static class DeadDropDefOf
    {
        public static ThingDef DD_DeadDropCairn;
        public static JobDef DD_ServiceDeadDrop;
        public static ResearchProjectDef DD_GreyChannels;
        public static IncidentDef DD_SearchParty;

        static DeadDropDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DeadDropDefOf));
        }
    }
}
