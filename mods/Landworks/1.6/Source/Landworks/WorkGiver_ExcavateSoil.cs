using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Landworks
{
    public class WorkGiver_ExcavateSoil : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(LandworksDefOf.LW_Excavate);
        }

        public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
        {
            foreach (Designation des in pawn.Map.designationManager.SpawnedDesignationsOfDef(LandworksDefOf.LW_Excavate))
            {
                yield return des.target.Cell;
            }
        }

        public override bool HasJobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
        {
            if (pawn.Map.designationManager.DesignationAt(c, LandworksDefOf.LW_Excavate) == null)
            {
                return false;
            }
            if (!TerrainUtility.CanExcavate(c.GetTerrain(pawn.Map)))
            {
                return false;
            }
            if (c.IsForbidden(pawn) || !pawn.CanReserve(c, 1, -1, null, forced))
            {
                return false;
            }
            return true;
        }

        public override Job JobOnCell(Pawn pawn, IntVec3 cell, bool forced = false)
        {
            return JobMaker.MakeJob(LandworksDefOf.LW_ExcavateSoil, cell);
        }
    }
}
