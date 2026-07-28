using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalKit
{
    public class PersonalKitMod : Mod
    {
        public static PersonalKitSettings Settings;

        /// <summary>Vanilla 1.6 ships with ~39.5% hard cap.</summary>
        public const float VanillaTradePriceMax = 0.395f;

        public PersonalKitMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<PersonalKitSettings>();
            // Defs are not fully ready in Mod ctor; apply after defs load.
            LongEventHandler.ExecuteWhenFinished(ApplyAll);
        }

        public override string SettingsCategory() => "PersonalKit_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("PersonalKit_Section_Trade".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.Label("PersonalKit_TradePrice_Desc".Translate());
            listing.Gap(6f);

            listing.CheckboxLabeled(
                "PersonalKit_TradePrice_Uncapped".Translate(),
                ref Settings.tradePriceImprovementUncapped,
                "PersonalKit_TradePrice_UncappedTip".Translate());

            if (!Settings.tradePriceImprovementUncapped)
            {
                float percent = Settings.tradePriceImprovementMax * 100f;
                listing.Label("PersonalKit_TradePrice_Max".Translate(percent.ToString("F0")));
                // 40% (vanilla) .. 300%
                percent = listing.Slider(percent, 40f, 300f);
                Settings.tradePriceImprovementMax = percent / 100f;
            }
            else
            {
                listing.Label("PersonalKit_TradePrice_UncappedHint".Translate());
            }

            listing.Gap();
            if (listing.ButtonText("PersonalKit_ResetDefaults".Translate()))
            {
                Settings.ResetToDefaults();
            }

            if (listing.ButtonText("PersonalKit_RestoreVanillaCap".Translate()))
            {
                Settings.tradePriceImprovementUncapped = false;
                Settings.tradePriceImprovementMax = VanillaTradePriceMax;
            }

            listing.End();

            // Apply live so the player can open a trade and see the change without reload.
            ApplyAll();
            base.DoSettingsWindowContents(inRect);
        }

        public static void ApplyAll()
        {
            ApplyTradePriceImprovementCap();
        }

        public static void ApplyTradePriceImprovementCap()
        {
            StatDef stat = StatDefOf.TradePriceImprovement;
            if (stat == null)
            {
                return;
            }

            if (Settings == null || Settings.tradePriceImprovementUncapped)
            {
                // Effectively no cap (old behaviour). Keep a huge ceiling so FinalizeValue still has a number.
                stat.maxValue = 999f;
                stat.displayMaxWhenAboveOrEqual = false;
            }
            else
            {
                float max = Mathf.Clamp(Settings.tradePriceImprovementMax, 0f, 10f);
                // Never go below vanilla floor accidentally via bad save data for "custom" mode.
                if (max < 0.01f)
                {
                    max = VanillaTradePriceMax;
                }
                stat.maxValue = max;
                // Match vanilla UX when the value is near the configured ceiling.
                stat.displayMaxWhenAboveOrEqual = max <= 1.01f;
            }
        }
    }

    public class PersonalKitSettings : ModSettings
    {
        /// <summary>When true, remove the ~40% negotiation trade bonus cap (pre-1.6-style).</summary>
        public bool tradePriceImprovementUncapped = true;

        /// <summary>Used when uncapped is false. 1.0 = +100% price improvement.</summary>
        public float tradePriceImprovementMax = 1f;

        public void ResetToDefaults()
        {
            tradePriceImprovementUncapped = true;
            tradePriceImprovementMax = 1f;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref tradePriceImprovementUncapped, "tradePriceImprovementUncapped", true);
            Scribe_Values.Look(ref tradePriceImprovementMax, "tradePriceImprovementMax", 1f);
        }
    }
}
