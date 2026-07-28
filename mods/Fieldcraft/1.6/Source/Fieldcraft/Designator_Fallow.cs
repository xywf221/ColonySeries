using RimWorld;
using UnityEngine;
using Verse;

namespace Fieldcraft
{
    public class Designator_Fallow : Designator
    {
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;
        public override bool DragDrawMeasurements => true;

        public Designator_Fallow()
        {
            defaultLabel = "FC_Designator_Fallow_Label".Translate();
            defaultDesc = "FC_Designator_Fallow_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Fallow", true);
            useMouseIcon = true;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_Claim;
            hotKey = KeyBindingDefOf.Misc8;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map) || c.Fogged(Map))
            {
                return false;
            }
            if (FieldcraftDefOf.FC_CropRotation != null
                && !FieldcraftDefOf.FC_CropRotation.IsFinished
                && !DebugSettings.godMode)
            {
                return "FC_NeedCropRotation".Translate();
            }
            TerrainDef terrain = Map.terrainGrid.TerrainAt(c);
            bool trackNatural = FieldcraftMod.Settings == null || FieldcraftMod.Settings.trackNaturalSoil;
            if (!LandworksBridge.IsBudgetEligible(terrain, trackNatural))
            {
                return "FC_CannotFallowTerrain".Translate(terrain != null ? terrain.label : "???");
            }
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            // Toggle: second pass clears fallow. No persistent designation job needed.
            MapComponent_Fieldcraft.For(Map)?.ToggleFallow(c);
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
            MapComponent_Fieldcraft comp = MapComponent_Fieldcraft.For(Map);
            if (comp == null)
            {
                return;
            }
            // Draw simple highlight on fallow cells near mouse — skip full map.
        }
    }
}
