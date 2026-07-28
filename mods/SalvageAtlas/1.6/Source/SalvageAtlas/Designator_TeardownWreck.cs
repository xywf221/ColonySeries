using RimWorld;
using UnityEngine;
using Verse;

namespace SalvageAtlas
{
    /// <summary>
    /// Player verb: risky teardown of wrecks/chunks for materials + fragments.
    /// Requires Teardown branch rank ≥ 1. Failures waste fragments and may spark/explode.
    /// </summary>
    public class Designator_TeardownWreck : Designator
    {
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;
        public override bool DragDrawMeasurements => true;
        protected override DesignationDef Designation => SalvageAtlasDefOf.SA_TeardownWreck;

        public Designator_TeardownWreck()
        {
            defaultLabel = "SA_Designator_Teardown_Label".Translate();
            defaultDesc = "SA_Designator_Teardown_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/TeardownWreck", true);
            useMouseIcon = true;
            soundDragSustain = SoundDefOf.Designate_DragStandard;
            soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
            soundSucceeded = SoundDefOf.Designate_Claim;
            hotKey = KeyBindingDefOf.Misc10;
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
            GameComponent_SalvageAtlas atlas = GameComponent_SalvageAtlas.Get();
            if (atlas == null || !atlas.HasTeardownUnlocked)
            {
                return "SA_NeedTeardownRank".Translate();
            }
            if (!SalvageUtility.IsTeardownTarget(t))
            {
                return false;
            }
            MapComponent_SalvageAtlas mapComp = MapComponent_SalvageAtlas.For(Map);
            if (mapComp != null && mapComp.WasSalvaged(t))
            {
                return "SA_AlreadySalvaged".Translate();
            }
            if (Map.designationManager.DesignationOn(t, SalvageAtlasDefOf.SA_TeardownWreck) != null)
            {
                return false;
            }
            return true;
        }

        public override void DesignateThing(Thing t)
        {
            Map.designationManager.RemoveAllDesignationsOn(t);
            Map.designationManager.AddDesignation(new Designation(t, SalvageAtlasDefOf.SA_TeardownWreck));
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }
    }
}
