using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ArtisanMark
{
    /// <summary>
    /// Stamp maker identity when a recipe finishes producing goods.
    /// PostProcessProduct is private — postfix MakeRecipeProducts (iterator) via finalization
    /// and also Bill iteration + Notify_RecipeProduced path.
    /// </summary>
    [HarmonyPatch(typeof(GenRecipe), nameof(GenRecipe.MakeRecipeProducts))]
    public static class Patch_GenRecipe_MakeRecipeProducts
    {
        public static void Postfix(RecipeDef recipeDef, Pawn worker, List<Thing> ingredients, Thing dominantIngredient, IBillGiver billGiver, ref IEnumerable<Thing> __result)
        {
            var s = ArtisanMarkMod.Settings;
            if (s == null || !s.modEnabled || worker == null || __result == null)
            {
                return;
            }

            // Materialize once, stamp, re-yield. Recipe products are small lists.
            List<Thing> list = __result as List<Thing> ?? __result.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                ArtisanMarkUtility.StampProduct(list[i], worker);
            }
            __result = list;
        }
    }

    /// <summary>
    /// Fallback: any Thing.Notify_RecipeProduced call (vanilla empty virtual; mods may use).
    /// We postfix ThingWithComps path via Harmony on Thing.Notify_RecipeProduced.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.Notify_RecipeProduced))]
    public static class Patch_Thing_Notify_RecipeProduced
    {
        public static void Postfix(Thing __instance, Pawn pawn)
        {
            var s = ArtisanMarkMod.Settings;
            if (s == null || !s.modEnabled || pawn == null || __instance == null)
            {
                return;
            }

            ArtisanMarkUtility.StampProduct(__instance, pawn);
        }
    }

    /// <summary>
    /// Optional combat bonus: damage amount +2% cap when setting enabled.
    /// Applied once on outgoing damage from pawn with marked primary weapon + living maker same room.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_Thing_TakeDamage
    {
        public static void Prefix(ref DamageInfo dinfo)
        {
            var s = ArtisanMarkMod.Settings;
            if (s == null || !s.modEnabled || !s.enableCombatBonus)
            {
                return;
            }

            Thing instigator = dinfo.Instigator;
            if (!(instigator is Pawn attacker))
            {
                return;
            }

            if (!ArtisanMarkUtility.TryGetCombatBonus(attacker, out float bonus) || bonus <= 0f)
            {
                return;
            }

            float amount = dinfo.Amount * (1f + bonus);
            dinfo.SetAmount(amount);
        }
    }

    /// <summary>
    /// Optional hit-chance nudge via Verb.TryCastShot / shot report is invasive.
    /// Instead boost MeleeHitChance / ShootingAccuracyPawn via StatPart when combat bonus on.
    /// Registered at startup.
    /// </summary>
    public class StatPart_ArtisanMarkCombat : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            var s = ArtisanMarkMod.Settings;
            if (s == null || !s.modEnabled || !s.enableCombatBonus)
            {
                return;
            }

            if (!req.HasThing || !(req.Thing is Pawn pawn))
            {
                return;
            }

            if (!ArtisanMarkUtility.TryGetCombatBonus(pawn, out float bonus) || bonus <= 0f)
            {
                return;
            }

            val *= 1f + bonus;
        }

        public override string ExplanationPart(StatRequest req)
        {
            var s = ArtisanMarkMod.Settings;
            if (s == null || !s.modEnabled || !s.enableCombatBonus)
            {
                return null;
            }

            if (!req.HasThing || !(req.Thing is Pawn pawn))
            {
                return null;
            }

            if (!ArtisanMarkUtility.TryGetCombatBonus(pawn, out float bonus) || bonus <= 0f)
            {
                return null;
            }

            return "AM_StatPart_Combat".Translate(bonus.ToStringPercent());
        }
    }

    [StaticConstructorOnStartup]
    public static class CombatStatInjector
    {
        static CombatStatInjector()
        {
            // Hit only via stats. Damage is scaled once in TakeDamage Prefix (avoids double dip).
            TryAdd(StatDefOf.MeleeHitChance);
            TryAdd(StatDefOf.ShootingAccuracyPawn);
        }

        private static void TryAdd(StatDef stat)
        {
            if (stat == null)
            {
                return;
            }
            if (stat.parts == null)
            {
                stat.parts = new List<StatPart>();
            }
            for (int i = 0; i < stat.parts.Count; i++)
            {
                if (stat.parts[i] is StatPart_ArtisanMarkCombat)
                {
                    return;
                }
            }
            stat.parts.Add(new StatPart_ArtisanMarkCombat());
        }
    }
}
