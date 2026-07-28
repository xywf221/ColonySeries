using RimWorld;
using Verse;

namespace RumorMill
{
    [DefOf]
    public static class RumorMillDefOf
    {
        public static JobDef RM_Commend;
        public static InteractionDef RM_RumorChat;
        public static ThoughtDef RM_CommendedOther;
        public static ThoughtDef RM_WasCommended;
        public static ThoughtDef RM_WasCommendedSocial;
        public static ThoughtDef RM_OpinionFromRumor;

        static RumorMillDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RumorMillDefOf));
        }
    }
}
