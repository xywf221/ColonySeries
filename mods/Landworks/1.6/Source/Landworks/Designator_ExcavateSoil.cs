using RimWorld;
using UnityEngine;
using Verse;

namespace Landworks
{
    public class Designator_ExcavateSoil : Designator
    {
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;

        public override bool DragDrawMeasurements => true;

        public Designator_ExcavateSoil()
        {
            defaultLabel = "LW_Designator_Excavate_Label".Translate();
            defaultDesc = "LW_Designator_Excavate_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Excavate", true);
            useMouseIcon = true;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_Mine;
            hotKey = KeyBindingDefOf.Misc6;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map) || c.Fogged(Map))
            {
                return false;
            }
            if (Map.designationManager.DesignationAt(c, LandworksDefOf.LW_Excavate) != null)
            {
                return false;
            }
            if (c.GetEdifice(Map) != null)
            {
                return "LW_CannotExcavateBuilding".Translate();
            }
            TerrainDef terrain = Map.terrainGrid.TerrainAt(c);
            if (!TerrainUtility.CanExcavate(terrain))
            {
                return "LW_CannotExcavateTerrain".Translate(terrain != null ? terrain.label : "???");
            }
            if (LandworksDefOf.LW_BasicEarthworks != null && !LandworksDefOf.LW_BasicEarthworks.IsFinished && !DebugSettings.godMode)
            {
                return "LW_NeedBasicEarthworks".Translate();
            }
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            Map.designationManager.AddDesignation(new Designation(c, LandworksDefOf.LW_Excavate));
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }
    }
}
