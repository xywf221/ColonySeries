using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PersonalKit
{
    /// <summary>
    /// Old-style surgery: lower psylink (PsychicAmplifier) by 1 and spawn a
    /// psylink neuroformer. Vanilla no longer ships a remove/extract recipe.
    /// Gated by PersonalKitSettings.extractPsylinkLevel (off = never available).
    /// </summary>
    public class Recipe_ExtractPsylinkLevel : Recipe_Surgery
    {
        private static bool Operable(Hediff hediff)
        {
            return hediff is Hediff_Level { level: > 0 } level
                && level.def == HediffDefOf.PsychicAmplifier;
        }

        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            return MedicalRecipesUtility.GetFixedPartsToApplyOn(
                recipe,
                pawn,
                record => pawn.health.hediffSet.hediffs.Any(h => h.Part == record && Operable(h)));
        }

        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.extractPsylinkLevel)
            {
                return false;
            }
            if (!base.AvailableOnNow(thing, part))
            {
                return false;
            }
            if (thing is not Pawn pawn)
            {
                return false;
            }
            return pawn.health?.hediffSet?.hediffs != null
                && pawn.health.hediffSet.hediffs.Any(Operable);
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill))
                {
                    return;
                }
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            }

            Hediff_Level psylink = pawn.health.hediffSet.hediffs
                .FirstOrDefault(h => Operable(h) && h.Part == part) as Hediff_Level;
            if (psylink == null)
            {
                return;
            }

            if (IsViolationOnPawn(pawn, part, Faction.OfPlayer))
            {
                ReportViolation(pawn, billDoer, pawn.HomeFaction, -70);
            }

            // Level 6 → 5 … Level 1 → 0 (ShouldRemove strips the hediff).
            psylink.ChangeLevel(-1);

            ThingDef neuroformer = ThingDefOf.PsychicAmplifier
                ?? DefDatabase<ThingDef>.GetNamedSilentFail("PsychicAmplifier");
            if (neuroformer == null)
            {
                return;
            }

            Thing item = ThingMaker.MakeThing(neuroformer);
            if (billDoer != null && billDoer.Map != null)
            {
                GenPlace.TryPlaceThing(item, billDoer.Position, billDoer.Map, ThingPlaceMode.Near);
            }
            else if (pawn.Map != null)
            {
                GenPlace.TryPlaceThing(item, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }
        }
    }
}
