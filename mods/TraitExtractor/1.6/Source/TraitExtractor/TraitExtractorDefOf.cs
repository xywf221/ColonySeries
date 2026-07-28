using RimWorld;
using Verse;

namespace TraitExtractor
{
    [DefOf]
    public static class TraitExtractorDefOf
    {
        public static ThingDef TE_TraitSerum;
        public static ThingDef TE_ExtractionKit;
        public static RecipeDef TE_ExtractTrait;
        public static HediffDef TE_NeuralTrauma;
        public static HediffDef TE_NeuroAdaptation;
        public static HediffDef TE_NeuralScar;
        public static HediffDef TE_ExtractionCooldown;
        public static ThoughtDef TE_HadTraitExtracted;
        public static ThoughtDef TE_WitnessedExtraction;
        public static ThoughtDef TE_InjectedTrait;

        static TraitExtractorDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(TraitExtractorDefOf));
        }
    }
}
