using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Fieldcraft
{
    public class WorkGiver_Irrigate : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(FieldcraftDefOf.FC_Irrigate);
        }

        public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
        {
            foreach (Designation des in pawn.Map.designationManager.SpawnedDesignationsOfDef(FieldcraftDefOf.FC_Irrigate))
            {
                yield return des.target.Cell;
            }
        }

        public override bool HasJobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
        {
            if (pawn.Map.designationManager.DesignationAt(c, FieldcraftDefOf.FC_Irrigate) == null)
            {
                return false;
            }
            if (c.IsForbidden(pawn) || !pawn.CanReserve(c, 1, -1, null, forced))
            {
                return false;
            }
            TerrainDef t = c.GetTerrain(pawn.Map);
            if (t != FieldcraftDefOf.FC_PaddyDry && !LandworksBridge.CanFormPaddy(t))
            {
                return false;
            }
            return true;
        }

        public override Job JobOnCell(Pawn pawn, IntVec3 cell, bool forced = false)
        {
            return JobMaker.MakeJob(FieldcraftDefOf.FC_IrrigatePaddy, cell);
        }
    }

    public class WorkGiver_Drain : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(FieldcraftDefOf.FC_Drain);
        }

        public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
        {
            foreach (Designation des in pawn.Map.designationManager.SpawnedDesignationsOfDef(FieldcraftDefOf.FC_Drain))
            {
                yield return des.target.Cell;
            }
        }

        public override bool HasJobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
        {
            if (pawn.Map.designationManager.DesignationAt(c, FieldcraftDefOf.FC_Drain) == null)
            {
                return false;
            }
            if (c.IsForbidden(pawn) || !pawn.CanReserve(c, 1, -1, null, forced))
            {
                return false;
            }
            return c.GetTerrain(pawn.Map) == FieldcraftDefOf.FC_PaddyFlooded;
        }

        public override Job JobOnCell(Pawn pawn, IntVec3 cell, bool forced = false)
        {
            return JobMaker.MakeJob(FieldcraftDefOf.FC_DrainPaddy, cell);
        }
    }

    public class WorkGiver_PlowIn : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(FieldcraftDefOf.FC_PlowIn);
        }

        public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
        {
            foreach (Designation des in pawn.Map.designationManager.SpawnedDesignationsOfDef(FieldcraftDefOf.FC_PlowIn))
            {
                yield return des.target.Cell;
            }
        }

        public override bool HasJobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
        {
            if (pawn.Map.designationManager.DesignationAt(c, FieldcraftDefOf.FC_PlowIn) == null)
            {
                return false;
            }
            if (c.IsForbidden(pawn) || !pawn.CanReserve(c, 1, -1, null, forced))
            {
                return false;
            }
            Plant plant = c.GetPlant(pawn.Map);
            return plant != null && plant.Growth >= 0.35f;
        }

        public override Job JobOnCell(Pawn pawn, IntVec3 cell, bool forced = false)
        {
            Plant plant = cell.GetPlant(pawn.Map);
            if (plant == null)
            {
                return null;
            }
            return JobMaker.MakeJob(FieldcraftDefOf.FC_PlowInPlant, plant);
        }
    }
}
