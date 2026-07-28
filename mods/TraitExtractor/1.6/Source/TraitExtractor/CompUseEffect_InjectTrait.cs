using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace TraitExtractor
{
    public class CompUseEffect_InjectTrait : CompUseEffect
    {
        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            if (!TraitUtility.HasStory(p))
            {
                return "TE_Reason_NoStory".Translate();
            }
            CompTraitSerum serum = parent.TryGetComp<CompTraitSerum>();
            if (serum == null || !serum.HasTrait)
            {
                return "TE_Reason_SerumEmpty".Translate();
            }
            if (TraitUtility.IsOnCooldown(p))
            {
                return "TE_Reason_Cooldown".Translate();
            }
            if (TraitUtility.HasSameTrait(p, serum.traitDef, serum.degree))
            {
                return "TE_Reason_AlreadyHas".Translate(new Trait(serum.traitDef, serum.degree).LabelCap);
            }
            List<Trait> conflicts = TraitUtility.ConflictingTraits(p, serum.traitDef, serum.degree);
            if (conflicts.Count > 0 && (TraitExtractorMod.Settings == null || !TraitExtractorMod.Settings.allowConflictReplace))
            {
                return "TE_Reason_Conflict".Translate(string.Join(", ", conflicts.Select(t => t.LabelCap)));
            }
            int count = p.story.traits.allTraits.Count(t => !t.Suppressed);
            int max = TraitExtractorMod.Settings?.MaxTraits ?? 4;
            // If conflicts will free slots, allow when after-replace count would fit.
            int after = count - conflicts.Count + 1;
            if (after > max)
            {
                return "TE_Reason_MaxTraits".Translate(max);
            }
            return true;
        }

        public override TaggedString ConfirmMessage(Pawn p)
        {
            CompTraitSerum serum = parent.TryGetComp<CompTraitSerum>();
            if (serum == null || !serum.HasTrait)
            {
                return null;
            }
            Trait t = new Trait(serum.traitDef, serum.degree);
            List<Trait> conflicts = TraitUtility.ConflictingTraits(p, serum.traitDef, serum.degree);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("TE_ConfirmInject".Translate(p.LabelShort, t.LabelCap));
            if (conflicts.Count > 0)
            {
                sb.AppendLine("TE_ConfirmConflicts".Translate(string.Join(", ", conflicts.Select(c => c.LabelCap))));
            }
            sb.AppendLine("TE_ConfirmRisk".Translate((int)((1f - (TraitExtractorMod.Settings?.InjectSuccess ?? 0.85f)) * 100f)));
            return sb.ToString().TrimEnd();
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            CompTraitSerum serum = parent.TryGetComp<CompTraitSerum>();
            if (serum == null || !serum.HasTrait)
            {
                return;
            }
            TryInject(usedBy, serum.traitDef, serum.degree, destroyOnFail: false);
            // CompUseEffectDestroySelf handles destruction after successful use path.
        }

        public static bool TryInject(Pawn pawn, TraitDef def, int degree, bool destroyOnFail)
        {
            if (!TraitUtility.HasStory(pawn) || def == null)
            {
                return false;
            }

            float success = TraitExtractorMod.Settings?.InjectSuccess ?? 0.85f;
            if (!Rand.Chance(success))
            {
                // Rejection: adaptation sickness, no trait, still cooldown.
                if (TraitExtractorDefOf.TE_NeuroAdaptation != null)
                {
                    Hediff adapt = HediffMaker.MakeHediff(TraitExtractorDefOf.TE_NeuroAdaptation, pawn);
                    adapt.Severity = 1f;
                    pawn.health.AddHediff(adapt);
                }
                TraitUtility.GiveCooldown(pawn);
                Messages.Message("TE_Msg_InjectReject".Translate(pawn.LabelShort, new Trait(def, degree).LabelCap),
                    pawn, MessageTypeDefOf.NegativeEvent);
                return false;
            }

            List<Trait> conflicts = TraitUtility.ConflictingTraits(pawn, def, degree);
            if (conflicts.Count > 0)
            {
                if (TraitExtractorMod.Settings != null && !TraitExtractorMod.Settings.allowConflictReplace)
                {
                    Messages.Message("TE_Reason_Conflict".Translate(string.Join(", ", conflicts.Select(t => t.LabelCap))),
                        pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return false;
                }
                foreach (Trait c in conflicts.ToList())
                {
                    pawn.story.traits.RemoveTrait(c);
                }
            }

            if (TraitUtility.HasSameTrait(pawn, def, degree))
            {
                return false;
            }

            int count = pawn.story.traits.allTraits.Count(t => !t.Suppressed);
            int max = TraitExtractorMod.Settings?.MaxTraits ?? 4;
            if (count >= max)
            {
                Messages.Message("TE_Reason_MaxTraits".Translate(max), pawn, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            pawn.story.traits.GainTrait(new Trait(def, degree));

            if (TraitExtractorDefOf.TE_NeuroAdaptation != null)
            {
                Hediff adapt = HediffMaker.MakeHediff(TraitExtractorDefOf.TE_NeuroAdaptation, pawn);
                adapt.Severity = TraitExtractorMod.Settings != null && TraitExtractorMod.Settings.easyMode ? 0.45f : 0.75f;
                pawn.health.AddHediff(adapt);
            }
            TraitUtility.GiveCooldown(pawn);
            TraitUtility.GiveThought(pawn, TraitExtractorDefOf.TE_InjectedTrait);

            string conflictNote = conflicts.Count > 0
                ? "TE_Msg_InjectReplaced".Translate(string.Join(", ", conflicts.Select(t => t.LabelCap)))
                : string.Empty;
            Messages.Message(
                "TE_Msg_InjectSuccess".Translate(pawn.LabelShort, new Trait(def, degree).LabelCap) + conflictNote,
                pawn, MessageTypeDefOf.PositiveEvent);
            return true;
        }
    }
}
