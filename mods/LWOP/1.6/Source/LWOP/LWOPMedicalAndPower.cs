using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LWOP.Buildings
{
    public class Recipe_LWOPRemoveBothLegs : RecipeWorker
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn == null || pawn.RaceProps == null || pawn.RaceProps.body == null)
            {
                yield break;
            }

            if (ExistingLegs(pawn).Any())
            {
                yield return pawn.RaceProps.body.corePart;
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (pawn == null)
            {
                return;
            }

            foreach (BodyPartRecord leg in ExistingLegs(pawn).ToList())
            {
                Hediff_MissingPart missingPart = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, leg);
                missingPart.IsFresh = true;
                pawn.health.AddHediff(missingPart, leg);
            }
        }

        public override string GetLabelWhenUsedOn(Pawn pawn, BodyPartRecord part)
        {
            return recipe.label;
        }

        private static IEnumerable<BodyPartRecord> ExistingLegs(Pawn pawn)
        {
            return pawn.RaceProps.body.AllParts
                .Where(part => part.def == BodyPartDefOf.Leg && !pawn.health.hediffSet.PartIsMissing(part));
        }
    }

    public class Recipe_LWOPHarvestRecoverableOrgans : RecipeWorker
    {
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            if (pawn == null || pawn.RaceProps == null || pawn.RaceProps.body == null)
            {
                yield break;
            }

            if (HarvestPlan(pawn).Any())
            {
                yield return pawn.RaceProps.body.corePart;
            }
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (pawn == null)
            {
                return;
            }

            List<Hediff> removableHediffs = RemovableImplants(pawn).ToList();
            List<BodyPartRecord> naturalParts = HarvestPlan(pawn).ToList();

            if (pawn.Spawned && pawn.Map != null)
            {
                foreach (Hediff hediff in removableHediffs)
                {
                    SpawnThing(hediff.def.spawnThingOnRemoved, pawn);
                }

                foreach (BodyPartRecord organ in naturalParts)
                {
                    MedicalRecipesUtility.SpawnNaturalPartIfClean(pawn, organ, pawn.Position, pawn.Map);
                }
            }

            foreach (Hediff hediff in removableHediffs)
            {
                if (pawn.health.hediffSet.hediffs.Contains(hediff))
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }

            foreach (BodyPartRecord organ in naturalParts.OrderBy(RemovalOrder))
            {
                AddMissingPart(pawn, organ);
            }
        }

        public override string GetLabelWhenUsedOn(Pawn pawn, BodyPartRecord part)
        {
            return recipe.label;
        }

        private static IEnumerable<BodyPartRecord> HarvestPlan(Pawn pawn)
        {
            foreach (BodyPartRecord organ in pawn.RaceProps.body.AllParts)
            {
                if (organ.def != null &&
                    organ.def.spawnThingOnRemoved != null &&
                    !pawn.health.hediffSet.PartIsMissing(organ) &&
                    MedicalRecipesUtility.IsCleanAndDroppable(pawn, organ))
                {
                    yield return organ;
                }
            }
        }

        private static IEnumerable<Hediff> RemovableImplants(Pawn pawn)
        {
            return pawn.health.hediffSet.hediffs
                .Where(hediff => hediff != null &&
                    hediff.def != null &&
                    hediff.def.spawnThingOnRemoved != null);
        }

        private static void SpawnThing(ThingDef thingDef, Pawn pawn)
        {
            if (thingDef == null || pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            Thing thing = ThingMaker.MakeThing(thingDef);
            GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
        }

        private static void AddMissingPart(Pawn pawn, BodyPartRecord part)
        {
            if (pawn.health.hediffSet.PartIsMissing(part))
            {
                return;
            }

            Hediff_MissingPart missingPart = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, pawn, part);
            missingPart.IsFresh = true;
            pawn.health.AddHediff(missingPart, part);
        }

        private static int RemovalOrder(BodyPartRecord part)
        {
            if (part.def == null)
            {
                return 0;
            }

            switch (part.def.defName)
            {
                case "Heart":
                    return 30;
                case "Liver":
                    return 20;
                case "Lung":
                    return 10;
                default:
                    return 0;
            }
        }
    }

    public class CompPowerPlantLWOPSolar : CompPowerPlantSolar
    {
        protected override float DesiredPowerOutput
        {
            get
            {
                float solarOutput = base.DesiredPowerOutput;
                return solarOutput < 5000f ? 5000f : solarOutput;
            }
        }
    }
}
