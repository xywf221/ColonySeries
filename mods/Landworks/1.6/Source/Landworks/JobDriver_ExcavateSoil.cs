using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Landworks
{
    public class JobDriver_ExcavateSoil : JobDriver
    {
        private float workLeft;
        private float totalWork;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA.Cell, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => Map.designationManager.DesignationAt(TargetLocA, LandworksDefOf.LW_Excavate) == null);
            this.FailOn(() => !TerrainUtility.CanExcavate(TargetLocA.GetTerrain(Map)));

            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.Touch);

            Toil doWork = new Toil();
            doWork.initAction = () =>
            {
                TerrainDef terrain = TargetLocA.GetTerrain(Map);
                totalWork = TerrainUtility.WorkToExcavate(terrain);
                workLeft = totalWork;
            };
            doWork.tickAction = () =>
            {
                float speed = pawn.GetStatValue(StatDefOf.MiningSpeed);
                if (speed < 0.2f)
                {
                    speed = 0.2f;
                }
                workLeft -= speed;
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Mining, 0.08f);
                }
                if (workLeft <= 0f)
                {
                    DoExcavation();
                    ReadyForNextToil();
                }
            };
            doWork.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            doWork.WithProgressBar(TargetIndex.A, () => 1f - workLeft / totalWork);
            doWork.defaultCompleteMode = ToilCompleteMode.Never;
            doWork.WithEffect(EffecterDefOf.Mine, TargetIndex.A);
            doWork.PlaySustainerOrSound(SoundDefOf.Interact_ConstructDirt);
            yield return doWork;
        }

        private void DoExcavation()
        {
            IntVec3 cell = TargetLocA;
            TerrainDef before = cell.GetTerrain(Map);
            int yieldCount = TerrainUtility.FillYield(before);
            TerrainDef after = TerrainUtility.ResultAfterExcavation(before);

            TerrainUtility.SetTerrainSilent(Map, cell, after);
            Map.designationManager.DesignationAt(cell, LandworksDefOf.LW_Excavate)?.Delete();

            if (yieldCount > 0 && LandworksDefOf.LW_SoilFill != null)
            {
                Thing fill = ThingMaker.MakeThing(LandworksDefOf.LW_SoilFill);
                fill.stackCount = yieldCount;
                GenPlace.TryPlaceThing(fill, cell, Map, ThingPlaceMode.Near);
            }

            MapComponent_Landworks.For(Map)?.RegisterExcavation(cell);

            if (pawn.needs?.mood?.thoughts?.memories != null && LandworksDefOf.LW_FilthyEarthworks != null)
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(LandworksDefOf.LW_FilthyEarthworks);
            }

            if (Rand.Chance(0.65f))
            {
                FilthMaker.TryMakeFilth(cell, Map, ThingDefOf.Filth_Dirt, 1);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workLeft, "workLeft", 0f);
            Scribe_Values.Look(ref totalWork, "totalWork", 0f);
        }
    }
}
