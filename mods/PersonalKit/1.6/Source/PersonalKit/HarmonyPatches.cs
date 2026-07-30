using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalKit
{
    // ══════════════════════════════════════════════════════════════════
    //  1. Trade — Buy price floor override (Transpiler)
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(TradeUtility), nameof(TradeUtility.GetPricePlayerBuy))]
    public static class Patch_TradeUtility_GetPricePlayerBuy
    {
        public static float GetUserMinBuyPrice()
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            // Disabled → vanilla 0.5 floor.
            if (s == null || !s.enableMinBuyFloor)
            {
                return PersonalKitMod.VanillaMinBuyPrice;
            }
            return s.minimumBuyPriceFactor;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool patched = false;
            foreach (CodeInstruction instr in instructions)
            {
                // Only replace the first 0.5f — the MinimumBuyPrice clamp.
                if (!patched && instr.opcode == OpCodes.Ldc_R4 && instr.operand is float f && f == 0.5f)
                {
                    yield return new CodeInstruction(
                        OpCodes.Call,
                        AccessTools.Method(typeof(Patch_TradeUtility_GetPricePlayerBuy), nameof(GetUserMinBuyPrice)));
                    patched = true;
                    continue;
                }
                yield return instr;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  2. Skill Decay Rate — scale negative XP from decay only
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Learn))]
    public static class Patch_SkillRecord_Learn
    {
        public static void Prefix(ref float xp, bool direct)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.enableSkillDecay) return;
            // Decay is negative XP from SkillRecord.Interval (direct=false).
            if (xp < 0f && !direct)
            {
                xp *= s.skillDecayRateMult;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  3. Passion / learning rate multiplier
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.LearnRateFactor))]
    public static class Patch_SkillRecord_LearnRateFactor
    {
        public static void Postfix(ref float __result, bool direct)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.enablePassionXp) return;
            if (!direct && !DebugSettings.fastLearning)
            {
                __result *= s.passionXpMult;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  4. Social Fight Frequency
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.SocialFightChance))]
    public static class Patch_SocialFightChance
    {
        public static void Postfix(ref float __result)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.enableSocialFight) return;
            __result *= s.socialFightMult;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  5. Inspiration Frequency (MTB ÷ mult → higher mult = more frequent)
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(InspirationHandler), "get_StartInspirationMTBDays")]
    public static class Patch_InspirationMTB
    {
        public static void Postfix(ref float __result)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.enableInspiration) return;
            if (__result > 0f)
            {
                float mult = s.inspirationMtbdMult;
                if (mult > 0.01f)
                    __result /= mult;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  6. Prisoner Recruitment — scale resistance reduction
    //     Robust Prefix/Postfix (no fragile IL match on guest.resistance).
    //     After vanilla reduces resistance by R, we adjust so net reduction
    //     is R * recruitMult.
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt), nameof(InteractionWorker_RecruitAttempt.Interacted))]
    public static class Patch_RecruitResistance
    {
        public static void Prefix(Pawn recipient, out float __state)
        {
            __state = recipient?.guest?.resistance ?? -1f;
        }

        public static void Postfix(Pawn recipient, float __state)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.enableRecruit) return;
            if (__state < 0f || recipient?.guest == null) return;

            float mult = s.recruitMult;
            if (Mathf.Abs(mult - 1f) < 0.001f) return;

            // Vanilla reduced resistance by (before - after). Scale that delta.
            float reduced = __state - recipient.guest.resistance;
            if (reduced <= 0f) return; // no reduction happened (tame path / inspired / etc.)

            float extra = reduced * (mult - 1f);
            recipient.guest.resistance = Mathf.Max(0f, recipient.guest.resistance - extra);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  7. Surgery Success Floor
    //     "Floor" here = chance to force success (skip fail check).
    //     Remaining probability still uses vanilla outcome.
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(Recipe_Surgery), "CheckSurgeryFail")]
    public static class Patch_SurgeryFloor
    {
        public static bool Prefix(ref bool __result)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.enableSurgeryFloor) return true;

            float floor = s.surgeryFloor;
            if (floor > 0f && Rand.Chance(floor))
            {
                __result = false; // not a failure
                return false; // skip original
            }
            return true;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  8. Keep Weapon on Down — move dropped primary into inventory
    //     MakeDowned clears then re-sets mindState.droppedWeapon via
    //     DropAndForbidEverything(rememberPrimary: true). Postfix runs after.
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class Patch_KeepWeaponOnDown
    {
        private static readonly FieldInfo PawnField = AccessTools.Field(typeof(Pawn_HealthTracker), "pawn");

        public static void Postfix(Pawn_HealthTracker __instance)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.keepWeaponOnDown) return;

            Pawn pawn = PawnField?.GetValue(__instance) as Pawn;
            if (pawn == null || pawn.Faction != Faction.OfPlayer) return;
            if (pawn.inventory?.innerContainer == null) return;
            if (pawn.mindState == null) return;

            Thing weapon = pawn.mindState.droppedWeapon;
            if (weapon == null || weapon.Destroyed || !weapon.Spawned) return;
            // Must be on same map (paranoia).
            if (weapon.Map != pawn.MapHeld) return;

            // Unforbid so it is usable from inventory later.
            if (weapon.TryGetComp<CompForbiddable>() is CompForbiddable forbid && forbid.Forbidden)
            {
                forbid.Forbidden = false;
            }

            weapon.DeSpawn();
            if (!pawn.inventory.innerContainer.TryAdd(weapon))
            {
                GenPlace.TryPlaceThing(weapon, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near);
            }
            // Clear so JobGiver_PickupDroppedWeapon does not try to re-equip from ground.
            pawn.mindState.droppedWeapon = null;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  9. Crafting Quality Floor
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(QualityUtility), nameof(QualityUtility.GenerateQualityCreatedByPawn), typeof(int), typeof(bool))]
    public static class Patch_QualityFloor
    {
        public static void Postfix(ref QualityCategory __result)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.enableQualityFloor) return;

            QualityCategory min = (QualityCategory)Mathf.Clamp(s.qualityFloor, 0, 6);
            if (__result < min)
                __result = min;
        }
    }
}
