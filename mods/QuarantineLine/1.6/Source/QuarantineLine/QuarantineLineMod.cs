using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace QuarantineLine
{
    public class QuarantineLineMod : Mod
    {
        public static QuarantineLineSettings Settings;
        private Vector2 settingsScroll;

        public QuarantineLineMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<QuarantineLineSettings>();
            new Harmony("quarantineline.isolation").PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "QL_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect scrollOuter = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, 780f);
            Widgets.BeginScrollView(scrollOuter, ref settingsScroll, view);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);

            listing.Label("QL_Settings_SectionMaster".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("QL_Settings_Enabled".Translate(), ref Settings.modEnabled);
            listing.CheckboxLabeled("QL_Settings_SpreadModifiers".Translate(), ref Settings.enableSpreadModifiers);
            listing.Label("QL_Settings_SpreadModifiersHint".Translate());
            listing.CheckboxLabeled("QL_Settings_EasyMode".Translate(), ref Settings.easyMode);
            if (Settings.easyMode)
            {
                listing.Label("QL_Settings_EasyModeHint".Translate());
            }

            listing.Gap();
            listing.Label("QL_Settings_SectionBalance".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.Label("QL_Settings_VisitRisk".Translate(Settings.visitRiskPercent.ToString("F0")));
            Settings.visitRiskPercent = Mathf.Round(listing.Slider(Settings.visitRiskPercent, 0f, 40f));

            listing.Label("QL_Settings_BaseExposure".Translate(Settings.baseExposurePercent.ToString("F0")));
            Settings.baseExposurePercent = Mathf.Round(listing.Slider(Settings.baseExposurePercent, 0f, 20f));

            listing.Label("QL_Settings_IsolationReduction".Translate(Settings.isolationReductionPercent.ToString("F0")));
            Settings.isolationReductionPercent = Mathf.Round(listing.Slider(Settings.isolationReductionPercent, 20f, 90f));

            listing.Label("QL_Settings_PatientMood".Translate(Settings.patientMoodPenalty.ToString("F0")));
            Settings.patientMoodPenalty = Mathf.Round(listing.Slider(Settings.patientMoodPenalty, 0f, 12f));

            listing.Label("QL_Settings_StrictMoodExtra".Translate(Settings.strictMoodExtra.ToString("F0")));
            Settings.strictMoodExtra = Mathf.Round(listing.Slider(Settings.strictMoodExtra, 0f, 10f));

            listing.Label("QL_Settings_DoctorWorkloadMood".Translate(Settings.doctorWorkloadMood.ToString("F0")));
            Settings.doctorWorkloadMood = Mathf.Round(listing.Slider(Settings.doctorWorkloadMood, 0f, 8f));

            listing.Gap();
            listing.Label("QL_Settings_SectionEvents".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("QL_Settings_CaravanDisease".Translate(), ref Settings.enableCaravanDisease);
            listing.CheckboxLabeled("QL_Settings_AnimalSource".Translate(), ref Settings.enableAnimalSource);
            listing.Label("QL_Settings_EventsHint".Translate());

            listing.Gap();
            listing.Label("QL_Settings_SectionPerformance".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.Label("QL_Settings_RareTick".Translate(Settings.rareTickInterval.ToString("F0")));
            Settings.rareTickInterval = Mathf.Round(listing.Slider(Settings.rareTickInterval, 1000f, 10000f) / 100f) * 100f;
            listing.Label("QL_Settings_SleepHint".Translate());

            listing.End();
            Widgets.EndScrollView();

            Rect resetRect = new Rect(inRect.x, inRect.yMax - 32f, 220f, 30f);
            if (Widgets.ButtonText(resetRect, "QL_Settings_Reset".Translate()))
            {
                Settings.ResetToDefaults();
            }

            base.DoSettingsWindowContents(inRect);
        }
    }

    public class QuarantineLineSettings : ModSettings
    {
        public bool modEnabled = true;

        /// <summary>Master kill-switch for exposure/spread math. Zone + mood still work.</summary>
        public bool enableSpreadModifiers = true;

        public bool easyMode;

        /// <summary>Per rare-pulse chance % when a healthy pawn visits a quarantine cell with sick.</summary>
        public float visitRiskPercent = 12f;

        /// <summary>Per rare-pulse chance % for casual same-room contact outside protocol.</summary>
        public float baseExposurePercent = 4f;

        /// <summary>How much strict isolation cuts outbreak exposure for pawns outside the ward.</summary>
        public float isolationReductionPercent = 65f;

        public float patientMoodPenalty = 4f;
        public float strictMoodExtra = 4f;
        public float doctorWorkloadMood = 2f;

        public bool enableCaravanDisease = true;
        public bool enableAnimalSource = true;

        public float rareTickInterval = 2500f;

        public float EasySpreadMult => easyMode ? 0.45f : 1f;
        public float EasyMoodMult => easyMode ? 0.6f : 1f;
        public int RareTickIntervalTicks => Mathf.Clamp(Mathf.RoundToInt(rareTickInterval), 1000, 10000);

        public float VisitRisk01 => Mathf.Clamp01(visitRiskPercent / 100f) * EasySpreadMult;
        public float BaseExposure01 => Mathf.Clamp01(baseExposurePercent / 100f) * EasySpreadMult;
        public float IsolationKeepFactor => 1f - Mathf.Clamp01(isolationReductionPercent / 100f);

        public int PatientMood => -Mathf.RoundToInt(patientMoodPenalty * EasyMoodMult);
        public int StrictPatientMood => -Mathf.RoundToInt((patientMoodPenalty + strictMoodExtra) * EasyMoodMult);
        public int DoctorMood => -Mathf.RoundToInt(doctorWorkloadMood * EasyMoodMult);

        public void ResetToDefaults()
        {
            modEnabled = true;
            enableSpreadModifiers = true;
            easyMode = false;
            visitRiskPercent = 12f;
            baseExposurePercent = 4f;
            isolationReductionPercent = 65f;
            patientMoodPenalty = 4f;
            strictMoodExtra = 4f;
            doctorWorkloadMood = 2f;
            enableCaravanDisease = true;
            enableAnimalSource = true;
            rareTickInterval = 2500f;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref modEnabled, "modEnabled", true);
            Scribe_Values.Look(ref enableSpreadModifiers, "enableSpreadModifiers", true);
            Scribe_Values.Look(ref easyMode, "easyMode", false);
            Scribe_Values.Look(ref visitRiskPercent, "visitRiskPercent", 12f);
            Scribe_Values.Look(ref baseExposurePercent, "baseExposurePercent", 4f);
            Scribe_Values.Look(ref isolationReductionPercent, "isolationReductionPercent", 65f);
            Scribe_Values.Look(ref patientMoodPenalty, "patientMoodPenalty", 4f);
            Scribe_Values.Look(ref strictMoodExtra, "strictMoodExtra", 4f);
            Scribe_Values.Look(ref doctorWorkloadMood, "doctorWorkloadMood", 2f);
            Scribe_Values.Look(ref enableCaravanDisease, "enableCaravanDisease", true);
            Scribe_Values.Look(ref enableAnimalSource, "enableAnimalSource", true);
            Scribe_Values.Look(ref rareTickInterval, "rareTickInterval", 2500f);
        }
    }
}
