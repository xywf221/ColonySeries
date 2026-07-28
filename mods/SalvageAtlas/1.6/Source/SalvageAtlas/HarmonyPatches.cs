using HarmonyLib;
using RimWorld;
using Verse;

namespace SalvageAtlas
{
    /// <summary>
    /// Optional light hooks. Kill drip is intentionally tiny so designate-salvage remains the player verb.
    /// No ResearchManager points ever.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            // PatchAll is invoked from SalvageAtlasMod ctor; this type just hosts patch classes.
        }
    }

    /// <summary>
    /// On mechanoid death, small chance to scatter 1 fragment near the corpse.
    /// Full yields still require designate → salvage job. Keeps no-mech maps idle.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill_MechFragment
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            if (!SalvageUtility.ModActive || !SalvageUtility.ResearchDone)
            {
                return;
            }
            if (__instance == null || !__instance.RaceProps.IsMechanoid)
            {
                return;
            }
            Map map = __instance.MapHeld;
            IntVec3 cell = __instance.PositionHeld;
            if (map == null || !cell.IsValid)
            {
                return;
            }
            // Low drip — scavenge designation is the main path.
            float chance = SalvageAtlasMod.Settings != null && SalvageAtlasMod.Settings.easyMode ? 0.55f : 0.35f;
            if (!Rand.Chance(chance))
            {
                return;
            }
            if (SalvageAtlasDefOf.SA_MechFragment == null)
            {
                return;
            }
            Thing frag = ThingMaker.MakeThing(SalvageAtlasDefOf.SA_MechFragment);
            frag.stackCount = 1;
            GenPlace.TryPlaceThing(frag, cell, map, ThingPlaceMode.Near);
            GameComponent_SalvageAtlas.Get()?.NotifySalvaged(1);
        }
    }
}
