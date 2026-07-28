using UnityEngine;
using Verse;

namespace TraitExtractor
{
    public class TraitExtractorMod : Mod
    {
        public static TraitExtractorSettings Settings;
        private Vector2 scroll;

        public TraitExtractorMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<TraitExtractorSettings>();
        }

        public override string SettingsCategory() => "TE_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect outer = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, outer.width - 20f, 720f);
            Widgets.BeginScrollView(outer, ref scroll, view);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);

            listing.Label("TE_Settings_SectionBalance".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.Label("TE_Settings_MaxTraits".Translate(Settings.maxTraits.ToString("F0")));
            Settings.maxTraits = Mathf.Round(listing.Slider(Settings.maxTraits, 1f, 8f));

            listing.Label("TE_Settings_InjectSuccess".Translate((Settings.injectSuccessChance * 100f).ToString("F0")));
            Settings.injectSuccessChance = listing.Slider(Settings.injectSuccessChance, 0.25f, 1f);

            listing.Label("TE_Settings_ScarChance".Translate((Settings.permanentScarChance * 100f).ToString("F0")));
            Settings.permanentScarChance = listing.Slider(Settings.permanentScarChance, 0f, 1f);

            listing.Label("TE_Settings_CooldownDays".Translate(Settings.cooldownDays.ToString("F1")));
            Settings.cooldownDays = listing.Slider(Settings.cooldownDays, 1f, 30f);

            listing.Gap();
            listing.Label("TE_Settings_SectionToggles".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("TE_Settings_AllowColonistExtract".Translate(), ref Settings.allowExtractFromColonists);
            listing.CheckboxLabeled("TE_Settings_AllowConflictReplace".Translate(), ref Settings.allowConflictReplace);
            listing.CheckboxLabeled("TE_Settings_BlockGeneTraits".Translate(), ref Settings.blockGeneBoundTraits);
            listing.CheckboxLabeled("TE_Settings_BlockScenForced".Translate(), ref Settings.blockScenarioForced);
            listing.CheckboxLabeled("TE_Settings_EasyMode".Translate(), ref Settings.easyMode);
            if (Settings.easyMode)
            {
                listing.Label("TE_Settings_EasyModeHint".Translate());
            }

            listing.End();
            Widgets.EndScrollView();

            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 32f, 220f, 30f), "TE_Settings_Reset".Translate()))
            {
                Settings.ResetToDefaults();
            }

            base.DoSettingsWindowContents(inRect);
        }
    }

    public class TraitExtractorSettings : ModSettings
    {
        public float maxTraits = 4f;
        public float injectSuccessChance = 0.85f;
        public float permanentScarChance = 0.35f;
        public float cooldownDays = 12f;
        public bool allowExtractFromColonists = true;
        public bool allowConflictReplace = true;
        public bool blockGeneBoundTraits = true;
        public bool blockScenarioForced = true;
        public bool easyMode;

        public int MaxTraits => Mathf.Clamp(Mathf.RoundToInt(maxTraits), 1, 12);
        public float InjectSuccess => easyMode ? Mathf.Clamp01(injectSuccessChance + 0.1f) : injectSuccessChance;
        public float ScarChance => easyMode ? permanentScarChance * 0.4f : permanentScarChance;
        public float CooldownDays => easyMode ? cooldownDays * 0.5f : cooldownDays;

        public void ResetToDefaults()
        {
            maxTraits = 4f;
            injectSuccessChance = 0.85f;
            permanentScarChance = 0.35f;
            cooldownDays = 12f;
            allowExtractFromColonists = true;
            allowConflictReplace = true;
            blockGeneBoundTraits = true;
            blockScenarioForced = true;
            easyMode = false;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref maxTraits, "maxTraits", 4f);
            Scribe_Values.Look(ref injectSuccessChance, "injectSuccessChance", 0.85f);
            Scribe_Values.Look(ref permanentScarChance, "permanentScarChance", 0.35f);
            Scribe_Values.Look(ref cooldownDays, "cooldownDays", 12f);
            Scribe_Values.Look(ref allowExtractFromColonists, "allowExtractFromColonists", true);
            Scribe_Values.Look(ref allowConflictReplace, "allowConflictReplace", true);
            Scribe_Values.Look(ref blockGeneBoundTraits, "blockGeneBoundTraits", true);
            Scribe_Values.Look(ref blockScenarioForced, "blockScenarioForced", true);
            Scribe_Values.Look(ref easyMode, "easyMode", false);
        }
    }
}
