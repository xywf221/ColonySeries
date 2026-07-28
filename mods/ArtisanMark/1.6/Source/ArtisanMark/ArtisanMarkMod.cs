using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ArtisanMark
{
    public class ArtisanMarkMod : Mod
    {
        public static ArtisanMarkSettings Settings;
        private Vector2 settingsScroll;

        public ArtisanMarkMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ArtisanMarkSettings>();
            new Harmony("artisanmark.maker").PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "AM_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect scrollOuter = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, 560f);
            Widgets.BeginScrollView(scrollOuter, ref settingsScroll, view);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);

            listing.Label("AM_Settings_SectionToggles".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("AM_Settings_Enabled".Translate(), ref Settings.modEnabled);
            listing.CheckboxLabeled("AM_Settings_StampWeapons".Translate(), ref Settings.stampWeapons);
            listing.CheckboxLabeled("AM_Settings_StampApparel".Translate(), ref Settings.stampApparel);
            listing.CheckboxLabeled("AM_Settings_StampArt".Translate(), ref Settings.stampArt);
            listing.CheckboxLabeled("AM_Settings_StampProstheses".Translate(), ref Settings.stampProstheses);
            listing.CheckboxLabeled("AM_Settings_ShowInspect".Translate(), ref Settings.showInspectString);

            listing.Gap();
            listing.Label("AM_Settings_SectionMood".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("AM_Settings_MoodEnabled".Translate(), ref Settings.enableMoodThoughts);
            listing.Label("AM_Settings_SelfMadeMood".Translate(Settings.selfMadeMood.ToString("F0")));
            Settings.selfMadeMood = listing.Slider(Settings.selfMadeMood, 0f, 5f);
            listing.Label("AM_Settings_LegacyMood".Translate(Settings.legacyMood.ToString("F0")));
            Settings.legacyMood = listing.Slider(Settings.legacyMood, 0f, 8f);
            listing.Label("AM_Settings_LegacyHint".Translate());

            listing.Gap();
            listing.Label("AM_Settings_SectionCombat".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("AM_Settings_CombatBonus".Translate(), ref Settings.enableCombatBonus);
            if (Settings.enableCombatBonus)
            {
                listing.Label("AM_Settings_CombatBonusPct".Translate((Settings.combatBonusPercent * 100f).ToString("F0")));
                Settings.combatBonusPercent = listing.Slider(Settings.combatBonusPercent, 0.01f, 0.10f);
                listing.Label("AM_Settings_CombatBonusHint".Translate());
            }

            listing.End();
            Widgets.EndScrollView();

            Rect resetRect = new Rect(inRect.x, inRect.yMax - 32f, 220f, 30f);
            if (Widgets.ButtonText(resetRect, "AM_Settings_Reset".Translate()))
            {
                Settings.ResetToDefaults();
            }

            base.DoSettingsWindowContents(inRect);
        }
    }

    public class ArtisanMarkSettings : ModSettings
    {
        public bool modEnabled = true;
        public bool stampWeapons = true;
        public bool stampApparel = true;
        public bool stampArt = true;
        public bool stampProstheses = true;
        public bool showInspectString = true;
        public bool enableMoodThoughts = true;
        public float selfMadeMood = 2f;
        public float legacyMood = 4f;
        /// <summary>Default OFF. Same-room living maker → +2% hit/damage cap, no multi-stack.</summary>
        public bool enableCombatBonus;
        public float combatBonusPercent = 0.02f;

        public void ResetToDefaults()
        {
            modEnabled = true;
            stampWeapons = true;
            stampApparel = true;
            stampArt = true;
            stampProstheses = true;
            showInspectString = true;
            enableMoodThoughts = true;
            selfMadeMood = 2f;
            legacyMood = 4f;
            enableCombatBonus = false;
            combatBonusPercent = 0.02f;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref modEnabled, "modEnabled", true);
            Scribe_Values.Look(ref stampWeapons, "stampWeapons", true);
            Scribe_Values.Look(ref stampApparel, "stampApparel", true);
            Scribe_Values.Look(ref stampArt, "stampArt", true);
            Scribe_Values.Look(ref stampProstheses, "stampProstheses", true);
            Scribe_Values.Look(ref showInspectString, "showInspectString", true);
            Scribe_Values.Look(ref enableMoodThoughts, "enableMoodThoughts", true);
            Scribe_Values.Look(ref selfMadeMood, "selfMadeMood", 2f);
            Scribe_Values.Look(ref legacyMood, "legacyMood", 4f);
            Scribe_Values.Look(ref enableCombatBonus, "enableCombatBonus", false);
            Scribe_Values.Look(ref combatBonusPercent, "combatBonusPercent", 0.02f);
        }
    }
}
