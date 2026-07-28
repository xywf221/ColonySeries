using RimWorld;
using UnityEngine;
using Verse;

namespace Fieldcraft
{
    public class Designator_Topdress : Designator
    {
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;

        public override bool DragDrawMeasurements => true;

        public Designator_Topdress()
        {
            defaultLabel = "FC_Designator_Topdress_Label".Translate();
            defaultDesc = "FC_Designator_Topdress_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Topdress", true);
            useMouseIcon = true;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_Claim;
            hotKey = KeyBindingDefOf.Misc7;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map) || c.Fogged(Map))
            {
                return false;
            }
            if (Map.designationManager.DesignationAt(c, FieldcraftDefOf.FC_Topdress) != null)
            {
                return false;
            }
            if (FieldcraftDefOf.FC_SoilBudgeting != null
                && !FieldcraftDefOf.FC_SoilBudgeting.IsFinished
                && !DebugSettings.godMode)
            {
                return "FC_NeedSoilBudgeting".Translate();
            }
            TerrainDef terrain = Map.terrainGrid.TerrainAt(c);
            bool trackNatural = FieldcraftMod.Settings == null || FieldcraftMod.Settings.trackNaturalSoil;
            if (!LandworksBridge.IsBudgetEligible(terrain, trackNatural)
                && terrain != LandworksBridge.ExhaustedTerrain)
            {
                return "FC_CannotTopdressTerrain".Translate(terrain != null ? terrain.label : "???");
            }
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            Map.designationManager.AddDesignation(new Designation(c, FieldcraftDefOf.FC_Topdress));
            MapComponent_Fieldcraft.For(Map)?.EnsureTracked(c);
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }
    }
}
