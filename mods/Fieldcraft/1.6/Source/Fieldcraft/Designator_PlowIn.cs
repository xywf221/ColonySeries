using RimWorld;
using UnityEngine;
using Verse;

namespace Fieldcraft
{
    public class Designator_PlowIn : Designator
    {
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;
        public override bool DragDrawMeasurements => true;

        public Designator_PlowIn()
        {
            defaultLabel = "FC_Designator_PlowIn_Label".Translate();
            defaultDesc = "FC_Designator_PlowIn_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/PlowIn", true);
            useMouseIcon = true;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_Claim;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map) || c.Fogged(Map))
            {
                return false;
            }
            if (Map.designationManager.DesignationAt(c, FieldcraftDefOf.FC_PlowIn) != null)
            {
                return false;
            }
            if (FieldcraftDefOf.FC_GreenManure != null
                && !FieldcraftDefOf.FC_GreenManure.IsFinished
                && !DebugSettings.godMode)
            {
                return "FC_NeedGreenManure".Translate();
            }
            Plant plant = c.GetPlant(Map);
            if (plant == null)
            {
                return "FC_NoPlantToPlow".Translate();
            }
            if (plant.def.plant != null && plant.def.plant.IsTree)
            {
                return "FC_CannotPlowTree".Translate();
            }
            if (plant.Growth < 0.35f)
            {
                return "FC_PlantTooYoung".Translate();
            }
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            Map.designationManager.AddDesignation(new Designation(c, FieldcraftDefOf.FC_PlowIn));
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }
    }
}
