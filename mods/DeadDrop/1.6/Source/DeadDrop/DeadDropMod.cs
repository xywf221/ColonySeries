using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeadDrop
{
    public class DeadDropMod : Mod
    {
        public static DeadDropSettings Settings;
        private Vector2 settingsScroll;

        public DeadDropMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<DeadDropSettings>();
            var harmony = new Harmony("deaddrop.greymarket");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "DD_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect scrollOuter = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, 720f);
            Widgets.BeginScrollView(scrollOuter, ref settingsScroll, view);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);

            listing.CheckboxLabeled("DD_Settings_MasterEnabled".Translate(), ref Settings.masterEnabled);
            listing.GapLine();

            listing.Label("DD_Settings_SectionBalance".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.Label("DD_Settings_RefreshMinDays".Translate(Settings.refreshMinDays.ToString("F1")));
            Settings.refreshMinDays = listing.Slider(Settings.refreshMinDays, 1f, 10f);
            if (Settings.refreshMaxDays < Settings.refreshMinDays)
            {
                Settings.refreshMaxDays = Settings.refreshMinDays;
            }

            listing.Label("DD_Settings_RefreshMaxDays".Translate(Settings.refreshMaxDays.ToString("F1")));
            Settings.refreshMaxDays = listing.Slider(Settings.refreshMaxDays, 2f, 14f);
            if (Settings.refreshMaxDays < Settings.refreshMinDays)
            {
                Settings.refreshMinDays = Settings.refreshMaxDays;
            }

            listing.Label("DD_Settings_ProfitMin".Translate(Settings.profitMin.ToString("F2")));
            Settings.profitMin = listing.Slider(Settings.profitMin, 1.0f, 2.5f);
            if (Settings.profitMax < Settings.profitMin)
            {
                Settings.profitMax = Settings.profitMin;
            }

            listing.Label("DD_Settings_ProfitMax".Translate(Settings.profitMax.ToString("F2")));
            Settings.profitMax = listing.Slider(Settings.profitMax, 1.1f, 3.0f);
            if (Settings.profitMax < Settings.profitMin)
            {
                Settings.profitMin = Settings.profitMax;
            }

            listing.Label("DD_Settings_DiscoveryChance".Translate(Settings.baseDiscoveryChance.ToStringPercent()));
            Settings.baseDiscoveryChance = listing.Slider(Settings.baseDiscoveryChance, 0.05f, 0.60f);

            listing.Label("DD_Settings_SearchPartyChance".Translate(Settings.searchPartyChance.ToStringPercent()));
            Settings.searchPartyChance = listing.Slider(Settings.searchPartyChance, 0f, 1f);

            listing.Label("DD_Settings_GoodwillHitMin".Translate(Settings.goodwillHitMin.ToString("F0")));
            Settings.goodwillHitMin = Mathf.Round(listing.Slider(Settings.goodwillHitMin, 5f, 40f));
            if (Settings.goodwillHitMax < Settings.goodwillHitMin)
            {
                Settings.goodwillHitMax = Settings.goodwillHitMin;
            }

            listing.Label("DD_Settings_GoodwillHitMax".Translate(Settings.goodwillHitMax.ToString("F0")));
            Settings.goodwillHitMax = Mathf.Round(listing.Slider(Settings.goodwillHitMax, 10f, 50f));
            if (Settings.goodwillHitMax < Settings.goodwillHitMin)
            {
                Settings.goodwillHitMin = Settings.goodwillHitMax;
            }

            listing.Gap();
            listing.Label("DD_Settings_SectionToggles".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("DD_Settings_AllowSearchParty".Translate(), ref Settings.allowSearchParty);
            listing.CheckboxLabeled("DD_Settings_HonestRefuse".Translate(), ref Settings.honestPawnsRefuse);
            listing.CheckboxLabeled("DD_Settings_EasyMode".Translate(), ref Settings.easyMode);
            if (Settings.easyMode)
            {
                listing.Label("DD_Settings_EasyModeHint".Translate());
            }

            listing.Gap();
            listing.Label("DD_Settings_TraitExtractor".Translate(
                TraitSerumBridge.SerumDef != null
                    ? "DD_Settings_TE_Linked".Translate()
                    : "DD_Settings_TE_Solo".Translate()));

            listing.End();
            Widgets.EndScrollView();

            Rect resetRect = new Rect(inRect.x, inRect.yMax - 32f, 220f, 30f);
            if (Widgets.ButtonText(resetRect, "DD_Settings_Reset".Translate()))
            {
                Settings.ResetToDefaults();
            }

            base.DoSettingsWindowContents(inRect);
        }
    }

    public class DeadDropSettings : ModSettings
    {
        public bool masterEnabled = true;
        public float refreshMinDays = 3f;
        public float refreshMaxDays = 7f;
        public float profitMin = 1.3f;
        public float profitMax = 2.0f;
        public float baseDiscoveryChance = 0.22f;
        public float searchPartyChance = 0.40f;
        public float goodwillHitMin = 15f;
        public float goodwillHitMax = 30f;
        public bool allowSearchParty = true;
        public bool honestPawnsRefuse = true;
        public bool easyMode;

        public float DiscoveryMult => easyMode ? 0.5f : 1f;
        public bool SearchPartyAllowed => allowSearchParty && !easyMode;

        public int RollRefreshDelayTicks()
        {
            float min = Mathf.Min(refreshMinDays, refreshMaxDays);
            float max = Mathf.Max(refreshMinDays, refreshMaxDays);
            float days = Rand.Range(min, max);
            return Mathf.Max(1, Mathf.RoundToInt(days * GenDate.TicksPerDay));
        }

        public float RollProfitMult()
        {
            float min = Mathf.Min(profitMin, profitMax);
            float max = Mathf.Max(profitMin, profitMax);
            return Rand.Range(min, max);
        }

        public int RollGoodwillHit()
        {
            int min = Mathf.RoundToInt(Mathf.Min(goodwillHitMin, goodwillHitMax));
            int max = Mathf.RoundToInt(Mathf.Max(goodwillHitMin, goodwillHitMax));
            return Rand.RangeInclusive(min, max);
        }

        public void ResetToDefaults()
        {
            masterEnabled = true;
            refreshMinDays = 3f;
            refreshMaxDays = 7f;
            profitMin = 1.3f;
            profitMax = 2.0f;
            baseDiscoveryChance = 0.22f;
            searchPartyChance = 0.40f;
            goodwillHitMin = 15f;
            goodwillHitMax = 30f;
            allowSearchParty = true;
            honestPawnsRefuse = true;
            easyMode = false;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref masterEnabled, "masterEnabled", true);
            Scribe_Values.Look(ref refreshMinDays, "refreshMinDays", 3f);
            Scribe_Values.Look(ref refreshMaxDays, "refreshMaxDays", 7f);
            Scribe_Values.Look(ref profitMin, "profitMin", 1.3f);
            Scribe_Values.Look(ref profitMax, "profitMax", 2.0f);
            Scribe_Values.Look(ref baseDiscoveryChance, "baseDiscoveryChance", 0.22f);
            Scribe_Values.Look(ref searchPartyChance, "searchPartyChance", 0.40f);
            Scribe_Values.Look(ref goodwillHitMin, "goodwillHitMin", 15f);
            Scribe_Values.Look(ref goodwillHitMax, "goodwillHitMax", 30f);
            Scribe_Values.Look(ref allowSearchParty, "allowSearchParty", true);
            Scribe_Values.Look(ref honestPawnsRefuse, "honestPawnsRefuse", true);
            Scribe_Values.Look(ref easyMode, "easyMode", false);
        }
    }
}
