using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace TraitExtractor
{
    public class Recipe_ExtractTrait : Recipe_Surgery
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (!(thing is Pawn pawn) || !TraitUtility.HasStory(pawn))
            {
                return false;
            }
            if (!pawn.RaceProps.Humanlike)
            {
                return false;
            }
            if (pawn.DevelopmentalStage != DevelopmentalStage.Adult)
            {
                return false;
            }
            if (TraitUtility.IsOnCooldown(pawn))
            {
                return false;
            }
            if (TraitUtility.ExtractableTraits(pawn).Count == 0)
            {
                return false;
            }
            if (pawn.IsColonist && TraitExtractorMod.Settings != null && !TraitExtractorMod.Settings.allowExtractFromColonists)
            {
                return false;
            }
            return true;
        }

        public override string GetLabelWhenUsedOn(Pawn pawn, BodyPartRecord part)
        {
            int n = TraitUtility.ExtractableTraits(pawn).Count;
            return "TE_ExtractLabel".Translate(n);
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    ApplyTrauma(pawn, failed: true);
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            }

            List<Trait> candidates = TraitUtility.ExtractableTraits(pawn);
            if (candidates.Count == 0)
            {
                Messages.Message("TE_Msg_NoExtractable".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // Multiple traits: let the player choose. Single trait: extract immediately.
            if (candidates.Count == 1 || Find.WindowStack == null)
            {
                FinishExtraction(pawn, billDoer, part, candidates[0]);
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (Trait trait in candidates)
            {
                Trait local = trait;
                options.Add(new FloatMenuOption(
                    local.LabelCap,
                    () =>
                    {
                        // Patient may have died / lost the trait while the menu was open.
                        if (pawn.Destroyed || pawn.Dead || !TraitUtility.HasStory(pawn))
                        {
                            return;
                        }
                        Trait still = pawn.story.traits.GetTrait(local.def, local.Degree);
                        if (still == null || !TraitUtility.CanExtractTrait(still, out _))
                        {
                            Messages.Message("TE_Msg_NoExtractable".Translate(pawn.LabelShort), pawn, MessageTypeDefOf.RejectInput, historical: false);
                            return;
                        }
                        FinishExtraction(pawn, billDoer, part, still);
                    }));
            }
            options.Add(new FloatMenuOption(
                "TE_ExtractRandom".Translate(),
                () =>
                {
                    if (pawn.Destroyed || pawn.Dead || !TraitUtility.HasStory(pawn))
                    {
                        return;
                    }
                    List<Trait> again = TraitUtility.ExtractableTraits(pawn);
                    if (again.Count == 0)
                    {
                        return;
                    }
                    FinishExtraction(pawn, billDoer, part, again.RandomElement());
                }));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void FinishExtraction(Pawn pawn, Pawn billDoer, BodyPartRecord part, Trait chosen)
        {
            if (chosen == null || !TraitUtility.HasStory(pawn))
            {
                return;
            }

            TraitDef def = chosen.def;
            int degree = chosen.Degree;
            string label = chosen.LabelCap;
            string source = pawn.LabelShort;

            Thing serum = TraitUtility.MakeSerum(def, degree, source);
            pawn.story.traits.RemoveTrait(chosen);

            ApplyTrauma(pawn, failed: false);
            TraitUtility.GiveCooldown(pawn);
            TraitUtility.GiveThought(pawn, TraitExtractorDefOf.TE_HadTraitExtracted);
            TraitUtility.NotifyNearbyWitnesses(pawn, pawn.Map);

            // Only foreign pawns count as harmful medical violations.
            if (billDoer != null
                && pawn.Faction != null
                && pawn.Faction != Faction.OfPlayer
                && IsViolationOnPawn(pawn, part, Faction.OfPlayer))
            {
                ReportViolation(pawn, billDoer, pawn.HomeFaction ?? pawn.Faction, -25, HistoryEventDefOf.PerformedHarmfulSurgery);
            }

            if (pawn.Map != null)
            {
                GenPlace.TryPlaceThing(serum, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }

            Messages.Message(
                "TE_Msg_ExtractSuccess".Translate(source, label),
                new LookTargets(new TargetInfo[] { pawn, serum }),
                MessageTypeDefOf.NeutralEvent);
        }

        private void ApplyTrauma(Pawn pawn, bool failed)
        {
            if (pawn?.health == null)
            {
                return;
            }
            if (TraitExtractorDefOf.TE_NeuralTrauma != null)
            {
                Hediff trauma = HediffMaker.MakeHediff(TraitExtractorDefOf.TE_NeuralTrauma, pawn);
                trauma.Severity = failed
                    ? 1f
                    : (TraitExtractorMod.Settings != null && TraitExtractorMod.Settings.easyMode ? 0.55f : 0.85f);
                pawn.health.AddHediff(trauma);
            }

            float scarChance = TraitExtractorMod.Settings?.ScarChance ?? 0.35f;
            if (failed)
            {
                scarChance = Mathf.Clamp01(scarChance + 0.35f);
            }
            if (Rand.Chance(scarChance)
                && TraitExtractorDefOf.TE_NeuralScar != null
                && !pawn.health.hediffSet.HasHediff(TraitExtractorDefOf.TE_NeuralScar))
            {
                pawn.health.AddHediff(TraitExtractorDefOf.TE_NeuralScar);
            }
        }
    }
}
