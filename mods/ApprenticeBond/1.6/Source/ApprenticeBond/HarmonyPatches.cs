using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ApprenticeBond
{
    /// <summary>
    /// When a mentor gains skill XP, share a capped slice to the bonded apprentice if same room.
    /// </summary>
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Learn))]
    public static class Patch_SkillRecord_Learn
    {
        private static readonly AccessTools.FieldRef<SkillRecord, Pawn> PawnField =
            AccessTools.FieldRefAccess<SkillRecord, Pawn>("pawn");

        // Learn(float xp, bool direct = false, bool ignoreLearnRate = false)
        public static void Postfix(SkillRecord __instance, float xp, bool direct, bool ignoreLearnRate)
        {
            if (ApprenticeBondUtility.SharingXp || !ApprenticeBondUtility.Enabled)
            {
                return;
            }
            if (xp <= 0f || __instance?.def == null)
            {
                return;
            }
            Pawn pawn;
            try
            {
                pawn = PawnField(__instance);
            }
            catch
            {
                return;
            }
            if (pawn == null)
            {
                return;
            }
            // Natural work XP. Our own shares use SharingXp guard so they never cascade.
            ApprenticeBondUtility.TryShareFromMentorLearn(pawn, __instance.def, xp);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill
    {
        public static void Prefix(Pawn __instance)
        {
            if (!ApprenticeBondUtility.Enabled || __instance == null)
            {
                return;
            }
            GameComponent_ApprenticeBond.Get()?.NotifyPawnDied(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_Pawn_GetInspectString
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            string extra = ApprenticeBondUtility.InspectLine(__instance);
            if (extra.NullOrEmpty())
            {
                return;
            }
            if (__result.NullOrEmpty())
            {
                __result = extra;
            }
            else
            {
                __result = __result + "\n" + extra;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo g in __result)
            {
                yield return g;
            }

            if (!ApprenticeBondUtility.Enabled
                || __instance == null
                || !__instance.IsColonistPlayerControlled
                || __instance.Downed
                || !ApprenticeBondUtility.IsEligibleColonist(__instance))
            {
                yield break;
            }

            GameComponent_ApprenticeBond comp = GameComponent_ApprenticeBond.Get();
            if (comp == null)
            {
                yield break;
            }

            ApprenticeBondEntry existing = comp.FindBondInvolving(__instance);

            if (existing == null)
            {
                // Form bond: this pawn is mentor; target apprentice then pick skill
                yield return new Command_Action
                {
                    defaultLabel = "AB_Gizmo_FormBond".Translate(),
                    defaultDesc = "AB_Gizmo_FormBondDesc".Translate(),
                    icon = TexCommand.Install,
                    action = () => BeginFormBondTargeting(__instance)
                };
            }
            else
            {
                Pawn mentor = ApprenticeBondUtility.ResolvePawn(existing.mentorId);
                Pawn apprentice = ApprenticeBondUtility.ResolvePawn(existing.apprenticeId);
                string skillLabel = existing.Skill?.LabelCap ?? existing.skillDefName;

                yield return new Command_Action
                {
                    defaultLabel = "AB_Gizmo_BreakBond".Translate(),
                    defaultDesc = "AB_Gizmo_BreakBondDesc".Translate(
                        mentor?.LabelShort ?? existing.mentorNameSnapshot ?? "?",
                        apprentice?.LabelShort ?? existing.apprenticeNameSnapshot ?? "?",
                        skillLabel),
                    icon = TexCommand.ClearPrioritizedWork,
                    action = () =>
                    {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            "AB_Confirm_BreakBond".Translate(
                                mentor?.LabelShort ?? "?",
                                apprentice?.LabelShort ?? "?",
                                skillLabel),
                            () => comp.BreakBond(existing, applyBreakMood: true, "AB_Reason_PlayerBroke"),
                            destructive: true));
                    }
                };

                if (ApprenticeBondUtility.IsGraduationEligible(existing, mentor, apprentice))
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "AB_Gizmo_Graduate".Translate(),
                        defaultDesc = "AB_Gizmo_GraduateDesc".Translate(
                            apprentice?.LabelShort ?? "?",
                            skillLabel),
                        icon = TexCommand.DesirePower,
                        action = () =>
                        {
                            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                                "AB_Confirm_Graduate".Translate(
                                    apprentice?.LabelShort ?? "?",
                                    mentor?.LabelShort ?? "?",
                                    skillLabel),
                                () => comp.Graduate(existing)));
                        }
                    };
                }
            }
        }

        private static void BeginFormBondTargeting(Pawn mentor)
        {
            Find.Targeter.BeginTargeting(new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = false,
                canTargetItems = false,
                canTargetSelf = false,
                mapObjectTargetsMustBeAutoAttackable = false,
                validator = t =>
                {
                    if (!(t.Thing is Pawn p))
                    {
                        return false;
                    }
                    if (!ApprenticeBondUtility.IsEligibleColonist(p) || p == mentor)
                    {
                        return false;
                    }
                    // At least one skill where mentor is higher
                    return GetCandidateSkills(mentor, p).Count > 0;
                }
            },
            loc =>
            {
                Pawn apprentice = loc.Thing as Pawn;
                if (apprentice == null)
                {
                    return;
                }
                List<FloatMenuOption> opts = new List<FloatMenuOption>();
                List<SkillDef> skills = GetCandidateSkills(mentor, apprentice);
                for (int i = 0; i < skills.Count; i++)
                {
                    SkillDef skill = skills[i];
                    int mLv = mentor.skills.GetSkill(skill).Level;
                    int aLv = apprentice.skills.GetSkill(skill).Level;
                    opts.Add(new FloatMenuOption(
                        "AB_Float_SkillChoice".Translate(skill.LabelCap, mLv, aLv),
                        () =>
                        {
                            GameComponent_ApprenticeBond.Get()?.TryFormBond(mentor, apprentice, skill);
                        }));
                }
                if (opts.Count == 0)
                {
                    Messages.Message("AB_Fail_NoCandidateSkill".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            },
            null,
            null,
            null);
        }

        private static List<SkillDef> GetCandidateSkills(Pawn mentor, Pawn apprentice)
        {
            var list = new List<SkillDef>();
            if (mentor?.skills?.skills == null || apprentice?.skills == null)
            {
                return list;
            }
            GameComponent_ApprenticeBond comp = GameComponent_ApprenticeBond.Get();
            for (int i = 0; i < mentor.skills.skills.Count; i++)
            {
                SkillRecord rec = mentor.skills.skills[i];
                if (rec == null || rec.def == null || rec.TotallyDisabled)
                {
                    continue;
                }
                AcceptanceReport ok = comp != null
                    ? comp.CanFormBond(mentor, apprentice, rec.def)
                    : AcceptanceReport.WasAccepted;
                // CanFormBond also checks busy — when targeting we already know free; still use level check
                SkillRecord aRec = apprentice.skills.GetSkill(rec.def);
                if (aRec == null || aRec.TotallyDisabled)
                {
                    continue;
                }
                if (rec.Level > aRec.Level)
                {
                    list.Add(rec.def);
                }
            }
            // Prefer higher mentor advantage first
            list.Sort((a, b) =>
            {
                int da = mentor.skills.GetSkill(a).Level - apprentice.skills.GetSkill(a).Level;
                int db = mentor.skills.GetSkill(b).Level - apprentice.skills.GetSkill(b).Level;
                return db.CompareTo(da);
            });
            return list;
        }
    }
}
