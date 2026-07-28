using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SalvageAtlas
{
    public class WorkGiver_AnalyzeFragments : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForDef(SalvageAtlasDefOf.SA_SalvageBench);

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!SalvageUtility.ModActive || !SalvageUtility.ResearchDone)
            {
                return true;
            }
            if (SalvageAtlasDefOf.SA_MechFragment == null)
            {
                return true;
            }
            List<Thing> frags = pawn.Map.listerThings.ThingsOfDef(SalvageAtlasDefOf.SA_MechFragment);
            return frags == null || frags.Count == 0;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            if (SalvageAtlasDefOf.SA_SalvageBench == null)
            {
                yield break;
            }
            List<Thing> list = pawn.Map.listerThings.ThingsOfDef(SalvageAtlasDefOf.SA_SalvageBench);
            if (list == null)
            {
                yield break;
            }
            for (int i = 0; i < list.Count; i++)
            {
                yield return list[i];
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Building_SalvageBench) || !t.Spawned)
            {
                return false;
            }
            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }
            if (!pawn.CanReach(t, PathEndMode.InteractionCell, Danger.Deadly))
            {
                return false;
            }
            Thing frag = FindFragment(pawn);
            if (frag == null)
            {
                JobFailReason.Is("SA_Msg_NoFragments".Translate());
                return false;
            }
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Thing frag = FindFragment(pawn);
            if (frag == null)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(SalvageAtlasDefOf.SA_AnalyzeFragments, t, frag);
            job.count = Mathf.Min(5, frag.stackCount);
            return job;
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
                x => !x.IsForbidden(pawn) && pawn.CanReserve(x) && x.stackCount > 0);
        }
    }
}
