using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FordRights
{
    public class FordRightsMod : Mod
    {
        public static FordRightsSettings Settings;
        private Vector2 settingsScroll;

        public FordRightsMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<FordRightsSettings>();
            new Harmony("fordrights.river").PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "FR_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect scrollOuter = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, 780f);
            Widgets.BeginScrollView(scrollOuter, ref settingsScroll, view);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);

            listing.Label("FR_Settings_SectionMaster".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("FR_Settings_Enabled".Translate(), ref Settings.modEnabled);
            listing.CheckboxLabeled("FR_Settings_EasyMode".Translate(), ref Settings.easyMode);
            if (Settings.easyMode)
            {
                listing.Label("FR_Settings_EasyModeHint".Translate());
            }

            listing.Gap();
            listing.Label("FR_Settings_SectionToll".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("FR_Settings_TollsEnabled".Translate(), ref Settings.tollsEnabled);

            listing.Label("FR_Settings_TollMin".Translate(Settings.tollMin.ToString("F0")));
            Settings.tollMin = Mathf.Round(listing.Slider(Settings.tollMin, 20f, 200f));
            if (Settings.tollMax < Settings.tollMin)
            {
                Settings.tollMax = Settings.tollMin;
            }

            listing.Label("FR_Settings_TollMax".Translate(Settings.tollMax.ToString("F0")));
            Settings.tollMax = Mathf.Round(listing.Slider(Settings.tollMax, 30f, 300f));
            if (Settings.tollMax < Settings.tollMin)
            {
                Settings.tollMin = Settings.tollMax;
            }

            listing.Label("FR_Settings_DailyTollCount".Translate(Settings.dailyTollCountCap.ToString("F0")));
            Settings.dailyTollCountCap = Mathf.Round(listing.Slider(Settings.dailyTollCountCap, 1f, 6f));

            listing.Label("FR_Settings_DailySilverCap".Translate(Settings.dailySilverCap.ToString("F0")));
            Settings.dailySilverCap = Mathf.Round(listing.Slider(Settings.dailySilverCap, 50f, 800f));

            listing.Label("FR_Settings_SeasonalSilverCap".Translate(Settings.seasonalSilverCap.ToString("F0")));
            Settings.seasonalSilverCap = Mathf.Round(listing.Slider(Settings.seasonalSilverCap, 200f, 5000f));

            listing.Gap();
            listing.Label("FR_Settings_SectionFishing".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("FR_Settings_FishingEnabled".Translate(), ref Settings.fishingEnabled);
            listing.Label("FR_Settings_FishingCooldownHours".Translate(Settings.fishingCooldownHours.ToString("F1")));
            Settings.fishingCooldownHours = listing.Slider(Settings.fishingCooldownHours, 4f, 48f);
            listing.Label("FR_Settings_FishPerCatch".Translate(Settings.fishPerCatch.ToString("F0")));
            Settings.fishPerCatch = Mathf.Round(listing.Slider(Settings.fishPerCatch, 1f, 4f));

            listing.Gap();
            listing.Label("FR_Settings_SectionFlood".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("FR_Settings_FloodEnabled".Translate(), ref Settings.floodEnabled);
            listing.Label("FR_Settings_FloodDamage".Translate(Settings.floodDamagePercent.ToString("F0")));
            Settings.floodDamagePercent = Mathf.Round(listing.Slider(Settings.floodDamagePercent, 30f, 100f));

            listing.End();
            Widgets.EndScrollView();

            Rect resetRect = new Rect(inRect.x, inRect.yMax - 32f, 220f, 30f);
            if (Widgets.ButtonText(resetRect, "FR_Settings_Reset".Translate()))
            {
                Settings.ResetToDefaults();
            }

            base.DoSettingsWindowContents(inRect);
        }
    }

    public class FordRightsSettings : ModSettings
    {
        public bool modEnabled = true;
        public bool easyMode;

        public bool tollsEnabled = true;
        public float tollMin = 50f;
        public float tollMax = 150f;
        public float dailyTollCountCap = 2f;
        public float dailySilverCap = 300f;
        public float seasonalSilverCap = 1800f;

        public bool fishingEnabled = true;
        public float fishingCooldownHours = 18f;
        public float fishPerCatch = 1f;

        public bool floodEnabled = true;
        public float floodDamagePercent = 85f;

        public float EasyFloodMult => easyMode ? 0.35f : 1f;
        public float EasyFishingCooldownMult => easyMode ? 0.65f : 1f;
        public float EasyTollMult => easyMode ? 0.85f : 1f;

        public int RollTollSilver()
        {
            float min = Mathf.Min(tollMin, tollMax);
            float max = Mathf.Max(tollMin, tollMax);
            float raw = Rand.Range(min, max) * EasyTollMult;
            return Mathf.Clamp(Mathf.RoundToInt(raw), 10, 500);
        }

        public int DailyTollCountCap => Mathf.Clamp(Mathf.RoundToInt(dailyTollCountCap), 1, 8);
        public int DailySilverCap => Mathf.Clamp(Mathf.RoundToInt(dailySilverCap), 50, 2000);
        public int SeasonalSilverCap => Mathf.Clamp(Mathf.RoundToInt(seasonalSilverCap), 100, 10000);

        public int FishingCooldownTicks
        {
            get
            {
                float hours = Mathf.Max(2f, fishingCooldownHours * EasyFishingCooldownMult);
                return Mathf.RoundToInt(hours * GenDate.TicksPerHour);
            }
        }

        public int FishCount => Mathf.Clamp(Mathf.RoundToInt(fishPerCatch), 1, 5);

        public float FloodDamageFraction => Mathf.Clamp01(floodDamagePercent / 100f);

        public void ResetToDefaults()
        {
            modEnabled = true;
            easyMode = false;
            tollsEnabled = true;
            tollMin = 50f;
            tollMax = 150f;
            dailyTollCountCap = 2f;
            dailySilverCap = 300f;
            seasonalSilverCap = 1800f;
            fishingEnabled = true;
            fishingCooldownHours = 18f;
            fishPerCatch = 1f;
            floodEnabled = true;
            floodDamagePercent = 85f;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref modEnabled, "modEnabled", true);
            Scribe_Values.Look(ref easyMode, "easyMode", false);
            Scribe_Values.Look(ref tollsEnabled, "tollsEnabled", true);
            Scribe_Values.Look(ref tollMin, "tollMin", 50f);
            Scribe_Values.Look(ref tollMax, "tollMax", 150f);
            Scribe_Values.Look(ref dailyTollCountCap, "dailyTollCountCap", 2f);
            Scribe_Values.Look(ref dailySilverCap, "dailySilverCap", 300f);
            Scribe_Values.Look(ref seasonalSilverCap, "seasonalSilverCap", 1800f);
            Scribe_Values.Look(ref fishingEnabled, "fishingEnabled", true);
            Scribe_Values.Look(ref fishingCooldownHours, "fishingCooldownHours", 18f);
            Scribe_Values.Look(ref fishPerCatch, "fishPerCatch", 1f);
            Scribe_Values.Look(ref floodEnabled, "floodEnabled", true);
            Scribe_Values.Look(ref floodDamagePercent, "floodDamagePercent", 85f);
        }
    }
}
