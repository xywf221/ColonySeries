using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DeadDrop
{
    public class Building_DeadDrop : Building
    {
        public MapComponent_DeadDrop Comp => MapComponent_DeadDrop.For(Map);

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            MapComponent_DeadDrop comp = Comp;
            if (comp == null)
            {
                return text;
            }

            string extra = comp.InspectExtra();
            if (text.NullOrEmpty())
            {
                return extra;
            }
            return text + "\n" + extra;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }

            DeadDropSettings settings = DeadDropMod.Settings;
            if (settings != null && !settings.masterEnabled)
            {
                yield break;
            }

            MapComponent_DeadDrop comp = Comp;
            if (comp == null)
            {
                yield break;
            }

            if (comp.activeOrder != null && comp.activeOrder.state == DeadDropOrderState.Offered)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DD_Gizmo_Accept".Translate(),
                    defaultDesc = "DD_Gizmo_AcceptDesc".Translate(comp.activeOrder.DescribeFull()),
                    icon = TexCommand.DesirePower,
                    action = () => comp.AcceptOrder()
                };

                yield return new Command_Action
                {
                    defaultLabel = "DD_Gizmo_Decline".Translate(),
                    defaultDesc = "DD_Gizmo_DeclineDesc".Translate(),
                    icon = TexCommand.ClearPrioritizedWork,
                    action = () => comp.DeclineOrder()
                };
            }

            if (comp.activeOrder != null && comp.activeOrder.state == DeadDropOrderState.Accepted)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DD_Gizmo_Service".Translate(),
                    defaultDesc = "DD_Gizmo_ServiceDesc".Translate(),
                    icon = TexCommand.GatherSpotActive,
                    action = () =>
                    {
                        comp.pendingService = true;
                        Messages.Message("DD_Message_ServiceQueued".Translate(), this, MessageTypeDefOf.TaskCompletion);
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DD_Gizmo_CancelAccepted".Translate(),
                    defaultDesc = "DD_Gizmo_CancelAcceptedDesc".Translate(),
                    icon = TexCommand.ClearPrioritizedWork,
                    action = () => comp.DeclineOrder()
                };
            }

            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Force order",
                    action = () =>
                    {
                        comp.activeOrder = null;
                        comp.pendingService = false;
                        comp.TryOfferNewOrder();
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: +10 rep",
                    action = () => comp.AdjustReputation(10)
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Complete now",
                    action = () =>
                    {
                        if (comp.activeOrder != null)
                        {
                            comp.activeOrder.state = DeadDropOrderState.Accepted;
                            List<Pawn> colonists = Map.mapPawns.FreeColonistsSpawned;
                            Pawn p = colonists != null && colonists.Count > 0 ? colonists[0] : null;
                            if (p != null)
                            {
                                comp.CompleteService(p, this);
                            }
                        }
                    }
                };
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption opt in base.GetFloatMenuOptions(selPawn))
            {
                yield return opt;
            }

            MapComponent_DeadDrop comp = Comp;
            if (comp == null || !comp.pendingService || comp.activeOrder == null ||
                comp.activeOrder.state != DeadDropOrderState.Accepted)
            {
                yield break;
            }

            if (!selPawn.CanReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption("DD_Float_CannotReach".Translate(), null);
                yield break;
            }

            if (DeadDropMod.Settings != null &&
                DeadDropMod.Settings.honestPawnsRefuse &&
                selPawn.story?.traits != null &&
                selPawn.story.traits.HasTrait(TraitDefOf.Kind))
            {
                yield return new FloatMenuOption("DD_Float_TooHonest".Translate(selPawn.LabelShort), null);
                yield break;
            }

            if (!MapComponent_DeadDrop.CanFulfill(comp.activeOrder, Map, out string reason))
            {
                yield return new FloatMenuOption(reason, null);
                yield break;
            }

            yield return new FloatMenuOption(
                "DD_Float_Service".Translate(selPawn.LabelShort),
                () =>
                {
                    Job job = JobMaker.MakeJob(DeadDropDefOf.DD_ServiceDeadDrop, this);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
        }
    }
}
