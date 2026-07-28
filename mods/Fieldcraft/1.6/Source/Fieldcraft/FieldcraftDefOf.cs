using RimWorld;
using Verse;

namespace Fieldcraft
{
    [DefOf]
    public static class FieldcraftDefOf
    {
        public static ThingDef FC_Compost;
        public static ThingDef FC_CompostPit;
        public static ThingDef Plant_FC_GreenManure;

        public static TerrainDef FC_PaddyDry;
        public static TerrainDef FC_PaddyFlooded;

        public static DesignationDef FC_Topdress;
        public static DesignationDef FC_Fallow;
        public static DesignationDef FC_Irrigate;
        public static DesignationDef FC_Drain;
        public static DesignationDef FC_PlowIn;

        public static JobDef FC_TopdressSoil;
        public static JobDef FC_IrrigatePaddy;
        public static JobDef FC_DrainPaddy;
        public static JobDef FC_PlowInPlant;

        public static ResearchProjectDef FC_SoilBudgeting;
        public static ResearchProjectDef FC_CompostWorks;
        public static ResearchProjectDef FC_CropRotation;
        public static ResearchProjectDef FC_PaddyWorks;
        public static ResearchProjectDef FC_GreenManure;

        static FieldcraftDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FieldcraftDefOf));
        }
    }
}
