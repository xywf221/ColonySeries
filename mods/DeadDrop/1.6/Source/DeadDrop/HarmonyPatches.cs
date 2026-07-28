using HarmonyLib;
using RimWorld;
using Verse;

namespace DeadDrop
{
    /// <summary>
    /// Light hooks only. Order refresh is MapComponent rare-tick driven.
    /// Patch keeps research-gated designator visibility tidy if needed later.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            // Harmony.PatchAll is invoked from DeadDropMod ctor.
            // Static ctor reserved for soft-link resolution.
            _ = TraitSerumBridge.SerumDef;
        }
    }

    // Optional: nudge placement tip when building outside edge band.
    public class PlaceWorker_DeadDropEdge : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            // Prefer map edge (within 12 cells of border) — still allow elsewhere with warning via UI only.
            // Hard-block only fogged / non-standable handled by base.
            if (!loc.InBounds(map))
            {
                return false;
            }
            return true;
        }

        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, UnityEngine.Color ghostCol, Thing thing = null)
        {
            // No extra overlay; keep cheap.
        }

        public static bool IsNearMapEdge(IntVec3 c, Map map, int band = 12)
        {
            return c.x < band || c.z < band || c.x > map.Size.x - 1 - band || c.z > map.Size.z - 1 - band;
        }
    }
}
