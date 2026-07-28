using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace WatchRoster
{
    public class Building_WatchPost : Building
    {
        public IntVec3 WatchSpot
        {
            get
            {
                // Prefer interaction cell if defined; else stand on the post cell.
                if (def.hasInteractionCell)
                {
                    return InteractionCell;
                }

                return Position;
            }
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder();
            string bas = base.GetInspectString();
            if (!bas.NullOrEmpty())
            {
                sb.Append(bas);
            }

            WatchRosterSettings s = WatchRosterMod.Settings;
            if (s == null || !s.masterEnabled)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }

                sb.Append("WR_Inspect_Disabled".Translate());
                return sb.ToString();
            }

            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            bool night = WatchUtility.IsWatchNight(Map);
            bool manned = WatchUtility.IsMannedPost(this);
            if (manned)
            {
                sb.Append("WR_Inspect_Manned".Translate());
            }
            else if (night)
            {
                sb.Append("WR_Inspect_EmptyNight".Translate());
            }
            else
            {
                sb.Append("WR_Inspect_DayIdle".Translate());
            }

            sb.AppendLine();
            sb.Append("WR_Inspect_PostCount".Translate(
                WatchUtility.CountPosts(Map),
                s.MaxWatchPosts));

            return sb.ToString().TrimEnd();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }

            if (!DebugSettings.ShowDevGizmos)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "DEV: Force night check",
                action = () =>
                {
                    Messages.Message(
                        $"night={WatchUtility.IsWatchNight(Map)} manned={WatchUtility.CountMannedPosts(Map)} fireMult={WatchUtility.FireObserveIntervalMult(Map):F2}",
                        this,
                        MessageTypeDefOf.NeutralEvent,
                        historical: false);
                }
            };
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption opt in base.GetFloatMenuOptions(selPawn))
            {
                yield return opt;
            }

            if (selPawn == null || !selPawn.IsColonistPlayerControlled)
            {
                yield break;
            }

            WatchRosterSettings s = WatchRosterMod.Settings;
            if (s == null || !s.masterEnabled)
            {
                yield break;
            }

            if (!WatchUtility.IsWatchNight(Map) && !s.allowDayWatch)
            {
                yield return new FloatMenuOption(
                    "WR_Float_NotNight".Translate(),
                    null);
                yield break;
            }

            AcceptanceReport can = CanPawnWatch(selPawn, forced: true);
            if (!can.Accepted)
            {
                yield return new FloatMenuOption(
                    "WR_Float_CannotWatch".Translate(can.Reason),
                    null);
                yield break;
            }

            yield return new FloatMenuOption(
                "WR_Float_StandWatch".Translate(),
                () =>
                {
                    Job job = JobMaker.MakeJob(WatchRosterDefOf.WR_StandWatch, this, WatchSpot);
                    job.playerForced = true;
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
        }

        public AcceptanceReport CanPawnWatch(Pawn pawn, bool forced = false)
        {
            if (pawn == null || !Spawned)
            {
                return false;
            }

            if (this.IsForbidden(pawn))
            {
                return "ForbiddenLower".Translate();
            }

            if (!pawn.CanReserve(this, 1, -1, null, forced))
            {
                return "Reserved".Translate();
            }

            if (!pawn.CanReach(WatchSpot, PathEndMode.OnCell, Danger.Deadly))
            {
                return "NoPath".Translate();
            }

            if (pawn.WorkTagIsDisabled(WorkTags.Violent) &&
                pawn.workSettings != null &&
                !pawn.workSettings.WorkIsActive(WorkTypeDefOf.Hunting))
            {
                // Still allow if hunting work is on; Violent-disabled pawns can stand watch (lookout, not fight).
            }

            return true;
        }
    }

    public class PlaceWorker_WatchPostLimit : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(
            BuildableDef checkingDef,
            IntVec3 loc,
            Rot4 rot,
            Map map,
            Thing thingToIgnore = null,
            Thing thing = null)
        {
            WatchRosterSettings s = WatchRosterMod.Settings;
            int max = s?.MaxWatchPosts ?? 3;
            int existing = 0;
            if (WatchRosterDefOf.WR_WatchPost != null)
            {
                existing = map.listerThings.ThingsOfDef(WatchRosterDefOf.WR_WatchPost).Count;
            }

            // Also count blueprints / frames of this def.
            List<Thing> blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint);
            if (blueprints != null)
            {
                for (int i = 0; i < blueprints.Count; i++)
                {
                    if (blueprints[i].def.entityDefToBuild == WatchRosterDefOf.WR_WatchPost)
                    {
                        existing++;
                    }
                }
            }

            List<Thing> frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame);
            if (frames != null)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i].def.entityDefToBuild == WatchRosterDefOf.WR_WatchPost)
                    {
                        existing++;
                    }
                }
            }

            if (existing >= max)
            {
                return "WR_Place_MaxPosts".Translate(max);
            }

            return true;
        }
    }
}
