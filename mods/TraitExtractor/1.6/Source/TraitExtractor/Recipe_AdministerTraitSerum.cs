using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TraitExtractor
{
    /// <summary>
    /// Doctor-administered injection via medical bill (works on prisoners / bedridden).
    /// </summary>
    public class Recipe_AdministerTraitSerum : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            return thing is Pawn p && TraitUtility.HasStory(p) && p.RaceProps.Humanlike;
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            Thing serum = ingredients?.FirstOrDefault(t => t.def == TraitExtractorDefOf.TE_TraitSerum);
            if (serum == null)
            {
                // Fallback: find nearby / inventory not needed — bill system provides ingredients.
                Messages.Message("TE_Reason_SerumEmpty".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            CompTraitSerum comp = serum.TryGetComp<CompTraitSerum>();
            if (comp == null || !comp.HasTrait)
            {
                Messages.Message("TE_Reason_SerumEmpty".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                serum.Destroy();
                return;
            }

            AcceptanceReport report = CanInjectReport(pawn, comp);
            if (!report.Accepted)
            {
                Messages.Message(report.Reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                // Don't destroy serum on soft fail — put it back if possible.
                if (!serum.Destroyed && pawn.Map != null)
                {
                    GenPlace.TryPlaceThing(serum, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                }
                return;
            }

            // Consume serum (bill may have already reserved; destroy after).
            TraitDef def = comp.traitDef;
            int degree = comp.degree;
            if (!serum.Destroyed)
            {
                serum.Destroy();
            }

            CompUseEffect_InjectTrait.TryInject(pawn, def, degree, destroyOnFail: false);
            if (billDoer != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            }
        }

        private static AcceptanceReport CanInjectReport(Pawn p, CompTraitSerum serum)
        {
            if (TraitUtility.IsOnCooldown(p))
            {
                return "TE_Reason_Cooldown".Translate();
            }
            if (TraitUtility.HasSameTrait(p, serum.traitDef, serum.degree))
            {
                return "TE_Reason_AlreadyHas".Translate(new Trait(serum.traitDef, serum.degree).LabelCap);
            }
            List<Trait> conflicts = TraitUtility.ConflictingTraits(p, serum.traitDef, serum.degree);
            if (conflicts.Count > 0 && TraitExtractorMod.Settings != null && !TraitExtractorMod.Settings.allowConflictReplace)
            {
                return "TE_Reason_Conflict".Translate(string.Join(", ", conflicts.Select(t => t.LabelCap)));
            }
            int count = p.story.traits.allTraits.Count(t => !t.Suppressed);
            int max = TraitExtractorMod.Settings?.MaxTraits ?? 4;
            if (count - conflicts.Count + 1 > max)
            {
                return "TE_Reason_MaxTraits".Translate(max);
            }
            return true;
        }

        // Ingredients are consumed by bill framework; override to avoid double-destroy when we re-place.
        public override void ConsumeIngredient(Thing ingredient, RecipeDef recipe, Map map)
        {
            // Trait serum is destroyed in ApplyOnPawn after reading data.
            if (ingredient.def == TraitExtractorDefOf.TE_TraitSerum)
            {
                return;
            }
            base.ConsumeIngredient(ingredient, recipe, map);
        }
    }
}
