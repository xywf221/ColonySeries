using RimWorld;
using Verse;

namespace ApprenticeBond
{
    /// <summary>
    /// One mentor + one apprentice on a single skill. No multi-web: a pawn is in at most one bond.
    /// </summary>
    public class ApprenticeBondEntry : IExposable
    {
        public string mentorId;
        public string apprenticeId;
        public string skillDefName;
        public int startTick;
        public float dailyXpGranted;
        public int dailyXpDayKey = -1;
        public bool graduationSuggested;
        public string mentorNameSnapshot;
        public string apprenticeNameSnapshot;

        public SkillDef Skill
        {
            get
            {
                if (skillDefName.NullOrEmpty())
                {
                    return null;
                }
                return DefDatabase<SkillDef>.GetNamedSilentFail(skillDefName);
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref mentorId, "mentorId");
            Scribe_Values.Look(ref apprenticeId, "apprenticeId");
            Scribe_Values.Look(ref skillDefName, "skillDefName");
            Scribe_Values.Look(ref startTick, "startTick", 0);
            Scribe_Values.Look(ref dailyXpGranted, "dailyXpGranted", 0f);
            Scribe_Values.Look(ref dailyXpDayKey, "dailyXpDayKey", -1);
            Scribe_Values.Look(ref graduationSuggested, "graduationSuggested", false);
            Scribe_Values.Look(ref mentorNameSnapshot, "mentorNameSnapshot");
            Scribe_Values.Look(ref apprenticeNameSnapshot, "apprenticeNameSnapshot");
        }

        public void ResetDailyIfNeeded()
        {
            int day = GenDate.DaysPassed;
            if (dailyXpDayKey != day)
            {
                dailyXpDayKey = day;
                dailyXpGranted = 0f;
            }
        }

        public float RemainingDailyCap(ApprenticeBondSettings settings)
        {
            ResetDailyIfNeeded();
            float cap = settings?.EffectiveDailyCap ?? 2000f;
            return cap - dailyXpGranted;
        }

        public void AddDailyXp(float amount)
        {
            ResetDailyIfNeeded();
            dailyXpGranted += amount;
        }
    }
}
