using RimWorld;
using Verse;

namespace Landworks
{
    [DefOf]
    public static class LandworksDefOf
    {
        public static ThingDef LW_SoilFill;
        public static ThingDef LW_Compost;
        public static ThingDef LW_SoilStabilizer;

        public static TerrainDef LW_ReclaimedSoil;
        public static TerrainDef LW_AmendedSoil;
        public static TerrainDef LW_TilledSoil;
        public static TerrainDef LW_ExhaustedSoil;
        public static TerrainDef LW_DrainedMarsh;
        public static TerrainDef LW_EmbankmentFill;
        public static TerrainDef LW_HeavyEmbankment;
        public static TerrainDef LW_ExcavationScar;

        public static DesignationDef LW_Excavate;
        public static JobDef LW_ExcavateSoil;
        public static ThoughtDef LW_FilthyEarthworks;

        public static IncidentDef LW_GeologicalBacklash;
        public static IncidentDef LW_SoilCollapse;

        public static ResearchProjectDef LW_BasicEarthworks;

        static LandworksDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(LandworksDefOf));
        }
    }
}
