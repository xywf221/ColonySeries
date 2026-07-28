using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace TraitExtractor
{
    /// <summary>
    /// Generates pre-filled trait serums for traders (random personality patterns, weighted by commonality).
    /// </summary>
    public class StockGenerator_TraitSerum : StockGenerator
    {
        private struct TraitOffer
        {
            public TraitDef def;
            public int degree;
            public float weight;
        }

        public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
        {
            int count = countRange.RandomInRange;
            for (int i = 0; i < count; i++)
            {
                Thing serum = TryMakeRandomSerum();
                if (serum != null)
                {
                    yield return serum;
                }
            }
        }

        public override bool HandlesThingDef(ThingDef thingDef)
        {
            return thingDef == TraitExtractorDefOf.TE_TraitSerum;
        }

        public override Tradeability TradeabilityFor(ThingDef thingDef)
        {
            if (thingDef == TraitExtractorDefOf.TE_TraitSerum)
            {
                return Tradeability.All;
            }
            return Tradeability.None;
        }

        private static Thing TryMakeRandomSerum()
        {
            if (TraitExtractorDefOf.TE_TraitSerum == null)
            {
                return null;
            }

            List<TraitOffer> offers = new List<TraitOffer>();
            foreach (TraitDef def in DefDatabase<TraitDef>.AllDefsListForReading)
            {
                if (def?.degreeDatas == null || def.degreeDatas.Count == 0)
                {
                    continue;
                }
                foreach (TraitDegreeData data in def.degreeDatas)
                {
                    // Prefer degree commonality; fall back to gender-averaged def commonality.
                    float c = data.commonality;
                    if (c <= 0f)
                    {
                        c = (def.GetGenderSpecificCommonality(Gender.Male)
                             + def.GetGenderSpecificCommonality(Gender.Female)) * 0.5f;
                    }
                    if (c <= 0f)
                    {
                        c = 0.05f;
                    }
                    TraitOffer offer;
                    offer.def = def;
                    offer.degree = data.degree;
                    offer.weight = c;
                    offers.Add(offer);
                }
            }

            if (offers.Count == 0)
            {
                return null;
            }

            TraitOffer chosen = offers.RandomElementByWeight(o => o.weight);
            return TraitUtility.MakeSerum(chosen.def, chosen.degree, "TE_SerumSource_Market".Translate());
        }
    }
}
