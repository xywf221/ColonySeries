using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
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
    //  10b. Psychic soothe pulser → ArtifactMoodBoost mood mult
    //       The artifact gives every pawn on the map a +15 memory for 1 day
    //       (vanilla). Scale the final mood offset only — duration and stack
    //       cap come from the def (ApplySoothePulserDef), since there is no
    //       per-instance hook for them.
    // ══════════════════════════════════════════════════════════════════
    //       Patched on the base Thought.BaseMoodOffset getter on purpose:
    //         - Thought_Situational does not override it (inherits the base),
    //           and Thought_Memory.MoodOffset() calls base.MoodOffset(), which
    //           reads it too — so one postfix covers all three soothe sources.
    //         - It sits *below* effectMultiplyingStat in Thought.MoodOffset(), so
    //           PsychicSensitivity still applies on top: pawns with 0 psychic
    //           sensitivity stay immune, as in vanilla. Setting the absolute value
    //           on MoodOffset() instead would break that immunity.
    //       PsychicHarmonizer is a separate override with its own patch (#12).
    [HarmonyPatch(typeof(Thought), "get_BaseMoodOffset")]
    public static class Patch_PsychicSoothe_BaseMoodOffset
    {
        private static bool resolved;
        private static ThoughtDef emanatorSootheDef;
        private static ThoughtDef psychicDroneDef;
        private static ThoughtDef artifactMoodBoostDef;

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            emanatorSootheDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("PsychicEmanatorSoothe");
            psychicDroneDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("PsychicDrone");
            artifactMoodBoostDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("ArtifactMoodBoost");
        }

        public static void Postfix(Thought __instance, ref float __result)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null) return;
            if (!s.enableEmanatorMood && !s.enablePsychicDroneMood && !s.enableSoothePulserMood) return;

            ThoughtDef def = __instance?.def;
            if (def == null) return;
            Resolve();

            if (def == emanatorSootheDef)
            {
                if (s.enableEmanatorMood) __result = s.emanatorMood;
            }
            else if (def == psychicDroneDef)
            {
                if (!s.enablePsychicDroneMood) return;
                // Stage 0 is the positive "psychic soothe"; 1-4 are the negative drone.
                int stage = __instance.CurStageIndex;
                __result = stage <= 0
                    ? s.psychicSootheMood
                    : PersonalKitMod.DroneMoodAt(stage, s.psychicDroneMood);
            }
            else if (def == artifactMoodBoostDef)
            {
                if (s.enableSoothePulserMood) __result = s.soothePulserMood;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  10c. Psychic emanator (building) — radius
    //       Vanilla radius is a hard-coded const (15) in the worker. When the
    //       range override is on we replace the worker entirely so our radius
    //       wins; the power-off check and wall-piercing behaviour are preserved.
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(ThoughtWorker_PsychicEmanatorSoothe), "CurrentStateInternal")]
    public static class Patch_EmanatorSoothe_Range
    {
        public static bool Prefix(Pawn p, ref ThoughtState __result)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.enableEmanatorRange) return true; // vanilla

            __result = false;
            if (p == null || !p.Spawned) return false;

            List<Thing> list = p.Map?.listerThings?.ThingsOfDef(ThingDefOf.PsychicEmanator);
            if (list == null) return false;

            float range = Mathf.Clamp(s.emanatorRange, 0.5f, 1000f);
            for (int i = 0; i < list.Count; i++)
            {
                Thing thing = list[i];
                if (thing == null) continue;
                CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
                // Same rule as vanilla: no power comp, or powered on.
                if (power != null && !power.PowerOn) continue;
                if (p.Position.InHorDistOf(thing.Position, range))
                {
                    __result = ThoughtState.ActiveAtStage(0);
                    return false;
                }
            }
            return false;
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

    // ══════════════════════════════════════════════════════════════════
    //  13. Corpse-worn apparel → treat as clean
    //      Vanilla marks apparel worn by a corpse as tainted
    //      (Apparel.Notify_PawnKilled sets wornByCorpseInt); that blocks
    //      wearing (mood) and selling. When enabled, WornByCorpse reports
    //      false so such apparel is fully clean: no label marker, no mood
    //      penalty, sellable at normal price. Covers both newly-tainted and
    //      already-tainted items, and toggling restores vanilla instantly.
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(Apparel), nameof(Apparel.WornByCorpse), MethodType.Getter)]
    public static class Patch_Apparel_WornByCorpse
    {
        public static bool Prefix(ref bool __result)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.noCorpseWornChar) return true; // vanilla getter
            __result = false;
            return false; // skip original getter
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  14. Forced lovin' — bed command gizmo
    //      When enabled, a double bed (non-medical, non-prisoner, non-slave,
    //      not for babies) owned by colonists gets an extra command gizmo
    //      "Initiate lovin'". Clicking it forces the assigned couple to start
    //      a lovin' job right now:
    //        - If one partner is already sleeping in the bed, we give THAT
    //          partner the Lovin job (with the other as target). Their
    //          JobDriver_Lovin.initAction starts the other partner's Lovin
    //          job, so both climb in together — the vanilla flow.
    //        - If nobody is in the bed, we give the Lovin job to the first
    //          owner; again the driver auto-starts the partner once they
    //          arrive.
    //      We also clear canLovinTick (the 1.5–36h cooldown) on both so the
    //      forced job is not blocked. This only issues a job — it never
    //      creates love relations or overrides consent; vanilla lovin'
    //      still checks partner-in-bed / awake on the driver's laydown toil.
    // ══════════════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.GetGizmos))]
    public static class Patch_Building_Bed_ForceLovin
    {
        private static Pawn GetPartnerInBed(Building_Bed bed, Pawn self)
        {
            foreach (Pawn occupant in bed.CurOccupants)
            {
                if (occupant != self
                    && occupant.RaceProps.Humanlike
                    && LovePartnerRelationUtility.LovePartnerRelationExists(self, occupant))
                {
                    return occupant;
                }
            }
            return null;
        }

        private static bool IsUsableBed(Building_Bed bed)
        {
            return bed != null
                && bed.SleepingSlotsCount >= 2
                && !bed.Medical
                && !bed.ForPrisoners
                && !bed.ForSlaves
                && !bed.ForHumanBabies
                && bed.Faction == Faction.OfPlayer;
        }

        private static Pawn FirstLovePartner(Pawn pawn)
        {
            foreach (DirectPawnRelation rel in pawn.relations.DirectRelations)
            {
                if (LovePartnerRelationUtility.IsLovePartnerRelation(rel.def)
                    && rel.otherPawn != null
                    && !rel.otherPawn.Destroyed
                    && rel.otherPawn.RaceProps.Humanlike)
                {
                    return rel.otherPawn;
                }
            }
            return null;
        }

        private static string BedOwnerNames(Building_Bed bed)
        {
            List<Pawn> owners = bed.OwnersForReading;
            if (owners == null || owners.Count == 0)
            {
                return "NoBody".Translate();
            }
            string names = "";
            for (int i = 0; i < owners.Count; i++)
            {
                if (i > 0) names += ", ";
                names += owners[i].LabelShort;
            }
            return names;
        }

        public static void Postfix(Building_Bed __instance, ref IEnumerable<Gizmo> __result)
        {
            PersonalKitSettings s = PersonalKitMod.Settings;
            if (s == null || !s.enableForceLovin)
            {
                return;
            }
            if (__instance == null || !__instance.Spawned)
            {
                return;
            }
            if (!IsUsableBed(__instance))
            {
                return;
            }

            // Collect the assigned owners who are awake, colonist, adult, and
            // have at least one living love partner.
            List<Pawn> eligible = new List<Pawn>();
            List<Pawn> owners = __instance.OwnersForReading;
            if (owners == null) return;
            for (int i = 0; i < owners.Count; i++)
            {
                Pawn owner = owners[i];
                if (owner == null || owner.Destroyed || !owner.RaceProps.Humanlike)
                {
                    continue;
                }
                if (owner.Faction != Faction.OfPlayer)
                {
                    continue;
                }
                if (owner.Drafted || owner.InMentalState || !owner.health.capacities.CanBeAwake)
                {
                    continue;
                }
                if (owner.ageTracker != null && owner.ageTracker.AgeBiologicalYears < 16f)
                {
                    continue;
                }
                if (FirstLovePartner(owner) == null)
                {
                    continue;
                }
                eligible.Add(owner);
            }
            if (eligible.Count == 0)
            {
                return;
            }

            Command_Action cmd = new Command_Action
            {
                defaultLabel = "PK_ForceLovin_CommandLabel".Translate(),
                defaultDesc = "PK_ForceLovin_CommandDesc".Translate(),
                icon = FleckDefOf.Heart.graphicData?.Graphic?.MatSingle?.mainTexture as Texture2D,
                action = delegate
                {
                    Building_Bed bed = __instance;
                    if (bed == null || !bed.Spawned || !IsUsableBed(bed))
                    {
                        return;
                    }

                    // Prefer the partner already lying in the bed: giving THEM
                    // the Lovin job (target = the other) makes their
                    // JobDriver_Lovin.initAction start the other's job, so
                    // both climb in together.
                    Pawn starter = null;
                    Pawn target = null;
                    for (int i = 0; i < eligible.Count; i++)
                    {
                        Pawn candidate = eligible[i];
                        Pawn partner = GetPartnerInBed(bed, candidate);
                        if (partner != null)
                        {
                            starter = candidate;
                            target = partner;
                            break;
                        }
                    }
                    if (starter == null)
                    {
                        starter = eligible[0];
                        target = FirstLovePartner(starter);
                    }

                    if (starter == null || target == null || starter == target)
                    {
                        return;
                    }
                    if (starter.Destroyed || target.Destroyed
                        || !starter.health.capacities.CanBeAwake || !target.health.capacities.CanBeAwake)
                    {
                        Messages.Message("PK_ForceLovin_Unavailable".Translate(),
                            new LookTargets(starter, target), MessageTypeDefOf.NeutralEvent);
                        return;
                    }

                    // Clear the cooldown so the forced act is not blocked.
                    Pawn_MindState starterMind = starter.mindState;
                    Pawn_MindState targetMind = target.mindState;
                    if (starterMind != null) starterMind.canLovinTick = 0;
                    if (targetMind != null) targetMind.canLovinTick = 0;

                    Job job = JobMaker.MakeJob(JobDefOf.Lovin, target, bed);
                    starter.jobs.TryTakeOrderedJob(job, JobTag.Misc);

                    Messages.Message("PK_ForceLovin_Started".Translate(starter.LabelShort, target.LabelShort),
                        new LookTargets(starter, target), MessageTypeDefOf.PositiveEvent);
                }
            };

            // Append after existing gizmos (vanilla prisoner/medical toggles).
            List<Gizmo> list = new List<Gizmo>(__result);
            list.Add(cmd);
            __result = list;
        }
    }
}
