using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ApprenticeBond
{
    public static class ApprenticeBondUtility
    {
        /// <summary>Re-entry guard so bond Learn() does not cascade into another share.</summary>
        public static bool SharingXp;

        public static bool Enabled =>
            ApprenticeBondMod.Settings == null || ApprenticeBondMod.Settings.modEnabled;

        public static bool IsEligibleColonist(Pawn p)
        {
            return p != null
                   && p.RaceProps != null
                   && p.RaceProps.Humanlike
                   && p.IsColonist
                   && p.skills != null
                   && !p.Dead;
        }

        public static Pawn ResolvePawn(string thingId)
        {
            if (thingId.NullOrEmpty())
            {
                return null;
            }
            // Prefer live maps / world pawns
            List<Map> maps = Find.Maps;
            if (maps != null)
            {
                for (int m = 0; m < maps.Count; m++)
                {
                    Map map = maps[m];
                    List<Pawn> pawns = map?.mapPawns?.AllPawns;
                    if (pawns == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < pawns.Count; i++)
                    {
                        Pawn p = pawns[i];
                        if (p != null && p.ThingID == thingId)
                        {
                            return p;
                        }
                    }
                }
            }
            // World pawns (caravans, etc.) — still no cross-map XP share, but resolve for UI/death
            if (Find.World?.worldPawns != null)
            {
                foreach (Pawn p in Find.World.worldPawns.AllPawnsAliveOrDead)
                {
                    if (p != null && p.ThingID == thingId)
                    {
                        return p;
                    }
                }
            }
            return null;
        }

        public static bool SameRoom(Pawn a, Pawn b)
        {
            if (a == null || b == null || !a.Spawned || !b.Spawned || a.Map != b.Map)
            {
                return false;
            }
            // Outdoor / no-room: require close proximity so "same room" still means co-located work
            Room ra = a.GetRoom();
            Room rb = b.GetRoom();
            if (ra == null || rb == null || ra.PsychologicallyOutdoors || rb.PsychologicallyOutdoors)
            {
                return a.Position.InHorDistOf(b.Position, 8f);
            }
            return ra == rb;
        }

        public static bool IsGraduationEligible(ApprenticeBondEntry entry, Pawn mentor, Pawn apprentice)
        {
            if (entry == null || mentor?.skills == null || apprentice?.skills == null)
            {
                return false;
            }
            SkillDef skill = entry.Skill;
            if (skill == null)
            {
                return false;
            }
            var settings = ApprenticeBondMod.Settings;
            int gap = settings?.graduationLevelGap ?? 2;
            int minTicks = settings?.GraduationMinTicks ?? (30 * GenDate.TicksPerDay);

            SkillRecord m = mentor.skills.GetSkill(skill);
            SkillRecord a = apprentice.skills.GetSkill(skill);
            if (m == null || a == null)
            {
                return false;
            }

            bool byLevel = a.Level >= m.Level - gap;
            bool byTime = Find.TickManager.TicksGame - entry.startTick >= minTicks;
            // Design: skill ≥ mentor −2 OR full X days
            return byLevel || byTime;
        }

        public static string InspectLine(Pawn pawn)
        {
            if (!Enabled || pawn == null)
            {
                return null;
            }
            GameComponent_ApprenticeBond comp = GameComponent_ApprenticeBond.Get();
            ApprenticeBondEntry e = comp?.FindBondInvolving(pawn);
            if (e == null)
            {
                return null;
            }
            SkillDef skill = e.Skill;
            string skillLabel = skill?.label ?? e.skillDefName;
            if (e.mentorId == pawn.ThingID)
            {
                string other = ResolvePawn(e.apprenticeId)?.LabelShort ?? e.apprenticeNameSnapshot ?? "?";
                e.ResetDailyIfNeeded();
                return "AB_Inspect_Mentor".Translate(other, skillLabel, e.dailyXpGranted.ToString("F0"));
            }
            if (e.apprenticeId == pawn.ThingID)
            {
                string other = ResolvePawn(e.mentorId)?.LabelShort ?? e.mentorNameSnapshot ?? "?";
                e.ResetDailyIfNeeded();
                float cap = ApprenticeBondMod.Settings?.EffectiveDailyCap ?? 2000f;
                return "AB_Inspect_Apprentice".Translate(
                    other,
                    skillLabel,
                    e.dailyXpGranted.ToString("F0"),
                    cap.ToString("F0"));
            }
            return null;
        }

        public static void MaybeWarnColonySize()
        {
            var settings = ApprenticeBondMod.Settings;
            if (settings == null || !settings.showColonySizeHint)
            {
                return;
            }
            int colonists = 0;
            List<Map> maps = Find.Maps;
            if (maps == null)
            {
                return;
            }
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (map?.IsPlayerHome != true)
                {
                    continue;
                }
                colonists += map.mapPawns?.FreeColonistsCount ?? 0;
            }
            if (colonists > settings.colonySizeHint)
            {
                Messages.Message(
                    "AB_Msg_ColonySizeHint".Translate(colonists, settings.colonySizeHint),
                    MessageTypeDefOf.SilentInput,
                    historical: false);
            }
        }

        /// <summary>
        /// Mentor gained XP on bonded skill → share to apprentice (same room only) + tiny mentor teach bonus.
        /// Called from Harmony postfix with re-entry guard.
        /// </summary>
        public static void TryShareFromMentorLearn(Pawn mentor, SkillDef skill, float xpGained)
        {
            if (!Enabled || SharingXp || mentor == null || skill == null || xpGained <= 0f)
            {
                return;
            }
            if (!IsEligibleColonist(mentor) || !mentor.Spawned)
            {
                return;
            }
            GameComponent_ApprenticeBond comp = GameComponent_ApprenticeBond.Get();
            ApprenticeBondEntry entry = comp?.FindBondAsMentor(mentor);
            if (entry == null || entry.skillDefName != skill.defName)
            {
                return;
            }
            Pawn apprentice = ResolvePawn(entry.apprenticeId);
            if (apprentice == null || apprentice.Dead || !apprentice.Spawned || apprentice.Map != mentor.Map)
            {
                return; // no cross-map XP
            }
            if (!SameRoom(mentor, apprentice))
            {
                return;
            }
            if (apprentice.skills == null || apprentice.skills.GetSkill(skill) == null
                || apprentice.skills.GetSkill(skill).TotallyDisabled)
            {
                return;
            }

            var settings = ApprenticeBondMod.Settings;
            float share = settings?.EffectiveApprenticeShare ?? 0.20f;
            float teach = settings?.EffectiveMentorTeach ?? 0.05f;

            float remaining = entry.RemainingDailyCap(settings);
            if (remaining <= 0f)
            {
                return;
            }

            float rawShare = xpGained * share;
            float granted = Mathf.Min(rawShare, remaining);
            if (granted <= 0.01f)
            {
                return;
            }

            SharingXp = true;
            try
            {
                // direct: true so learning passion/global factors don't double-dip; still respects TotallyDisabled
                apprentice.skills.Learn(skill, granted, direct: true, ignoreLearnRate: false);
                entry.AddDailyXp(granted);

                // Mentor teaching tip — small, also direct, not re-shared (guard)
                float teachXp = xpGained * teach;
                if (teachXp > 0.01f)
                {
                    mentor.skills.Learn(skill, teachXp, direct: true, ignoreLearnRate: false);
                }
            }
            finally
            {
                SharingXp = false;
            }
        }
    }
}
