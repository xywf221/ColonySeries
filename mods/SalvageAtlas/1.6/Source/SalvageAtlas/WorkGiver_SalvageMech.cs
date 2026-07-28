using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SalvageAtlas
{
    public class WorkGiver_SalvageMech : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!SalvageUtility.ModActive || !SalvageUtility.ResearchDone)
            {
                return true;
            }
            return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(SalvageAtlasDefOf.SA_SalvageMech);
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Designation des in pawn.Map.designationManager.SpawnedDesignationsOfDef(SalvageAtlasDefOf.SA_SalvageMech))
            {
                if (des.target.HasThing)
                {
                    yield return des.target.Thing;
                }
            }
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (t == null || t.Destroyed || !t.Spawned)
            {
                return false;
            }
            if (pawn.Map.designationManager.DesignationOn(t, SalvageAtlasDefOf.SA_SalvageMech) == null)
            {
                return false;
            }
            MapComponent_SalvageAtlas mapComp = MapComponent_SalvageAtlas.For(pawn.Map);
            if (mapComp != null && mapComp.WasSalvaged(t))
            {
                return false;
            }
            if (!SalvageUtility.IsMechSalvageTarget(t))
            {
                return false;
            }
            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }
            if (!pawn.CanReach(t, PathEndMode.Touch, Danger.Deadly))
            {
                return false;
            }
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(SalvageAtlasDefOf.SA_SalvageMechCorpse, t);
        }
    }
}
