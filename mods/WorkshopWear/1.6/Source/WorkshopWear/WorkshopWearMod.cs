using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace WorkshopWear
{
    public class WorkshopWearMod : Mod
    {
        public static WorkshopWearSettings Settings;
        private Vector2 settingsScroll;

        public WorkshopWearMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<WorkshopWearSettings>();
            new Harmony("workshopwear.maintenance").PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "WW_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect scrollOuter = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, 720f);
            Widgets.BeginScrollView(scrollOuter, ref settingsScroll, view);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);

            listing.Label("WW_Settings_SectionBalance".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.Label("WW_Settings_WearPerBill".Translate(Settings.wearPerBill.ToString("F2")));
            Settings.wearPerBill = listing.Slider(Settings.wearPerBill, 0.2f, 3f);

            listing.Label("WW_Settings_WornThreshold".Translate(Settings.wornThreshold.ToString("F0")));
            Settings.wornThreshold = listing.Slider(Settings.wornThreshold, 40f, 100f);

            listing.Label("WW_Settings_WornSpeed".Translate(Settings.wornSpeedFactor.ToStringPercent()));
            Settings.wornSpeedFactor = listing.Slider(Settings.wornSpeedFactor, 0.55f, 0.95f);

            listing.Label("WW_Settings_OverworkSpeed".Translate(Settings.overworkSpeedBonus.ToStringPercent()));
            Settings.overworkSpeedBonus = listing.Slider(Settings.overworkSpeedBonus, 0.1f, 0.5f);

            listing.Label("WW_Settings_JamChance".Translate(Settings.overworkJamChance.ToStringPercent()));
            Settings.overworkJamChance = listing.Slider(Settings.overworkJamChance, 0.02f, 0.4f);

            listing.Gap();
            listing.Label("WW_Settings_SectionToggles".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("WW_Settings_Enabled".Translate(), ref Settings.modEnabled);
            listing.CheckboxLabeled("WW_Settings_Overwork".Translate(), ref Settings.enableOverwork);
            listing.CheckboxLabeled("WW_Settings_Easy".Translate(), ref Settings.easyMode);
            if (Settings.easyMode)
            {
                listing.Label("WW_Settings_EasyHint".Translate());
            }
            listing.CheckboxLabeled("WW_Settings_IncludeResearch".Translate(), ref Settings.includeResearchBenches);
            listing.CheckboxLabeled("WW_Settings_IncludeSculpting".Translate(), ref Settings.includeSculptingTables);
            listing.CheckboxLabeled("WW_Settings_QualityRisk".Translate(), ref Settings.overworkQualityRisk);
            listing.CheckboxLabeled("WW_Settings_DevExplode".Translate(), ref Settings.devJamExplosions);

            listing.End();
            Widgets.EndScrollView();

            Rect resetRect = new Rect(inRect.x, inRect.yMax - 32f, 220f, 30f);
            if (Widgets.ButtonText(resetRect, "WW_Settings_Reset".Translate()))
            {
                Settings.ResetToDefaults();
            }

            base.DoSettingsWindowContents(inRect);
        }
    }

    public class WorkshopWearSettings : ModSettings
    {
        public bool modEnabled = true;

        /// <summary>~100 bills to Worn at default 1.0 and wornThreshold 100.</summary>
        public float wearPerBill = 1.0f;

        /// <summary>Wear value at which prose state becomes Worn (max conceptual scale).</summary>
        public float wornThreshold = 100f;

        /// <summary>Modest slowdown at full Worn — not a 45% death spiral.</summary>
        public float wornSpeedFactor = 0.78f;

        /// <summary>Added to 1.0 during overwork burst (0.25 => +25%).</summary>
        public float overworkSpeedBonus = 0.25f;

        /// <summary>Base jam chance when overwork ends (scaled by wear).</summary>
        public float overworkJamChance = 0.12f;

        public bool enableOverwork = true;
        public bool easyMode;
        public bool includeResearchBenches;
        public bool includeSculptingTables;

        /// <summary>Deviation: tiny quality-down risk while overworking.</summary>
        public bool overworkQualityRisk = true;

        /// <summary>Dev / extreme only: jam may spark a tiny fire. Off by default.</summary>
        public bool devJamExplosions;

        public float WearMult => easyMode ? 0.5f : 1f;
        public bool JamsAllowed => !easyMode;

        public void ResetToDefaults()
        {
            modEnabled = true;
            wearPerBill = 1.0f;
            wornThreshold = 100f;
            wornSpeedFactor = 0.78f;
            overworkSpeedBonus = 0.25f;
            overworkJamChance = 0.12f;
            enableOverwork = true;
            easyMode = false;
            includeResearchBenches = false;
            includeSculptingTables = false;
            overworkQualityRisk = true;
            devJamExplosions = false;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref modEnabled, "modEnabled", true);
            Scribe_Values.Look(ref wearPerBill, "wearPerBill", 1.0f);
            Scribe_Values.Look(ref wornThreshold, "wornThreshold", 100f);
            Scribe_Values.Look(ref wornSpeedFactor, "wornSpeedFactor", 0.78f);
            Scribe_Values.Look(ref overworkSpeedBonus, "overworkSpeedBonus", 0.25f);
            Scribe_Values.Look(ref overworkJamChance, "overworkJamChance", 0.12f);
            Scribe_Values.Look(ref enableOverwork, "enableOverwork", true);
            Scribe_Values.Look(ref easyMode, "easyMode", false);
            Scribe_Values.Look(ref includeResearchBenches, "includeResearchBenches", false);
            Scribe_Values.Look(ref includeSculptingTables, "includeSculptingTables", false);
            Scribe_Values.Look(ref overworkQualityRisk, "overworkQualityRisk", true);
            Scribe_Values.Look(ref devJamExplosions, "devJamExplosions", false);
        }
    }
}
