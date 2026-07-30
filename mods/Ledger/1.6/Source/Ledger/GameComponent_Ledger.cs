using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ledger
{
    public class LedgerDebt : IExposable
    {
        public int amount;
        public int dueTick;
        public string factionLoadId;
        public string factionLabel;
        public bool extendedOnce;
        public string note;

        public Faction Faction
        {
            get
            {
                if (factionLoadId.NullOrEmpty())
                {
                    return null;
                }

                List<Faction> all = Find.FactionManager.AllFactionsListForReading;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].GetUniqueLoadID() == factionLoadId)
                    {
                        return all[i];
                    }
                }

                return null;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref amount, "amount", 0);
            Scribe_Values.Look(ref dueTick, "dueTick", 0);
            Scribe_Values.Look(ref factionLoadId, "factionLoadId");
            Scribe_Values.Look(ref factionLabel, "factionLabel");
            Scribe_Values.Look(ref extendedOnce, "extendedOnce", false);
            Scribe_Values.Look(ref note, "note");
        }
    }

    /// <summary>
    /// v2 credit leverage: player draws silver against credit, then repays / extends / defaults.
    /// No power tax. No permanent global markup. Reserve mood opt-in only.
    /// </summary>
    public class GameComponent_Ledger : GameComponent
    {
        public float creditScore = 70f;
        public List<LedgerDebt> debts = new List<LedgerDebt>();
        public int lastHonestTradeTick;
        public int nextPulseTick;
        public int lastStatusLetterTick;
        public int lastCollectionLetterTick;
        public int lastDrawTick;

        private const int MinDrawAmount = 50;
        private const int DrawCooldownTicks = 2500; // 1h between draws (anti-spam)

        public GameComponent_Ledger(Game game)
        {
        }

        public static GameComponent_Ledger Get() => Current.Game?.GetComponent<GameComponent_Ledger>();

        public bool Enabled => LedgerMod.Settings == null || LedgerMod.Settings.modEnabled;

        public int TotalDebt
        {
            get
            {
                int t = 0;
                if (debts == null)
                {
                    return 0;
                }

                for (int i = 0; i < debts.Count; i++)
                {
                    t += debts[i].amount;
                }

                return t;
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref creditScore, "creditScore", 70f);
            Scribe_Collections.Look(ref debts, "debts", LookMode.Deep);
            Scribe_Values.Look(ref lastHonestTradeTick, "lastHonestTradeTick", 0);
            Scribe_Values.Look(ref nextPulseTick, "nextPulseTick", 0);
            Scribe_Values.Look(ref lastStatusLetterTick, "lastStatusLetterTick", 0);
            Scribe_Values.Look(ref lastCollectionLetterTick, "lastCollectionLetterTick", 0);
            Scribe_Values.Look(ref lastDrawTick, "lastDrawTick", 0);
            if (debts == null)
            {
                debts = new List<LedgerDebt>();
            }
        }

        public override void GameComponentTick()
        {
            if (!Enabled || Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (nextPulseTick <= 0)
            {
                nextPulseTick = now + 2500;
            }

            if (now < nextPulseTick)
            {
                return;
            }

            nextPulseTick = now + 2500;
            TickDebts(now);
            MaybeWeeklyLetter(now);
        }

        private void TickDebts(int now)
        {
            if (debts == null || debts.Count == 0)
            {
                return;
            }

            SortDebtsByDue();
            for (int i = debts.Count - 1; i >= 0; i--)
            {
                LedgerDebt d = debts[i];
                if (now < d.dueTick)
                {
                    continue;
                }

                TryCollectionOrDefault(d);
            }
        }

        private void TryCollectionOrDefault(LedgerDebt d)
        {
            int now = Find.TickManager.TicksGame;
            // Grace: first overdue day → demand letter; still unpaid past +1 day → default
            if (now - d.dueTick < GenDate.TicksPerDay)
            {
                if (now - lastCollectionLetterTick > GenDate.TicksPerDay / 2)
                {
                    lastCollectionLetterTick = now;
                    Find.LetterStack.ReceiveLetter(
                        "LD_Letter_DueTitle".Translate(),
                        "LD_Letter_DueBody".Translate(d.amount, d.factionLabel ?? "?", creditScore.ToString("F0")),
                        LetterDefOf.NegativeEvent);
                }

                return;
            }

            DefaultDebt(d);
            debts.Remove(d);
        }

        public void AdjustCredit(float delta)
        {
            LedgerSettings s = LedgerMod.Settings;
            if (s != null && s.easyMode && delta < 0f)
            {
                delta *= 0.5f;
            }

            creditScore = Mathf.Clamp(creditScore + delta, 0f, 100f);
        }

        public int AvailableCreditRoom()
        {
            LedgerSettings s = LedgerMod.Settings ?? new LedgerSettings();
            if (creditScore < s.minCreditToBorrow)
            {
                return 0;
            }

            int cap = s.DebtCap(creditScore);
            return Mathf.Max(0, cap - TotalDebt);
        }

        public bool CanBorrow(int amount)
        {
            return Enabled && amount > 0 && amount <= AvailableCreditRoom();
        }

        public bool CanDrawNow(out string reason)
        {
            reason = null;
            if (!Enabled)
            {
                reason = "LD_Msg_Disabled".Translate();
                return false;
            }

            LedgerSettings s = LedgerMod.Settings ?? new LedgerSettings();
            if (!s.enableDrawCredit)
            {
                reason = "LD_Msg_DrawDisabled".Translate();
                return false;
            }

            if (creditScore < s.minCreditToBorrow)
            {
                reason = "LD_Msg_CreditTooLow".Translate(creditScore.ToString("F0"), s.minCreditToBorrow.ToString("F0"));
                return false;
            }

            int room = AvailableCreditRoom();
            if (room < MinDrawAmount)
            {
                reason = "LD_Msg_NoRoom".Translate(room, MinDrawAmount);
                return false;
            }

            int now = Find.TickManager.TicksGame;
            if (lastDrawTick > 0 && now - lastDrawTick < DrawCooldownTicks)
            {
                int left = (DrawCooldownTicks - (now - lastDrawTick) + 2499) / 2500;
                reason = "LD_Msg_DrawCooldown".Translate(Mathf.Max(1, left));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Player verb: borrow silver against colony credit. Spawns silver near the actor, opens a dated debt.
        /// </summary>
        public bool TryDrawCredit(int amount, Map map, Pawn actor, Faction preferredCreditor = null)
        {
            if (map == null || amount < MinDrawAmount)
            {
                return false;
            }

            if (!CanDrawNow(out string reason))
            {
                Messages.Message(reason, MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            amount = Mathf.Clamp(amount, MinDrawAmount, AvailableCreditRoom());
            if (!CanBorrow(amount))
            {
                Messages.Message("LD_Msg_NoRoom".Translate(AvailableCreditRoom(), MinDrawAmount),
                    MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            Faction creditor = preferredCreditor ?? PickCreditorFaction();
            if (!TryOpenDebt(amount, creditor, "draw"))
            {
                return false;
            }

            if (!SpawnSilverNear(map, amount, actor))
            {
                // Roll back debt if silver could not be placed (extremely rare).
                if (debts.Count > 0)
                {
                    debts.RemoveAt(debts.Count - 1);
                }

                Messages.Message("LD_Msg_SpawnFailed".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            lastDrawTick = Find.TickManager.TicksGame;
            // Tiny leverage cost — not interest; keeps free draws from being pure free money emotionally.
            AdjustCredit(-1f);
            Messages.Message(
                "LD_Msg_Drew".Translate(amount, creditor?.Name ?? "LD_UnknownCreditor".Translate(), creditScore.ToString("F0")),
                actor,
                MessageTypeDefOf.PositiveEvent);
            return true;
        }

        public bool TryOpenDebt(int amount, Faction faction, string note = null)
        {
            if (!CanBorrow(amount))
            {
                return false;
            }

            LedgerSettings s = LedgerMod.Settings ?? new LedgerSettings();
            var d = new LedgerDebt
            {
                amount = amount,
                dueTick = Find.TickManager.TicksGame + s.DueTicks,
                factionLoadId = faction?.GetUniqueLoadID(),
                factionLabel = faction?.Name ?? "LD_UnknownCreditor".Translate(),
                note = note
            };
            debts.Add(d);
            SortDebtsByDue();
            Find.LetterStack.ReceiveLetter(
                "LD_Letter_DebtOpenedTitle".Translate(),
                "LD_Letter_DebtOpenedBody".Translate(
                    amount,
                    d.factionLabel,
                    s.debtDueDays.ToString("F0"),
                    creditScore.ToString("F0")),
                LetterDefOf.NeutralEvent);
            return true;
        }

        public bool TryRepay(LedgerDebt d, Map map)
        {
            if (d == null || map == null)
            {
                return false;
            }

            int have = CountSilver(map);
            if (have < d.amount)
            {
                Messages.Message("LD_Msg_NeedSilver".Translate(d.amount, have), MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            // Consume first; only clear debt if fully paid (CountSilver and Consume must agree).
            if (!ConsumeSilver(map, d.amount))
            {
                Messages.Message("LD_Msg_NeedSilver".Translate(d.amount, CountSilver(map)),
                    MessageTypeDefOf.RejectInput, historical: false);
                return false;
            }

            debts.Remove(d);
            AdjustCredit(4f + Mathf.Min(6f, d.amount / 100f));
            Faction f = d.Faction;
            if (f != null)
            {
                f.TryAffectGoodwillWith(Faction.OfPlayer, 3);
            }

            Messages.Message("LD_Msg_Repaid".Translate(d.amount, creditScore.ToString("F0")), MessageTypeDefOf.PositiveEvent);
            return true;
        }

        public bool TryExtend(LedgerDebt d)
        {
            if (d == null || d.extendedOnce)
            {
                return false;
            }

            LedgerSettings s = LedgerMod.Settings ?? new LedgerSettings();
            d.extendedOnce = true;
            d.dueTick = Find.TickManager.TicksGame + s.DueTicks;
            AdjustCredit(-3f * s.Easy);
            SortDebtsByDue();
            Messages.Message("LD_Msg_Extended".Translate(d.amount, creditScore.ToString("F0")), MessageTypeDefOf.NeutralEvent);
            return true;
        }

        public void DefaultDebt(LedgerDebt d)
        {
            AdjustCredit(-12f - Mathf.Min(10f, d.amount / 80f));
            Faction f = d.Faction;
            int hit = Mathf.Clamp(Mathf.RoundToInt(8 + d.amount / 50f), 8, 25);
            if (f != null && !f.IsPlayer)
            {
                f.TryAffectGoodwillWith(Faction.OfPlayer, -hit, canSendMessage: true, canSendHostilityLetter: true);
            }

            LedgerSettings s = LedgerMod.Settings;
            bool party = s != null && s.allowCollectionIncident && !s.easyMode && Rand.Chance(0.35f);
            string body = "LD_Letter_DefaultBody".Translate(
                d.amount,
                d.factionLabel ?? "?",
                creditScore.ToString("F0"),
                hit).Resolve();
            if (party)
            {
                body += "\n\n" + "LD_Letter_DefaultCollectors".Translate().Resolve();
            }

            Find.LetterStack.ReceiveLetter(
                "LD_Letter_DefaultTitle".Translate(),
                body,
                LetterDefOf.NegativeEvent);

            if (party)
            {
                TrySpawnCollectors(f);
            }
        }

        private void TrySpawnCollectors(Faction f)
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map == null)
            {
                return;
            }

            try
            {
                IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail("RaidEnemy");
                if (def != null && f != null && f.HostileTo(Faction.OfPlayer))
                {
                    var parms = new IncidentParms
                    {
                        target = map,
                        faction = f,
                        points = Mathf.Clamp(StorytellerUtility.DefaultThreatPointsNow(map) * 0.25f, 100f, 400f)
                    };
                    def.Worker.TryExecute(parms);
                }
            }
            catch
            {
                // Letter already sent.
            }
        }

        public void RecordHonestTrade(int silverAbs, Faction other)
        {
            if (silverAbs < 80)
            {
                return;
            }

            lastHonestTradeTick = Find.TickManager.TicksGame;
            AdjustCredit(0.6f);
        }

        private void MaybeWeeklyLetter(int now)
        {
            LedgerSettings s = LedgerMod.Settings;
            bool force = s != null && s.forceWeeklyLetter;
            bool interesting = TotalDebt > 0 || creditScore < 40f || force;
            if (!interesting)
            {
                return;
            }

            if (lastStatusLetterTick > 0 && now - lastStatusLetterTick < GenDate.TicksPerDay * 7)
            {
                return;
            }

            // Align roughly to week boundaries without the old ultra-narrow modulo window.
            int day = now / GenDate.TicksPerDay;
            if (day % 7 != 0)
            {
                return;
            }

            // Only once near the start of that day.
            if (now % GenDate.TicksPerDay > 3000)
            {
                return;
            }

            lastStatusLetterTick = now;
            SendStatusLetter();
        }

        public void SendStatusLetter()
        {
            var sb = new StringBuilder();
            sb.AppendLine("LD_Letter_StatusIntro".Translate(
                creditScore.ToString("F0"),
                TotalDebt,
                AvailableCreditRoom()));
            if (debts != null)
            {
                SortDebtsByDue();
                for (int i = 0; i < debts.Count; i++)
                {
                    LedgerDebt d = debts[i];
                    int hours = Mathf.Max(0, (d.dueTick - Find.TickManager.TicksGame) / 2500);
                    sb.AppendLine("LD_Letter_StatusDebt".Translate(d.amount, d.factionLabel ?? "?", hours));
                }
            }

            lastStatusLetterTick = Find.TickManager.TicksGame;
            Find.LetterStack.ReceiveLetter(
                "LD_Letter_StatusTitle".Translate(),
                sb.ToString(),
                LetterDefOf.NeutralEvent);
        }

        public void SortDebtsByDue()
        {
            if (debts == null || debts.Count < 2)
            {
                return;
            }

            debts.Sort((a, b) => a.dueTick.CompareTo(b.dueTick));
        }

        public static int CountSilver(Map map)
        {
            if (map == null)
            {
                return 0;
            }

            // Same source as ConsumeSilver so UI and repay cannot desync.
            int sum = 0;
            List<Thing> list = map.listerThings.ThingsOfDef(ThingDefOf.Silver);
            for (int i = 0; i < list.Count; i++)
            {
                Thing t = list[i];
                if (t == null || t.Destroyed || t.stackCount <= 0)
                {
                    continue;
                }
                sum += t.stackCount;
            }

            return sum;
        }

        /// <summary>Destroy silver stacks until amount is paid. Returns false if short.</summary>
        public static bool ConsumeSilver(Map map, int amount)
        {
            if (map == null || amount <= 0)
            {
                return amount <= 0;
            }

            int left = amount;
            List<Thing> list = map.listerThings.ThingsOfDef(ThingDefOf.Silver).ToList();
            // Prefer unforbidden stacks first
            list.Sort((a, b) =>
            {
                bool fa = a.IsForbidden(Faction.OfPlayer);
                bool fb = b.IsForbidden(Faction.OfPlayer);
                if (fa != fb)
                {
                    return fa ? 1 : -1;
                }

                return b.stackCount.CompareTo(a.stackCount);
            });

            for (int i = 0; i < list.Count; i++)
            {
                if (left <= 0)
                {
                    break;
                }

                Thing t = list[i];
                if (t == null || t.Destroyed || t.stackCount <= 0)
                {
                    continue;
                }

                int take = Mathf.Min(left, t.stackCount);
                t.SplitOff(take).Destroy(DestroyMode.Vanish);
                left -= take;
            }

            return left <= 0;
        }

        /// <summary>Spawn silver in legal stacks (stackLimit chunks) near actor.</summary>
        public static bool SpawnSilverNear(Map map, int amount, Pawn actor)
        {
            if (map == null || amount <= 0)
            {
                return false;
            }

            IntVec3 cell = actor != null && actor.Spawned && actor.Map == map
                ? actor.Position
                : map.Center;
            if (!cell.IsValid || !cell.InBounds(map))
            {
                cell = map.Center;
            }

            int stackLimit = ThingDefOf.Silver.stackLimit;
            if (stackLimit <= 0)
            {
                stackLimit = 75;
            }

            int left = amount;
            int placed = 0;
            while (left > 0)
            {
                int chunk = Mathf.Min(left, stackLimit);
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = chunk;
                if (!GenPlace.TryPlaceThing(silver, cell, map, ThingPlaceMode.Near))
                {
                    // Best-effort: destroy any already-placed chunks on total failure of first chunk.
                    if (placed == 0)
                    {
                        if (!silver.Destroyed)
                        {
                            silver.Destroy(DestroyMode.Vanish);
                        }
                        return false;
                    }
                    // Partial place: still return true if some silver landed; caller already opened debt.
                    // Prefer not to leave zero silver after debt.
                    if (placed < amount / 2)
                    {
                        return false;
                    }
                    break;
                }
                placed += chunk;
                left -= chunk;
            }

            return placed > 0;
        }

        public static Faction PickCreditorFaction()
        {
            List<Faction> candidates = new List<Faction>();
            foreach (Faction f in Find.FactionManager.AllFactionsVisible)
            {
                if (f == null || f.IsPlayer || f.defeated || f.temporary)
                {
                    continue;
                }

                if (f.HostileTo(Faction.OfPlayer))
                {
                    continue;
                }

                if (f.def == null || f.def.hidden)
                {
                    continue;
                }

                // Humanlike trader-ish factions preferred
                if (f.def.humanlikeFaction || f.def.permanentEnemy == false)
                {
                    candidates.Add(f);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            // Best goodwill first, slight random among top.
            candidates.Sort((a, b) => b.PlayerGoodwill.CompareTo(a.PlayerGoodwill));
            int take = Mathf.Min(3, candidates.Count);
            return candidates[Rand.Range(0, take)];
        }

        public string MainButtonTip()
        {
            return "LD_TabTip".Translate(creditScore.ToString("F0"), TotalDebt, AvailableCreditRoom());
        }

        /// <summary>Suggested draw amounts for the float menu (filtered by room).</summary>
        public List<int> SuggestedDrawAmounts()
        {
            int room = AvailableCreditRoom();
            int[] presets = { 50, 100, 250, 500, 1000 };
            var list = new List<int>();
            for (int i = 0; i < presets.Length; i++)
            {
                if (presets[i] <= room)
                {
                    list.Add(presets[i]);
                }
            }

            if (room >= MinDrawAmount && (list.Count == 0 || list[list.Count - 1] != room))
            {
                // Max available (rounded down to 10s for readability if large)
                int max = room >= 100 ? (room / 10) * 10 : room;
                if (max >= MinDrawAmount && !list.Contains(max))
                {
                    list.Add(max);
                }
            }

            return list;
        }
    }
}
