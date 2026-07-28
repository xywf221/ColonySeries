using RimWorld;
using UnityEngine;
using Verse;

namespace QuarantineLine
{
    /// <summary>
    /// Click-anywhere designator that toggles map-wide strict isolation protocol.
    /// Not a cell painter — one click flips policy.
    /// </summary>
    public class Designator_IsolationPolicy : Designator
    {
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;

        public Designator_IsolationPolicy()
        {
            defaultLabel = "QL_Designator_Isolation_Label".Translate();
            defaultDesc = "QL_Designator_Isolation_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/IsolationPolicy", true);
            useMouseIcon = true;
            soundSucceeded = SoundDefOf.Click;
            hotKey = KeyBindingDefOf.Misc7;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!QuarantineUtility.Enabled)
            {
                return "QL_Disabled".Translate();
            }
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            MapComponent_Quarantine.For(Map)?.ToggleStrictIsolation();
        }

        public override void SelectedProcessInput(Event ev)
        {
            // Allow single click without drag requirement.
            base.SelectedProcessInput(ev);
        }

        public override void ProcessInput(Event ev)
        {
            // Immediate toggle when tool selected and confirmed — also support right-click cancel via base.
            base.ProcessInput(ev);
        }

        public override void SelectedUpdate()
        {
            MapComponent_Quarantine comp = MapComponent_Quarantine.For(Map);
            if (comp == null)
            {
                return;
            }
            // Status in mouse tooltip area via messages on toggle; draw bracket.
            GenUI.RenderMouseoverBracket();
        }
    }
}
