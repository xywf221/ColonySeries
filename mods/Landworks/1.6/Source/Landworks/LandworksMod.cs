using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Landworks
{
    public class LandworksMod : Mod
    {
        public static LandworksSettings Settings;
        private Vector2 settingsScroll;

        public LandworksMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<LandworksSettings>();
            var harmony = new Harmony("landworks.terraforming");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "Landworks";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Leave room for a reset button at the bottom.
            Rect scrollOuter = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, 920f);
            Widgets.BeginScrollView(scrollOuter, ref settingsScroll, view);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);

            listing.Label("LW_Settings_SectionBalance".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.Label("LW_Settings_StressGain".Translate(Settings.stressGainMultiplier.ToString("F2")));
            Settings.stressGainMultiplier = listing.Slider(Settings.stressGainMultiplier, 0.1f, 3f);

            listing.Label("LW_Settings_StressDecay".Translate(Settings.stressDecayPerHour.ToString("F2")));
            Settings.stressDecayPerHour = listing.Slider(Settings.stressDecayPerHour, 0f, 2f);

            listing.Label("LW_Settings_ExcavateStress".Translate(Settings.excavateStress.ToString("F2")));
            Settings.excavateStress = listing.Slider(Settings.excavateStress, 0f, 3f);

            listing.Label("LW_Settings_DegradeDays".Translate(Settings.amendedSoilLifetimeDays.ToString("F0")));
            Settings.amendedSoilLifetimeDays = listing.Slider(Settings.amendedSoilLifetimeDays, 3f, 90f);

            listing.Label("LW_Settings_TilledDays".Translate(Settings.tilledSoilLifetimeDays.ToString("F0")));
            Settings.tilledSoilLifetimeDays = listing.Slider(Settings.tilledSoilLifetimeDays, 1f, 30f);

            listing.Label("LW_Settings_FillYield".Translate(Settings.fillYieldMultiplier.ToString("F2")));
            Settings.fillYieldMultiplier = listing.Slider(Settings.fillYieldMultiplier, 0.25f, 3f);

            listing.Label("LW_Settings_ExcavateWork".Translate(Settings.excavateWorkMultiplier.ToString("F2")));
            Settings.excavateWorkMultiplier = listing.Slider(Settings.excavateWorkMultiplier, 0.25f, 3f);

            listing.Label("LW_Settings_BacklashSeverity".Translate(Settings.backlashSeverityMultiplier.ToString("F2")));
            Settings.backlashSeverityMultiplier = listing.Slider(Settings.backlashSeverityMultiplier, 0.1f, 3f);

            listing.Label("LW_Settings_BacklashMinStress".Translate(Settings.backlashMinStress.ToString("F0")));
            Settings.backlashMinStress = listing.Slider(Settings.backlashMinStress, 10f, 80f);

            listing.Gap();
            listing.Label("LW_Settings_SectionStabilizer".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.Label("LW_Settings_StabilizerRadius".Translate(Settings.stabilizerRadius.ToString("F1")));
            Settings.stabilizerRadius = listing.Slider(Settings.stabilizerRadius, 3f, 20f);

            listing.Label("LW_Settings_StabilizerRelief".Translate(Settings.stabilizerStressReliefPerDay.ToString("F1")));
            Settings.stabilizerStressReliefPerDay = listing.Slider(Settings.stabilizerStressReliefPerDay, 0f, 20f);

            listing.Gap();
            listing.Label("LW_Settings_SectionPerformance".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.Label("LW_Settings_PerformanceHint".Translate());

            listing.Label("LW_Settings_RareTickInterval".Translate(Settings.rareTickInterval.ToString("F0")));
            Settings.rareTickInterval = Mathf.Round(listing.Slider(Settings.rareTickInterval, 1000f, 10000f) / 100f) * 100f;

            listing.Label("LW_Settings_MaxDegradesPerPulse".Translate(Settings.maxDegradesPerPulse.ToString("F0")));
            Settings.maxDegradesPerPulse = Mathf.Round(listing.Slider(Settings.maxDegradesPerPulse, 8f, 256f));

            listing.Gap();
            listing.Label("LW_Settings_SectionToggles".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("LW_Settings_EnableBacklash".Translate(), ref Settings.enableGeologicalBacklash);
            listing.CheckboxLabeled("LW_Settings_EasyMode".Translate(), ref Settings.easyMode);
            if (Settings.easyMode)
            {
                listing.Label("LW_Settings_EasyModeHint".Translate());
            }

            listing.End();
            Widgets.EndScrollView();

            Rect resetRect = new Rect(inRect.x, inRect.yMax - 32f, 220f, 30f);
            if (Widgets.ButtonText(resetRect, "LW_Settings_ResetDefaults".Translate()))
            {
                Settings.ResetToDefaults();
            }

            base.DoSettingsWindowContents(inRect);
        }
    }

    public class LandworksSettings : ModSettings
    {
        // Balance
        public float stressGainMultiplier = 1f;
        public float stressDecayPerHour = 0.35f;
        public float excavateStress = 0.6f;
        public float amendedSoilLifetimeDays = 18f;
        public float tilledSoilLifetimeDays = 5f;
        public float fillYieldMultiplier = 1f;
        public float excavateWorkMultiplier = 1f;
        public float backlashSeverityMultiplier = 1f;
        public float backlashMinStress = 30f;

        // Stabilizer
        public float stabilizerRadius = 9.9f;
        public float stabilizerStressReliefPerDay = 4f;

        // Performance
        public float rareTickInterval = 2500f;
        public float maxDegradesPerPulse = 64f;

        // Toggles
        public bool enableGeologicalBacklash = true;
        public bool easyMode;

        public float EasyMult => easyMode ? 0.45f : 1f;
        public float EasyLifetimeMult => easyMode ? 1.75f : 1f;
        public int RareTickIntervalTicks => Mathf.Clamp(Mathf.RoundToInt(rareTickInterval), 1000, 10000);
        public int MaxDegradesPerPulse => Mathf.Clamp(Mathf.RoundToInt(maxDegradesPerPulse), 8, 256);

        public void ResetToDefaults()
        {
            stressGainMultiplier = 1f;
            stressDecayPerHour = 0.35f;
            excavateStress = 0.6f;
            amendedSoilLifetimeDays = 18f;
            tilledSoilLifetimeDays = 5f;
            fillYieldMultiplier = 1f;
            excavateWorkMultiplier = 1f;
            backlashSeverityMultiplier = 1f;
            backlashMinStress = 30f;
            stabilizerRadius = 9.9f;
            stabilizerStressReliefPerDay = 4f;
            rareTickInterval = 2500f;
            maxDegradesPerPulse = 64f;
            enableGeologicalBacklash = true;
            easyMode = false;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref stressGainMultiplier, "stressGainMultiplier", 1f);
            Scribe_Values.Look(ref stressDecayPerHour, "stressDecayPerHour", 0.35f);
            Scribe_Values.Look(ref excavateStress, "excavateStress", 0.6f);
            Scribe_Values.Look(ref amendedSoilLifetimeDays, "amendedSoilLifetimeDays", 18f);
            Scribe_Values.Look(ref tilledSoilLifetimeDays, "tilledSoilLifetimeDays", 5f);
            Scribe_Values.Look(ref fillYieldMultiplier, "fillYieldMultiplier", 1f);
            Scribe_Values.Look(ref excavateWorkMultiplier, "excavateWorkMultiplier", 1f);
            Scribe_Values.Look(ref backlashSeverityMultiplier, "backlashSeverityMultiplier", 1f);
            Scribe_Values.Look(ref backlashMinStress, "backlashMinStress", 30f);
            Scribe_Values.Look(ref stabilizerRadius, "stabilizerRadius", 9.9f);
            Scribe_Values.Look(ref stabilizerStressReliefPerDay, "stabilizerStressReliefPerDay", 4f);
            Scribe_Values.Look(ref rareTickInterval, "rareTickInterval", 2500f);
            Scribe_Values.Look(ref maxDegradesPerPulse, "maxDegradesPerPulse", 64f);
            Scribe_Values.Look(ref enableGeologicalBacklash, "enableGeologicalBacklash", true);
            Scribe_Values.Look(ref easyMode, "easyMode", false);
        }
    }
}
