using RimWorld;
using Verse;

namespace ApprenticeBond
{
    /// <summary>While bonded: +mood for mentor and apprentice (design +2/+2).</summary>
    public class ThoughtWorker_ApprenticeBonded : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var s = ApprenticeBondMod.Settings;
            if (s == null || !s.modEnabled || !s.enableMoodThoughts || p == null)
            {
                return ThoughtState.Inactive;
            }
            GameComponent_ApprenticeBond comp = GameComponent_ApprenticeBond.Get();
            ApprenticeBondEntry e = comp?.FindBondInvolving(p);
            if (e == null)
            {
                return ThoughtState.Inactive;
            }
            string otherName;
            if (e.mentorId == p.ThingID)
            {
                otherName = ApprenticeBondUtility.ResolvePawn(e.apprenticeId)?.LabelShort
                            ?? e.apprenticeNameSnapshot
                            ?? "?";
            }
            else
            {
                otherName = ApprenticeBondUtility.ResolvePawn(e.mentorId)?.LabelShort
                            ?? e.mentorNameSnapshot
                            ?? "?";
            }
            string skill = e.Skill?.label ?? e.skillDefName ?? "?";
            return ThoughtState.ActiveAtStage(0, otherName + " / " + skill);
        }
    }

    public class Thought_ApprenticeBonded : Thought_Situational
    {
        public override float MoodOffset()
        {
            var s = ApprenticeBondMod.Settings;
            if (s == null)
            {
                return base.MoodOffset();
            }
            return s.bondMood;
        }
    }

    public class Thought_BondBroken : Thought_Memory
    {
        public override float MoodOffset()
        {
            var s = ApprenticeBondMod.Settings;
            if (s == null)
            {
                return base.MoodOffset();
            }
            return s.breakMood;
        }
    }

    public class Thought_MentorDied : Thought_Memory
    {
        public override float MoodOffset()
        {
            var s = ApprenticeBondMod.Settings;
            if (s == null)
            {
                return base.MoodOffset();
            }
            // Design −8～−12; default −10
            return s.mentorDeathMood;
        }
    }

    public class Thought_Graduated : Thought_Memory
    {
        public override float MoodOffset()
        {
            var s = ApprenticeBondMod.Settings;
            if (s == null)
            {
                return base.MoodOffset();
            }
            return s.graduationMood;
        }
    }
}
