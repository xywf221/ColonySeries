using RimWorld;
using UnityEngine;
using Verse;

namespace SalvageAtlas
{
    /// <summary>
    /// Player verb: mark mech corpses / wrecks for fragment salvage.
    /// </summary>
    public class Designator_SalvageMech : Designator
    {
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;
        public override bool DragDrawMeasurements => true;
        protected override DesignationDef Designation => SalvageAtlasDefOf.SA_SalvageMech;

        public Designator_SalvageMech()
        {
            defaultLabel = "SA_Designator_Salvage_Label".Translate();
            defaultDesc = "SA_Designator_Salvage_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/SalvageMech", true);
            useMouseIcon = true;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_Claim;
            hotKey = KeyBindingDefOf.Misc9;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            if (!c.InBounds(Map) || c.Fogged(Map))
            {
                return false;
            }
            foreach (Thing t in c.GetThingList(Map))
            {
                if (CanDesignateThing(t).Accepted)
                {
                    return true;
                }
            }
            return false;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            foreach (Thing t in c.GetThingList(Map))
            {
                if (CanDesignateThing(t).Accepted)
                {
                    DesignateThing(t);
                }
            }
        }

        public override AcceptanceReport CanDesignateThing(Thing t)
        {
            if (!SalvageUtility.ModActive)
            {
                return "SA_Disabled".Translate();
            }
            if (!SalvageUtility.ResearchDone)
            {
                return "SA_NeedResearch".Translate();
            }
            if (!SalvageUtility.IsMechSalvageTarget(t))
            {
                return false;
            }
            MapComponent_SalvageAtlas mapComp = MapComponent_SalvageAtlas.For(Map);
            if (mapComp != null && mapComp.WasSalvaged(t))
            {
                return "SA_AlreadySalvaged".Translate();
            }
            if (Map.designationManager.DesignationOn(t, SalvageAtlasDefOf.SA_SalvageMech) != null)
            {
                return false;
            }
            // Teardown designation takes priority on wrecks if both exist — still allow salvage mark.
            return true;
        }

        public override void DesignateThing(Thing t)
        {
            Map.designationManager.RemoveAllDesignationsOn(t);
            Map.designationManager.AddDesignation(new Designation(t, SalvageAtlasDefOf.SA_SalvageMech));
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }
    }
}
