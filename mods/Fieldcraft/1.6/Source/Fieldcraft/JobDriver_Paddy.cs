using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Fieldcraft
{
    public class JobDriver_Irrigate : JobDriver
    {
        private float workLeft;
        private const float FormWork = 450f;
        private const float FloodWork = 280f;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA.Cell, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Map.designationManager.DesignationAt(TargetLocA, FieldcraftDefOf.FC_Irrigate) == null);

            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);

            Toil work = new Toil();
            work.initAction = () =>
            {
                TerrainDef t = TargetLocA.GetTerrain(Map);
                workLeft = t == FieldcraftDefOf.FC_PaddyDry ? FloodWork : FormWork;
            };
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
                    pawn.skills.Learn(SkillDefOf.Plants, 0.06f);
                }
                if (workLeft <= 0f)
                {
                    Finish();
                    ReadyForNextToil();
                }
            };
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            work.WithProgressBar(TargetIndex.A, () =>
            {
                TerrainDef t = TargetLocA.GetTerrain(Map);
                float total = t == FieldcraftDefOf.FC_PaddyDry ? FloodWork : FormWork;
                return 1f - workLeft / total;
            });
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithEffect(EffecterDefOf.Sow, TargetIndex.A);
            work.PlaySustainerOrSound(SoundDefOf.Interact_ConstructDirt);
            yield return work;
        }

        private void Finish()
        {
            IntVec3 cell = TargetLocA;
            Map.designationManager.DesignationAt(cell, FieldcraftDefOf.FC_Irrigate)?.Delete();
            TerrainDef cur = cell.GetTerrain(Map);
            if (cur == FieldcraftDefOf.FC_PaddyDry && FieldcraftDefOf.FC_PaddyFlooded != null)
            {
                LandworksBridge.SetTerrainSilent(Map, cell, FieldcraftDefOf.FC_PaddyFlooded);
            }
            else if (LandworksBridge.CanFormPaddy(cur) && FieldcraftDefOf.FC_PaddyDry != null)
            {
                LandworksBridge.SetTerrainSilent(Map, cell, FieldcraftDefOf.FC_PaddyDry);
            }
            MapComponent_Fieldcraft.For(Map)?.EnsureTracked(cell);
            if (Rand.Chance(0.5f))
            {
                FilthMaker.TryMakeFilth(cell, Map, ThingDefOf.Filth_Dirt, 1);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workLeft, "workLeft", 0f);
        }
    }

    public class JobDriver_Drain : JobDriver
    {
        private float workLeft;
        private const float BaseWork = 320f;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA.Cell, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Map.designationManager.DesignationAt(TargetLocA, FieldcraftDefOf.FC_Drain) == null);
            this.FailOn(() => TargetLocA.GetTerrain(Map) != FieldcraftDefOf.FC_PaddyFlooded);

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
                    pawn.skills.Learn(SkillDefOf.Plants, 0.05f);
                }
                if (workLeft <= 0f)
                {
                    Map.designationManager.DesignationAt(TargetLocA, FieldcraftDefOf.FC_Drain)?.Delete();
                    if (FieldcraftDefOf.FC_PaddyDry != null)
                    {
                        LandworksBridge.SetTerrainSilent(Map, TargetLocA, FieldcraftDefOf.FC_PaddyDry);
                    }
                    ReadyForNextToil();
                }
            };
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            work.WithProgressBar(TargetIndex.A, () => 1f - workLeft / BaseWork);
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithEffect(EffecterDefOf.ConstructDirt, TargetIndex.A);
            yield return work;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workLeft, "workLeft", 0f);
        }
    }

    public class JobDriver_PlowIn : JobDriver
    {
        private float workLeft;
        private const float BaseWork = 350f;

        private Plant Plant => job.targetA.Thing as Plant;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Map.designationManager.DesignationAt(TargetThingA.Position, FieldcraftDefOf.FC_PlowIn) == null);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

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
                    pawn.skills.Learn(SkillDefOf.Plants, 0.1f);
                }
                if (workLeft <= 0f)
                {
                    Plant plant = Plant;
                    IntVec3 cell = plant != null ? plant.Position : TargetThingA.Position;
                    Map.designationManager.DesignationAt(cell, FieldcraftDefOf.FC_PlowIn)?.Delete();
                    if (plant != null)
                    {
                        MapComponent_Fieldcraft.For(Map)?.TryPlowIn(cell, plant);
                    }
                    ReadyForNextToil();
                }
            };
            work.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            work.WithProgressBar(TargetIndex.A, () => 1f - workLeft / BaseWork);
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithEffect(EffecterDefOf.Sow, TargetIndex.A);
            work.PlaySustainerOrSound(SoundDefOf.Interact_ConstructDirt);
            yield return work;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workLeft, "workLeft", 0f);
        }
    }
}
