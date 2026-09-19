using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LWOP.Buildings
{
    public static class LWOPTransportshipUtility
    {
        public static bool IsLWOPTransportship(Thing thing)
        {
            return thing != null &&
                thing.def != null &&
                string.Equals(thing.def.defName, "LWOPTransportship", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsLWOPTransportship(ThingComp comp)
        {
            return comp != null && IsLWOPTransportship(comp.parent);
        }
    }

    [HarmonyPatch(typeof(CompShuttle), "get_HasPilot")]
    public static class LWOPTransportshipAlwaysHasPilotPatch
    {
        public static void Postfix(CompShuttle __instance, ref bool __result)
        {
            if (LWOPTransportshipUtility.IsLWOPTransportship(__instance))
            {
                __result = true;
            }
        }
    }

    public class CompProperties_LWOPTransportshipTurretGun : CompProperties_TurretGun
    {
        public CompProperties_LWOPTransportshipTurretGun()
        {
            compClass = typeof(CompLWOPTransportshipTurretGun);
        }
    }

    public class CompLWOPTransportshipTurretGun : CompTurretGun
    {
        private const BindingFlags InstanceAnyVisibility = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo GunField = typeof(CompTurretGun).GetField("gun", InstanceAnyVisibility);
        private static readonly FieldInfo CurrentTargetField = typeof(CompTurretGun).GetField("currentTarget", InstanceAnyVisibility);
        private static readonly MethodInfo MakeGunMethod = typeof(CompTurretGun).GetMethod("MakeGun", InstanceAnyVisibility);
        private LocalTargetInfo manualTarget = LocalTargetInfo.Invalid;

        public override void PostPostMake()
        {
        }

        public override void CompTick()
        {
            if (!ActiveOnMap())
            {
                ClearManualTarget();
                return;
            }

            if (EnsureGun())
            {
                MaintainManualTarget();
                base.CompTick();
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!ActiveOnMap())
            {
                yield break;
            }

            if (!EnsureGun())
            {
                yield break;
            }

            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            yield return ManualFireCommand();

            if (manualTarget.IsValid)
            {
                yield return ClearManualFireCommand();
            }
        }

        public override List<PawnRenderNode> CompRenderNodes()
        {
            if (!ActiveOnMap())
            {
                return new List<PawnRenderNode>();
            }

            return EnsureGun() ? base.CompRenderNodes() : new List<PawnRenderNode>();
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            return EnsureGun() ? base.SpecialDisplayStats() : Enumerable.Empty<StatDrawEntry>();
        }

        public override void PostExposeData()
        {
        }

        private bool ActiveOnMap()
        {
            return LWOPTransportshipUtility.IsLWOPTransportship(parent) &&
                parent.Spawned &&
                parent.Map != null &&
                parent.Faction != null &&
                !parent.Destroyed;
        }

        private bool EnsureGun()
        {
            if (GunField == null || MakeGunMethod == null)
            {
                return false;
            }

            if (GunField.GetValue(this) == null)
            {
                MakeGunMethod.Invoke(this, null);
            }

            return GunField.GetValue(this) != null;
        }

        private Command_Target ManualFireCommand()
        {
            Command_Target command = new Command_Target
            {
                defaultLabel = "LWOPManualFire".Translate().ToString(),
                defaultDesc = "LWOPManualFireDesc".Translate().ToString(),
                icon = TexCommand.Attack,
                targetingParams = new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = true,
                    canTargetBuildings = true,
                    canTargetItems = true,
                    mapObjectTargetsMustBeAutoAttackable = false
                },
                onUpdate = delegate(LocalTargetInfo target)
                {
                    DrawAttackRange();
                },
                action = TryManualFire
            };

            return new Command_LWOPManualFire(command, DrawAttackRange);
        }

        private Command_Action ClearManualFireCommand()
        {
            return new Command_Action
            {
                defaultLabel = "LWOPClearManualFire".Translate().ToString(),
                defaultDesc = "LWOPClearManualFireDesc".Translate().ToString(),
                icon = TexCommand.ClearPrioritizedWork,
                action = ClearManualTarget
            };
        }

        private void TryManualFire(LocalTargetInfo target)
        {
            if (!ActiveOnMap() || !EnsureGun())
            {
                return;
            }

            Verb verb = AttackVerb;
            if (verb == null)
            {
                return;
            }

            if (!verb.CanHitTarget(target))
            {
                Messages.Message("CannotHitTarget".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (target.HasThing)
            {
                manualTarget = target;
                SetCurrentTarget(target);
                TryStartManualBurst();
                return;
            }

            ClearManualTarget();
            verb.TryStartCastOn(target, false, true, false, false);
        }

        private void MaintainManualTarget()
        {
            if (!manualTarget.IsValid)
            {
                return;
            }

            if (!ManualTargetStillValid())
            {
                ClearManualTarget();
                return;
            }

            SetCurrentTarget(manualTarget);
            TryStartManualBurst();
        }

        private bool ManualTargetStillValid()
        {
            if (!manualTarget.IsValid || !manualTarget.HasThing)
            {
                return false;
            }

            Thing thing = manualTarget.Thing;
            if (thing == null || thing.Destroyed || thing.Discarded || !thing.Spawned || thing.Map != parent.Map)
            {
                return false;
            }

            Pawn pawn = thing as Pawn;
            if (pawn != null && pawn.Dead)
            {
                return false;
            }

            Verb verb = AttackVerb;
            return verb != null && verb.CanHitTarget(manualTarget);
        }

        private void SetCurrentTarget(LocalTargetInfo target)
        {
            if (CurrentTargetField != null)
            {
                CurrentTargetField.SetValue(this, target);
            }
        }

        private void TryStartManualBurst()
        {
            Verb verb = AttackVerb;
            if (verb == null || verb.Bursting || !verb.Available() || !verb.CanHitTarget(manualTarget))
            {
                return;
            }

            verb.TryStartCastOn(manualTarget, false, true, false, true);
        }

        private void ClearManualTarget()
        {
            manualTarget = LocalTargetInfo.Invalid;
            SetCurrentTarget(LocalTargetInfo.Invalid);
        }

        private void DrawAttackRange()
        {
            if (!ActiveOnMap())
            {
                return;
            }

            Verb verb = AttackVerb;
            if (verb != null && verb.verbProps != null && verb.verbProps.range > 0f)
            {
                GenDraw.DrawRadiusRing(parent.Position, verb.verbProps.range);
            }
        }

        private class Command_LWOPManualFire : Command_Target
        {
            private readonly Action drawRange;

            public Command_LWOPManualFire(Command_Target source, Action drawRange)
            {
                this.drawRange = drawRange;
                defaultLabel = source.defaultLabel;
                defaultDesc = source.defaultDesc;
                icon = source.icon;
                targetingParams = source.targetingParams;
                onUpdate = source.onUpdate;
                action = source.action;
            }

            public override void GizmoUpdateOnMouseover()
            {
                base.GizmoUpdateOnMouseover();
                if (drawRange != null)
                {
                    drawRange();
                }
            }
        }
    }
}
