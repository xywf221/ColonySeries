using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Fieldcraft
{
    public class FieldcraftMod : Mod
    {
        public static FieldcraftSettings Settings;
        private Vector2 settingsScroll;

        public FieldcraftMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<FieldcraftSettings>();
            var harmony = new Harmony("fieldcraft.soilbudget");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            LongEventHandler.ExecuteWhenFinished(LandworksBridge.Init);
        }

        public override string SettingsCategory() => "FC_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect scrollOuter = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, 980f);
            Widgets.BeginScrollView(scrollOuter, ref settingsScroll, view);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);

            listing.Label("FC_Settings_SectionBalance".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.Label("FC_Settings_HarvestDrain".Translate(Settings.harvestDrainMultiplier.ToString("F2")));
            Settings.harvestDrainMultiplier = listing.Slider(Settings.harvestDrainMultiplier, 0.1f, 3f);

            listing.Label("FC_Settings_NaturalLeak".Translate(Settings.naturalLeakPerDay.ToString("F2")));
            Settings.naturalLeakPerDay = listing.Slider(Settings.naturalLeakPerDay, 0f, 5f);

            listing.Label("FC_Settings_TopdressRecharge".Translate(Settings.topdressRecharge.ToString("F0")));
            Settings.topdressRecharge = listing.Slider(Settings.topdressRecharge, 10f, 100f);

            listing.Label("FC_Settings_ResidueChance".Translate(Settings.residueReturnChance.ToStringPercent()));
            Settings.residueReturnChance = listing.Slider(Settings.residueReturnChance, 0f, 1f);

            listing.Label("FC_Settings_ResidueBudget".Translate(Settings.residueBudgetReturn.ToString("F1")));
            Settings.residueBudgetReturn = listing.Slider(Settings.residueBudgetReturn, 0f, 20f);

            listing.Label("FC_Settings_ZeroBudgetDays".Translate(Settings.zeroBudgetExhaustDays.ToString("F1")));
            Settings.zeroBudgetExhaustDays = listing.Slider(Settings.zeroBudgetExhaustDays, 0.5f, 15f);

            listing.Label("FC_Settings_FallowRecharge".Translate(Settings.fallowRechargePerDay.ToString("F1")));
            Settings.fallowRechargePerDay = listing.Slider(Settings.fallowRechargePerDay, 0f, 15f);

            listing.Label("FC_Settings_GreenManureMult".Translate(Settings.greenManureMultiplier.ToString("F2")));
            Settings.greenManureMultiplier = listing.Slider(Settings.greenManureMultiplier, 0.25f, 3f);

            listing.Label("FC_Settings_MonoCropBonus".Translate(Settings.monoCropDrainBonus.ToStringPercent()));
            Settings.monoCropDrainBonus = listing.Slider(Settings.monoCropDrainBonus, 0f, 1f);

            listing.Gap();
            listing.Label("FC_Settings_SectionPerformance".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.Label("FC_Settings_PerformanceHint".Translate());

            listing.Label("FC_Settings_RareTickInterval".Translate(Settings.rareTickInterval.ToString("F0")));
            Settings.rareTickInterval = Mathf.Round(listing.Slider(Settings.rareTickInterval, 1000f, 10000f) / 100f) * 100f;

            listing.Label("FC_Settings_MaxCellsPerPulse".Translate(Settings.maxCellsPerPulse.ToString("F0")));
            Settings.maxCellsPerPulse = Mathf.Round(listing.Slider(Settings.maxCellsPerPulse, 8f, 256f));

            listing.Gap();
            listing.Label("FC_Settings_SectionToggles".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("FC_Settings_TrackNaturalSoil".Translate(), ref Settings.trackNaturalSoil);
            listing.CheckboxLabeled("FC_Settings_ResidueSpawnCompost".Translate(), ref Settings.residueMaySpawnCompost);
            listing.CheckboxLabeled("FC_Settings_MonoCropPenalty".Translate(), ref Settings.monoCropPenalty);
            listing.CheckboxLabeled("FC_Settings_EasyMode".Translate(), ref Settings.easyMode);
            if (Settings.easyMode)
            {
                listing.Label("FC_Settings_EasyModeHint".Translate());
            }

            listing.Gap();
            string bridge = LandworksBridge.Active
                ? "FC_Settings_LandworksLinked".Translate()
                : "FC_Settings_LandworksSolo".Translate();
            listing.Label(bridge);

            listing.End();
            Widgets.EndScrollView();

            Rect resetRect = new Rect(inRect.x, inRect.yMax - 32f, 220f, 30f);
            if (Widgets.ButtonText(resetRect, "FC_Settings_Reset".Translate()))
            {
                Settings.ResetToDefaults();
            }

            base.DoSettingsWindowContents(inRect);
        }
    }

    public class FieldcraftSettings : ModSettings
    {
        public float harvestDrainMultiplier = 1f;
        public float naturalLeakPerDay = 0.8f;
        public float topdressRecharge = 40f;
        public float residueReturnChance = 0.35f;
        public float residueBudgetReturn = 6f;
        public float zeroBudgetExhaustDays = 3f;
        public float fallowRechargePerDay = 4f;
        public float greenManureMultiplier = 1f;
        public float monoCropDrainBonus = 0.35f;
        public float rareTickInterval = 2500f;
        public float maxCellsPerPulse = 64f;
        public bool trackNaturalSoil = true;
        public bool residueMaySpawnCompost = true;
        public bool monoCropPenalty = true;
        public bool easyMode;

        public float EasyMult => easyMode ? 0.45f : 1f;
        public float EasyLeakMult => easyMode ? 0.5f : 1f;
        public int RareTickIntervalTicks => Mathf.Clamp(Mathf.RoundToInt(rareTickInterval), 1000, 10000);
        public int MaxCellsPerPulse => Mathf.Clamp(Mathf.RoundToInt(maxCellsPerPulse), 8, 256);

        public void ResetToDefaults()
        {
            harvestDrainMultiplier = 1f;
            naturalLeakPerDay = 0.8f;
            topdressRecharge = 40f;
            residueReturnChance = 0.35f;
            residueBudgetReturn = 6f;
            zeroBudgetExhaustDays = 3f;
            fallowRechargePerDay = 4f;
            greenManureMultiplier = 1f;
            monoCropDrainBonus = 0.35f;
            rareTickInterval = 2500f;
            maxCellsPerPulse = 64f;
            trackNaturalSoil = true;
            residueMaySpawnCompost = true;
            monoCropPenalty = true;
            easyMode = false;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref harvestDrainMultiplier, "harvestDrainMultiplier", 1f);
            Scribe_Values.Look(ref naturalLeakPerDay, "naturalLeakPerDay", 0.8f);
            Scribe_Values.Look(ref topdressRecharge, "topdressRecharge", 40f);
            Scribe_Values.Look(ref residueReturnChance, "residueReturnChance", 0.35f);
            Scribe_Values.Look(ref residueBudgetReturn, "residueBudgetReturn", 6f);
            Scribe_Values.Look(ref zeroBudgetExhaustDays, "zeroBudgetExhaustDays", 3f);
            Scribe_Values.Look(ref fallowRechargePerDay, "fallowRechargePerDay", 4f);
            Scribe_Values.Look(ref greenManureMultiplier, "greenManureMultiplier", 1f);
            Scribe_Values.Look(ref monoCropDrainBonus, "monoCropDrainBonus", 0.35f);
            Scribe_Values.Look(ref rareTickInterval, "rareTickInterval", 2500f);
            Scribe_Values.Look(ref maxCellsPerPulse, "maxCellsPerPulse", 64f);
            Scribe_Values.Look(ref trackNaturalSoil, "trackNaturalSoil", true);
            Scribe_Values.Look(ref residueMaySpawnCompost, "residueMaySpawnCompost", true);
            Scribe_Values.Look(ref monoCropPenalty, "monoCropPenalty", true);
            Scribe_Values.Look(ref easyMode, "easyMode", false);
        }
    }
}
