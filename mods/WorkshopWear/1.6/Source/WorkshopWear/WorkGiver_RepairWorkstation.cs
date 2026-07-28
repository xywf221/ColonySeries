using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WorkshopWear
{
    public abstract class WorkGiver_RepairWorkstationBase : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial);

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            var settings = WorkshopWearMod.Settings;
            if (settings != null && !settings.modEnabled)
            {
                return true;
            }
            return pawn.Map?.listerBuildings?.allBuildingsColonist == null
                   || pawn.Map.listerBuildings.allBuildingsColonist.Count == 0;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            List<Building> list = pawn.Map.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < list.Count; i++)
            {
                Building b = list[i];
                CompWorkshopWear comp = b.GetComp<CompWorkshopWear>();
                if (comp != null && IsCandidate(comp))
                {
                    yield return b;
                }
            }
        }

        protected abstract bool IsCandidate(CompWorkshopWear comp);

        protected abstract bool HasMaterials(Map map);

        protected abstract JobDef JobDef { get; }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompWorkshopWear comp = t.TryGetComp<CompWorkshopWear>();
            if (comp == null || !IsCandidate(comp))
            {
                return false;
            }
            if (t.IsForbidden(pawn) || t.IsBurning())
            {
                return false;
            }
            if (!pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }
            if (!pawn.CanReach(t, PathEndMode.InteractionCell, Danger.Deadly))
            {
                return false;
            }
            if (!HasMaterials(pawn.Map))
            {
                JobFailReason.Is("WW_Fail_NoMaterials".Translate());
                return false;
            }
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(JobDef, t);
        }
    }

    /// <summary>Cheap steel slapdash repair — does not clear jam.</summary>
    public class WorkGiver_SlapdashRepair : WorkGiver_RepairWorkstationBase
    {
        protected override JobDef JobDef =>
            DefDatabase<JobDef>.GetNamed("WW_SlapdashRepair");

        protected override bool IsCandidate(CompWorkshopWear comp) => comp.NeedsSlapdash;

        protected override bool HasMaterials(Map map) => WorkshopWearUtility.CanDoSlapdash(map);

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            CompWorkshopWear comp = t.TryGetComp<CompWorkshopWear>();
            // Jammed machines need proper repair — don't waste slapdash AI on them
            if (comp != null && comp.jammed)
            {
                return false;
            }
            return base.HasJobOnThing(pawn, t, forced);
        }
    }

    /// <summary>Components + steel proper overhaul — full restore, clears jam.</summary>
    public class WorkGiver_ProperRepair : WorkGiver_RepairWorkstationBase
    {
        protected override JobDef JobDef =>
            DefDatabase<JobDef>.GetNamed("WW_ProperRepair");

        protected override bool IsCandidate(CompWorkshopWear comp) => comp.NeedsProper;

        protected override bool HasMaterials(Map map) => WorkshopWearUtility.CanDoProper(map);
    }

    public abstract class JobDriver_RepairWorkstationBase : JobDriver
    {
        protected Building Building => (Building)job.targetA.Thing;
        protected CompWorkshopWear Comp => Building?.GetComp<CompWorkshopWear>();

        protected abstract float BaseWorkAmount { get; }
        protected abstract int MinCraftingSkill { get; }
        protected abstract void FinishRepair(Pawn worker);

        private float workLeft;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Comp == null || !Comp.IsEnabled);

            // Optional: grab steel if not carrying
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil work = new Toil();
            work.initAction = () =>
            {
                workLeft = BaseWorkAmount;
                // Skill gate soft-check (proper repair prefers higher crafting)
                if (MinCraftingSkill > 0 && pawn.skills != null)
                {
                    int level = pawn.skills.GetSkill(SkillDefOf.Crafting)?.Level ?? 0;
                    if (level < MinCraftingSkill)
                    {
                        // Slower work if under-skilled rather than hard fail
                        workLeft *= 1.35f;
                    }
                }
            };
            work.tickAction = () =>
            {
                float speed = pawn.GetStatValue(StatDefOf.WorkSpeedGlobal);
                if (speed < 0.2f)
                {
                    speed = 0.2f;
                }
                // Crafting skill speeds repair
                if (pawn.skills != null)
                {
                    int level = pawn.skills.GetSkill(SkillDefOf.Crafting)?.Level ?? 5;
                    speed *= 0.7f + level / 40f;
                    pawn.skills.Learn(SkillDefOf.Crafting, 0.11f);
                }

                workLeft -= speed;
                if (workLeft <= 0f)
                {
                    FinishRepair(pawn);
                    ReadyForNextToil();
                }
            };
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            work.WithProgressBar(TargetIndex.A, () => 1f - workLeft / BaseWorkAmount);
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithEffect(EffecterDefOf.ConstructMetal, TargetIndex.A);
            work.PlaySustainerOrSound(SoundDefOf.Interact_ConstructDirt);
            yield return work;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workLeft, "workLeft", 0f);
        }
    }

    public class JobDriver_SlapdashRepair : JobDriver_RepairWorkstationBase
    {
        // Fast: ~half a day of focused crafting at skill 8
        protected override float BaseWorkAmount => 900f;
        protected override int MinCraftingSkill => 0;

        protected override void FinishRepair(Pawn worker)
        {
            int got = WorkshopWearUtility.ConsumeCarriedOrMap(
                worker, ThingDefOf.Steel, WorkshopWearUtility.SlapdashSteelCost);
            if (got < WorkshopWearUtility.SlapdashSteelCost)
            {
                Messages.Message(
                    "WW_Fail_NoMaterials".Translate(),
                    Building,
                    MessageTypeDefOf.RejectInput,
                    historical: false);
                return;
            }

            Comp?.ApplySlapdashRepair(worker);
        }
    }

    public class JobDriver_ProperRepair : JobDriver_RepairWorkstationBase
    {
        // Slow: multi-hour overhaul
        protected override float BaseWorkAmount => 2800f;
        protected override int MinCraftingSkill => 6;

        protected override void FinishRepair(Pawn worker)
        {
            int steel = WorkshopWearUtility.ConsumeCarriedOrMap(
                worker, ThingDefOf.Steel, WorkshopWearUtility.ProperSteelCost);
            int comps = WorkshopWearUtility.ConsumeCarriedOrMap(
                worker, ThingDefOf.ComponentIndustrial, WorkshopWearUtility.ProperComponentCost);

            if (steel < WorkshopWearUtility.ProperSteelCost
                || comps < WorkshopWearUtility.ProperComponentCost)
            {
                Messages.Message(
                    "WW_Fail_NoMaterials".Translate(),
                    Building,
                    MessageTypeDefOf.RejectInput,
                    historical: false);
                return;
            }

            Comp?.ApplyProperRepair(worker);
        }
    }
}
