using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ledger
{
    /// <summary>
    /// Honest-trade credit tick only. Does NOT invent mid-deal shortfalls —
    /// vanilla trade cannot finish with unpaid silver. Leverage is the Draw Credit gizmo.
    /// </summary>
    [HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
    public static class Patch_TradeDeal_TryExecute
    {
        public static void Postfix(bool __result)
        {
            if (!__result)
            {
                return;
            }

            GameComponent_Ledger ledger = GameComponent_Ledger.Get();
            if (ledger == null || !ledger.Enabled)
            {
                return;
            }

            // Approximate deal size from silver movement is awkward post-hoc;
            // give a small credit bump for any successful player trade session.
            Faction other = TradeSession.trader?.Faction;
            // Use a fixed modest bump when a deal closes (player negotiated something).
            ledger.RecordHonestTrade(100, other);
        }
    }

    /// <summary>
    /// Colonist gizmos: status, draw credit (player verb), repay/extend per debt.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_Pawn_GetGizmos_Ledger
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo g in __result)
            {
                yield return g;
            }

            GameComponent_Ledger ledger = GameComponent_Ledger.Get();
            if (ledger == null || !ledger.Enabled)
            {
                yield break;
            }

            if (__instance == null || !__instance.IsColonistPlayerControlled)
            {
                yield break;
            }

            // Only show full bookkeeping on one colonist at a time feel: still OK on all —
            // but skip downed/mental to reduce noise.
            if (__instance.Downed || __instance.Dead)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "LD_Gizmo_Status".Translate(),
                defaultDesc = ledger.MainButtonTip(),
                icon = TexCommand.DesirePower,
                action = () => ledger.SendStatusLetter()
            };

            LedgerSettings settings = LedgerMod.Settings;
            if (settings == null || settings.enableDrawCredit)
            {
                bool canDraw = ledger.CanDrawNow(out string drawReason);
                yield return new Command_Action
                {
                    defaultLabel = "LD_Gizmo_Draw".Translate(ledger.AvailableCreditRoom()),
                    defaultDesc = canDraw
                        ? "LD_Gizmo_DrawDesc".Translate(
                            ledger.creditScore.ToString("F0"),
                            ledger.AvailableCreditRoom(),
                            (LedgerMod.Settings?.debtDueDays ?? 7f).ToString("F0"))
                        : drawReason,
                    icon = TexCommand.Install,
                    Disabled = !canDraw,
                    disabledReason = canDraw ? null : drawReason,
                    action = () => OpenDrawMenu(__instance, ledger)
                };
            }

            if (ledger.debts != null && ledger.debts.Count > 0)
            {
                ledger.SortDebtsByDue();

                yield return new Command_Action
                {
                    defaultLabel = "LD_Gizmo_RepayMenu".Translate(ledger.debts.Count),
                    defaultDesc = "LD_Gizmo_RepayMenuDesc".Translate(),
                    icon = TexCommand.ForbidOff,
                    action = () => OpenRepayMenu(__instance, ledger)
                };

                // Quick-repay oldest (most urgent)
                LedgerDebt oldest = ledger.debts[0];
                yield return new Command_Action
                {
                    defaultLabel = "LD_Gizmo_Repay".Translate(oldest.amount),
                    defaultDesc = "LD_Gizmo_RepayDesc".Translate(oldest.factionLabel ?? "?", oldest.amount),
                    icon = TexCommand.Install,
                    action = () =>
                    {
                        Map map = __instance.Map ?? Find.AnyPlayerHomeMap;
                        ledger.TryRepay(oldest, map);
                    }
                };

                bool anyExtendable = false;
                for (int i = 0; i < ledger.debts.Count; i++)
                {
                    if (!ledger.debts[i].extendedOnce)
                    {
                        anyExtendable = true;
                        break;
                    }
                }

                if (anyExtendable)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "LD_Gizmo_ExtendMenu".Translate(),
                        defaultDesc = "LD_Gizmo_ExtendDesc".Translate(),
                        icon = TexCommand.ClearPrioritizedWork,
                        action = () => OpenExtendMenu(ledger)
                    };
                }
            }

            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: +10 credit",
                    action = () =>
                    {
                        ledger.AdjustCredit(10f);
                        Messages.Message($"credit={ledger.creditScore:F0}", MessageTypeDefOf.NeutralEvent, false);
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: force draw 100",
                    action = () =>
                    {
                        ledger.lastDrawTick = 0;
                        Map map = __instance.Map ?? Find.AnyPlayerHomeMap;
                        ledger.TryDrawCredit(100, map, __instance);
                    }
                };
            }
        }

        private static void OpenDrawMenu(Pawn pawn, GameComponent_Ledger ledger)
        {
            if (!ledger.CanDrawNow(out string reason))
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            List<int> amounts = ledger.SuggestedDrawAmounts();
            if (amounts.Count == 0)
            {
                Messages.Message("LD_Msg_NoRoom".Translate(ledger.AvailableCreditRoom(), 50),
                    MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            var opts = new List<FloatMenuOption>();
            Faction creditor = GameComponent_Ledger.PickCreditorFaction();
            string credName = creditor?.Name ?? "LD_UnknownCreditor".Translate();
            for (int i = 0; i < amounts.Count; i++)
            {
                int amt = amounts[i];
                opts.Add(new FloatMenuOption(
                    "LD_Float_DrawAmount".Translate(amt, credName),
                    () =>
                    {
                        Map map = pawn.Map ?? Find.AnyPlayerHomeMap;
                        ledger.TryDrawCredit(amt, map, pawn, creditor);
                    }));
            }

            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private static void OpenRepayMenu(Pawn pawn, GameComponent_Ledger ledger)
        {
            if (ledger.debts == null || ledger.debts.Count == 0)
            {
                return;
            }

            ledger.SortDebtsByDue();
            Map map = pawn.Map ?? Find.AnyPlayerHomeMap;
            int have = GameComponent_Ledger.CountSilver(map);
            var opts = new List<FloatMenuOption>();
            for (int i = 0; i < ledger.debts.Count; i++)
            {
                LedgerDebt d = ledger.debts[i];
                int hours = Mathf.Max(0, (d.dueTick - Find.TickManager.TicksGame) / 2500);
                string label = "LD_Float_RepayDebt".Translate(
                    d.amount,
                    d.factionLabel ?? "?",
                    hours,
                    have);
                bool can = have >= d.amount;
                opts.Add(new FloatMenuOption(
                    label,
                    can ? () => ledger.TryRepay(d, map) : null));
            }

            Find.WindowStack.Add(new FloatMenu(opts));
        }

        private static void OpenExtendMenu(GameComponent_Ledger ledger)
        {
            if (ledger.debts == null || ledger.debts.Count == 0)
            {
                return;
            }

            ledger.SortDebtsByDue();
            var opts = new List<FloatMenuOption>();
            for (int i = 0; i < ledger.debts.Count; i++)
            {
                LedgerDebt d = ledger.debts[i];
                if (d.extendedOnce)
                {
                    opts.Add(new FloatMenuOption(
                        "LD_Float_AlreadyExtended".Translate(d.amount, d.factionLabel ?? "?"),
                        null));
                    continue;
                }

                opts.Add(new FloatMenuOption(
                    "LD_Float_ExtendDebt".Translate(d.amount, d.factionLabel ?? "?"),
                    () => ledger.TryExtend(d)));
            }

            Find.WindowStack.Add(new FloatMenu(opts));
        }
    }

    /// <summary>Optional reserve mood — only if setting enabled (default OFF).</summary>
    public class ThoughtWorker_LedgerReserveOptIn : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            LedgerSettings s = LedgerMod.Settings;
            if (s == null || !s.modEnabled || !s.enableReserveMood)
            {
                return ThoughtState.Inactive;
            }

            if (p?.Map == null || !p.IsColonist)
            {
                return ThoughtState.Inactive;
            }

            int meals = p.Map.resourceCounter.GetCount(ThingDefOf.MealSimple)
                        + p.Map.resourceCounter.GetCount(ThingDefOf.MealFine);
            ThingDef lavish = DefDatabase<ThingDef>.GetNamedSilentFail("MealLavish");
            if (lavish != null)
            {
                meals += p.Map.resourceCounter.GetCount(lavish);
            }

            int col = p.Map.mapPawns.FreeColonistsSpawnedCount;
            if (col <= 0)
            {
                return ThoughtState.Inactive;
            }

            if (meals >= col * 3)
            {
                return ThoughtState.Inactive;
            }

            return ThoughtState.ActiveDefault;
        }
    }
}
