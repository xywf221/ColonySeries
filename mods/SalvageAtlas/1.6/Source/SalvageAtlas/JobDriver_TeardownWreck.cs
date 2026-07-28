using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SalvageAtlas
{
    public class JobDriver_TeardownWreck : JobDriver
    {
        private float workLeft;
        private float totalWork = 600f;

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
                if (Map.designationManager.DesignationOn(t, SalvageAtlasDefOf.SA_TeardownWreck) == null)
                {
                    return true;
                }
                GameComponent_SalvageAtlas atlas = GameComponent_SalvageAtlas.Get();
                return atlas == null || !atlas.HasTeardownUnlocked;
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil work = new Toil();
            work.initAction = () =>
            {
                float mult = SalvageAtlasMod.Settings?.SalvageWorkMult ?? 1f;
                int tier = GameComponent_SalvageAtlas.Get()?.TeardownTier ?? 1;
                totalWork = (500f + tier * 40f) * mult;
                // Higher skill slightly faster.
                if (pawn.skills != null)
                {
                    int craft = pawn.skills.GetSkill(SkillDefOf.Crafting)?.Level ?? 0;
                    totalWork *= Mathf.Clamp(1.15f - craft * 0.015f, 0.7f, 1.15f);
                }
                workLeft = totalWork;
            };
            work.tickAction = () =>
            {
                float speed = pawn.GetStatValue(StatDefOf.GeneralLaborSpeed);
                if (speed < 0.2f)
                {
                    speed = 0.2f;
                }
                if (pawn.skills != null)
                {
                    pawn.skills.Learn(SkillDefOf.Crafting, 0.12f);
                    pawn.skills.Learn(SkillDefOf.Intellectual, 0.05f);
                }
                workLeft -= speed;
                if (workLeft <= 0f)
                {
                    FinishTeardown();
                    ReadyForNextToil();
                }
            };
            work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            work.WithProgressBar(TargetIndex.A, () => 1f - workLeft / Mathf.Max(1f, totalWork));
            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithEffect(EffecterDefOf.ConstructMetal, TargetIndex.A);
            yield return work;
        }

        private void FinishTeardown()
        {
            Thing t = Target;
            if (t == null || t.Destroyed)
            {
                return;
            }
            Map.designationManager.DesignationOn(t, SalvageAtlasDefOf.SA_TeardownWreck)?.Delete();

            GameComponent_SalvageAtlas atlas = GameComponent_SalvageAtlas.Get();
            int tier = atlas?.TeardownTier ?? 1;
            float chance = atlas?.TeardownSuccessChance(pawn) ?? 0.5f;

            if (Rand.Chance(chance))
            {
                SalvageUtility.ApplyTeardownSuccess(t, pawn, tier);
            }
            else
            {
                SalvageUtility.ApplyTeardownFailure(t, pawn, tier);
                // Letter on failure so the hazard is readable (design §2.8).
                Find.LetterStack.ReceiveLetter(
                    "SA_Letter_TeardownFailTitle".Translate(),
                    "SA_Letter_TeardownFailBody".Translate(pawn.LabelShort, t.Label),
                    LetterDefOf.NegativeEvent,
                    new TargetInfo(t.PositionHeld, Map));
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workLeft, "workLeft", 0f);
            Scribe_Values.Look(ref totalWork, "totalWork", 600f);
        }
    }
}
