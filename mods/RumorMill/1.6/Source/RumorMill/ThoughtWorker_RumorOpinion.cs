using RimWorld;
using Verse;

namespace RumorMill
{
    /// <summary>Social opinion from active rumor tags — no mental-break forcing.</summary>
    public class ThoughtWorker_RumorOpinion : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other)
        {
            if (!RumorUtility.Enabled || p == null || other == null || p == other)
            {
                return ThoughtState.Inactive;
            }
            if (!RelationsUtility.PawnsKnowEachOther(p, other))
            {
                return ThoughtState.Inactive;
            }
            if (!other.RaceProps.Humanlike)
            {
                return ThoughtState.Inactive;
            }
            int offset = RumorUtility.SocialOpinionOffset(p, other);
            if (offset == 0)
            {
                return ThoughtState.Inactive;
            }
            PawnRumorState state = RumorUtility.Comp?.TryGet(other);
            if (state == null || state.tags.Count == 0)
            {
                return ThoughtState.Inactive;
            }
            // Stage index 0 used; opinion comes from OpinionOffsetOfSecondary in stages via dynamic?
            // ThoughtWorker social: return ActiveAtStage; stage opinion is fixed in XML.
            // We use a single stage with base 0 and override via Thought_Memory? Better: custom Thought_SituationalSocial.
            return ThoughtState.ActiveAtStage(offset > 0 ? 0 : 1);
        }
    }

    /// <summary>Dynamic opinion magnitude so settings apply without many stages.</summary>
    public class Thought_RumorSocial : Thought_SituationalSocial
    {
        public override float OpinionOffset()
        {
            if (pawn == null || otherPawn == null)
            {
                return 0f;
            }
            return RumorUtility.SocialOpinionOffset(pawn, otherPawn);
        }

        public override string LabelCap
        {
            get
            {
                PawnRumorState state = RumorUtility.Comp?.TryGet(otherPawn);
                if (state != null && state.tags.Count > 0)
                {
                    return "RM_Thought_RumorLabel".Translate(state.tags[0].LabelCap);
                }
                return base.LabelCap;
            }
        }
    }
}
