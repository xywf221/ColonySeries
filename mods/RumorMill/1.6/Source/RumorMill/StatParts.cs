using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RumorMill
{
    /// <summary>NegotiationAbility offset from the pawn's own rumor tags (recruit/diplomacy face).</summary>
    public class StatPart_RumorNegotiation : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!RumorUtility.Enabled || !req.HasThing)
            {
                return;
            }
            Pawn pawn = req.Thing as Pawn;
            if (pawn == null)
            {
                return;
            }
            float offset = RumorUtility.NegotiationOffset(pawn);
            if (offset != 0f)
            {
                val += offset;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!RumorUtility.Enabled || !req.HasThing)
            {
                return null;
            }
            Pawn pawn = req.Thing as Pawn;
            if (pawn == null)
            {
                return null;
            }
            float offset = RumorUtility.NegotiationOffset(pawn);
            if (offset == 0f)
            {
                return null;
            }
            return "RM_Stat_Negotiation".Translate(offset.ToStringPercentSigned());
        }
    }

    public class StatPart_RumorTradePrice : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!RumorUtility.Enabled || !req.HasThing)
            {
                return;
            }
            Pawn pawn = req.Thing as Pawn;
            if (pawn == null)
            {
                return;
            }
            float offset = RumorUtility.TradePriceOffset(pawn);
            if (offset != 0f)
            {
                val += offset;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!RumorUtility.Enabled || !req.HasThing)
            {
                return null;
            }
            Pawn pawn = req.Thing as Pawn;
            if (pawn == null)
            {
                return null;
            }
            float offset = RumorUtility.TradePriceOffset(pawn);
            if (offset == 0f)
            {
                return null;
            }
            return "RM_Stat_Trade".Translate(offset.ToStringPercentSigned());
        }
    }

    /// <summary>Inject StatParts at runtime so we do not fight XML load order on core stats.</summary>
    public static class StatPartsInjector
    {
        private static bool injected;

        public static void EnsureInjected()
        {
            if (injected)
            {
                return;
            }
            injected = true;
            TryAdd(StatDefOf.NegotiationAbility, new StatPart_RumorNegotiation());
            TryAdd(StatDefOf.TradePriceImprovement, new StatPart_RumorTradePrice());
        }

        private static void TryAdd(StatDef stat, StatPart part)
        {
            if (stat == null || part == null)
            {
                return;
            }
            if (stat.parts == null)
            {
                stat.parts = new List<StatPart>();
            }
            for (int i = 0; i < stat.parts.Count; i++)
            {
                if (stat.parts[i] != null && stat.parts[i].GetType() == part.GetType())
                {
                    return;
                }
            }
            part.parentStat = stat;
            stat.parts.Add(part);
        }
    }
}
