using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ApprenticeBond
{
    public class GameComponent_ApprenticeBond : GameComponent
    {
        private List<ApprenticeBondEntry> bonds = new List<ApprenticeBondEntry>();
        private int nextCheckTick;
        private static int CheckInterval => GenDate.TicksPerDay / 4; // 6h pulse, not per-tick mesh

        public GameComponent_ApprenticeBond(Game game)
        {
        }

        public static GameComponent_ApprenticeBond Get()
        {
            return Current.Game?.GetComponent<GameComponent_ApprenticeBond>();
        }

        public IReadOnlyList<ApprenticeBondEntry> Bonds => bonds;

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref bonds, "bonds", LookMode.Deep);
            Scribe_Values.Look(ref nextCheckTick, "nextCheckTick", 0);
            if (bonds == null)
            {
                bonds = new List<ApprenticeBondEntry>();
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (nextCheckTick <= 0)
            {
                nextCheckTick = Find.TickManager.TicksGame + CheckInterval;
            }
        }

        public override void GameComponentTick()
        {
            if (!ApprenticeBondUtility.Enabled || Current.ProgramState != ProgramState.Playing)
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            if (now < nextCheckTick)
            {
                return;
            }
            nextCheckTick = now + CheckInterval;
            CleanupAndSuggest();
        }

        public ApprenticeBondEntry FindBondInvolving(Pawn pawn)
        {
            if (pawn == null || bonds.Count == 0)
            {
                return null;
            }
            string id = pawn.ThingID;
            for (int i = 0; i < bonds.Count; i++)
            {
                ApprenticeBondEntry e = bonds[i];
                if (e != null && (e.mentorId == id || e.apprenticeId == id))
                {
                    return e;
                }
            }
            return null;
        }

        public ApprenticeBondEntry FindBondAsMentor(Pawn mentor)
        {
            if (mentor == null)
            {
                return null;
            }
            string id = mentor.ThingID;
            for (int i = 0; i < bonds.Count; i++)
            {
                ApprenticeBondEntry e = bonds[i];
                if (e != null && e.mentorId == id)
                {
                    return e;
                }
            }
            return null;
        }

        public ApprenticeBondEntry FindBondAsApprentice(Pawn apprentice)
        {
            if (apprentice == null)
            {
                return null;
            }
            string id = apprentice.ThingID;
            for (int i = 0; i < bonds.Count; i++)
            {
                ApprenticeBondEntry e = bonds[i];
                if (e != null && e.apprenticeId == id)
                {
                    return e;
                }
            }
            return null;
        }

        public AcceptanceReport CanFormBond(Pawn mentor, Pawn apprentice, SkillDef skill)
        {
            if (!ApprenticeBondUtility.Enabled)
            {
                return "AB_Fail_Disabled".Translate();
            }
            if (mentor == null || apprentice == null || skill == null)
            {
                return "AB_Fail_Invalid".Translate();
            }
            if (mentor == apprentice)
            {
                return "AB_Fail_Self".Translate();
            }
            if (!ApprenticeBondUtility.IsEligibleColonist(mentor) || !ApprenticeBondUtility.IsEligibleColonist(apprentice))
            {
                return "AB_Fail_NotColonist".Translate();
            }
            if (FindBondInvolving(mentor) != null)
            {
                return "AB_Fail_MentorBusy".Translate(mentor.LabelShort);
            }
            if (FindBondInvolving(apprentice) != null)
            {
                return "AB_Fail_ApprenticeBusy".Translate(apprentice.LabelShort);
            }
            if (mentor.skills == null || apprentice.skills == null)
            {
                return "AB_Fail_NoSkills".Translate();
            }
            SkillRecord mRec = mentor.skills.GetSkill(skill);
            SkillRecord aRec = apprentice.skills.GetSkill(skill);
            if (mRec == null || aRec == null)
            {
                return "AB_Fail_NoSkills".Translate();
            }
            if (mRec.TotallyDisabled || aRec.TotallyDisabled)
            {
                return "AB_Fail_SkillDisabled".Translate(skill.LabelCap);
            }
            if (mRec.Level <= aRec.Level)
            {
                return "AB_Fail_MentorNotHigher".Translate(mentor.LabelShort, skill.label, apprentice.LabelShort);
            }
            return AcceptanceReport.WasAccepted;
        }

        public bool TryFormBond(Pawn mentor, Pawn apprentice, SkillDef skill)
        {
            AcceptanceReport ok = CanFormBond(mentor, apprentice, skill);
            if (!ok.Accepted)
            {
                Messages.Message(ok.Reason, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            var entry = new ApprenticeBondEntry
            {
                mentorId = mentor.ThingID,
                apprenticeId = apprentice.ThingID,
                skillDefName = skill.defName,
                startTick = Find.TickManager.TicksGame,
                mentorNameSnapshot = mentor.LabelShort,
                apprenticeNameSnapshot = apprentice.LabelShort,
                dailyXpDayKey = GenDate.DaysPassed,
                dailyXpGranted = 0f
            };
            bonds.Add(entry);

            if (ApprenticeBondMod.Settings != null && ApprenticeBondMod.Settings.enableMoodThoughts)
            {
                // Bond mood is situational via ThoughtWorker; optional short celebrate memory
                TryAddMemory(mentor, ApprenticeBondDefOf.AB_BondFormed, apprentice);
                TryAddMemory(apprentice, ApprenticeBondDefOf.AB_BondFormed, mentor);
            }

            Messages.Message(
                "AB_Msg_BondFormed".Translate(mentor.LabelShort, apprentice.LabelShort, skill.LabelCap),
                new LookTargets(new TargetInfo[] { mentor, apprentice }),
                MessageTypeDefOf.PositiveEvent);

            ApprenticeBondUtility.MaybeWarnColonySize();
            return true;
        }

        public void BreakBond(ApprenticeBondEntry entry, bool applyBreakMood, string reasonKey = null)
        {
            if (entry == null)
            {
                return;
            }
            Pawn mentor = ApprenticeBondUtility.ResolvePawn(entry.mentorId);
            Pawn apprentice = ApprenticeBondUtility.ResolvePawn(entry.apprenticeId);
            bonds.Remove(entry);

            if (applyBreakMood && ApprenticeBondMod.Settings != null && ApprenticeBondMod.Settings.enableMoodThoughts)
            {
                if (mentor != null && !mentor.Dead)
                {
                    TryAddMemory(mentor, ApprenticeBondDefOf.AB_BondBroken, apprentice);
                }
                if (apprentice != null && !apprentice.Dead)
                {
                    TryAddMemory(apprentice, ApprenticeBondDefOf.AB_BondBroken, mentor);
                }
            }

            string reason = reasonKey != null ? reasonKey.Translate() : "AB_Msg_BondBroken".Translate();
            string mName = mentor?.LabelShort ?? entry.mentorNameSnapshot ?? "?";
            string aName = apprentice?.LabelShort ?? entry.apprenticeNameSnapshot ?? "?";
            Messages.Message(
                "AB_Msg_BondBrokenDetail".Translate(mName, aName, reason),
                MessageTypeDefOf.NeutralEvent);
        }

        public void Graduate(ApprenticeBondEntry entry)
        {
            if (entry == null)
            {
                return;
            }
            Pawn mentor = ApprenticeBondUtility.ResolvePawn(entry.mentorId);
            Pawn apprentice = ApprenticeBondUtility.ResolvePawn(entry.apprenticeId);
            SkillDef skill = entry.Skill;
            bonds.Remove(entry);

            if (ApprenticeBondMod.Settings != null && ApprenticeBondMod.Settings.enableMoodThoughts)
            {
                if (mentor != null && !mentor.Dead)
                {
                    TryAddMemory(mentor, ApprenticeBondDefOf.AB_GraduatedMentor, apprentice);
                }
                if (apprentice != null && !apprentice.Dead)
                {
                    TryAddMemory(apprentice, ApprenticeBondDefOf.AB_GraduatedApprentice, mentor);
                }
            }

            string skillLabel = skill?.LabelCap ?? entry.skillDefName;
            List<TargetInfo> gradTargets = new List<TargetInfo>();
            if (apprentice != null)
            {
                gradTargets.Add(apprentice);
            }
            if (mentor != null)
            {
                gradTargets.Add(mentor);
            }
            Messages.Message(
                "AB_Msg_Graduated".Translate(
                    apprentice?.LabelShort ?? entry.apprenticeNameSnapshot ?? "?",
                    mentor?.LabelShort ?? entry.mentorNameSnapshot ?? "?",
                    skillLabel),
                new LookTargets(gradTargets),
                MessageTypeDefOf.PositiveEvent);
        }

        public int DissolveAll(bool breakMood)
        {
            int n = bonds.Count;
            // Copy — BreakBond mutates list
            List<ApprenticeBondEntry> copy = new List<ApprenticeBondEntry>(bonds);
            for (int i = 0; i < copy.Count; i++)
            {
                BreakBond(copy[i], breakMood, "AB_Reason_Dissolved");
            }
            return n;
        }

        public void NotifyPawnDied(Pawn dead)
        {
            if (dead == null || bonds.Count == 0)
            {
                return;
            }
            string id = dead.ThingID;
            List<ApprenticeBondEntry> doomed = null;
            for (int i = 0; i < bonds.Count; i++)
            {
                ApprenticeBondEntry e = bonds[i];
                if (e == null)
                {
                    continue;
                }
                if (e.mentorId == id)
                {
                    // Mentor died → trauma for apprentice, then remove bond
                    Pawn apprentice = ApprenticeBondUtility.ResolvePawn(e.apprenticeId);
                    if (apprentice != null && !apprentice.Dead
                        && ApprenticeBondMod.Settings != null
                        && ApprenticeBondMod.Settings.enableMoodThoughts)
                    {
                        TryAddMemory(apprentice, ApprenticeBondDefOf.AB_MentorDied, dead);
                    }
                    if (doomed == null)
                    {
                        doomed = new List<ApprenticeBondEntry>();
                    }
                    doomed.Add(e);
                }
                else if (e.apprenticeId == id)
                {
                    if (doomed == null)
                    {
                        doomed = new List<ApprenticeBondEntry>();
                    }
                    doomed.Add(e);
                }
            }
            if (doomed == null)
            {
                return;
            }
            for (int i = 0; i < doomed.Count; i++)
            {
                ApprenticeBondEntry e = doomed[i];
                bonds.Remove(e);
                string mName = e.mentorNameSnapshot ?? "?";
                string aName = e.apprenticeNameSnapshot ?? "?";
                if (e.mentorId == id)
                {
                    Messages.Message(
                        "AB_Msg_MentorDied".Translate(mName, aName),
                        MessageTypeDefOf.NegativeEvent);
                }
                else
                {
                    Messages.Message(
                        "AB_Msg_ApprenticeDied".Translate(aName, mName),
                        MessageTypeDefOf.NeutralEvent);
                }
            }
        }

        private void CleanupAndSuggest()
        {
            if (bonds.Count == 0)
            {
                return;
            }
            List<ApprenticeBondEntry> remove = null;
            for (int i = 0; i < bonds.Count; i++)
            {
                ApprenticeBondEntry e = bonds[i];
                if (e == null)
                {
                    if (remove == null)
                    {
                        remove = new List<ApprenticeBondEntry>();
                    }
                    remove.Add(e);
                    continue;
                }
                Pawn mentor = ApprenticeBondUtility.ResolvePawn(e.mentorId);
                Pawn apprentice = ApprenticeBondUtility.ResolvePawn(e.apprenticeId);
                if (mentor == null || mentor.Dead || apprentice == null || apprentice.Dead)
                {
                    // Death path should already handle; residual cleanup without double mood
                    if (remove == null)
                    {
                        remove = new List<ApprenticeBondEntry>();
                    }
                    remove.Add(e);
                    continue;
                }
                if (!ApprenticeBondUtility.IsEligibleColonist(mentor) || !ApprenticeBondUtility.IsEligibleColonist(apprentice))
                {
                    if (remove == null)
                    {
                        remove = new List<ApprenticeBondEntry>();
                    }
                    remove.Add(e);
                    continue;
                }

                if (ApprenticeBondMod.Settings != null
                    && ApprenticeBondMod.Settings.autoSuggestGraduation
                    && !e.graduationSuggested
                    && ApprenticeBondUtility.IsGraduationEligible(e, mentor, apprentice))
                {
                    e.graduationSuggested = true;
                    Messages.Message(
                        "AB_Msg_ReadyToGraduate".Translate(
                            apprentice.LabelShort,
                            mentor.LabelShort,
                            e.Skill?.LabelCap ?? e.skillDefName),
                        new LookTargets(new TargetInfo[] { apprentice, mentor }),
                        MessageTypeDefOf.PositiveEvent);
                }
            }
            if (remove != null)
            {
                for (int i = 0; i < remove.Count; i++)
                {
                    bonds.Remove(remove[i]);
                }
            }
        }

        private static void TryAddMemory(Pawn pawn, ThoughtDef def, Pawn other)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null || def == null)
            {
                return;
            }
            if (other != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(def, other);
            }
            else
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(def);
            }
        }
    }
}
