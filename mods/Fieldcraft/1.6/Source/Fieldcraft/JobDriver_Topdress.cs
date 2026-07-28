using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Fieldcraft
{
    public class JobDriver_Topdress : JobDriver
    {
        private float workLeft;
        private const float BaseWork = 280f;

        private Thing Compost => job.targetB.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(job.targetA.Cell, job, 1, -1, null, errorOnFailed))
            {
                return false;
            }
            if (Compost != null && !pawn.Reserve(Compost, job, 1, 1, null, errorOnFailed))
            {
                return false;
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            this.FailOn(() => Map.designationManager.DesignationAt(TargetLocA, FieldcraftDefOf.FC_Topdress) == null);

            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);

            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);

            Toil work = new Toil();
            work.initAction = () => { workLeft = BaseWork; };
            work.tickAction = () =>
            {
                float speed = pawn.GetStatValue(StatDefOf.PlantWorkSpeed);
                if (speed < 0.2f)
                {
                    speed = 0.2f;
                }
                workLeft -= speed;
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Plants, 0.08f);
                }
                if (workLeft <= 0f)
                {
                    FinishTopdress();
                    ReadyForNextToil();
                }
            };
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            work.WithProgressBar(TargetIndex.A, () => 1f - workLeft / BaseWork);
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithEffect(EffecterDefOf.Sow, TargetIndex.A);
            work.PlaySustainerOrSound(SoundDefOf.Interact_ConstructDirt);
            yield return work;
        }

        private void FinishTopdress()
        {
            IntVec3 cell = TargetLocA;
            Map.designationManager.DesignationAt(cell, FieldcraftDefOf.FC_Topdress)?.Delete();

            Thing carried = pawn.carryTracker?.CarriedThing;
            if (carried != null && IsCompostDef(carried.def))
            {
                if (carried.stackCount <= 1)
                {
                    carried.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    carried.SplitOff(1).Destroy(DestroyMode.Vanish);
                }
            }
            else if (Compost != null && !Compost.Destroyed)
            {
                Compost.SplitOff(1).Destroy(DestroyMode.Vanish);
            }

            float amount = FieldcraftMod.Settings != null ? FieldcraftMod.Settings.topdressRecharge : 40f;
            MapComponent_Fieldcraft.For(Map)?.TryTopdress(cell, amount);

            if (Rand.Chance(0.4f))
            {
                FilthMaker.TryMakeFilth(cell, Map, ThingDefOf.Filth_Dirt, 1);
            }
        }

        private static bool IsCompostDef(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }
            if (def == FieldcraftDefOf.FC_Compost)
            {
                return true;
            }
            return def.defName == "LW_Compost";
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workLeft, "workLeft", 0f);
        }
    }
}
