using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SalvageAtlas
{
    public class Building_SalvageBench : Building_WorkTable
    {
        public GameComponent_SalvageAtlas Atlas => GameComponent_SalvageAtlas.Get();

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            GameComponent_SalvageAtlas atlas = Atlas;
            if (atlas == null)
            {
                return text;
            }
            string extra = atlas.InspectSummary();
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

            if (SalvageAtlasMod.Settings != null && !SalvageAtlasMod.Settings.masterEnabled)
            {
                yield break;
            }

            GameComponent_SalvageAtlas atlas = Atlas;
            if (atlas == null)
            {
                yield break;
            }

            // Unlock branch ranks
            for (int i = 0; i < AtlasBranches.BranchCount; i++)
            {
                AtlasBranchId id = (AtlasBranchId)i;
                AtlasRankDef next = atlas.NextRank(id);
                int cur = atlas.GetRank(id);
                bool can = atlas.CanUnlock(id, out string fail);
                string label = next != null
                    ? "SA_Gizmo_Unlock".Translate(AtlasBranches.LabelKey(id).Translate(), next.rank, next.fragmentCost)
                    : "SA_Gizmo_Maxed".Translate(AtlasBranches.LabelKey(id).Translate());
                string desc = next != null
                    ? next.descKey.Translate() + "\n\n" + "SA_Gizmo_Bank".Translate(atlas.analyzedFragments)
                    : "SA_BranchMaxed".Translate();
                if (!can && fail != null)
                {
                    desc = desc + "\n" + fail;
                }

                AtlasBranchId captured = id;
                yield return new Command_Action
                {
                    defaultLabel = label,
                    defaultDesc = desc,
                    icon = TexCommand.Install,
                    Disabled = !can,
                    disabledReason = fail ?? string.Empty,
                    action = () =>
                    {
                        atlas.TryUnlock(captured);
                    }
                };
            }

            // Print one-shot schematic if credits pending
            if (atlas.schematicsPending > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "SA_Gizmo_PrintSchematic".Translate(atlas.schematicsPending),
                    defaultDesc = "SA_Gizmo_PrintSchematicDesc".Translate(),
                    icon = TexCommand.DesirePower,
                    action = () => SalvageUtility.PrintSchematic(Map, Position)
                };
            }

            // Apply local field patches (requires rank >= 1)
            if (atlas.GetRank(AtlasBranchId.FieldKit) > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "SA_Gizmo_PatchArmor".Translate(),
                    defaultDesc = "SA_Gizmo_PatchArmorDesc".Translate(),
                    icon = TexCommand.ForbidOff,
                    action = () => BeginPatchOnColonist(AtlasBranchId.FieldKit)
                };
            }
            if (atlas.GetRank(AtlasBranchId.Optics) > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "SA_Gizmo_PatchOptics".Translate(),
                    defaultDesc = "SA_Gizmo_PatchOpticsDesc".Translate(),
                    icon = TexCommand.ForbidOff,
                    action = () => BeginPatchOnColonist(AtlasBranchId.Optics)
                };
            }
            if (atlas.GetRank(AtlasBranchId.Coolant) > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "SA_Gizmo_PatchCoolant".Translate(),
                    defaultDesc = "SA_Gizmo_PatchCoolantDesc".Translate(),
                    icon = TexCommand.ForbidOff,
                    action = () => BeginPatchOnColonist(AtlasBranchId.Coolant)
                };
            }

            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: +10 analyzed",
                    action = () => atlas.AddAnalyzed(10)
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: +5 fragments item",
                    action = () =>
                    {
                        Thing t = ThingMaker.MakeThing(SalvageAtlasDefOf.SA_MechFragment);
                        t.stackCount = 5;
                        GenPlace.TryPlaceThing(t, Position, Map, ThingPlaceMode.Near);
                    }
                };
            }
        }

        private void BeginPatchOnColonist(AtlasBranchId branch)
        {
            // Target the first free colonist near the bench, or selected pawn.
            Pawn p = Find.Selector.SingleSelectedThing as Pawn;
            if (p == null || !p.IsColonistPlayerControlled || p.Map != Map)
            {
                List<Pawn> list = Map.mapPawns.FreeColonistsSpawned;
                p = null;
                float best = 99999f;
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Pawn c = list[i];
                        if (c.Dead || c.Downed)
                        {
                            continue;
                        }
                        float d = c.Position.DistanceTo(Position);
                        if (d < best)
                        {
                            best = d;
                            p = c;
                        }
                    }
                }
            }
            if (p == null)
            {
                Messages.Message("SA_Msg_NoColonist".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            SalvageUtility.ApplyFieldPatch(p, branch);
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption opt in base.GetFloatMenuOptions(selPawn))
            {
                yield return opt;
            }

            if (!SalvageUtility.ModActive || !SalvageUtility.ResearchDone)
            {
                yield break;
            }
            if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
            {
                yield return new FloatMenuOption("SA_Float_CannotReach".Translate(), null);
                yield break;
            }

            // Manual analyze job if fragments exist.
            yield return new FloatMenuOption(
                "SA_Float_Analyze".Translate(),
                () =>
                {
                    Thing frag = FindFragment(selPawn);
                    if (frag == null)
                    {
                        Messages.Message("SA_Msg_NoFragments".Translate(), MessageTypeDefOf.RejectInput);
                        return;
                    }
                    Job job = JobMaker.MakeJob(SalvageAtlasDefOf.SA_AnalyzeFragments, this, frag);
                    job.count = Mathf.Min(frag.stackCount, 5);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
        }

        private static Thing FindFragment(Pawn pawn)
        {
            if (SalvageAtlasDefOf.SA_MechFragment == null)
            {
                return null;
            }
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(SalvageAtlasDefOf.SA_MechFragment),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                x => !x.IsForbidden(pawn) && pawn.CanReserve(x));
        }
    }
}
