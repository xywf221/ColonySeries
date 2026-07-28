using HarmonyLib;
using RimWorld;
using Verse;

namespace WorkshopWear
{
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.Notify_IterationCompleted))]
    public static class Patch_Bill_Notify_IterationCompleted
    {
        public static void Postfix(Bill_Production __instance, Pawn billDoer)
        {
            var settings = WorkshopWearMod.Settings;
            if (settings != null && !settings.modEnabled)
            {
                return;
            }

            ThingWithComps bench = __instance.billStack?.billGiver as ThingWithComps;
            CompWorkshopWear comp = bench?.GetComp<CompWorkshopWear>();
            if (comp == null)
            {
                return;
            }

            comp.OnBillIterationCompleted();

            // Deviation: tiny quality risk while overworking (player opted into the hero button).
            if (settings != null
                && settings.overworkQualityRisk
                && comp.overworkActive
                && billDoer != null
                && Rand.Chance(0.04f))
            {
                // Soft narrative nudge only — no item rewrite mid-flight.
                // Real quality is locked at product finish; we just warn once in a while.
                if (Rand.Chance(0.35f))
                {
                    Messages.Message(
                        "WW_Message_OverworkQualityRisk".Translate(bench.LabelShort),
                        bench,
                        MessageTypeDefOf.SilentInput,
                        historical: false);
                }
            }
        }
    }

    public class StatPart_WorkshopWear : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!req.HasThing)
            {
                return;
            }

            var settings = WorkshopWearMod.Settings;
            if (settings != null && !settings.modEnabled)
            {
                return;
            }

            CompWorkshopWear comp = req.Thing.TryGetComp<CompWorkshopWear>();
            if (comp == null)
            {
                return;
            }

            val *= comp.SpeedFactor;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!req.HasThing)
            {
                return null;
            }

            var settings = WorkshopWearMod.Settings;
            if (settings != null && !settings.modEnabled)
            {
                return null;
            }

            CompWorkshopWear comp = req.Thing.TryGetComp<CompWorkshopWear>();
            if (comp == null)
            {
                return null;
            }

            return "WW_StatPart".Translate(comp.State.ToString(), comp.SpeedFactor.ToStringPercent());
        }
    }
}
