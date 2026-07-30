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
    //  2. Skill Decay Rate
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Learn))]
    public static class Patch_SkillRecord_Learn
    {
        public static void Prefix(ref float xp, bool direct)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.enableSkillDecay) return;
            if (xp < 0f && !direct)
            {
                xp *= s.skillDecayRateMult;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  3. Passion / learning rate
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
    //  5. Inspiration Frequency
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
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt), nameof(InteractionWorker_RecruitAttempt.Interacted))]
    public static class Patch_RecruitResistance
    {
        /// <summary>Called from patched IL. Returns 1 when feature disabled.</summary>
        public static float GetRecruitMult()
        {
            return PersonalKitMod.Settings?.EffectiveRecruitMult ?? 1f;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);

            for (int i = 0; i < list.Count - 3; i++)
            {
                if (list[i].opcode == OpCodes.Ldfld
                    && list[i].operand is FieldInfo fi1 && fi1.Name == "guest"
                    && list[i + 1].opcode == OpCodes.Ldfld
                    && list[i + 1].operand is FieldInfo fi2 && fi2.Name == "resistance")
                {
                    // Insert: call GetRecruitMult, mul  (on top of num5 before guest.resistance load for Min)
                    var injected = new List<CodeInstruction>
                    {
                        new CodeInstruction(
                            OpCodes.Call,
                            AccessTools.Method(typeof(Patch_RecruitResistance), nameof(GetRecruitMult))),
                        new CodeInstruction(OpCodes.Mul)
                    };
                    list.InsertRange(i, injected);
                    break;
                }
            }
            return list;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  7. Surgery Success Floor
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
    //  8. Keep Weapon on Down
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    public static class Patch_KeepWeaponOnDown
    {
        private static readonly FieldInfo PawnField = AccessTools.Field(typeof(Pawn_HealthTracker), "pawn");

        public static void Postfix(Pawn_HealthTracker __instance)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.keepWeaponOnDown) return;

            Pawn pawn = PawnField.GetValue(__instance) as Pawn;
            if (pawn?.Faction != Faction.OfPlayer) return;
            if (pawn.inventory == null) return;

            Thing weapon = pawn.mindState?.droppedWeapon;
            if (weapon == null || weapon.Destroyed || !weapon.Spawned) return;

            weapon.DeSpawn();
            if (!pawn.inventory.innerContainer.TryAdd(weapon))
            {
                GenPlace.TryPlaceThing(weapon, pawn.PositionHeld, pawn.MapHeld, ThingPlaceMode.Near);
            }
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
