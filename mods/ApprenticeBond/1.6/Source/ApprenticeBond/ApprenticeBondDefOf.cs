using RimWorld;
using Verse;

namespace ApprenticeBond
{
    [DefOf]
    public static class ApprenticeBondDefOf
    {
        public static ThoughtDef AB_Bonded;
        public static ThoughtDef AB_BondFormed;
        public static ThoughtDef AB_BondBroken;
        public static ThoughtDef AB_MentorDied;
        public static ThoughtDef AB_GraduatedMentor;
        public static ThoughtDef AB_GraduatedApprentice;

        static ApprenticeBondDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ApprenticeBondDefOf));
        }
    }
}
