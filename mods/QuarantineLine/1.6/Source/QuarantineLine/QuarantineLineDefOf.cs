using RimWorld;
using Verse;

namespace QuarantineLine
{
    [DefOf]
    public static class QuarantineLineDefOf
    {
        public static DesignationDef QL_Quarantine;
        public static ThoughtDef QL_Quarantined;
        public static ThoughtDef QL_VisitingWard;
        public static ThoughtDef QL_IsolationRounds;
        public static IncidentDef QL_CaravanDisease;
        public static IncidentDef QL_AnimalSource;

        static QuarantineLineDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(QuarantineLineDefOf));
        }
    }
}
