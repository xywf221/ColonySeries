using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SalvageAtlas
{
    public class JobDriver_SalvageMechCorpse : JobDriver
    {
        private float workLeft;
        private float totalWork = 400f;

        private Thing Target => job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() =>
            {
                Thing t = Target;
                if (t == null || t.Destroyed)
                {
                    return true;
                }
                if (Map.designationManager.DesignationOn(t, SalvageAtlasDefOf.SA_SalvageMech) == null)
                {
                    return true;
                }
                MapComponent_SalvageAtlas mapComp = MapComponent_SalvageAtlas.For(Map);
                return mapComp != null && mapComp.WasSalvaged(t);
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil work = new Toil();
            work.initAction = () =>
            {
                float mult = SalvageAtlasMod.Settings?.SalvageWorkMult ?? 1f;
                totalWork = 320f + SalvageUtility.EstimateFragmentYield(Target) * 40f;
                totalWork *= mult;
                workLeft = totalWork;
            };
            work.tickAction = () =>
            {
                float speed = pawn.GetStatValue(StatDefOf.GeneralLaborSpeed);
                if (speed < 0.2f)
                {
                    speed = 0.2f;
                }
                // Intellectual + crafting both help reverse-engineering.
                if (pawn.skills != null)
                {
                    int intel = pawn.skills.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
                    int craft = pawn.skills.GetSkill(SkillDefOf.Crafting)?.Level ?? 0;
                    speed *= 1f + (intel + craft) * 0.01f;
                    pawn.skills.Learn(SkillDefOf.Intellectual, 0.11f);
                    pawn.skills.Learn(SkillDefOf.Crafting, 0.06f);
                }
                workLeft -= speed;
                if (workLeft <= 0f)
                {
                    FinishSalvage();
                    ReadyForNextToil();
                }
            };
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            work.WithProgressBar(TargetIndex.A, () => 1f - workLeft / Mathf.Max(1f, totalWork));
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithEffect(EffecterDefOf.ConstructMetal, TargetIndex.A);
            yield return work;
        }

        private void FinishSalvage()
        {
            Thing t = Target;
            if (t == null || t.Destroyed)
            {
                return;
            }
            Map.designationManager.DesignationOn(t, SalvageAtlasDefOf.SA_SalvageMech)?.Delete();
            int yield = SalvageUtility.EstimateFragmentYield(t);
            SalvageUtility.CompleteSalvage(t, pawn, yield);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workLeft, "workLeft", 0f);
            Scribe_Values.Look(ref totalWork, "totalWork", 400f);
        }
    }
}
