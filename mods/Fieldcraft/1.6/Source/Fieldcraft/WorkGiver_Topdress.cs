using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Fieldcraft
{
    public class WorkGiver_Topdress : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(FieldcraftDefOf.FC_Topdress);
        }

        public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
        {
            foreach (Designation des in pawn.Map.designationManager.SpawnedDesignationsOfDef(FieldcraftDefOf.FC_Topdress))
            {
                yield return des.target.Cell;
            }
        }

        public override bool HasJobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
        {
            if (pawn.Map.designationManager.DesignationAt(c, FieldcraftDefOf.FC_Topdress) == null)
            {
                return false;
            }
            if (c.IsForbidden(pawn) || !pawn.CanReserve(c, 1, -1, null, forced))
            {
                return false;
            }
            Thing compost = FindCompost(pawn);
            if (compost == null)
            {
                JobFailReason.Is("FC_NoCompost".Translate());
                return false;
            }
            return true;
        }

        public override Job JobOnCell(Pawn pawn, IntVec3 cell, bool forced = false)
        {
            Thing compost = FindCompost(pawn);
            if (compost == null)
            {
                return null;
            }
            Job job = JobMaker.MakeJob(FieldcraftDefOf.FC_TopdressSoil, cell, compost);
            job.count = 1;
            return job;
        }

        private static Thing FindCompost(Pawn pawn)
        {
            // Accept Fieldcraft compost and Landworks compost interchangeably.
            Thing t = ClosestOfDef(pawn, FieldcraftDefOf.FC_Compost);
            if (t != null)
            {
                return t;
            }
            ThingDef lw = DefDatabase<ThingDef>.GetNamedSilentFail("LW_Compost");
            if (lw != null)
            {
                return ClosestOfDef(pawn, lw);
            }
            return null;
        }

        private static Thing ClosestOfDef(Pawn pawn, ThingDef def)
        {
            if (def == null)
            {
                return null;
            }
            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(def),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                x => !x.IsForbidden(pawn) && pawn.CanReserve(x));
        }
    }
}
