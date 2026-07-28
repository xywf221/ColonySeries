using RimWorld;
using Verse;

namespace SalvageAtlas
{
    [DefOf]
    public static class SalvageAtlasDefOf
    {
        public static ThingDef SA_MechFragment;
        public static ThingDef SA_SalvageBench;
        public static ThingDef SA_AtlasSchematic;

        public static DesignationDef SA_SalvageMech;
        public static DesignationDef SA_TeardownWreck;

        public static JobDef SA_SalvageMechCorpse;
        public static JobDef SA_AnalyzeFragments;
        public static JobDef SA_TeardownWreckJob;

        public static ResearchProjectDef SA_SalvageBasics;

        public static HediffDef SA_FieldPatch_Armor;
        public static HediffDef SA_FieldPatch_Optics;
        public static HediffDef SA_FieldPatch_Coolant;

        static SalvageAtlasDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SalvageAtlasDefOf));
        }
    }
}
