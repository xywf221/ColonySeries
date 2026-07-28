using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RumorMill
{
    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    public static class Patch_TendUtility_DoTend
    {
        public static void Postfix(Pawn doctor, Pawn patient, Thing medicine)
        {
            if (doctor == null || patient == null)
            {
                return;
            }
            RumorUtility.NotifyTendSuccess(doctor, patient);
        }
    }

    [HarmonyPatch(typeof(ExecutionUtility), nameof(ExecutionUtility.DoExecutionByCut))]
    public static class Patch_ExecutionUtility_DoExecutionByCut
    {
        public static void Postfix(Pawn executioner, Pawn victim, int bloodPerWeight, bool spawnBlood)
        {
            if (executioner == null)
            {
                return;
            }
            RumorUtility.NotifyBloodyDeed(executioner, "RM_Msg_BloodyHands_Execute");
        }
    }

    [HarmonyPatch(typeof(Recipe_RemoveBodyPart), nameof(Recipe_RemoveBodyPart.ApplyOnPawn))]
    public static class Patch_Recipe_RemoveBodyPart_ApplyOnPawn
    {
        public static void Postfix(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer == null || bill?.recipe == null)
            {
                return;
            }
            // Organ harvest / malicious removal — not every amputation (still a body-part remove, but
            // design wants organ harvest + malice; use recipe products or IsViolation).
            RecipeDef recipe = bill.recipe;
            bool organish = RumorUtility.IsOrganRemovalRecipe(recipe);
            // Worker property casing differs across versions; organish recipe name is enough for Phase 1.
            if (organish)
            {
                RumorUtility.NotifyBloodyDeed(billDoer, "RM_Msg_BloodyHands_Organ");
            }
        }
    }

    [HarmonyPatch(typeof(Recipe_ExecuteByCut), nameof(Recipe_ExecuteByCut.ApplyOnPawn))]
    public static class Patch_Recipe_ExecuteByCut_ApplyOnPawn
    {
        public static void Postfix(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            if (billDoer != null)
            {
                RumorUtility.NotifyBloodyDeed(billDoer, "RM_Msg_BloodyHands_Execute");
            }
        }
    }

    [HarmonyPatch(typeof(RecordsUtility), nameof(RecordsUtility.Notify_PawnKilled))]
    public static class Patch_RecordsUtility_Notify_PawnKilled
    {
        public static void Postfix(Pawn killed, Pawn killer)
        {
            if (killer == null || killed == null)
            {
                return;
            }
            RumorUtility.NotifyRaidKill(killer, killed);
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class Patch_MentalStateHandler_TryStartMentalState
    {
        public static void Postfix(MentalStateHandler __instance, MentalStateDef stateDef, bool __result)
        {
            if (!__result || stateDef == null)
            {
                return;
            }
            // Flee / give-up style breaks only (string match — avoids DefOf version drift)
            string n = stateDef.defName ?? string.Empty;
            bool flee = n.IndexOf("Flee", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("GiveUp", StringComparison.OrdinalIgnoreCase) >= 0
                        || n == "PanicFlee"
                        || n == "GiveUpExit"
                        || n == "PanicFleeFire";
            if (!flee)
            {
                return;
            }
            Pawn pawn = GetPawn(__instance);
            if (pawn != null)
            {
                RumorUtility.NotifyFlee(pawn);
            }
        }

        private static readonly AccessTools.FieldRef<MentalStateHandler, Pawn> PawnField =
            AccessTools.FieldRefAccess<MentalStateHandler, Pawn>("pawn");

        private static Pawn GetPawn(MentalStateHandler handler)
        {
            try
            {
                return PawnField(handler);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Squeamish: when death thoughts are distributed, count humanlike witnesses lightly.
    /// Uses vanilla pipeline so we do not invent a second death mesh.
    /// </summary>
    [HarmonyPatch(typeof(PawnDiedOrDownedThoughtsUtility), "TryGiveThoughts", new Type[] { typeof(Pawn), typeof(DamageInfo?), typeof(PawnDiedOrDownedThoughtsKind) })]
    public static class Patch_PawnDiedOrDownedThoughtsUtility_TryGiveThoughts
    {
        public static void Postfix(Pawn victim, DamageInfo? dinfo, PawnDiedOrDownedThoughtsKind thoughtsKind)
        {
            if (thoughtsKind != PawnDiedOrDownedThoughtsKind.Died || victim?.Map == null)
            {
                return;
            }
            if (!victim.RaceProps.Humanlike)
            {
                return;
            }
            // Bounded: only colonists on the same map who can see the corpse cell, max few checks
            Map map = victim.Map;
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists == null)
            {
                return;
            }
            IntVec3 cell = victim.Position;
            int counted = 0;
            for (int i = 0; i < colonists.Count && counted < 8; i++)
            {
                Pawn c = colonists[i];
                if (c == null || c == victim || c.Dead || c.Downed)
                {
                    continue;
                }
                if (!c.health.capacities.CapableOf(PawnCapacityDefOf.Sight))
                {
                    continue;
                }
                if (!c.Position.InHorDistOf(cell, 12f))
                {
                    continue;
                }
                if (!GenSight.LineOfSight(c.Position, cell, map, skipFirstCell: true))
                {
                    continue;
                }
                counted++;
                RumorUtility.NotifyDeathWitnessed(c);
            }
        }
    }

    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceFor))]
    public static class Patch_Tradeable_GetPriceFor
    {
        public static void Postfix(Tradeable __instance, TradeAction action, ref float __result)
        {
            if (!RumorUtility.Enabled || __instance.IsCurrency)
            {
                return;
            }
            Pawn negotiator = TradeSession.playerNegotiator;
            float offset = RumorUtility.TradePriceOffset(negotiator);
            if (Math.Abs(offset) < 0.0001f)
            {
                return;
            }
            // Positive reputation → better deals: buy cheaper, sell higher
            if (action == TradeAction.PlayerBuys)
            {
                __result *= 1f - offset;
            }
            else if (action == TradeAction.PlayerSells)
            {
                __result *= 1f + offset;
            }
        }
    }

    [HarmonyPatch(typeof(Tradeable), nameof(Tradeable.GetPriceTooltip))]
    public static class Patch_Tradeable_GetPriceTooltip
    {
        public static void Postfix(Tradeable __instance, TradeAction action, ref string __result)
        {
            if (!RumorUtility.Enabled)
            {
                return;
            }
            Pawn negotiator = TradeSession.playerNegotiator;
            float offset = RumorUtility.TradePriceOffset(negotiator);
            if (Math.Abs(offset) < 0.001f || negotiator == null)
            {
                return;
            }
            string line = "RM_TradeTooltip".Translate(negotiator.LabelShort, offset.ToStringPercentSigned());
            if (__result.NullOrEmpty())
            {
                __result = line;
            }
            else
            {
                __result = __result + "\n" + line;
            }
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt), nameof(InteractionWorker_RecruitAttempt.Interacted))]
    public static class Patch_RecruitAttempt_Interacted
    {
        // Resistance is reduced inside Interacted; we adjust guest.resistance slightly before via Prefix
        public static void Prefix(Pawn initiator, Pawn recipient)
        {
            if (!RumorUtility.Enabled || recipient?.guest == null)
            {
                return;
            }
            float offset = RumorUtility.NegotiationOffset(initiator);
            if (Math.Abs(offset) < 0.0001f)
            {
                return;
            }
            // Positive offset → slightly lower resistance (easier recruit). Cap small.
            float delta = -offset * 2.5f; // ~±0.2 resistance per strong tag set
            if (recipient.guest.resistance > 0f)
            {
                recipient.guest.resistance = Math.Max(0f, recipient.guest.resistance + delta);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith))]
    public static class Patch_Interactions_TryInteractWith
    {
        public static void Postfix(Pawn_InteractionsTracker __instance, Pawn recipient, InteractionDef intDef, bool __result)
        {
            if (!__result || recipient == null || intDef == null)
            {
                return;
            }
            Pawn initiator = GetPawn(__instance);
            if (initiator == null)
            {
                return;
            }
            // Chitchat / deep talk / kind words → mild rumor reinforce (player agency via social)
            string n = intDef.defName ?? string.Empty;
            if (n == "Chitchat" || n == "DeepTalk" || n == "KindWords" || intDef == RumorMillDefOf.RM_RumorChat)
            {
                RumorUtility.ApplyChatSpread(initiator, recipient);
            }
        }

        private static readonly AccessTools.FieldRef<Pawn_InteractionsTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_InteractionsTracker, Pawn>("pawn");

        private static Pawn GetPawn(Pawn_InteractionsTracker tracker)
        {
            try
            {
                return PawnField(tracker);
            }
            catch
            {
                return null;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
    public static class Patch_Pawn_GetInspectString
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            if (!RumorUtility.Enabled || __instance == null)
            {
                return;
            }
            string extra = RumorUtility.InspectStringFor(__instance);
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

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo g in __result)
            {
                yield return g;
            }
            if (!RumorUtility.Enabled
                || RumorMillMod.Settings == null
                || !RumorMillMod.Settings.enableCommend
                || __instance == null
                || !__instance.IsColonistPlayerControlled
                || __instance.Downed)
            {
                yield break;
            }
            // Multi-select: first selected colonist commends this one if different
            yield return new Command_Action
            {
                defaultLabel = "RM_Gizmo_Commend".Translate(),
                defaultDesc = "RM_Gizmo_CommendDesc".Translate(),
                icon = TexCommand.Install,
                action = () =>
                {
                    Pawn speaker = FindCommendSpeaker(__instance);
                    if (speaker == null)
                    {
                        Messages.Message("RM_Msg_CommendNeedSpeaker".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }
                    if (speaker == __instance)
                    {
                        // Self-commend allowed but weaker flavor — still works as player verb
                        RumorUtility.ApplyCommend(speaker, __instance);
                        return;
                    }
                    Job job = JobMaker.MakeJob(RumorMillDefOf.RM_Commend, __instance);
                    job.playerForced = true;
                    speaker.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
            };
        }

        private static Pawn FindCommendSpeaker(Pawn target)
        {
            List<Pawn> selected = Find.Selector.SelectedPawns;
            if (selected != null)
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    Pawn p = selected[i];
                    if (p != null && p != target && p.IsColonistPlayerControlled && !p.Downed && p.Spawned)
                    {
                        return p;
                    }
                }
            }
            // Fallback: any free colonist on map
            if (target.Map?.mapPawns?.FreeColonistsSpawned != null)
            {
                List<Pawn> list = target.Map.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < list.Count; i++)
                {
                    Pawn p = list[i];
                    if (p != target && !p.Downed)
                    {
                        return p;
                    }
                }
            }
            return target.IsColonistPlayerControlled ? target : null;
        }
    }

    /// <summary>TE soft-link: when hediff added matches extraction trauma/scar.</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff), new Type[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_HealthTracker_AddHediff
    {
        public static void Postfix(Pawn_HealthTracker __instance, Hediff hediff, BodyPartRecord part, DamageInfo? dinfo, DamageWorker.DamageResult result)
        {
            if (hediff?.def == null)
            {
                return;
            }
            string n = hediff.def.defName;
            if (n != "TE_NeuralScar" && n != "TE_NeuralTrauma")
            {
                return;
            }
            Pawn pawn = GetPawn(__instance);
            if (pawn != null)
            {
                RumorUtility.NotifyNerved(pawn);
            }
        }

        private static readonly AccessTools.FieldRef<Pawn_HealthTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_HealthTracker, Pawn>("pawn");

        private static Pawn GetPawn(Pawn_HealthTracker tracker)
        {
            try
            {
                return PawnField(tracker);
            }
            catch
            {
                return null;
            }
        }
    }
}
