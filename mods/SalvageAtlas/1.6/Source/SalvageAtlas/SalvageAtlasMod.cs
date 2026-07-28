using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace SalvageAtlas
{
    public class SalvageAtlasMod : Mod
    {
        public static SalvageAtlasSettings Settings;
        private Vector2 settingsScroll;

        public SalvageAtlasMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<SalvageAtlasSettings>();
            var harmony = new Harmony("salvageatlas.reverse");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "SA_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect scrollOuter = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, 780f);
            Widgets.BeginScrollView(scrollOuter, ref settingsScroll, view);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);

            listing.CheckboxLabeled("SA_Settings_MasterEnabled".Translate(), ref Settings.masterEnabled);
            listing.GapLine();

            listing.Label("SA_Settings_SectionBalance".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.Label("SA_Settings_FragmentMult".Translate(Settings.fragmentYieldMult.ToString("F2")));
            Settings.fragmentYieldMult = listing.Slider(Settings.fragmentYieldMult, 0.25f, 3f);

            listing.Label("SA_Settings_AnalyzeWork".Translate(Settings.analyzeWorkMult.ToString("F2")));
            Settings.analyzeWorkMult = listing.Slider(Settings.analyzeWorkMult, 0.25f, 3f);

            listing.Label("SA_Settings_TeardownRisk".Translate(Settings.teardownRiskMult.ToString("F2")));
            Settings.teardownRiskMult = listing.Slider(Settings.teardownRiskMult, 0.1f, 2.5f);

            listing.Label("SA_Settings_SalvageWork".Translate(Settings.salvageWorkMult.ToString("F2")));
            Settings.salvageWorkMult = listing.Slider(Settings.salvageWorkMult, 0.25f, 3f);

            listing.Gap();
            listing.Label("SA_Settings_SectionToggles".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("SA_Settings_AllowFire".Translate(), ref Settings.allowFireOnFail);
            listing.CheckboxLabeled("SA_Settings_AllowExplosion".Translate(), ref Settings.allowExplosionOnFail);
            listing.CheckboxLabeled("SA_Settings_LetterOnUnlock".Translate(), ref Settings.letterOnUnlock);
            listing.CheckboxLabeled("SA_Settings_EasyMode".Translate(), ref Settings.easyMode);
            if (Settings.easyMode)
            {
                listing.Label("SA_Settings_EasyModeHint".Translate());
            }

            listing.Gap();
            listing.Label("SA_Settings_IdleHint".Translate());

            listing.End();
            Widgets.EndScrollView();

            Rect resetRect = new Rect(inRect.x, inRect.yMax - 32f, 220f, 30f);
            if (Widgets.ButtonText(resetRect, "SA_Settings_Reset".Translate()))
            {
                Settings.ResetToDefaults();
            }

            base.DoSettingsWindowContents(inRect);
        }
    }

    public class SalvageAtlasSettings : ModSettings
    {
        public bool masterEnabled = true;
        public float fragmentYieldMult = 1f;
        public float analyzeWorkMult = 1f;
        public float teardownRiskMult = 1f;
        public float salvageWorkMult = 1f;
        public bool allowFireOnFail = true;
        public bool allowExplosionOnFail = true;
        public bool letterOnUnlock = true;
        public bool easyMode;

        public float YieldMult => fragmentYieldMult * (easyMode ? 1.5f : 1f);
        public float AnalyzeWorkMult => analyzeWorkMult * (easyMode ? 0.7f : 1f);
        public float SalvageWorkMult => salvageWorkMult * (easyMode ? 0.75f : 1f);
        public float RiskMult => teardownRiskMult * (easyMode ? 0.4f : 1f);
        public bool FireAllowed => allowFireOnFail && !easyMode;
        public bool ExplosionAllowed => allowExplosionOnFail; // easy still allows tiny sparks, but lower chance

        public void ResetToDefaults()
        {
            masterEnabled = true;
            fragmentYieldMult = 1f;
            analyzeWorkMult = 1f;
            teardownRiskMult = 1f;
            salvageWorkMult = 1f;
            allowFireOnFail = true;
            allowExplosionOnFail = true;
            letterOnUnlock = true;
            easyMode = false;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref masterEnabled, "masterEnabled", true);
            Scribe_Values.Look(ref fragmentYieldMult, "fragmentYieldMult", 1f);
            Scribe_Values.Look(ref analyzeWorkMult, "analyzeWorkMult", 1f);
            Scribe_Values.Look(ref teardownRiskMult, "teardownRiskMult", 1f);
            Scribe_Values.Look(ref salvageWorkMult, "salvageWorkMult", 1f);
            Scribe_Values.Look(ref allowFireOnFail, "allowFireOnFail", true);
            Scribe_Values.Look(ref allowExplosionOnFail, "allowExplosionOnFail", true);
            Scribe_Values.Look(ref letterOnUnlock, "letterOnUnlock", true);
            Scribe_Values.Look(ref easyMode, "easyMode", false);
        }
    }
}
