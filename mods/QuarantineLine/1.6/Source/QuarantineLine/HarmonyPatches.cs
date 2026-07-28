using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace QuarantineLine
{
    /// <summary>Wake map component when infectious hediff is added.</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new Type[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_HealthTracker_AddHediff
    {
        private static readonly AccessTools.FieldRef<Pawn_HealthTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_HealthTracker, Pawn>("pawn");

        public static void Postfix(Pawn_HealthTracker __instance, Hediff hediff)
        {
            if (!QuarantineUtility.Enabled || hediff == null || !QuarantineUtility.IsInfectiousHediff(hediff))
            {
                return;
            }
            Pawn pawn = null;
            try
            {
                pawn = PawnField(__instance);
            }
            catch
            {
                return;
            }
            if (pawn?.Map == null)
            {
                return;
            }
            // Only care about player-relevant pawns for wake.
            if (!pawn.IsColonist && !pawn.IsPrisonerOfColony && !(pawn.Faction?.IsPlayer ?? false))
            {
                return;
            }
            MapComponent_Quarantine.For(pawn.Map)?.NotifyPossibleInfection();
        }
    }

    /// <summary>Inspect string: quarantine / isolation status.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_Pawn_GetInspectString
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            string extra = QuarantineUtility.InspectLine(__instance);
            if (extra.NullOrEmpty())
            {
                return;
            }
            if (__result.NullOrEmpty())
            {
                __result = extra;
            }
            else
            {
                __result = __result + "\n" + extra;
            }
        }
    }

    /// <summary>
    /// Map-level gizmo via PlaySettings-style: add isolation toggle on selected colonists
    /// when outbreak is awake — player verb without hunting the architect tab.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo g in __result)
            {
                yield return g;
            }

            if (!QuarantineUtility.Enabled
                || __instance == null
                || !__instance.IsColonistPlayerControlled
                || __instance.Map == null)
            {
                yield break;
            }

            MapComponent_Quarantine comp = MapComponent_Quarantine.For(__instance.Map);
            if (comp == null)
            {
                yield break;
            }

            // Always allow policy toggle once disease has ever been relevant or zone exists;
            // still cheap when asleep (just a button).
            yield return new Command_Toggle
            {
                defaultLabel = "QL_Gizmo_Strict_Label".Translate(),
                defaultDesc = "QL_Gizmo_Strict_Desc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Designators/IsolationPolicy", true),
                isActive = () => comp.StrictIsolation,
                toggleAction = () => comp.ToggleStrictIsolation()
            };

            if (comp.IsAwake && QuarantineUtility.IsInfectiousCarrier(__instance))
            {
                bool inWard = comp.IsQuarantineCell(__instance.Position);
                yield return new Command_Action
                {
                    defaultLabel = inWard
                        ? "QL_Gizmo_ReleasePatient_Label".Translate()
                        : "QL_Gizmo_SendToWard_Label".Translate(),
                    defaultDesc = inWard
                        ? "QL_Gizmo_ReleasePatient_Desc".Translate()
                        : "QL_Gizmo_SendToWard_Desc".Translate(),
                    icon = ContentFinder<Texture2D>.Get(
                        inWard ? "UI/Designators/QuarantineRelease" : "UI/Designators/QuarantineMark",
                        true),
                    action = () =>
                    {
                        if (inWard)
                        {
                            // Release the room footprint around patient? Keep simple: clear their cell only.
                            comp.ReleaseCell(__instance.Position);
                            Messages.Message(
                                "QL_Msg_PatientCellReleased".Translate(__instance.LabelShort),
                                __instance,
                                MessageTypeDefOf.TaskCompletion,
                                historical: false);
                        }
                        else
                        {
                            // Mark current cell + adjacent 3x3 as ward seed if empty.
                            CellRect rect = CellRect.CenteredOn(__instance.Position, 1);
                            foreach (IntVec3 c in rect)
                            {
                                if (c.InBounds(__instance.Map))
                                {
                                    comp.MarkCell(c);
                                }
                            }
                            Messages.Message(
                                "QL_Msg_PatientWardSeeded".Translate(__instance.LabelShort),
                                __instance,
                                MessageTypeDefOf.TaskCompletion,
                                historical: false);
                        }
                    }
                };
            }
        }
    }

    /// <summary>
    /// Light tend-time exposure: non-doctor visitors already handled by pulse;
    /// doctors tend more often — under open protocol, tiny extra seed chance on tend
    /// if patient is infectious and doctor is not immune. Strict isolation lowers this.
    /// Does not replace vanilla tend/medicine.
    /// </summary>
    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    public static class Patch_TendUtility_DoTend
    {
        public static void Postfix(Pawn doctor, Pawn patient, Thing medicine)
        {
            if (!QuarantineUtility.SpreadEnabled || doctor == null || patient == null)
            {
                return;
            }
            if (doctor.Map == null || doctor.Map != patient.Map)
            {
                return;
            }
            if (!QuarantineUtility.IsInfectiousCarrier(patient))
            {
                return;
            }
            if (QuarantineUtility.IsInfectiousCarrier(doctor))
            {
                return;
            }

            Hediff src = QuarantineUtility.FirstInfectiousHediff(patient);
            if (src?.def == null || !QuarantineUtility.CanCatch(doctor, src.def))
            {
                return;
            }

            MapComponent_Quarantine comp = MapComponent_Quarantine.For(doctor.Map);
            QuarantineLineSettings settings = QuarantineLineMod.Settings;
            if (settings == null)
            {
                return;
            }

            float chance = settings.VisitRisk01 * 0.35f; // tend is closer contact than a visit pulse
            if (comp != null && comp.StrictIsolation)
            {
                // PPE / protocol: much safer for medical staff.
                chance *= settings.IsolationKeepFactor * 0.5f;
            }
            if (comp != null && comp.IsQuarantineCell(patient.Position) && !comp.StrictIsolation)
            {
                chance *= 1.1f; // open ward, still contact
            }
            chance = Mathf.Min(chance, settings.easyMode ? 0.04f : 0.12f);

            if (Rand.Chance(chance) && QuarantineUtility.TrySeedInfection(doctor, src.def))
            {
                Messages.Message(
                    "QL_Msg_DoctorCaught".Translate(doctor.LabelShort, src.def.label),
                    doctor,
                    MessageTypeDefOf.NegativeHealthEvent);
            }
        }
    }
}
