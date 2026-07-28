using RimWorld;
using UnityEngine;
using Verse;

namespace QuarantineLine
{
    public class Designator_QuarantineMark : Designator
    {
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;
        public override bool DragDrawMeasurements => true;

        public Designator_QuarantineMark()
        {
            defaultLabel = "QL_Designator_Mark_Label".Translate();
            defaultDesc = "QL_Designator_Mark_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/QuarantineMark", true);
            useMouseIcon = true;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_PlanAdd;
            hotKey = KeyBindingDefOf.Misc5;
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
            if (!c.Standable(Map) && c.GetEdifice(Map) == null)
            {
                // Allow floors/rooms; reject pure void / deep water etc. if not standable and empty.
                TerrainDef t = c.GetTerrain(Map);
                if (t == null || t.passability == Traversability.Impassable)
                {
                    return false;
                }
            }
            MapComponent_Quarantine comp = MapComponent_Quarantine.For(Map);
            if (comp != null && comp.IsQuarantineCell(c))
            {
                return "QL_AlreadyMarked".Translate();
            }
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            MapComponent_Quarantine.For(Map)?.MarkCell(c);
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
            MapComponent_Quarantine comp = MapComponent_Quarantine.For(Map);
            if (comp == null)
            {
                return;
            }
            // Light highlight of existing ward cells near camera is skipped — designations draw themselves.
        }
    }
}
