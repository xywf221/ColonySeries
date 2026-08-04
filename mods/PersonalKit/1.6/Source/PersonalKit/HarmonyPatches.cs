using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
// ThingStyleCategoryWithPriority lives in Verse

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

    // ══════════════════════════════════════════════════════════════════
    //  10. Psychic Harmonizer — mood offset mult
    //      Range is applied by mutating HediffCompProperties (ApplyAll).
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(Thought_PsychicHarmonizer), nameof(Thought_PsychicHarmonizer.MoodOffset))]
    public static class Patch_PsychicHarmonizer_MoodOffset
    {
        public static void Postfix(ref float __result)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.enableHarmonizerMood) return;
            __result *= s.harmonizerMoodMult;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  11. Psychic Harmonizer — allow multiple carriers to stack
    //      Vanilla skips / discards when the *recipient* already has the
    //      PsychicHarmonizer hediff, so carriers never buff each other.
    //      When stacking is on we re-run apply + discard without that gate;
    //      same-source memory dedupe (harmonizer == parent) is kept.
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(HediffComp_PsychicHarmonizer), "AffectPawns")]
    public static class Patch_PsychicHarmonizer_AffectPawns
    {
        public static bool Prefix(HediffComp_PsychicHarmonizer __instance, Pawn p, List<Pawn> pawns)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.harmonizerAllowStack) return true; // vanilla

            Hediff parent = __instance.parent;
            HediffCompProperties_PsychicHarmonizer props = __instance.Props;
            if (parent == null || props?.thought == null || p == null || pawns == null) return false;

            float range = props.range;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (p == other) continue;
                if (!p.RaceProps.Humanlike) continue;
                if (other?.needs?.mood?.thoughts == null) continue;
                if (other.Position.DistanceTo(p.Position) > range) continue;
                // Intentionally NOT checking HasHediff(PsychicHarmonizer) on recipient.

                bool already = false;
                List<Thought_Memory> memories = other.needs.mood.thoughts.memories.Memories;
                for (int m = 0; m < memories.Count; m++)
                {
                    if (memories[m] is Thought_PsychicHarmonizer existing
                        && existing.harmonizer == parent)
                    {
                        already = true;
                        break;
                    }
                }
                if (already) continue;

                Thought_PsychicHarmonizer thought =
                    (Thought_PsychicHarmonizer)ThoughtMaker.MakeThought(props.thought);
                thought.harmonizer = parent;
                thought.otherPawn = parent.pawn;
                other.needs.mood.thoughts.memories.TryGainMemory(thought);
            }
            return false; // skip vanilla
        }
    }

    [HarmonyPatch(typeof(Thought_PsychicHarmonizer), "get_ShouldDiscard")]
    public static class Patch_PsychicHarmonizer_ShouldDiscard
    {
        // When stacking is on, re-run discard without "recipient has implant" gate.
        public static bool Prefix(Thought_PsychicHarmonizer __instance, ref bool __result)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.harmonizerAllowStack) return true;

            Hediff harmonizer = __instance.harmonizer;
            Pawn source = harmonizer?.pawn;
            if (source == null)
            {
                __result = true;
                return false;
            }
            if (source.health.Dead || source.needs?.mood == null)
            {
                __result = true;
                return false;
            }

            Pawn recipient = __instance.pawn;
            if (recipient == null)
            {
                __result = true;
                return false;
            }

            if (!source.Spawned && !recipient.Spawned)
            {
                Caravan a = source.GetCaravan();
                Caravan b = recipient.GetCaravan();
                if (a != null && a == b)
                {
                    __result = false;
                    return false;
                }
            }

            if (source.Spawned && recipient.Spawned && source.Map == recipient.Map)
            {
                HediffComp_PsychicHarmonizer comp = harmonizer.TryGetComp<HediffComp_PsychicHarmonizer>();
                float range = comp != null ? comp.Props.range : PersonalKitMod.VanillaHarmonizerRange;
                __result = source.Position.DistanceTo(recipient.Position) > range;
                return false;
            }

            __result = true;
            return false;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  12. Ideology — skip Morbid style category for furniture etc.
    //      Vanilla: ideo "style categories" (forced by some memes e.g. Pain
    //      is Virtue / Cannibal). No global off switch. When enabled we
    //      resolve styles as if Morbid were not on the list.
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(IdeoStyleTracker), nameof(IdeoStyleTracker.StyleForThingDef))]
    public static class Patch_IdeoStyle_SkipMorbid
    {
        private static readonly FieldInfo StyleForField =
            AccessTools.Field(typeof(IdeoStyleTracker), "styleForThingDef");
        private static readonly FieldInfo IdeoField =
            AccessTools.Field(typeof(IdeoStyleTracker), "ideo");

        private static bool IsMorbid(StyleCategoryDef cat)
        {
            return cat != null && cat.defName == "Morbid";
        }

        private static bool IsMorbidPair(StyleCategoryPair pair)
        {
            if (pair == null) return false;
            if (IsMorbid(pair.category)) return true;
            return pair.styleDef != null && IsMorbid(pair.styleDef.Category);
        }

        public static bool Prefix(IdeoStyleTracker __instance, ThingDef thing, Precept precept, ref StyleCategoryPair __result)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.disableMorbidStyle) return true;
            if (!ModsConfig.IdeologyActive) return true;
            if (thing == null)
            {
                __result = null;
                return false;
            }

            var dict = StyleForField?.GetValue(__instance) as Dictionary<ThingDef, StyleCategoryPair>;
            if (dict != null && dict.TryGetValue(thing, out StyleCategoryPair cached))
            {
                if (!IsMorbidPair(cached))
                {
                    __result = cached;
                    return false;
                }
                dict.Remove(thing);
            }

            Ideo ideo = IdeoField?.GetValue(__instance) as Ideo;

            if (Find.IdeoManager != null && Find.IdeoManager.classicMode
                && Find.IdeoManager.selectedStyleCategories != null)
            {
                foreach (StyleCategoryDef cat in Find.IdeoManager.selectedStyleCategories)
                {
                    if (IsMorbid(cat) || cat == null) continue;
                    ThingStyleDef style = cat.GetStyleForThingDef(thing, precept);
                    if (style == null) continue;
                    __result = new StyleCategoryPair { styleDef = style, category = cat };
                    dict?.SetOrAdd(thing, __result);
                    return false;
                }
            }

            if (ideo?.thingStyleCategories != null)
            {
                foreach (ThingStyleCategoryWithPriority entry in ideo.thingStyleCategories)
                {
                    StyleCategoryDef cat = entry?.category;
                    if (IsMorbid(cat) || cat == null) continue;
                    ThingStyleDef style = cat.GetStyleForThingDef(thing, precept);
                    if (style == null) continue;
                    __result = new StyleCategoryPair { styleDef = style, category = cat };
                    dict?.SetOrAdd(thing, __result);
                    return false;
                }
            }

            __result = null;
            return false;
        }
    }
}
