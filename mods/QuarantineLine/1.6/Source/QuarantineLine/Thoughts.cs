using RimWorld;
using Verse;

namespace QuarantineLine
{
    public class Thought_Quarantined : Thought_Situational
    {
        public override float MoodOffset()
        {
            QuarantineLineSettings s = QuarantineLineMod.Settings;
            if (s == null)
            {
                return base.MoodOffset();
            }
            // Stage 0 open ward, stage 1 strict.
            return CurStageIndex >= 1 ? s.StrictPatientMood : s.PatientMood;
        }
    }

    public class Thought_VisitingWard : Thought_Situational
    {
        public override float MoodOffset()
        {
            QuarantineLineSettings s = QuarantineLineMod.Settings;
            if (s == null)
            {
                return base.MoodOffset();
            }
            // Mild unease — half of patient open-ward penalty, at least -1 when enabled.
            int v = s.PatientMood; // already negative
            if (v >= 0)
            {
                return 0f;
            }
            return (int)(v * 0.5f);
        }
    }

    public class Thought_IsolationRounds : Thought_Situational
    {
        public override float MoodOffset()
        {
            QuarantineLineSettings s = QuarantineLineMod.Settings;
            if (s == null)
            {
                return base.MoodOffset();
            }
            return s.DoctorMood;
        }
    }
}
