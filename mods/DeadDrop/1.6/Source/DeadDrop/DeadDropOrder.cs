using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeadDrop
{
    public enum DeadDropOrderKind : byte
    {
        Buy = 0,
        Sell = 1,
        Both = 2
    }

    public enum DeadDropOrderState : byte
    {
        None = 0,
        Offered = 1,
        Accepted = 2
    }

    /// <summary>
    /// One grey-market order. At most one active (Offered/Accepted) per map.
    /// </summary>
    public class DeadDropOrder : IExposable
    {
        public DeadDropOrderKind kind = DeadDropOrderKind.Sell;
        public DeadDropOrderState state = DeadDropOrderState.None;

        public string deliverDefName;
        public int deliverCount;
        public string rewardDefName;
        public int rewardCount;

        /// <summary>Positive = player receives silver. Negative = player pays silver.</summary>
        public int silverDelta;

        public float heat = 0.35f;
        public int expiryTick;
        public string codename = "Grey";
        public int marketValueEstimate;

        public ThingDef DeliverDef =>
            string.IsNullOrEmpty(deliverDefName) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(deliverDefName);

        public ThingDef RewardDef =>
            string.IsNullOrEmpty(rewardDefName) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(rewardDefName);

        public bool NeedsGoodsDelivery =>
            (kind == DeadDropOrderKind.Sell || kind == DeadDropOrderKind.Both) &&
            DeliverDef != null && deliverCount > 0;

        public bool NeedsSilverPayment => silverDelta < 0;

        public bool NeedsRewardPickup =>
            (kind == DeadDropOrderKind.Buy || kind == DeadDropOrderKind.Both) &&
            RewardDef != null && rewardCount > 0;

        public int SilverToPay => silverDelta < 0 ? -silverDelta : 0;
        public int SilverToReceive => silverDelta > 0 ? silverDelta : 0;

        public bool IsLive => state == DeadDropOrderState.Offered || state == DeadDropOrderState.Accepted;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind", DeadDropOrderKind.Sell);
            Scribe_Values.Look(ref state, "state", DeadDropOrderState.None);
            Scribe_Values.Look(ref deliverDefName, "deliverDefName");
            Scribe_Values.Look(ref deliverCount, "deliverCount", 0);
            Scribe_Values.Look(ref rewardDefName, "rewardDefName");
            Scribe_Values.Look(ref rewardCount, "rewardCount", 0);
            Scribe_Values.Look(ref silverDelta, "silverDelta", 0);
            Scribe_Values.Look(ref heat, "heat", 0.35f);
            Scribe_Values.Look(ref expiryTick, "expiryTick", 0);
            Scribe_Values.Look(ref codename, "codename", "Grey");
            Scribe_Values.Look(ref marketValueEstimate, "marketValueEstimate", 0);
        }

        public string SummaryLabel()
        {
            switch (kind)
            {
                case DeadDropOrderKind.Buy:
                    return "DD_OrderKind_Buy".Translate();
                case DeadDropOrderKind.Sell:
                    return "DD_OrderKind_Sell".Translate();
                default:
                    return "DD_OrderKind_Both".Translate();
            }
        }

        public string DescribeFull()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("DD_Order_Codename".Translate(codename));
            sb.AppendLine("DD_Order_KindLine".Translate(SummaryLabel()));
            sb.AppendLine("DD_Order_Heat".Translate(heat.ToStringPercent()));

            if (NeedsGoodsDelivery)
            {
                sb.AppendLine("DD_Order_Deliver".Translate(deliverCount, DeliverDef?.label ?? deliverDefName));
            }
            if (NeedsSilverPayment)
            {
                sb.AppendLine("DD_Order_PaySilver".Translate(SilverToPay));
            }
            if (NeedsRewardPickup)
            {
                sb.AppendLine("DD_Order_ReceiveGoods".Translate(rewardCount, RewardDef?.label ?? rewardDefName));
            }
            if (SilverToReceive > 0)
            {
                sb.AppendLine("DD_Order_ReceiveSilver".Translate(SilverToReceive));
            }

            int hoursLeft = Mathf.Max(0, (expiryTick - Find.TickManager.TicksGame) / 2500);
            sb.AppendLine("DD_Order_Expires".Translate(hoursLeft));
            return sb.ToString().TrimEnd();
        }

        public float DiscoveryChance(DeadDropSettings settings, Pawn actor)
        {
            float chance = (settings?.baseDiscoveryChance ?? 0.22f) * heat;
            chance *= settings != null ? settings.DiscoveryMult : 1f;

            // Honest / kind pawns are worse smugglers.
            if (actor?.story?.traits != null && actor.story.traits.HasTrait(TraitDefOf.Kind))
            {
                chance *= 1.35f;
            }

            // Night is safer.
            if (actor?.Map != null)
            {
                float glow = actor.Map.glowGrid.GroundGlowAt(actor.Position);
                if (glow < 0.3f)
                {
                    chance *= 0.75f;
                }
            }

            return Mathf.Clamp01(chance);
        }

        public static DeadDropOrder Generate(int reputation, DeadDropSettings settings)
        {
            DeadDropOrder order = new DeadDropOrder();
            order.codename = RandomCodename();
            order.state = DeadDropOrderState.Offered;
            order.expiryTick = Find.TickManager.TicksGame + Mathf.RoundToInt(Rand.Range(2.5f, 4.5f) * GenDate.TicksPerDay);

            float profit = settings != null ? settings.RollProfitMult() : Rand.Range(1.3f, 2.0f);
            int rep = Mathf.Clamp(reputation, 0, 100);

            // Weight kind by reputation: higher rep → more buy/both and hotter goods.
            float roll = Rand.Value;
            if (rep < 25)
            {
                order.kind = roll < 0.65f ? DeadDropOrderKind.Sell : DeadDropOrderKind.Buy;
            }
            else if (rep < 60)
            {
                if (roll < 0.45f) order.kind = DeadDropOrderKind.Sell;
                else if (roll < 0.80f) order.kind = DeadDropOrderKind.Buy;
                else order.kind = DeadDropOrderKind.Both;
            }
            else
            {
                if (roll < 0.30f) order.kind = DeadDropOrderKind.Sell;
                else if (roll < 0.65f) order.kind = DeadDropOrderKind.Buy;
                else order.kind = DeadDropOrderKind.Both;
            }

            List<ThingDef> sellPool = BuildSellPool(rep);
            List<ThingDef> buyPool = BuildBuyPool(rep);

            if (order.kind == DeadDropOrderKind.Sell || order.kind == DeadDropOrderKind.Both)
            {
                ThingDef goods = sellPool.RandomElement();
                order.deliverDefName = goods.defName;
                float unit = Mathf.Max(1f, goods.BaseMarketValue);
                // Target roughly 150–900 silver worth depending on rep.
                float targetValue = Mathf.Lerp(120f, 900f, rep / 100f) * Rand.Range(0.75f, 1.25f);
                order.deliverCount = Mathf.Clamp(Mathf.RoundToInt(targetValue / unit), 1, Mathf.Min(goods.stackLimit, 50));
                int baseWorth = Mathf.RoundToInt(unit * order.deliverCount);
                order.silverDelta = Mathf.Max(10, Mathf.RoundToInt(baseWorth * profit));
                order.heat = Mathf.Clamp(0.25f + unit / 400f + rep * 0.002f, 0.2f, 1.1f);
                order.marketValueEstimate = baseWorth;
            }

            if (order.kind == DeadDropOrderKind.Buy || order.kind == DeadDropOrderKind.Both)
            {
                ThingDef goods = buyPool.RandomElement();
                order.rewardDefName = goods.defName;
                float unit = Mathf.Max(1f, goods.BaseMarketValue);
                float targetValue = Mathf.Lerp(100f, 800f, rep / 100f) * Rand.Range(0.7f, 1.2f);
                order.rewardCount = Mathf.Clamp(Mathf.RoundToInt(targetValue / unit), 1, Mathf.Min(goods.stackLimit, 40));
                int baseWorth = Mathf.RoundToInt(unit * order.rewardCount);
                int pay = Mathf.Max(8, Mathf.RoundToInt(baseWorth / profit));
                if (order.kind == DeadDropOrderKind.Buy)
                {
                    order.silverDelta = -pay;
                    order.heat = Mathf.Clamp(0.30f + unit / 350f + rep * 0.0025f, 0.25f, 1.2f);
                    order.marketValueEstimate = baseWorth;
                }
                else
                {
                    // Both: net silver after paying for reward on top of sell payout.
                    order.silverDelta -= pay;
                    order.heat = Mathf.Clamp(order.heat + 0.15f + unit / 500f, 0.3f, 1.35f);
                    order.marketValueEstimate += baseWorth;
                }
            }

            // Sanity: if defs failed somehow, force a simple yayo sell.
            if (order.NeedsGoodsDelivery && order.DeliverDef == null)
            {
                order.deliverDefName = ThingDefOf.MedicineIndustrial.defName;
                order.deliverCount = 5;
                order.silverDelta = Mathf.Max(order.silverDelta, 80);
            }
            if (order.NeedsRewardPickup && order.RewardDef == null)
            {
                order.rewardDefName = ThingDefOf.MedicineIndustrial.defName;
                order.rewardCount = 3;
                if (order.silverDelta >= 0)
                {
                    order.silverDelta = -60;
                }
            }

            return order;
        }

        private static List<ThingDef> BuildSellPool(int rep)
        {
            // Player delivers these (often contraband-ish).
            List<ThingDef> pool = new List<ThingDef>();
            void Add(string name, int minRep = 0)
            {
                if (rep < minRep) return;
                ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (d != null) pool.Add(d);
            }

            Add("Yayo");
            Add("Flake");
            Add("SmokeleafJoint");
            Add("Beer", 0);
            Add("MedicineIndustrial", 10);
            Add("ComponentIndustrial", 15);
            Add("GoJuice", 20);
            Add("WakeUp", 20);
            Add("Luciferium", 45);
            Add("MedicineUltratech", 50);
            Add("ComponentSpacer", 55);
            Add("AIPersonaCore", 80);

            // Organs if present
            Add("Heart", 30);
            Add("Lung", 25);
            Add("Kidney", 25);
            Add("Liver", 30);

            if (TraitSerumBridge.Available && rep >= 40)
            {
                pool.Add(TraitSerumBridge.SerumDef);
            }

            if (pool.Count == 0)
            {
                pool.Add(ThingDefOf.Silver);
            }
            return pool;
        }

        private static List<ThingDef> BuildBuyPool(int rep)
        {
            // Player receives these from the drop (tainted/contraband flavour).
            List<ThingDef> pool = new List<ThingDef>();
            void Add(string name, int minRep = 0)
            {
                if (rep < minRep) return;
                ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(name);
                if (d != null) pool.Add(d);
            }

            Add("Yayo");
            Add("Flake");
            Add("Gun_Revolver", 0);
            Add("Gun_Autopistol", 10);
            Add("MedicineIndustrial", 5);
            Add("ComponentIndustrial", 15);
            Add("Neutroamine", 20);
            Add("GoJuice", 25);
            Add("Apparel_FlakVest", 30);
            Add("Gun_AssaultRifle", 40);
            Add("Luciferium", 50);
            Add("MedicineUltratech", 55);
            Add("ComponentSpacer", 60);
            Add("Gun_SniperRifle", 65);

            if (TraitSerumBridge.Available && rep >= 55)
            {
                pool.Add(TraitSerumBridge.SerumDef);
            }

            // Fallbacks for organ-ish intel substitutes when no TE
            if (!TraitSerumBridge.Available)
            {
                Add("Heart", 35);
                Add("Lung", 30);
            }

            if (pool.Count == 0)
            {
                pool.Add(ThingDefOf.MedicineIndustrial);
            }
            return pool;
        }

        private static string RandomCodename()
        {
            string[] a =
            {
                "Ash", "Cinder", "Needle", "Moth", "Rust", "Vesper", "Hollow", "Quarry",
                "Silk", "Crow", "Brine", "Lantern", "Hitch", "Splinter", "Cobalt", "Dusk"
            };
            string[] b =
            {
                "Relay", "Parcel", "Whisper", "Ledger", "Cairn", "Cache", "Signal", "Fold",
                "Route", "Blind", "Mark", "Drop", "Vein", "Pact", "Shade", "Gate"
            };
            return a.RandomElement() + " " + b.RandomElement();
        }
    }
}
