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
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, 2200f);
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
            // Odyssey / Royalty shuttle building. Skip if DLC/def absent.
            ThingDef shuttleDef = DefDatabase<ThingDef>.GetNamedSilentFail("Shuttle")
                                  ?? DefDatabase<ThingDef>.GetNamedSilentFail("PassengerShuttle");
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
            Scribe_Values.Look(ref enableInspiration, "enableInspiration", false);
            Scribe_Values.Look(ref inspirationMtbdMult, "inspirationMtbdMult", 1f);
        }
    }
}
