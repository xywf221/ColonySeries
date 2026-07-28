using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace SalvageAtlas
{
    public class WorkGiver_TeardownWreck : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            if (!SalvageUtility.ModActive || !SalvageUtility.ResearchDone)
            {
                return true;
            }
            GameComponent_SalvageAtlas atlas = GameComponent_SalvageAtlas.Get();
            if (atlas == null || !atlas.HasTeardownUnlocked)
            {
                return true;
            }
            return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(SalvageAtlasDefOf.SA_TeardownWreck);
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (Designation des in pawn.Map.designationManager.SpawnedDesignationsOfDef(SalvageAtlasDefOf.SA_TeardownWreck))
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
            if (pawn.Map.designationManager.DesignationOn(t, SalvageAtlasDefOf.SA_TeardownWreck) == null)
            {
                return false;
            }
            if (!SalvageUtility.IsTeardownTarget(t))
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
            return JobMaker.MakeJob(SalvageAtlasDefOf.SA_TeardownWreckJob, t);
        }
    }
}
