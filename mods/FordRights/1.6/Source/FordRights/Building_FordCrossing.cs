using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace FordRights
{
    public class Building_FordCrossing : Building
    {
        public bool hasAdjacentWaterCached;
        public int nextFishReadyTick;
        public int lastFishTick;
        public bool fishingQueued;

        public MapComponent_FordRights Comp => MapComponent_FordRights.For(Map);

        public bool HasAdjacentWater => hasAdjacentWaterCached;

        public bool IsOperational
        {
            get
            {
                FordRightsSettings s = FordRightsMod.Settings;
                if (s == null || !s.modEnabled)
                {
                    return false;
                }
                if (!Spawned || Destroyed)
                {
                    return false;
                }
                if (HitPoints <= 0)
                {
                    return false;
                }
                // Damaged below 35% → needs repair before tolls/fish.
                if (MaxHitPoints > 0 && HitPoints < MaxHitPoints * 0.35f)
                {
                    return false;
                }
                return hasAdjacentWaterCached;
            }
        }

        public bool CanFishNow
        {
            get
            {
                FordRightsSettings s = FordRightsMod.Settings;
                if (s == null || !s.modEnabled || !s.fishingEnabled)
                {
                    return false;
                }
                if (!IsOperational)
                {
                    return false;
                }
                return Find.TickManager.TicksGame >= nextFishReadyTick;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            RefreshWaterStatus();
            MapComponent_FordRights.For(map)?.RecheckWater(force: true);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hasAdjacentWaterCached, "hasAdjacentWaterCached", false);
            Scribe_Values.Look(ref nextFishReadyTick, "nextFishReadyTick", 0);
            Scribe_Values.Look(ref lastFishTick, "lastFishTick", 0);
            Scribe_Values.Look(ref fishingQueued, "fishingQueued", false);
        }

        public void RefreshWaterStatus()
        {
            hasAdjacentWaterCached = FordUtility.IsNearWater(Map, Position, FordUtility.WaterAdjacencyRadius);
        }

        public void OnFishCompleted(Pawn pawn, int fishCount)
        {
            FordRightsSettings s = FordRightsMod.Settings;
            lastFishTick = Find.TickManager.TicksGame;
            nextFishReadyTick = lastFishTick + (s != null ? s.FishingCooldownTicks : 18 * GenDate.TicksPerHour);
            fishingQueued = false;
            Comp?.NotifyFishCaught(fishCount);
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder();
            string bas = base.GetInspectString();
            if (!bas.NullOrEmpty())
            {
                sb.Append(bas);
            }

            MapComponent_FordRights comp = Comp;
            if (comp != null)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.Append(comp.InspectSummary(this));
            }

            if (!HasAdjacentWater)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.Append("FR_Inspect_IdleNoWater".Translate());
            }
            else if (MaxHitPoints > 0 && HitPoints < MaxHitPoints * 0.35f)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.Append("FR_Inspect_NeedsRepair".Translate());
            }
            else if (FordRightsMod.Settings != null && FordRightsMod.Settings.fishingEnabled)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                if (CanFishNow)
                {
                    sb.Append("FR_Inspect_FishReady".Translate());
                }
                else
                {
                    int hours = Mathf.Max(0, (nextFishReadyTick - Find.TickManager.TicksGame) / GenDate.TicksPerHour);
                    sb.Append("FR_Inspect_FishCooldown".Translate(hours));
                }
            }

            return sb.ToString().TrimEnd();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }

            FordRightsSettings settings = FordRightsMod.Settings;
            if (settings == null || !settings.modEnabled)
            {
                yield break;
            }

            if (settings.fishingEnabled && IsOperational)
            {
                yield return new Command_Action
                {
                    defaultLabel = "FR_Gizmo_QueueFish".Translate(),
                    defaultDesc = "FR_Gizmo_QueueFishDesc".Translate(),
                    icon = TexCommand.DesirePower,
                    action = () =>
                    {
                        if (!CanFishNow)
                        {
                            Messages.Message("FR_Message_FishNotReady".Translate(), this, MessageTypeDefOf.RejectInput, historical: false);
                            return;
                        }
                        fishingQueued = true;
                        Messages.Message("FR_Message_FishQueued".Translate(), this, MessageTypeDefOf.TaskCompletion);
                    }
                };
            }

            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Refresh water",
                    action = () =>
                    {
                        RefreshWaterStatus();
                        Comp?.RecheckWater(force: true);
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Ready fish",
                    action = () =>
                    {
                        nextFishReadyTick = 0;
                        fishingQueued = true;
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Collect toll",
                    action = () => Comp?.TryCollectToll(this, "DEV caravan", Faction.OfPlayer)
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Flood damage",
                    action = () => IncidentWorker_FordFlood.ApplyFloodDamage(this, settings)
                };
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption opt in base.GetFloatMenuOptions(selPawn))
            {
                yield return opt;
            }

            FordRightsSettings settings = FordRightsMod.Settings;
            if (settings == null || !settings.modEnabled || !settings.fishingEnabled)
            {
                yield break;
            }

            if (!selPawn.CanReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption("FR_Float_CannotReach".Translate(), null);
                yield break;
            }

            if (!IsOperational)
            {
                yield return new FloatMenuOption("FR_Float_NotOperational".Translate(), null);
                yield break;
            }

            if (!CanFishNow)
            {
                yield return new FloatMenuOption("FR_Float_FishCooldown".Translate(), null);
                yield break;
            }

            yield return new FloatMenuOption(
                "FR_Float_Fish".Translate(selPawn.LabelShort),
                () =>
                {
                    fishingQueued = true;
                    Job job = JobMaker.MakeJob(FordRightsDefOf.FR_FishAtFord, this);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
        }
    }
}
