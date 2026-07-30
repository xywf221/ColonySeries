using RimWorld;
using Verse;
using Verse.AI;

namespace PersonalKit
{
    /// <summary>
    /// Subclass of Building_PassengerShuttle that implements IAttackTarget
    /// so enemies treat the shuttle as a valid target at building-level priority.
    /// </summary>
    public class Building_PassengerShuttle_Aggro : Building_PassengerShuttle, IAttackTarget
    {
        Thing IAttackTarget.Thing => this;

        // 0.4f = same as normal buildings (turrets are 1.0f)
        public float TargetPriorityFactor => 0.4f;

        public bool ThreatDisabled(IAttackTargetSearcher disabledFor) => false;

        public LocalTargetInfo TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;
    }
}
