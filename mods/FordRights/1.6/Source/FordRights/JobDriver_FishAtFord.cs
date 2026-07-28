using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace FordRights
{
    public class JobDriver_FishAtFord : JobDriver
    {
        private float workLeft;
        private const float BaseWork = 900f;

        private Building_FordCrossing Ford => job.targetA.Thing as Building_FordCrossing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() =>
            {
                Building_FordCrossing ford = Ford;
                return ford == null || !ford.IsOperational;
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil fish = new Toil();
            fish.initAction = () =>
            {
                workLeft = BaseWork;
                // Animals/mood don't block; Hunting skill speeds it.
            };
            fish.tickAction = () =>
            {
                float speed = pawn.GetStatValue(StatDefOf.GeneralLaborSpeed);
                if (speed < 0.2f)
                {
                    speed = 0.2f;
                }
                if (pawn.skills != null)
                {
                    int level = pawn.skills.GetSkill(SkillDefOf.Animals).Level;
                    speed *= 1f + level * 0.015f;
                    pawn.skills.Learn(SkillDefOf.Animals, 0.08f);
                }

                workLeft -= speed;
                if (workLeft <= 0f)
                {
                    FinishFishing();
                    ReadyForNextToil();
                }
            };
            fish.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            fish.FailOn(() =>
            {
                Building_FordCrossing ford = Ford;
                return ford == null || !ford.CanFishNow && workLeft > BaseWork * 0.85f;
            });
            fish.WithProgressBar(TargetIndex.A, () => 1f - workLeft / BaseWork);
            fish.defaultCompleteMode = ToilCompleteMode.Never;
            fish.PlaySustainerOrSound(SoundDefOf.Interact_ConstructDirt);
            yield return fish;
        }

        private void FinishFishing()
        {
            Building_FordCrossing ford = Ford;
            if (ford == null || !ford.Spawned)
            {
                return;
            }

            // Re-validate at settle so AFK multi-queue doesn't print money.
            if (!ford.IsOperational)
            {
                Messages.Message("FR_Message_FishFailedWater".Translate(), ford, MessageTypeDefOf.RejectInput, historical: false);
                ford.fishingQueued = false;
                return;
            }

            FordRightsSettings s = FordRightsMod.Settings;
            if (s == null || !s.fishingEnabled)
            {
                ford.fishingQueued = false;
                return;
            }

            // Cooldown gate: if somehow two jobs raced, only first succeeds.
            if (Find.TickManager.TicksGame < ford.nextFishReadyTick && ford.lastFishTick > 0)
            {
                Messages.Message("FR_Message_FishNotReady".Translate(), ford, MessageTypeDefOf.RejectInput, historical: false);
                ford.fishingQueued = false;
                return;
            }

            ThingDef fishDef = FordRightsDefOf.FR_RiverFish;
            if (fishDef == null)
            {
                return;
            }

            int count = s.FishCount;
            // Tiny chance of a double catch for high animal skill — still small.
            if (pawn.skills != null && pawn.skills.GetSkill(SkillDefOf.Animals).Level >= 10 && Rand.Chance(0.12f))
            {
                count += 1;
            }

            Thing fish = ThingMaker.MakeThing(fishDef);
            fish.stackCount = count;
            GenPlace.TryPlaceThing(fish, ford.Position, Map, ThingPlaceMode.Near);

            ford.OnFishCompleted(pawn, count);

            Messages.Message(
                "FR_Message_FishCaught".Translate(pawn.LabelShort, count, fishDef.label),
                ford,
                MessageTypeDefOf.TaskCompletion);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workLeft, "workLeft", 0f);
        }
    }
}
