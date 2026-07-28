using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace WatchRoster
{
    public class WatchRosterMod : Mod
    {
        public static WatchRosterSettings Settings;
        private Vector2 settingsScroll;

        public WatchRosterMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<WatchRosterSettings>();
            new Harmony("watchroster.nightwatch").PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "WR_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect scrollOuter = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, 620f);
            Widgets.BeginScrollView(scrollOuter, ref settingsScroll, view);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);

            listing.CheckboxLabeled("WR_Settings_MasterEnabled".Translate(), ref Settings.masterEnabled);
            listing.GapLine();

            listing.Label("WR_Settings_SectionBalance".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.Label("WR_Settings_MaxPosts".Translate(Settings.maxWatchPosts.ToString("F0")));
            Settings.maxWatchPosts = Mathf.Round(listing.Slider(Settings.maxWatchPosts, 1f, 5f));

            listing.Label("WR_Settings_NightStart".Translate(Settings.nightStartHour.ToString("F0")));
            Settings.nightStartHour = Mathf.Round(listing.Slider(Settings.nightStartHour, 16f, 23f));

            listing.Label("WR_Settings_NightEnd".Translate(Settings.nightEndHour.ToString("F0")));
            Settings.nightEndHour = Mathf.Round(listing.Slider(Settings.nightEndHour, 3f, 10f));

            listing.Label("WR_Settings_WarnMinHours".Translate(Settings.warnMinHours.ToString("F2")));
            Settings.warnMinHours = listing.Slider(Settings.warnMinHours, 0.25f, 2f);
            if (Settings.warnMaxHours < Settings.warnMinHours)
            {
                Settings.warnMaxHours = Settings.warnMinHours;
            }

            listing.Label("WR_Settings_WarnMaxHours".Translate(Settings.warnMaxHours.ToString("F2")));
            Settings.warnMaxHours = listing.Slider(Settings.warnMaxHours, 0.5f, 3f);
            if (Settings.warnMaxHours < Settings.warnMinHours)
            {
                Settings.warnMinHours = Settings.warnMaxHours;
            }

            listing.Label("WR_Settings_FireMannedMult".Translate(Settings.fireMannedIntervalMult.ToString("F2")));
            Settings.fireMannedIntervalMult = listing.Slider(Settings.fireMannedIntervalMult, 0.25f, 1f);

            listing.Label("WR_Settings_FireEmptyMult".Translate(Settings.fireEmptyIntervalMult.ToString("F2")));
            Settings.fireEmptyIntervalMult = listing.Slider(Settings.fireEmptyIntervalMult, 1f, 3f);

            listing.Gap();
            listing.Label("WR_Settings_SectionToggles".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("WR_Settings_RaidWarning".Translate(), ref Settings.enableRaidWarning);
            listing.CheckboxLabeled("WR_Settings_FireWatch".Translate(), ref Settings.enableFireWatch);
            listing.CheckboxLabeled("WR_Settings_DayWatch".Translate(), ref Settings.allowDayWatch);
            listing.CheckboxLabeled("WR_Settings_EasyMode".Translate(), ref Settings.easyMode);
            if (Settings.easyMode)
            {
                listing.Label("WR_Settings_EasyModeHint".Translate());
            }

            listing.Gap();
            listing.Label("WR_Settings_NeverDoors".Translate().Colorize(ColoredText.SubtleGrayColor));
            listing.Label("WR_Settings_NeverRaidPoints".Translate().Colorize(ColoredText.SubtleGrayColor));

            listing.End();
            Widgets.EndScrollView();

            Rect resetRect = new Rect(inRect.x, inRect.yMax - 32f, 220f, 30f);
            if (Widgets.ButtonText(resetRect, "WR_Settings_Reset".Translate()))
            {
                Settings.ResetToDefaults();
            }

            base.DoSettingsWindowContents(inRect);
        }
    }

    public class WatchRosterSettings : ModSettings
    {
        public bool masterEnabled = true;
        public float maxWatchPosts = 3f;
        public float nightStartHour = 20f;
        public float nightEndHour = 5f;
        public float warnMinHours = 0.5f;
        public float warnMaxHours = 1.5f;
        public float fireMannedIntervalMult = 0.45f;
        public float fireEmptyIntervalMult = 1.6f;
        public bool enableRaidWarning = true;
        public bool enableFireWatch = true;
        public bool allowDayWatch;
        public bool easyMode;

        public int MaxWatchPosts => Mathf.Clamp(Mathf.RoundToInt(maxWatchPosts), 1, 5);
        public int NightStartHour => Mathf.Clamp(Mathf.RoundToInt(nightStartHour), 0, 23);
        public int NightEndHour => Mathf.Clamp(Mathf.RoundToInt(nightEndHour), 0, 23);

        public float WarnMinHours => easyMode ? Mathf.Max(warnMinHours, 0.75f) : warnMinHours;
        public float WarnMaxHours => easyMode ? Mathf.Max(warnMaxHours, 1.75f) : warnMaxHours;

        public float FireMannedMult => easyMode
            ? Mathf.Min(fireMannedIntervalMult, 0.35f)
            : fireMannedIntervalMult;

        public float FireEmptyMult => easyMode
            ? 1f
            : fireEmptyIntervalMult;

        public int RollWarningDelayTicks()
        {
            float min = Mathf.Min(WarnMinHours, WarnMaxHours);
            float max = Mathf.Max(WarnMinHours, WarnMaxHours);
            float hours = Rand.Range(min, max);
            return Mathf.Max(250, Mathf.RoundToInt(hours * GenDate.TicksPerHour));
        }

        public void ResetToDefaults()
        {
            masterEnabled = true;
            maxWatchPosts = 3f;
            nightStartHour = 20f;
            nightEndHour = 5f;
            warnMinHours = 0.5f;
            warnMaxHours = 1.5f;
            fireMannedIntervalMult = 0.45f;
            fireEmptyIntervalMult = 1.6f;
            enableRaidWarning = true;
            enableFireWatch = true;
            allowDayWatch = false;
            easyMode = false;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref masterEnabled, "masterEnabled", true);
            Scribe_Values.Look(ref maxWatchPosts, "maxWatchPosts", 3f);
            Scribe_Values.Look(ref nightStartHour, "nightStartHour", 20f);
            Scribe_Values.Look(ref nightEndHour, "nightEndHour", 5f);
            Scribe_Values.Look(ref warnMinHours, "warnMinHours", 0.5f);
            Scribe_Values.Look(ref warnMaxHours, "warnMaxHours", 1.5f);
            Scribe_Values.Look(ref fireMannedIntervalMult, "fireMannedIntervalMult", 0.45f);
            Scribe_Values.Look(ref fireEmptyIntervalMult, "fireEmptyIntervalMult", 1.6f);
            Scribe_Values.Look(ref enableRaidWarning, "enableRaidWarning", true);
            Scribe_Values.Look(ref enableFireWatch, "enableFireWatch", true);
            Scribe_Values.Look(ref allowDayWatch, "allowDayWatch", false);
            Scribe_Values.Look(ref easyMode, "easyMode", false);
        }
    }
}
