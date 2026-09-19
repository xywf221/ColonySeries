using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PersonalKit
{
    public class PersonalKitMod : Mod
    {
        public static PersonalKitSettings Settings;
        private Vector2 settingsScroll;

        /// <summary>Height of the settings page's virtual view; ample headroom for the loot panel.</summary>
        private const float SettingsViewHeight = 20000f;

        public const float VanillaTradePriceMax = 0.395f;
        public const float VanillaMinBuyPrice = 0.5f;
        public const float VanillaHarmonizerRange = 30f;
        /// <summary>Vanilla PsychicEmanator soothe radius — hard-coded in ThoughtWorker_PsychicEmanatorSoothe.</summary>
        public const float VanillaEmanatorRange = 15f;
        /// <summary>Vanilla ArtifactMoodBoost (psychic soothe pulser) duration in days.</summary>
        public const float VanillaSoothePulserDurationDays = 1f;
        /// <summary>Vanilla ArtifactMoodBoost stack cap.</summary>
        public const int VanillaSoothePulserStackLimit = 3;
        /// <summary>Vanilla ArtifactMoodBoost mood offset. Used as the "absolute value" default.</summary>
        public const float VanillaSoothePulserMood = 15f;
        /// <summary>Vanilla PsychicEmanatorSoothe mood offset.</summary>
        public const float VanillaEmanatorMood = 5f;
        /// <summary>Vanilla PsychicDrone stage 0 ("psychic soothe" event) mood offset.</summary>
        public const float VanillaPsychicSootheMood = 16f;
        /// <summary>Vanilla PsychicDrone stage 4 (extreme drone) mood offset — the anchor for stages 1-3.</summary>
        public const float VanillaPsychicDroneExtremeMood = -40f;
        /// <summary>
        /// Vanilla drone stages 1-4 are -12/-22/-30/-40, i.e. these fractions of the
        /// extreme (-40) value. One slider drives all four at vanilla proportions.
        /// </summary>
        private static readonly float[] DroneStageRatios = { 0.3f, 0.55f, 0.75f, 1f };

        /// <summary>
        /// Absolute mood for PsychicDrone stages 1-4 given the configured extreme value.
        /// Preserves vanilla proportions; flipping the sign turns the drone into a buff.
        /// </summary>
        public static float DroneMoodAt(int stageIndex, float extreme)
        {
            int i = Mathf.Clamp(stageIndex - 1, 0, DroneStageRatios.Length - 1);
            return extreme * DroneStageRatios[i];
        }

        /// <summary>Format a mood value with an explicit sign, e.g. +15 / -20 / 0.</summary>
        public static string Signed(float v)
        {
            int i = Mathf.RoundToInt(v);
            return (i > 0 ? "+" : "") + i.ToString();
        }

        public PersonalKitMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<PersonalKitSettings>();
            LongEventHandler.ExecuteWhenFinished(ApplyAll);
            var harmony = new Harmony("com.colonyseries.personalkit");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "PersonalKit_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect scrollOuter = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            // Listing.GetRect only advances curY and never bounds-checks, so rows
            // drawn past the view height return rects outside the scroll area and
            // get clipped — and widgets outside it stop receiving clicks. The loot
            // panel adds hundreds of rows when groups are expanded, so the view has
            // to be tall enough to hold everything.
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, SettingsViewHeight);
            Widgets.BeginScrollView(scrollOuter, ref settingsScroll, view);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);

            // ── Trade ──
            listing.Label("PK_Sec_Trade".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.CheckboxLabeled("PK_Trade_Enable".Translate(), ref Settings.enableTradeCap,
                "PK_Trade_EnableTip".Translate());
            if (Settings.enableTradeCap)
            {
                listing.CheckboxLabeled("PK_Trade_Uncapped".Translate(), ref Settings.tradePriceImprovementUncapped,
                    "PK_Trade_UncappedTip".Translate());
                if (!Settings.tradePriceImprovementUncapped)
                {
                    float pct = Settings.tradePriceImprovementMax * 100f;
                    listing.Label("PK_Trade_Max".Translate(pct.ToString("F0")));
                    pct = listing.Slider(pct, 40f, 300f);
                    Settings.tradePriceImprovementMax = pct / 100f;
                }
                else
                {
                    listing.Label("PK_Trade_UncappedHint".Translate());
                }
            }

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_MinBuy_Enable".Translate(), ref Settings.enableMinBuyFloor,
                "PK_MinBuy_EnableTip".Translate());
            if (Settings.enableMinBuyFloor)
            {
                listing.Label("PK_MinBuy_Desc".Translate());
                float minPct = Settings.minimumBuyPriceFactor * 100f;
                listing.Label("PK_MinBuy_Label".Translate(minPct.ToString("F0")));
                minPct = listing.Slider(minPct, -50f, 50f);
                Settings.minimumBuyPriceFactor = minPct / 100f;
            }

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_OrbitalSilver_Enable".Translate(), ref Settings.enableOrbitalSilverMult,
                "PK_OrbitalSilver_EnableTip".Translate());
            if (Settings.enableOrbitalSilverMult)
            {
                listing.Label("PK_OrbitalSilver_Label".Translate((Settings.orbitalSilverMult * 100f).ToString("F0")));
                Settings.orbitalSilverMult = listing.Slider(Settings.orbitalSilverMult, 0.25f, 5f);
            }

            // ── Skills ──
            listing.Gap(12f);
            listing.Label("PK_Sec_Skills".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.CheckboxLabeled("PK_SkillDecay_Enable".Translate(), ref Settings.enableSkillDecay,
                "PK_SkillDecay_EnableTip".Translate());
            if (Settings.enableSkillDecay)
            {
                listing.Label("PK_SkillDecay_Label".Translate((Settings.skillDecayRateMult * 100f).ToString("F0")));
                Settings.skillDecayRateMult = listing.Slider(Settings.skillDecayRateMult, 0f, 3f);
            }

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_Passion_Enable".Translate(), ref Settings.enablePassionXp,
                "PK_Passion_EnableTip".Translate());
            if (Settings.enablePassionXp)
            {
                listing.Label("PK_Passion_Label".Translate((Settings.passionXpMult * 100f).ToString("F0")));
                Settings.passionXpMult = listing.Slider(Settings.passionXpMult, 0.5f, 5f);
            }

            // ── Social ──
            listing.Gap(12f);
            listing.Label("PK_Sec_Social".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.CheckboxLabeled("PK_SocialFight_Enable".Translate(), ref Settings.enableSocialFight,
                "PK_SocialFight_EnableTip".Translate());
            if (Settings.enableSocialFight)
            {
                listing.Label("PK_SocialFight_Label".Translate((Settings.socialFightMult * 100f).ToString("F0")));
                Settings.socialFightMult = listing.Slider(Settings.socialFightMult, 0f, 2f);
            }

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_Recruit_Enable".Translate(), ref Settings.enableRecruit,
                "PK_Recruit_EnableTip".Translate());
            if (Settings.enableRecruit)
            {
                listing.Label("PK_Recruit_Label".Translate((Settings.recruitMult * 100f).ToString("F0")));
                Settings.recruitMult = listing.Slider(Settings.recruitMult, 0.5f, 5f);
            }

            // ── Medical ──
            listing.Gap(12f);
            listing.Label("PK_Sec_Medical".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.CheckboxLabeled("PK_Surgery_Enable".Translate(), ref Settings.enableSurgeryFloor,
                "PK_Surgery_EnableTip".Translate());
            if (Settings.enableSurgeryFloor)
            {
                listing.Label("PK_Surgery_Label".Translate((Settings.surgeryFloor * 100f).ToString("F0")));
                Settings.surgeryFloor = listing.Slider(Settings.surgeryFloor, 0f, 1f);
            }

            // ── Combat ──
            listing.Gap(12f);
            listing.Label("PK_Sec_Combat".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.CheckboxLabeled("PK_KeepWeapon".Translate(), ref Settings.keepWeaponOnDown,
                "PK_KeepWeaponTip".Translate());

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_ShuttleTarget".Translate(), ref Settings.shuttleAsAttackTarget,
                "PK_ShuttleTargetTip".Translate());

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_NoCorpseWornChar".Translate(), ref Settings.noCorpseWornChar,
                "PK_NoCorpseWornCharTip".Translate());

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_ForceLovin_Enable".Translate(), ref Settings.enableForceLovin,
                "PK_ForceLovin_EnableTip".Translate());

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_ShockPulser_Enable".Translate(), ref Settings.enableShockPulser,
                "PK_ShockPulser_EnableTip".Translate());
            if (Settings.enableShockPulser)
            {
                listing.Label("PK_ShockPulser_Duration".Translate(
                    Settings.shockPulserDownedHours.ToString("F1")));
                Settings.shockPulserDownedHours =
                    listing.Slider(Settings.shockPulserDownedHours, 0.5f, 24f);
            }

            // ── Crafting ──
            listing.Gap(12f);
            listing.Label("PK_Sec_Crafting".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.CheckboxLabeled("PK_Quality_Enable".Translate(), ref Settings.enableQualityFloor,
                "PK_Quality_EnableTip".Translate());
            if (Settings.enableQualityFloor)
            {
                int qRaw = Mathf.RoundToInt(listing.Slider(Settings.qualityFloor, 0f, 6f));
                string qLabel = ((QualityCategory)qRaw).GetLabel();
                listing.Label("PK_Quality_Label".Translate(qLabel));
                Settings.qualityFloor = qRaw;
            }

            // ── Psychic / Royalty ──
            listing.Gap(12f);
            listing.Label("PK_Sec_Psychic".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.Gap(4f);

            listing.CheckboxLabeled("PK_HarmonizerRange_Enable".Translate(), ref Settings.enableHarmonizerRange,
                "PK_HarmonizerRange_EnableTip".Translate());
            if (Settings.enableHarmonizerRange)
            {
                listing.Label("PK_HarmonizerRange_Label".Translate(Settings.harmonizerRange.ToString("F0")));
                Settings.harmonizerRange = listing.Slider(Settings.harmonizerRange, 5f, 500f);
            }

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_HarmonizerMood_Enable".Translate(), ref Settings.enableHarmonizerMood,
                "PK_HarmonizerMood_EnableTip".Translate());
            if (Settings.enableHarmonizerMood)
            {
                listing.Label("PK_HarmonizerMood_Label".Translate((Settings.harmonizerMoodMult * 100f).ToString("F0")));
                Settings.harmonizerMoodMult = listing.Slider(Settings.harmonizerMoodMult, 0f, 10f);
            }

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_HarmonizerStack".Translate(), ref Settings.harmonizerAllowStack,
                "PK_HarmonizerStackTip".Translate());

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_ExtractPsylink".Translate(), ref Settings.extractPsylinkLevel,
                "PK_ExtractPsylinkTip".Translate());

            // -- Psychic soothe pulser (artifact) -> ArtifactMoodBoost memory --
            listing.Gap(12f);
            listing.Label("PK_SoothePulser_Section".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.CheckboxLabeled("PK_SoothePulserMood_Enable".Translate(), ref Settings.enableSoothePulserMood,
                "PK_SoothePulserMood_EnableTip".Translate());
            if (Settings.enableSoothePulserMood)
            {
                listing.Label("PK_SoothePulserMood_Label".Translate(Signed(Settings.soothePulserMood)));
                Settings.soothePulserMood = listing.Slider(Settings.soothePulserMood, -100f, 100f);
                if (Settings.soothePulserMood < 0f)
                {
                    listing.Label("PK_MoodNegative_Hint".Translate());
                }
            }

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_SoothePulserDuration_Enable".Translate(), ref Settings.enableSoothePulserDuration,
                "PK_SoothePulserDuration_EnableTip".Translate());
            if (Settings.enableSoothePulserDuration)
            {
                listing.Label("PK_SoothePulserDuration_Label".Translate(
                    Settings.soothePulserDurationDays.ToString("F1")));
                Settings.soothePulserDurationDays =
                    listing.Slider(Settings.soothePulserDurationDays, 0.1f, 30f);
            }

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_SoothePulserStack_Enable".Translate(), ref Settings.enableSoothePulserStack,
                "PK_SoothePulserStack_EnableTip".Translate());
            if (Settings.enableSoothePulserStack)
            {
                int stack = Mathf.RoundToInt(listing.Slider(Settings.soothePulserStackLimit, 1f, 20f));
                listing.Label("PK_SoothePulserStack_Label".Translate(stack.ToString()));
                Settings.soothePulserStackLimit = stack;
            }

            // -- Psychic emanator building (+5 situational) --
            listing.Gap(12f);
            listing.Label("PK_Emanator_Section".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.CheckboxLabeled("PK_EmanatorMood_Enable".Translate(), ref Settings.enableEmanatorMood,
                "PK_EmanatorMood_EnableTip".Translate());
            if (Settings.enableEmanatorMood)
            {
                listing.Label("PK_EmanatorMood_Label".Translate(Signed(Settings.emanatorMood)));
                Settings.emanatorMood = listing.Slider(Settings.emanatorMood, -100f, 100f);
                if (Settings.emanatorMood < 0f)
                {
                    listing.Label("PK_MoodNegative_Hint".Translate());
                }
            }

            listing.Gap(4f);
            listing.CheckboxLabeled("PK_EmanatorRange_Enable".Translate(), ref Settings.enableEmanatorRange,
                "PK_EmanatorRange_EnableTip".Translate());
            if (Settings.enableEmanatorRange)
            {
                listing.Label("PK_EmanatorRange_Label".Translate(Settings.emanatorRange.ToString("F0")));
                Settings.emanatorRange = listing.Slider(Settings.emanatorRange, 1f, 200f);
            }

            // -- Psychic soothe / drone game condition --
            listing.Gap(12f);
            listing.Label("PK_PsychicDrone_Section".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.CheckboxLabeled("PK_PsychicDrone_Enable".Translate(), ref Settings.enablePsychicDroneMood,
                "PK_PsychicDrone_EnableTip".Translate());
            if (Settings.enablePsychicDroneMood)
            {
                listing.Label("PK_PsychicSoothe_Label".Translate(Signed(Settings.psychicSootheMood)));
                Settings.psychicSootheMood = listing.Slider(Settings.psychicSootheMood, -100f, 100f);

                listing.Gap(4f);
                // One slider drives all four drone levels at vanilla proportions:
                // -40 → -12 / -22 / -30 / -40.
                listing.Label("PK_PsychicDrone_Label".Translate(Signed(Settings.psychicDroneMood)));
                Settings.psychicDroneMood = listing.Slider(Settings.psychicDroneMood, -100f, 100f);
                listing.Label("PK_PsychicDrone_Breakdown".Translate(
                    Signed(PersonalKitMod.DroneMoodAt(1, Settings.psychicDroneMood)),
                    Signed(PersonalKitMod.DroneMoodAt(2, Settings.psychicDroneMood)),
                    Signed(PersonalKitMod.DroneMoodAt(3, Settings.psychicDroneMood)),
                    Signed(PersonalKitMod.DroneMoodAt(4, Settings.psychicDroneMood))));
            }

            // ── Psycast tweaks sub-panel ──
            listing.Gap(12f);
            listing.Label("PK_Psycast_SectionLabel".Translate());
            PsycastTweaks.DoSettingsWindow(listing, Settings.psycastEntries);

            // ── Loot tables ──
            listing.Gap(12f);
            listing.Label("PK_Loot_SectionLabel".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.CheckboxLabeled("PK_Loot_Enable".Translate(), ref Settings.enableLootEditor,
                "PK_Loot_EnableTip".Translate());
            if (Settings.enableLootEditor)
            {
                // Rows come from the outer listing so the settings page's own
                // scroll view handles scrolling (see LootTableUI class notes).
                LootTableUI.DoSettingsWindow(listing, Settings.lootOverrides);
                listing.Gap(4f);
            }

            // ── Ideology ──
            listing.Gap(12f);
            listing.Label("PK_Sec_Ideology".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.CheckboxLabeled("PK_DisableMorbidStyle".Translate(), ref Settings.disableMorbidStyle,
                "PK_DisableMorbidStyleTip".Translate());

            // ── Misc ──
            listing.Gap(12f);
            listing.Label("PK_Sec_Misc".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.CheckboxLabeled("PK_Inspiration_Enable".Translate(), ref Settings.enableInspiration,
                "PK_Inspiration_EnableTip".Translate());
            if (Settings.enableInspiration)
            {
                listing.Label("PK_Inspiration_Label".Translate((Settings.inspirationMtbdMult * 100f).ToString("F0")));
                Settings.inspirationMtbdMult = listing.Slider(Settings.inspirationMtbdMult, 0.25f, 3f);
            }

            // ── Reset ──
            listing.Gap(16f);
            if (listing.ButtonText("PK_ResetDefaults".Translate()))
            {
                Settings.ResetToDefaults();
            }
            if (listing.ButtonText("PK_DisableAll".Translate()))
            {
                Settings.DisableAll();
            }

            listing.End();
            Widgets.EndScrollView();

            ApplyAll();
            base.DoSettingsWindowContents(inRect);
        }

        public static void ApplyAll()
        {
            ApplyTradePriceImprovementCap();
            ApplyShuttleAsAttackTarget();
            ApplyHarmonizerRange();
            ApplySoothePulserDef();
            ApplyMorbidStyleBlock();
            ApplyOrbitalTraderSilver();
            ApplyPsycastTweaks();
            ApplyLootTables();
        }

        /// <summary>
        /// Push loot table overrides into the live ThingSetMakerDefs. Unlike the
        /// other Apply* methods this one is driven by a list (one entry per edited
        /// pool group), so "off" means everything is restored, not just ignored.
        /// </summary>
        public static void ApplyLootTables()
        {
            if (Settings == null) return;
            LootTableTweaks.ApplyAll(Settings.lootOverrides);
        }

        /// <summary>
        /// Psychic soothe pulser → ArtifactMoodBoost memory. Mood offset is scaled
        /// by a Harmony postfix (see Patch_ArtifactMoodBoost_MoodOffset); duration and
        /// stack cap have no hook, so they are def mutations anchored to a cached
        /// vanilla baseline (re-applying from the baseline, never compounding).
        /// </summary>
        private static float? vanillaSoothePulserDuration;
        private static int? vanillaSoothePulserStack;

        public static void ApplySoothePulserDef()
        {
            ThoughtDef def = DefDatabase<ThoughtDef>.GetNamedSilentFail("ArtifactMoodBoost");
            if (def == null) return;

            if (!vanillaSoothePulserDuration.HasValue) vanillaSoothePulserDuration = def.durationDays;
            if (!vanillaSoothePulserStack.HasValue) vanillaSoothePulserStack = def.stackLimit;

            bool on = Settings != null && Settings.enableSoothePulserDuration;
            def.durationDays = on
                ? Mathf.Clamp(Settings.soothePulserDurationDays, 0.01f, 1000f)
                : vanillaSoothePulserDuration.Value;

            def.stackLimit = (Settings != null && Settings.enableSoothePulserStack)
                ? Mathf.Clamp(Settings.soothePulserStackLimit, 1, 999)
                : vanillaSoothePulserStack.Value;
        }

        public static void ApplyPsycastTweaks()
        {
            if (Settings == null) return;
            PsycastTweaks.ApplyAll(Settings.psycastEntries);
        }

        /// <summary>
        /// When disableMorbidStyle is on: drop cached Morbid styles on ideos and
        /// clear StyleDef on already-spawned things that use the Morbid category.
        /// Vanilla path (no mod): Ideology → edit ideoligion → style categories.
        /// </summary>
        public static void ApplyMorbidStyleBlock()
        {
            if (!ModsConfig.IdeologyActive) return;
            if (Settings == null || !Settings.disableMorbidStyle) return;
            if (Find.IdeoManager == null) return;

            StyleCategoryDef morbid = DefDatabase<StyleCategoryDef>.GetNamedSilentFail("Morbid");
            if (morbid?.thingDefStyles == null) return;

            foreach (Ideo ideo in Find.IdeoManager.IdeosListForReading)
            {
                if (ideo?.style == null) continue;
                for (int i = 0; i < morbid.thingDefStyles.Count; i++)
                {
                    ThingDef td = morbid.thingDefStyles[i].ThingDef;
                    if (td != null)
                    {
                        ideo.style.ResetStyleForThing(td);
                    }
                }
            }

            if (Current.ProgramState != ProgramState.Playing || Find.Maps == null) return;

            // One-shot settings apply — not a hot path.
            for (int m = 0; m < Find.Maps.Count; m++)
            {
                Map map = Find.Maps[m];
                if (map?.listerThings?.AllThings == null) continue;
                List<Thing> all = map.listerThings.AllThings;
                for (int i = 0; i < all.Count; i++)
                {
                    Thing t = all[i];
                    ThingStyleDef style = t.StyleDef;
                    if (style == null) continue;
                    if (style.Category == morbid || (style.Category != null && style.Category.defName == "Morbid"))
                    {
                        t.StyleDef = null;
                    }
                }
            }
        }

        /// <summary>
        /// Mutate PsychicHarmonizer hediff comp range (vanilla 30). Affects both
        /// application and Thought_PsychicHarmonizer.ShouldDiscard distance checks.
        /// </summary>
        public static void ApplyHarmonizerRange()
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("PsychicHarmonizer");
            if (def?.comps == null) return;

            for (int i = 0; i < def.comps.Count; i++)
            {
                if (def.comps[i] is HediffCompProperties_PsychicHarmonizer props)
                {
                    if (Settings != null && Settings.enableHarmonizerRange)
                    {
                        props.range = Mathf.Clamp(Settings.harmonizerRange, 1f, 1000f);
                    }
                    else
                    {
                        props.range = VanillaHarmonizerRange;
                    }
                    return;
                }
            }
        }

        public static void ApplyTradePriceImprovementCap()
        {
            StatDef stat = StatDefOf.TradePriceImprovement;
            if (stat == null) return;

            // Disabled → restore vanilla hard cap.
            if (Settings == null || !Settings.enableTradeCap)
            {
                stat.maxValue = VanillaTradePriceMax;
                stat.displayMaxWhenAboveOrEqual = true;
                return;
            }

            if (Settings.tradePriceImprovementUncapped)
            {
                stat.maxValue = 999f;
                stat.displayMaxWhenAboveOrEqual = false;
            }
            else
            {
                float max = Mathf.Clamp(Settings.tradePriceImprovementMax, 0f, 10f);
                if (max < 0.01f) max = VanillaTradePriceMax;
                stat.maxValue = max;
                stat.displayMaxWhenAboveOrEqual = max <= 1.01f;
            }
        }

        /// <summary>
        /// Orbital traders carry silver via a StockGenerator_SingleDef (Silver)
        /// in each orbital TraderKindDef. Multiply its countRange so these
        /// traders arrive with more money to buy your goods. Restores exact
        /// vanilla ranges when disabled or when the slider moves. Defs are
        /// stable for a session, so vanilla ranges are cached once and the
        /// setting re-applies from that base (no compounding).
        /// </summary>
        private static readonly Dictionary<TraderKindDef, IntRange> orbitalSilverVanilla =
            new Dictionary<TraderKindDef, IntRange>();

        public static void ApplyOrbitalTraderSilver()
        {
            bool enabled = Settings != null && Settings.enableOrbitalSilverMult;
            float mult = enabled ? Mathf.Clamp(Settings.orbitalSilverMult, 0.1f, 10f) : 1f;

            foreach (TraderKindDef kind in DefDatabase<TraderKindDef>.AllDefsListForReading)
            {
                if (kind == null || !kind.orbital || kind.stockGenerators == null)
                {
                    continue;
                }
                for (int i = 0; i < kind.stockGenerators.Count; i++)
                {
                    StockGenerator gen = kind.stockGenerators[i];
                    if (gen is StockGenerator_SingleDef single && single.HandlesThingDef(ThingDefOf.Silver))
                    {
                        if (!orbitalSilverVanilla.TryGetValue(kind, out IntRange vanilla))
                        {
                            vanilla = single.countRange;
                            orbitalSilverVanilla[kind] = vanilla;
                        }
                        single.countRange = Mathf.Abs(mult - 1f) < 0.001f
                            ? vanilla
                            : new IntRange(
                                Mathf.RoundToInt(vanilla.min * mult),
                                Mathf.RoundToInt(vanilla.max * mult));
                        break;
                    }
                }
            }
        }

        public static void ApplyShuttleAsAttackTarget()
        {
            // Odyssey player craftable shuttle. Imperial "Shuttle" is a different Building.
            ThingDef shuttleDef = DefDatabase<ThingDef>.GetNamedSilentFail("PassengerShuttle");
            if (shuttleDef == null) return;
            if (shuttleDef.thingClass != typeof(Building_PassengerShuttle)
                && shuttleDef.thingClass != typeof(Building_PassengerShuttle_Aggro))
            {
                // Another mod already replaced the class — do not stomp.
                return;
            }

            if (Settings != null && Settings.shuttleAsAttackTarget)
            {
                shuttleDef.thingClass = typeof(Building_PassengerShuttle_Aggro);
            }
            else
            {
                // Restore vanilla class when disabled.
                // Already-spawned instances keep their runtime type until map reload.
                shuttleDef.thingClass = typeof(Building_PassengerShuttle);
            }
        }
    }

    public class PersonalKitSettings : ModSettings
    {
        // Trade
        public bool enableTradeCap = true;
        public bool tradePriceImprovementUncapped = true;
        public float tradePriceImprovementMax = 1f;
        public bool enableMinBuyFloor = true;
        public float minimumBuyPriceFactor = 0f;
        public bool enableOrbitalSilverMult;
        public float orbitalSilverMult = 1f;
        /// <summary>Hide the tainted-apparel char (e.g. Chinese pack's 亡) on labels.</summary>
        public bool noCorpseWornChar;

        // Skills
        public bool enableSkillDecay;
        public float skillDecayRateMult = 1f;
        public bool enablePassionXp;
        public float passionXpMult = 1f;

        // Social
        public bool enableSocialFight;
        public float socialFightMult = 1f;
        public bool enableRecruit;
        public float recruitMult = 1f;

        // Medical
        public bool enableSurgeryFloor;
        public float surgeryFloor = 0.5f;

        // Combat
        public bool keepWeaponOnDown;
        public bool shuttleAsAttackTarget;
        /// <summary>Add a "force lovin'" command gizmo to double beds owned by colonists.</summary>
        public bool enableForceLovin;
        /// <summary>Master switch for the PK_PsychicShockPulser artifact. Off = artifact does nothing.</summary>
        public bool enableShockPulser = true;
        /// <summary>How long shocked pawns stay down, in in-game hours (vanilla PsychicShock = 3).</summary>
        public float shockPulserDownedHours = 3f;

        // Crafting
        public bool enableQualityFloor;
        public int qualityFloor; // 0-6 QualityCategory when enabled

        // Psychic / Royalty
        /// <summary>Per-psycast override entries. Each entry has its own enabled flag.</summary>
        public List<PsycastTweaks.Entry> psycastEntries = new List<PsycastTweaks.Entry>();

        public bool enableHarmonizerRange;
        public float harmonizerRange = PersonalKitMod.VanillaHarmonizerRange;
        public bool enableHarmonizerMood;
        public float harmonizerMoodMult = 1f;
        public bool harmonizerAllowStack;
        /// <summary>Allow surgery that lowers psylink 1 level and spawns a neuroformer.</summary>
        public bool extractPsylinkLevel;

        // Psychic — soothe sources (absolute mood values; negative = mood penalty)
        /// <summary>Absolute mood offset for ArtifactMoodBoost (soothe pulser). Vanilla +15. Negative allowed.</summary>
        public bool enableSoothePulserMood;
        public float soothePulserMood = PersonalKitMod.VanillaSoothePulserMood;
        /// <summary>Override ArtifactMoodBoost duration (vanilla 1 day).</summary>
        public bool enableSoothePulserDuration;
        public float soothePulserDurationDays = PersonalKitMod.VanillaSoothePulserDurationDays;
        /// <summary>Override ArtifactMoodBoost stack cap (vanilla 3).</summary>
        public bool enableSoothePulserStack;
        public int soothePulserStackLimit = PersonalKitMod.VanillaSoothePulserStackLimit;

        /// <summary>Absolute mood offset for PsychicEmanatorSoothe (emanator building). Vanilla +5. Negative allowed.</summary>
        public bool enableEmanatorMood;
        public float emanatorMood = PersonalKitMod.VanillaEmanatorMood;
        /// <summary>Override PsychicEmanator radius (vanilla 15 cells, hard-coded in the worker).</summary>
        public bool enableEmanatorRange;
        public float emanatorRange = PersonalKitMod.VanillaEmanatorRange;

        /// <summary>Absolute mood override for the psychic soothe / drone event.</summary>
        public bool enablePsychicDroneMood;
        /// <summary>Absolute mood for PsychicDrone stage 0 (the good "psychic soothe"). Vanilla +16.</summary>
        public float psychicSootheMood = PersonalKitMod.VanillaPsychicSootheMood;
        /// <summary>
        /// Absolute mood for PsychicDrone stage 4 (extreme drone). Vanilla -40.
        /// Stages 1-3 are derived from it at vanilla proportions: -40 → -12/-22/-30/-40.
        /// Flip to positive and the drone becomes a mood buff instead.
        /// </summary>
        public float psychicDroneMood = PersonalKitMod.VanillaPsychicDroneExtremeMood;

        // Loot tables
        /// <summary>Master switch for the loot table editor. Off = every pool behaves as vanilla.</summary>
        public bool enableLootEditor;
        /// <summary>Per-group overrides. Empty by default, i.e. "read vanilla".</summary>
        public List<LootTableTweaks.GroupOverride> lootOverrides =
            new List<LootTableTweaks.GroupOverride>();

        // Ideology
        /// <summary>Skip Morbid style category so tables/chairs use next style or vanilla.</summary>
        public bool disableMorbidStyle;

        // Misc
        public bool enableInspiration;
        public float inspirationMtbdMult = 1f;

        /// <summary>Effective recruit mult: 1 when disabled.</summary>
        public float EffectiveRecruitMult => enableRecruit ? recruitMult : 1f;

        public void ResetToDefaults()
        {
            enableTradeCap = true;
            tradePriceImprovementUncapped = true;
            tradePriceImprovementMax = 1f;
            enableMinBuyFloor = true;
            minimumBuyPriceFactor = 0f;
            enableOrbitalSilverMult = false;
            orbitalSilverMult = 1f;
            noCorpseWornChar = false;
            enableSkillDecay = false;
            skillDecayRateMult = 1f;
            enablePassionXp = false;
            passionXpMult = 1f;
            enableSocialFight = false;
            socialFightMult = 1f;
            enableRecruit = false;
            recruitMult = 1f;
            enableSurgeryFloor = false;
            surgeryFloor = 0.5f;
            keepWeaponOnDown = false;
            shuttleAsAttackTarget = false;
            enableForceLovin = false;
            enableShockPulser = true;
            shockPulserDownedHours = 3f;
            enableQualityFloor = false;
            qualityFloor = 0;
            enableHarmonizerRange = false;
            harmonizerRange = PersonalKitMod.VanillaHarmonizerRange;
            enableHarmonizerMood = false;
            harmonizerMoodMult = 1f;
            harmonizerAllowStack = false;
            extractPsylinkLevel = false;
            enableSoothePulserMood = false;
            soothePulserMood = PersonalKitMod.VanillaSoothePulserMood;
            enableSoothePulserDuration = false;
            soothePulserDurationDays = PersonalKitMod.VanillaSoothePulserDurationDays;
            enableSoothePulserStack = false;
            soothePulserStackLimit = PersonalKitMod.VanillaSoothePulserStackLimit;
            enableEmanatorMood = false;
            emanatorMood = PersonalKitMod.VanillaEmanatorMood;
            enableEmanatorRange = false;
            emanatorRange = PersonalKitMod.VanillaEmanatorRange;
            enablePsychicDroneMood = false;
            psychicSootheMood = PersonalKitMod.VanillaPsychicSootheMood;
            psychicDroneMood = PersonalKitMod.VanillaPsychicDroneExtremeMood;
            disableMorbidStyle = false;
            enableInspiration = false;
            inspirationMtbdMult = 1f;
            psycastEntries.Clear();
            enableLootEditor = false;
            lootOverrides.Clear();
        }

        public void DisableAll()
        {
            enableTradeCap = false;
            enableMinBuyFloor = false;
            enableOrbitalSilverMult = false;
            noCorpseWornChar = false;
            enableSkillDecay = false;
            enablePassionXp = false;
            enableSocialFight = false;
            enableRecruit = false;
            enableSurgeryFloor = false;
            keepWeaponOnDown = false;
            shuttleAsAttackTarget = false;
            enableForceLovin = false;
            // NOTE: shock pulser master switch intentionally NOT cleared here —
            // "disable all" means "drop every vanilla override", and this artifact
            // ships with the mod (off = item exists but is inert).
            enableQualityFloor = false;
            enableHarmonizerRange = false;
            enableHarmonizerMood = false;
            harmonizerAllowStack = false;
            extractPsylinkLevel = false;
            enableSoothePulserMood = false;
            enableSoothePulserDuration = false;
            enableSoothePulserStack = false;
            enableEmanatorMood = false;
            enableEmanatorRange = false;
            enablePsychicDroneMood = false;
            disableMorbidStyle = false;
            enableInspiration = false;
            enableLootEditor = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableTradeCap, "enableTradeCap", true);
            Scribe_Values.Look(ref tradePriceImprovementUncapped, "tradePriceImprovementUncapped", true);
            Scribe_Values.Look(ref tradePriceImprovementMax, "tradePriceImprovementMax", 1f);
            Scribe_Values.Look(ref enableMinBuyFloor, "enableMinBuyFloor", true);
            Scribe_Values.Look(ref minimumBuyPriceFactor, "minimumBuyPriceFactor", 0f);
            Scribe_Values.Look(ref enableOrbitalSilverMult, "enableOrbitalSilverMult", false);
            Scribe_Values.Look(ref orbitalSilverMult, "orbitalSilverMult", 1f);
            Scribe_Values.Look(ref noCorpseWornChar, "noCorpseWornChar", false);
            Scribe_Values.Look(ref enableSkillDecay, "enableSkillDecay", false);
            Scribe_Values.Look(ref skillDecayRateMult, "skillDecayRateMult", 1f);
            Scribe_Values.Look(ref enablePassionXp, "enablePassionXp", false);
            Scribe_Values.Look(ref passionXpMult, "passionXpMult", 1f);
            Scribe_Values.Look(ref enableSocialFight, "enableSocialFight", false);
            Scribe_Values.Look(ref socialFightMult, "socialFightMult", 1f);
            Scribe_Values.Look(ref enableRecruit, "enableRecruit", false);
            Scribe_Values.Look(ref recruitMult, "recruitMult", 1f);
            Scribe_Values.Look(ref enableSurgeryFloor, "enableSurgeryFloor", false);
            Scribe_Values.Look(ref surgeryFloor, "surgeryFloor", 0.5f);
            Scribe_Values.Look(ref keepWeaponOnDown, "keepWeaponOnDown", false);
            Scribe_Values.Look(ref shuttleAsAttackTarget, "shuttleAsAttackTarget", false);
            Scribe_Values.Look(ref enableForceLovin, "enableForceLovin", false);
            Scribe_Values.Look(ref enableShockPulser, "enableShockPulser", true);
            Scribe_Values.Look(ref shockPulserDownedHours, "shockPulserDownedHours", 3f);
            Scribe_Values.Look(ref enableQualityFloor, "enableQualityFloor", false);
            Scribe_Values.Look(ref qualityFloor, "qualityFloor", 0);
            Scribe_Values.Look(ref enableHarmonizerRange, "enableHarmonizerRange", false);
            Scribe_Values.Look(ref harmonizerRange, "harmonizerRange", PersonalKitMod.VanillaHarmonizerRange);
            Scribe_Values.Look(ref enableHarmonizerMood, "enableHarmonizerMood", false);
            Scribe_Values.Look(ref harmonizerMoodMult, "harmonizerMoodMult", 1f);
            Scribe_Values.Look(ref harmonizerAllowStack, "harmonizerAllowStack", false);
            Scribe_Values.Look(ref extractPsylinkLevel, "extractPsylinkLevel", false);
            Scribe_Values.Look(ref enableSoothePulserMood, "enableSoothePulserMood", false);
            Scribe_Values.Look(ref soothePulserMood, "soothePulserMood",
                PersonalKitMod.VanillaSoothePulserMood);
            Scribe_Values.Look(ref enableSoothePulserDuration, "enableSoothePulserDuration", false);
            Scribe_Values.Look(ref soothePulserDurationDays, "soothePulserDurationDays",
                PersonalKitMod.VanillaSoothePulserDurationDays);
            Scribe_Values.Look(ref enableSoothePulserStack, "enableSoothePulserStack", false);
            Scribe_Values.Look(ref soothePulserStackLimit, "soothePulserStackLimit",
                PersonalKitMod.VanillaSoothePulserStackLimit);
            Scribe_Values.Look(ref enableEmanatorMood, "enableEmanatorMood", false);
            Scribe_Values.Look(ref emanatorMood, "emanatorMood", PersonalKitMod.VanillaEmanatorMood);
            Scribe_Values.Look(ref enableEmanatorRange, "enableEmanatorRange", false);
            Scribe_Values.Look(ref emanatorRange, "emanatorRange", PersonalKitMod.VanillaEmanatorRange);
            Scribe_Values.Look(ref enablePsychicDroneMood, "enablePsychicDroneMood", false);
            Scribe_Values.Look(ref psychicSootheMood, "psychicSootheMood",
                PersonalKitMod.VanillaPsychicSootheMood);
            Scribe_Values.Look(ref psychicDroneMood, "psychicDroneMood",
                PersonalKitMod.VanillaPsychicDroneExtremeMood);
            Scribe_Values.Look(ref disableMorbidStyle, "disableMorbidStyle", false);
            Scribe_Values.Look(ref enableInspiration, "enableInspiration", false);
            Scribe_Values.Look(ref inspirationMtbdMult, "inspirationMtbdMult", 1f);
            Scribe_Collections.Look(ref psycastEntries, "psycastEntries", LookMode.Deep);
            Scribe_Values.Look(ref enableLootEditor, "enableLootEditor", false);
            Scribe_Collections.Look(ref lootOverrides, "lootOverrides", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (psycastEntries == null) psycastEntries = new List<PsycastTweaks.Entry>();
                if (lootOverrides == null) lootOverrides = new List<LootTableTweaks.GroupOverride>();
            }
        }
    }
}
