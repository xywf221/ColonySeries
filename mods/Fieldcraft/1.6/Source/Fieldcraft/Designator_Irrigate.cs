using RimWorld;
using UnityEngine;
using Verse;

namespace Fieldcraft
{
    public class Designator_Irrigate : Designator
    {
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;
        public override bool DragDrawMeasurements => true;

        public Designator_Irrigate()
        {
            defaultLabel = "FC_Designator_Irrigate_Label".Translate();
            defaultDesc = "FC_Designator_Irrigate_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Irrigate", true);
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
            if (Map.designationManager.DesignationAt(c, FieldcraftDefOf.FC_Irrigate) != null)
            {
                return false;
            }
            if (FieldcraftDefOf.FC_PaddyWorks != null
                && !FieldcraftDefOf.FC_PaddyWorks.IsFinished
                && !DebugSettings.godMode)
            {
                return "FC_NeedPaddyWorks".Translate();
            }
            // Soft: if Landworks drainage exists and unfinished, still allow but warn via research desc only.
            TerrainDef terrain = Map.terrainGrid.TerrainAt(c);
            if (terrain == FieldcraftDefOf.FC_PaddyFlooded)
            {
                return "FC_AlreadyFlooded".Translate();
            }
            if (terrain == FieldcraftDefOf.FC_PaddyDry)
            {
                return true;
            }
            if (!LandworksBridge.CanFormPaddy(terrain))
            {
                return "FC_CannotIrrigateTerrain".Translate(terrain != null ? terrain.label : "???");
            }
            if (c.GetEdifice(Map) != null)
            {
                return "FC_CannotIrrigateBuilding".Translate();
            }
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            Map.designationManager.AddDesignation(new Designation(c, FieldcraftDefOf.FC_Irrigate));
            MapComponent_Fieldcraft.For(Map)?.EnsureTracked(c);
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }
    }

    public class Designator_Drain : Designator
    {
        public override DrawStyleCategoryDef DrawStyleCategory => DrawStyleCategoryDefOf.FilledRectangle;
        public override bool DragDrawMeasurements => true;

        public Designator_Drain()
        {
            defaultLabel = "FC_Designator_Drain_Label".Translate();
            defaultDesc = "FC_Designator_Drain_Desc".Translate();
            icon = ContentFinder<Texture2D>.Get("UI/Designators/Drain", true);
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
            if (Map.designationManager.DesignationAt(c, FieldcraftDefOf.FC_Drain) != null)
            {
                return false;
            }
            if (FieldcraftDefOf.FC_PaddyWorks != null
                && !FieldcraftDefOf.FC_PaddyWorks.IsFinished
                && !DebugSettings.godMode)
            {
                return "FC_NeedPaddyWorks".Translate();
            }
            TerrainDef terrain = Map.terrainGrid.TerrainAt(c);
            if (terrain != FieldcraftDefOf.FC_PaddyFlooded)
            {
                return "FC_NeedFloodedPaddy".Translate();
            }
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            Map.designationManager.AddDesignation(new Designation(c, FieldcraftDefOf.FC_Drain));
        }

        public override void SelectedUpdate()
        {
            GenUI.RenderMouseoverBracket();
        }
    }
}
