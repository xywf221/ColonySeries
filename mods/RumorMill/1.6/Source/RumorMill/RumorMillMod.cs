using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RumorMill
{
    public class RumorMillMod : Mod
    {
        public static RumorMillSettings Settings;
        private Vector2 settingsScroll;

        public RumorMillMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RumorMillSettings>();
            var harmony = new Harmony("rumormill.reputation");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            LongEventHandler.ExecuteWhenFinished(StatPartsInjector.EnsureInjected);
        }

        public override string SettingsCategory() => "RM_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect scrollOuter = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, 720f);
            Widgets.BeginScrollView(scrollOuter, ref settingsScroll, view);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);

            listing.CheckboxLabeled("RM_Settings_Enabled".Translate(), ref Settings.modEnabled);
            listing.CheckboxLabeled("RM_Settings_EasyMode".Translate(), ref Settings.easyMode);
            if (Settings.easyMode)
            {
                listing.Label("RM_Settings_EasyModeHint".Translate());
            }

            listing.GapLine();
            listing.Label("RM_Settings_SectionBalance".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.Label("RM_Settings_DecayDays".Translate(Settings.decayDays.ToString("F0")));
            Settings.decayDays = Mathf.Round(listing.Slider(Settings.decayDays, 15f, 90f));

            listing.Label("RM_Settings_RecruitEffect".Translate(Settings.recruitEffectPercent.ToString("F0")));
            Settings.recruitEffectPercent = Mathf.Round(listing.Slider(Settings.recruitEffectPercent, 0f, 15f));

            listing.Label("RM_Settings_TradeEffect".Translate(Settings.tradeEffectPercent.ToString("F0")));
            Settings.tradeEffectPercent = Mathf.Round(listing.Slider(Settings.tradeEffectPercent, 0f, 10f));

            listing.Label("RM_Settings_SocialEffect".Translate(Settings.socialOpinionEffect.ToString("F0")));
            Settings.socialOpinionEffect = Mathf.Round(listing.Slider(Settings.socialOpinionEffect, 0f, 20f));

            listing.Gap();
            listing.Label("RM_Settings_SectionAgency".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("RM_Settings_EnableCommend".Translate(), ref Settings.enableCommend);
            listing.CheckboxLabeled("RM_Settings_EnableChatSpread".Translate(), ref Settings.enableChatSpread);
            listing.Label("RM_Settings_ChatSpreadHint".Translate());

            listing.Gap();
            listing.Label("RM_Settings_SectionHooks".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("RM_Settings_HookReliable".Translate(), ref Settings.hookReliable);
            listing.CheckboxLabeled("RM_Settings_HookBloody".Translate(), ref Settings.hookBloody);
            listing.CheckboxLabeled("RM_Settings_HookSqueamish".Translate(), ref Settings.hookSqueamish);
            listing.CheckboxLabeled("RM_Settings_HookNerved".Translate(), ref Settings.hookNerved);
            listing.CheckboxLabeled("RM_Settings_HookWar".Translate(), ref Settings.hookWar);

            listing.End();
            Widgets.EndScrollView();

            Rect resetRect = new Rect(inRect.x, inRect.yMax - 32f, 220f, 30f);
            if (Widgets.ButtonText(resetRect, "RM_Settings_Reset".Translate()))
            {
                Settings.ResetToDefaults();
            }

            base.DoSettingsWindowContents(inRect);
        }
    }

    public class RumorMillSettings : ModSettings
    {
        public bool modEnabled = true;
        public bool easyMode;
        public float decayDays = 45f;
        public float recruitEffectPercent = 8f;
        public float tradeEffectPercent = 4f;
        public float socialOpinionEffect = 10f;
        public bool enableCommend = true;
        public bool enableChatSpread = true;
        public bool hookReliable = true;
        public bool hookBloody = true;
        public bool hookSqueamish = true;
        public bool hookNerved = true;
        public bool hookWar = true;

        public float EasyMagnitude => easyMode ? 0.55f : 1f;
        public float EasyDecayMult => easyMode ? 0.7f : 1f; // negatives fade faster in easy
        public int DecayTicks => Mathf.RoundToInt(Mathf.Clamp(decayDays, 15f, 90f) * GenDate.TicksPerDay);
        public float RecruitFactor => (recruitEffectPercent / 100f) * EasyMagnitude;
        public float TradeFactor => (tradeEffectPercent / 100f) * EasyMagnitude;
        public int SocialOpinion => Mathf.RoundToInt(socialOpinionEffect * EasyMagnitude);

        public void ResetToDefaults()
        {
            modEnabled = true;
            easyMode = false;
            decayDays = 45f;
            recruitEffectPercent = 8f;
            tradeEffectPercent = 4f;
            socialOpinionEffect = 10f;
            enableCommend = true;
            enableChatSpread = true;
            hookReliable = true;
            hookBloody = true;
            hookSqueamish = true;
            hookNerved = true;
            hookWar = true;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref modEnabled, "modEnabled", true);
            Scribe_Values.Look(ref easyMode, "easyMode", false);
            Scribe_Values.Look(ref decayDays, "decayDays", 45f);
            Scribe_Values.Look(ref recruitEffectPercent, "recruitEffectPercent", 8f);
            Scribe_Values.Look(ref tradeEffectPercent, "tradeEffectPercent", 4f);
            Scribe_Values.Look(ref socialOpinionEffect, "socialOpinionEffect", 10f);
            Scribe_Values.Look(ref enableCommend, "enableCommend", true);
            Scribe_Values.Look(ref enableChatSpread, "enableChatSpread", true);
            Scribe_Values.Look(ref hookReliable, "hookReliable", true);
            Scribe_Values.Look(ref hookBloody, "hookBloody", true);
            Scribe_Values.Look(ref hookSqueamish, "hookSqueamish", true);
            Scribe_Values.Look(ref hookNerved, "hookNerved", true);
            Scribe_Values.Look(ref hookWar, "hookWar", true);
        }
    }
}
