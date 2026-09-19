using HarmonyLib;
using RimWorld;
using Verse;

namespace ZhNames
{
    [StaticConstructorOnStartup]
    public static class ZhNamesInit
    {
        static ZhNamesInit()
        {
            new Harmony("zhnames.colonyseries").PatchAll();
        }
    }

    /// <summary>
    /// Intercept full-name generation ONLY when the pawn would fall through to the
    /// default name-bank path (no rule-pack name maker from kind/xenotype/backstory/
    /// culture, no creepjoiner). Everything else is left to vanilla.
    /// </summary>
    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "GenerateFullPawnName")]
    public static class Patch_GenerateFullPawnName
    {
        public static bool Prefix(
            RulePackDef pawnKindNameMaker,
            Pawn_StoryTracker story,
            XenotypeDef xenotype,
            RulePackDef nameGenner,
            CultureDef primaryCulture,
            bool creepjoiner,
            Gender gender,
            PawnNameCategory nameCategory,
            string forcedLastName,
            ref Name __result)
        {
            ZhNamesSettings s = ZhNamesMod.Settings;
            if (s == null || !s.masterEnabled)
            {
                return true;
            }
            if (creepjoiner || pawnKindNameMaker != null || nameGenner != null)
            {
                return true;
            }
            if (story?.Childhood?.nameMaker != null || story?.Adulthood?.nameMaker != null)
            {
                return true;
            }
            if (ModsConfig.BiotechActive && xenotype != null
                && xenotype.GetNameMaker(gender) != null)
            {
                return true;
            }
            // Respect cultures that define their own name maker (e.g. tribal).
            if (primaryCulture?.GetPawnNameMaker(gender) != null)
            {
                return true;
            }
            if (nameCategory != PawnNameCategory.HumanStandard)
            {
                return true;
            }

            __result = ZhNameGenerator.Generate(gender, forcedLastName);
            return false;
        }
    }
}
