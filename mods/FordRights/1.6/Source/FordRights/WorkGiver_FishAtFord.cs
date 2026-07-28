using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace FordRights
{
    public class WorkGiver_FishAtFord : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override ThingRequest PotentialWorkThingRequest =>
            ThingRequest.ForDef(FordRightsDefOf.FR_FordCrossing);

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            FordRightsSettings settings = FordRightsMod.Settings;
            if (settings == null || !settings.modEnabled || !settings.fishingEnabled)
            {
                return true;
            }

            MapComponent_FordRights comp = MapComponent_FordRights.For(pawn.Map);
            if (comp != null && !comp.cachedHasWater)
            {
                return true;
            }

            return false;
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            List<Thing> list = pawn.Map.listerThings.ThingsOfDef(FordRightsDefOf.FR_FordCrossing);
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
            if (!(t is Building_FordCrossing ford) || !t.Spawned)
            {
                return false;
            }

            if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1, null, forced))
            {
                return false;
            }

            if (!pawn.CanReach(t, PathEndMode.Touch, pawn.NormalMaxDanger()))
            {
                return false;
            }

            if (!ford.IsOperational)
            {
                JobFailReason.Is("FR_Float_NotOperational".Translate());
                return false;
            }

            if (!forced && !ford.fishingQueued)
            {
                return false;
            }

            if (!ford.CanFishNow)
            {
                JobFailReason.Is("FR_Float_FishCooldown".Translate());
                return false;
            }

            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return JobMaker.MakeJob(FordRightsDefOf.FR_FishAtFord, t);
        }
    }
}
