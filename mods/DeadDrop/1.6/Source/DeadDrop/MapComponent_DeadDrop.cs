using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace DeadDrop
{
    /// <summary>
    /// Per-map grey-market state. Rare ticks only; no AllCells scans.
    /// Max one live order per map.
    /// </summary>
    public class MapComponent_DeadDrop : MapComponent
    {
        public int reputation = 10;
        public DeadDropOrder activeOrder;
        public int nextOrderTick;
        public bool pendingService; // accepted and waiting for colonist job
        public int lastServiceTick;
        public int totalCompleted;
        public int totalExposed;

        private const int RareCheckInterval = 2500; // once per in-game hour

        public MapComponent_DeadDrop(Map map) : base(map)
        {
        }

        public static MapComponent_DeadDrop For(Map map)
        {
            return map?.GetComponent<MapComponent_DeadDrop>();
        }

        public bool HasLiveOrder => activeOrder != null && activeOrder.IsLive;

        public Building_DeadDrop FindCairn()
        {
            List<Thing> list = map.listerThings.ThingsOfDef(DeadDropDefOf.DD_DeadDropCairn);
            if (list == null || list.Count == 0)
            {
                return null;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Building_DeadDrop b && b.Spawned)
                {
                    return b;
                }
            }
            return null;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref reputation, "reputation", 10);
            Scribe_Values.Look(ref nextOrderTick, "nextOrderTick", 0);
            Scribe_Values.Look(ref pendingService, "pendingService", false);
            Scribe_Values.Look(ref lastServiceTick, "lastServiceTick", 0);
            Scribe_Values.Look(ref totalCompleted, "totalCompleted", 0);
            Scribe_Values.Look(ref totalExposed, "totalExposed", 0);
            Scribe_Deep.Look(ref activeOrder, "activeOrder");
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (nextOrderTick <= 0)
            {
                ScheduleNextOrder(initial: true);
            }
        }

        public override void MapComponentTick()
        {
            DeadDropSettings settings = DeadDropMod.Settings;
            if (settings == null || !settings.masterEnabled)
            {
                return;
            }

            // Rare pulse only.
            if (!map.IsHashIntervalTick(RareCheckInterval))
            {
                return;
            }

            int now = Find.TickManager.TicksGame;

            // Expire stale order.
            if (activeOrder != null && activeOrder.IsLive && now > activeOrder.expiryTick)
            {
                ExpireOrder(silent: false);
            }

            // Offer new order only if none live, cairn exists, research done.
            if (!HasLiveOrder && now >= nextOrderTick)
            {
                TryOfferNewOrder();
            }
        }

        public void ScheduleNextOrder(bool initial = false)
        {
            DeadDropSettings settings = DeadDropMod.Settings;
            int delay = settings != null
                ? settings.RollRefreshDelayTicks()
                : Mathf.RoundToInt(Rand.Range(3f, 7f) * GenDate.TicksPerDay);
            if (initial)
            {
                // First contact a bit sooner so mid-game unlock feels responsive.
                delay = Mathf.RoundToInt(delay * 0.55f);
            }
            nextOrderTick = Find.TickManager.TicksGame + delay;
        }

        public bool ResearchDone()
        {
            return DeadDropDefOf.DD_GreyChannels == null ||
                   DeadDropDefOf.DD_GreyChannels.IsFinished;
        }

        public void TryOfferNewOrder()
        {
            if (!ResearchDone())
            {
                ScheduleNextOrder();
                return;
            }

            Building_DeadDrop cairn = FindCairn();
            if (cairn == null)
            {
                // No drop marker yet — check again later, don't spam.
                nextOrderTick = Find.TickManager.TicksGame + GenDate.TicksPerDay;
                return;
            }

            if (HasLiveOrder)
            {
                return;
            }

            DeadDropSettings settings = DeadDropMod.Settings;
            activeOrder = DeadDropOrder.Generate(reputation, settings);
            pendingService = false;

            Find.LetterStack.ReceiveLetter(
                "DD_Letter_OrderTitle".Translate(activeOrder.codename),
                "DD_Letter_OrderBody".Translate(activeOrder.DescribeFull()),
                LetterDefOf.NeutralEvent,
                new TargetInfo(cairn));

            Messages.Message("DD_Message_OrderArrived".Translate(activeOrder.codename), cairn, MessageTypeDefOf.NeutralEvent);
        }

        public void AcceptOrder()
        {
            if (activeOrder == null || activeOrder.state != DeadDropOrderState.Offered)
            {
                return;
            }
            activeOrder.state = DeadDropOrderState.Accepted;
            pendingService = true;
            Messages.Message("DD_Message_OrderAccepted".Translate(activeOrder.codename), MessageTypeDefOf.TaskCompletion);
        }

        public void DeclineOrder()
        {
            if (activeOrder == null || !activeOrder.IsLive)
            {
                return;
            }
            string name = activeOrder.codename;
            activeOrder = null;
            pendingService = false;
            // Small rep ding for ghosting contacts, not harsh.
            AdjustReputation(-2);
            ScheduleNextOrder();
            Messages.Message("DD_Message_OrderDeclined".Translate(name), MessageTypeDefOf.NeutralEvent);
        }

        public void ExpireOrder(bool silent)
        {
            if (activeOrder == null)
            {
                return;
            }
            string name = activeOrder.codename;
            bool wasAccepted = activeOrder.state == DeadDropOrderState.Accepted;
            activeOrder = null;
            pendingService = false;
            if (wasAccepted)
            {
                AdjustReputation(-4);
            }
            ScheduleNextOrder();
            if (!silent)
            {
                Find.LetterStack.ReceiveLetter(
                    "DD_Letter_ExpiredTitle".Translate(),
                    "DD_Letter_ExpiredBody".Translate(name),
                    LetterDefOf.NeutralEvent);
            }
        }

        public void AdjustReputation(int delta)
        {
            reputation = Mathf.Clamp(reputation + delta, 0, 100);
        }

        /// <summary>
        /// Called by JobDriver when colonist finishes the drop exchange.
        /// </summary>
        public void CompleteService(Pawn actor, Building_DeadDrop cairn)
        {
            if (activeOrder == null || activeOrder.state != DeadDropOrderState.Accepted)
            {
                return;
            }

            DeadDropSettings settings = DeadDropMod.Settings;
            DeadDropOrder order = activeOrder;

            // Validate resources one last time.
            if (!CanFulfill(order, map, out string failReason))
            {
                Messages.Message(failReason, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            // Consume delivery goods / silver, spawn rewards.
            ExecuteTransfer(order, map, cairn.Position, actor);

            float discovery = order.DiscoveryChance(settings, actor);
            bool exposed = Rand.Chance(discovery);

            int repGain = order.kind == DeadDropOrderKind.Both ? 8 : 5;
            repGain += order.heat > 0.8f ? 2 : 0;

            string codename = order.codename;
            int silverNet = order.silverDelta;
            activeOrder = null;
            pendingService = false;
            lastServiceTick = Find.TickManager.TicksGame;
            totalCompleted++;

            if (exposed)
            {
                totalExposed++;
                AdjustReputation(-Mathf.Max(6, repGain));
                ResolveExposure(actor, cairn, settings, codename, discovery);
            }
            else
            {
                AdjustReputation(repGain);
                Find.LetterStack.ReceiveLetter(
                    "DD_Letter_SuccessTitle".Translate(codename),
                    "DD_Letter_SuccessBody".Translate(
                        actor.LabelShort,
                        codename,
                        DescribeOutcome(silverNet, order),
                        reputation,
                        discovery.ToStringPercent()),
                    LetterDefOf.PositiveEvent,
                    new TargetInfo(cairn));
            }

            ScheduleNextOrder();
        }

        private static string DescribeOutcome(int silverNet, DeadDropOrder order)
        {
            List<string> bits = new List<string>();
            if (silverNet > 0)
            {
                bits.Add("DD_Outcome_SilverIn".Translate(silverNet));
            }
            else if (silverNet < 0)
            {
                bits.Add("DD_Outcome_SilverOut".Translate(-silverNet));
            }
            if (order.NeedsRewardPickup && order.RewardDef != null)
            {
                bits.Add("DD_Outcome_GoodsIn".Translate(order.rewardCount, order.RewardDef.label));
            }
            if (order.NeedsGoodsDelivery && order.DeliverDef != null)
            {
                bits.Add("DD_Outcome_GoodsOut".Translate(order.deliverCount, order.DeliverDef.label));
            }
            return string.Join(", ", bits);
        }

        private void ResolveExposure(Pawn actor, Building_DeadDrop cairn, DeadDropSettings settings, string codename, float discovery)
        {
            int hit = settings != null ? settings.RollGoodwillHit() : Rand.RangeInclusive(15, 30);
            Faction victim = PickExposureFaction();
            string factionName = victim != null ? victim.Name : "DD_UnknownAuthorities".Translate().ToString();

            if (victim != null && victim != Faction.OfPlayer)
            {
                victim.TryAffectGoodwillWith(Faction.OfPlayer, -hit, canSendMessage: true, canSendHostilityLetter: true, HistoryEventDefOf.AttackedSettlement);
            }

            bool spawnParty = settings != null &&
                              settings.SearchPartyAllowed &&
                              Rand.Chance(settings.searchPartyChance);

            string body = "DD_Letter_ExposedBody".Translate(
                actor.LabelShort,
                codename,
                factionName,
                hit,
                reputation,
                discovery.ToStringPercent());

            if (spawnParty)
            {
                body += "\n\n" + "DD_Letter_ExposedSearch".Translate();
            }

            Find.LetterStack.ReceiveLetter(
                "DD_Letter_ExposedTitle".Translate(),
                body,
                LetterDefOf.NegativeEvent,
                new TargetInfo(cairn));

            if (spawnParty)
            {
                TryFireSearchParty(cairn, victim);
            }
        }

        private Faction PickExposureFaction()
        {
            List<Faction> candidates = Find.FactionManager.AllFactionsVisible
                .Where(f => !f.IsPlayer && !f.defeated && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer))
                .ToList();

            // Prefer Empire / civil outlanders if present.
            Faction empire = candidates.FirstOrDefault(f => f.def.defName == "Empire");
            if (empire != null && Rand.Chance(0.45f))
            {
                return empire;
            }

            Faction civil = candidates.FirstOrDefault(f => f.def.permanentEnemy == false && f.PlayerGoodwill >= -40);
            if (civil != null && Rand.Chance(0.6f))
            {
                return civil;
            }

            if (candidates.Count > 0)
            {
                return candidates.RandomElement();
            }

            // Fall back to any humanlike including hostile (still applies goodwill floor logic).
            return Find.FactionManager.AllFactionsVisible
                .Where(f => !f.IsPlayer && !f.defeated && f.def.humanlikeFaction)
                .InRandomOrder()
                .FirstOrDefault();
        }

        private void TryFireSearchParty(Building_DeadDrop cairn, Faction preferred)
        {
            if (DeadDropDefOf.DD_SearchParty == null)
            {
                SpawnSearchPartyManual(cairn, preferred);
                return;
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(DeadDropDefOf.DD_SearchParty.category, map);
            parms.faction = preferred;
            parms.target = map;
            parms.points = Mathf.Clamp(StorytellerUtility.DefaultThreatPointsNow(map) * 0.35f, 250f, 900f);
            if (!DeadDropDefOf.DD_SearchParty.Worker.TryExecute(parms))
            {
                SpawnSearchPartyManual(cairn, preferred);
            }
        }

        private void SpawnSearchPartyManual(Building_DeadDrop cairn, Faction preferred)
        {
            Faction faction = preferred;
            if (faction == null || faction.IsPlayer)
            {
                faction = Find.FactionManager.RandomEnemyFaction(allowHidden: false, allowDefeated: false, allowNonHumanlike: false);
            }
            if (faction == null)
            {
                return;
            }

            if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Neutral, out IntVec3 spawnCell))
            {
                if (!RCellFinder.TryFindRandomPawnEntryCell(out spawnCell, map, 0.5f))
                {
                    return;
                }
            }

            float points = Mathf.Clamp(StorytellerUtility.DefaultThreatPointsNow(map) * 0.30f, 200f, 700f);
            PawnGroupMakerParms groupParms = new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Combat,
                tile = map.Tile,
                faction = faction,
                points = points,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack
            };

            List<Pawn> pawns = null;
            try
            {
                pawns = PawnGroupMakerUtility.GeneratePawns(groupParms).ToList();
            }
            catch
            {
                pawns = null;
            }

            if (pawns == null || pawns.Count == 0)
            {
                // Last resort: 2–4 basic pawns via incident utility patterns.
                return;
            }

            // Cap party size — "search party", not colony wipe.
            while (pawns.Count > 5)
            {
                Pawn extra = pawns.Last();
                pawns.RemoveAt(pawns.Count - 1);
                extra.Destroy(DestroyMode.Vanish);
            }

            for (int i = 0; i < pawns.Count; i++)
            {
                IntVec3 cell = CellFinder.RandomClosewalkCellNear(spawnCell, map, 4);
                GenSpawn.Spawn(pawns[i], cell, map);
            }

            LordMaker.MakeNewLord(
                faction,
                new LordJob_AssaultColony(
                    faction,
                    canKidnap: false,
                    canTimeoutOrFlee: true,
                    sappers: false,
                    useAvoidGridSmart: true,
                    canSteal: false,
                    breachers: false),
                map,
                pawns);

            Find.LetterStack.ReceiveLetter(
                "DD_Letter_SearchPartyTitle".Translate(),
                "DD_Letter_SearchPartyBody".Translate(faction.Name, pawns.Count),
                LetterDefOf.ThreatSmall,
                new TargetInfo(spawnCell, map));
        }

        public static bool CanFulfill(DeadDropOrder order, Map map, out string reason)
        {
            reason = null;
            if (order == null)
            {
                reason = "DD_Fail_NoOrder".Translate();
                return false;
            }

            if (order.NeedsGoodsDelivery)
            {
                ThingDef def = order.DeliverDef;
                if (def == null)
                {
                    reason = "DD_Fail_MissingDef".Translate();
                    return false;
                }
                int have = CountThings(map, def);
                if (have < order.deliverCount)
                {
                    reason = "DD_Fail_NeedGoods".Translate(order.deliverCount, def.label, have);
                    return false;
                }
            }

            if (order.NeedsSilverPayment)
            {
                int have = CountThings(map, ThingDefOf.Silver);
                if (have < order.SilverToPay)
                {
                    reason = "DD_Fail_NeedSilver".Translate(order.SilverToPay, have);
                    return false;
                }
            }

            return true;
        }

        private static int CountThings(Map map, ThingDef def)
        {
            if (def == null) return 0;
            int total = 0;
            List<Thing> things = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; i++)
            {
                Thing t = things[i];
                if (t.IsForbidden(Faction.OfPlayer) || t.Position.Fogged(map))
                {
                    continue;
                }
                // Only player-reachable colony goods (not fogged ruins piles far away ideally —
                // still count owned stacks on map; WorkGiver will path-check).
                if (t.Faction != null && t.Faction != Faction.OfPlayer && t.def.CanHaveFaction)
                {
                    continue;
                }
                total += t.stackCount;
            }
            return total;
        }

        private static void ExecuteTransfer(DeadDropOrder order, Map map, IntVec3 dropCell, Pawn actor)
        {
            if (order.NeedsGoodsDelivery && order.DeliverDef != null)
            {
                ConsumeThings(map, order.DeliverDef, order.deliverCount, actor);
            }

            if (order.NeedsSilverPayment)
            {
                ConsumeThings(map, ThingDefOf.Silver, order.SilverToPay, actor);
            }

            if (order.SilverToReceive > 0)
            {
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = order.SilverToReceive;
                PlaceNear(silver, dropCell, map);
            }

            if (order.NeedsRewardPickup && order.RewardDef != null)
            {
                int left = order.rewardCount;
                while (left > 0)
                {
                    int take = Mathf.Min(left, order.RewardDef.stackLimit);
                    Thing thing = ThingMaker.MakeThing(order.RewardDef, GenStuff.DefaultStuffFor(order.RewardDef));
                    // Mild "tainted" feel: low quality if quality-comp exists.
                    CompQuality q = thing.TryGetComp<CompQuality>();
                    if (q != null)
                    {
                        q.SetQuality(QualityCategory.Normal, ArtGenerationContext.Outsider);
                    }
                    thing.stackCount = take;
                    // Slight hit point ding on gear.
                    if (thing.def.useHitPoints && (thing.def.IsWeapon || thing.def.IsApparel))
                    {
                        thing.HitPoints = Mathf.Max(1, Mathf.RoundToInt(thing.MaxHitPoints * Rand.Range(0.55f, 0.85f)));
                    }
                    PlaceNear(thing, dropCell, map);
                    left -= take;
                }
            }
        }

        private static void PlaceNear(Thing thing, IntVec3 cell, Map map)
        {
            GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
        }

        private static void ConsumeThings(Map map, ThingDef def, int count, Pawn preferNear)
        {
            int need = count;
            List<Thing> things = map.listerThings.ThingsOfDef(def)
                .OrderBy(t => preferNear != null ? t.Position.DistanceToSquared(preferNear.Position) : 0)
                .ToList();

            for (int i = 0; i < things.Count && need > 0; i++)
            {
                Thing t = things[i];
                if (t.Destroyed || t.IsForbidden(Faction.OfPlayer))
                {
                    continue;
                }
                int take = Mathf.Min(need, t.stackCount);
                Thing split = t.SplitOff(take);
                split.Destroy(DestroyMode.Vanish);
                need -= take;
            }
        }

        public string InspectExtra()
        {
            string repLine = "DD_Inspect_Rep".Translate(reputation);
            if (!HasLiveOrder)
            {
                int hours = Mathf.Max(0, (nextOrderTick - Find.TickManager.TicksGame) / 2500);
                return repLine + "\n" + "DD_Inspect_NoOrder".Translate(hours) +
                       "\n" + "DD_Inspect_Stats".Translate(totalCompleted, totalExposed);
            }

            string state = activeOrder.state == DeadDropOrderState.Accepted
                ? "DD_Inspect_Accepted".Translate()
                : "DD_Inspect_Offered".Translate();
            return repLine + "\n" + state + "\n" + activeOrder.DescribeFull() +
                   "\n" + "DD_Inspect_Stats".Translate(totalCompleted, totalExposed);
        }
    }
}
