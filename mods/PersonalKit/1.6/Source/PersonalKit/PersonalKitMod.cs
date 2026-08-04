using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalKit
{
    public class PersonalKitMod : Mod
    {
        public static PersonalKitSettings Settings;
        private Vector2 settingsScroll;

        public const float VanillaTradePriceMax = 0.395f;
        public const float VanillaMinBuyPrice = 0.5f;
        public const float VanillaHarmonizerRange = 30f;

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
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, 2500f);
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
            ApplyMorbidStyleBlock();
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

        // Crafting
        public bool enableQualityFloor;
        public int qualityFloor; // 0-6 QualityCategory when enabled

        // Psychic / Royalty
        public bool enableHarmonizerRange;
        public float harmonizerRange = PersonalKitMod.VanillaHarmonizerRange;
        public bool enableHarmonizerMood;
        public float harmonizerMoodMult = 1f;
        public bool harmonizerAllowStack;
        /// <summary>Allow surgery that lowers psylink 1 level and spawns a neuroformer.</summary>
        public bool extractPsylinkLevel;

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
            enableQualityFloor = false;
            qualityFloor = 0;
            enableHarmonizerRange = false;
            harmonizerRange = PersonalKitMod.VanillaHarmonizerRange;
            enableHarmonizerMood = false;
            harmonizerMoodMult = 1f;
            harmonizerAllowStack = false;
            extractPsylinkLevel = false;
            disableMorbidStyle = false;
            enableInspiration = false;
            inspirationMtbdMult = 1f;
        }

        public void DisableAll()
        {
            enableTradeCap = false;
            enableMinBuyFloor = false;
            enableSkillDecay = false;
            enablePassionXp = false;
            enableSocialFight = false;
            enableRecruit = false;
            enableSurgeryFloor = false;
            keepWeaponOnDown = false;
            shuttleAsAttackTarget = false;
            enableQualityFloor = false;
            enableHarmonizerRange = false;
            enableHarmonizerMood = false;
            harmonizerAllowStack = false;
            extractPsylinkLevel = false;
            disableMorbidStyle = false;
            enableInspiration = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableTradeCap, "enableTradeCap", true);
            Scribe_Values.Look(ref tradePriceImprovementUncapped, "tradePriceImprovementUncapped", true);
            Scribe_Values.Look(ref tradePriceImprovementMax, "tradePriceImprovementMax", 1f);
            Scribe_Values.Look(ref enableMinBuyFloor, "enableMinBuyFloor", true);
            Scribe_Values.Look(ref minimumBuyPriceFactor, "minimumBuyPriceFactor", 0f);
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
            Scribe_Values.Look(ref enableQualityFloor, "enableQualityFloor", false);
            Scribe_Values.Look(ref qualityFloor, "qualityFloor", 0);
            Scribe_Values.Look(ref enableHarmonizerRange, "enableHarmonizerRange", false);
            Scribe_Values.Look(ref harmonizerRange, "harmonizerRange", PersonalKitMod.VanillaHarmonizerRange);
            Scribe_Values.Look(ref enableHarmonizerMood, "enableHarmonizerMood", false);
            Scribe_Values.Look(ref harmonizerMoodMult, "harmonizerMoodMult", 1f);
            Scribe_Values.Look(ref harmonizerAllowStack, "harmonizerAllowStack", false);
            Scribe_Values.Look(ref extractPsylinkLevel, "extractPsylinkLevel", false);
            Scribe_Values.Look(ref disableMorbidStyle, "disableMorbidStyle", false);
            Scribe_Values.Look(ref enableInspiration, "enableInspiration", false);
            Scribe_Values.Look(ref inspirationMtbdMult, "inspirationMtbdMult", 1f);
        }
    }
}
