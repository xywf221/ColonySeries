using RimWorld;
using Verse;

namespace QuarantineLine
{
    /// <summary>Patients in the quarantine zone while infectious (or while strict + in zone).</summary>
    public class ThoughtWorker_Quarantined : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!QuarantineUtility.Enabled || p?.Map == null || !p.IsColonist)
            {
                return ThoughtState.Inactive;
            }
            MapComponent_Quarantine comp = MapComponent_Quarantine.For(p.Map);
            if (comp == null || !comp.IsAwake || !comp.IsQuarantineCell(p.Position))
            {
                return ThoughtState.Inactive;
            }
            // Only patients (infectious) feel isolation penalty — healthy visitors use visiting thought.
            if (!QuarantineUtility.IsInfectiousCarrier(p))
            {
                return ThoughtState.Inactive;
            }
            return ThoughtState.ActiveAtStage(comp.StrictIsolation ? 1 : 0);
        }
    }

    /// <summary>Healthy colonists standing in the quarantine zone (visiting risk / fear).</summary>
    public class ThoughtWorker_VisitingWard : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!QuarantineUtility.Enabled || p?.Map == null || !p.IsColonist)
            {
                return ThoughtState.Inactive;
            }
            if (QuarantineUtility.IsInfectiousCarrier(p))
            {
                return ThoughtState.Inactive;
            }
            MapComponent_Quarantine comp = MapComponent_Quarantine.For(p.Map);
            if (comp == null || !comp.IsAwake || !comp.IsQuarantineCell(p.Position))
            {
                return ThoughtState.Inactive;
            }
            return ThoughtState.ActiveAtStage(0);
        }
    }

    /// <summary>
    /// Doctors feel extra workload when strict isolation is on and sick exist —
    /// classic tradeoff: lower outbreak, higher medical pressure.
    /// </summary>
    public class ThoughtWorker_IsolationRounds : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!QuarantineUtility.Enabled || p?.Map == null || !p.IsColonist || p.Dead)
            {
                return ThoughtState.Inactive;
            }
            MapComponent_Quarantine comp = MapComponent_Quarantine.For(p.Map);
            if (comp == null || !comp.IsAwake || !comp.StrictIsolation || comp.CarrierCount <= 0)
            {
                return ThoughtState.Inactive;
            }
            // WorkTypeDefOf.Doctor may exist; also accept high medical skill as fallback.
            bool isDoctor = false;
            if (p.workSettings != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
            {
                int prio = p.workSettings.GetPriority(WorkTypeDefOf.Doctor);
                isDoctor = prio > 0 && prio <= 3;
            }
            if (!isDoctor && p.skills != null)
            {
                SkillRecord med = p.skills.GetSkill(SkillDefOf.Medicine);
                isDoctor = med != null && med.Level >= 6;
            }
            if (!isDoctor)
            {
                return ThoughtState.Inactive;
            }
            return ThoughtState.ActiveAtStage(0);
        }
    }
}
