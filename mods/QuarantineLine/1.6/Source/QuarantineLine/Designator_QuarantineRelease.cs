using RimWorld;
using UnityEngine;
using Verse;

namespace QuarantineLine
{
    public class Designator_QuarantineRelease : Designator
    {
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;
        public override bool DragDrawMeasurements => true;

        public Designator_QuarantineRelease()
        {
            defaultLabel = "QL_Designator_Release_Label".Translate();
            defaultDesc = "QL_Designator_Release_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/QuarantineRelease", true);
            useMouseIcon = true;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_PlanRemove;
            hotKey = KeyBindingDefOf.Misc6;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!QuarantineUtility.Enabled)
            {
                return "QL_Disabled".Translate();
            }
            if (!c.InBounds(Map) || c.Fogged(Map))
            {
                return false;
            }
            MapComponent_Quarantine comp = MapComponent_Quarantine.For(Map);
            if (comp == null || !comp.IsQuarantineCell(c))
            {
                return "QL_NotMarked".Translate();
            }
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            MapComponent_Quarantine.For(Map)?.ReleaseCell(c);
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }
    }
}
