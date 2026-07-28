using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ApprenticeBond
{
    public class ApprenticeBondMod : Mod
    {
        public static ApprenticeBondSettings Settings;
        private Vector2 settingsScroll;

        public ApprenticeBondMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ApprenticeBondSettings>();
            new Harmony("apprenticebond.mentor").PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "AB_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect scrollOuter = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            Rect view = new Rect(0f, 0f, scrollOuter.width - 20f, 780f);
            Widgets.BeginScrollView(scrollOuter, ref settingsScroll, view);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(view);

            listing.CheckboxLabeled("AB_Settings_Enabled".Translate(), ref Settings.modEnabled);
            listing.CheckboxLabeled("AB_Settings_EasyMode".Translate(), ref Settings.easyMode);
            if (Settings.easyMode)
            {
                listing.Label("AB_Settings_EasyModeHint".Translate());
            }

            listing.Gap();
            listing.Label("AB_Settings_SectionXp".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.Label("AB_Settings_ApprenticeShare".Translate((Settings.apprenticeSharePercent * 100f).ToString("F0")));
            Settings.apprenticeSharePercent = listing.Slider(Settings.apprenticeSharePercent, 0.15f, 0.25f);

            listing.Label("AB_Settings_MentorTeach".Translate((Settings.mentorTeachPercent * 100f).ToString("F0")));
            Settings.mentorTeachPercent = listing.Slider(Settings.mentorTeachPercent, 0.02f, 0.10f);

            listing.Label("AB_Settings_DailyCap".Translate(Settings.dailyApprenticeXpCap.ToString("F0")));
            Settings.dailyApprenticeXpCap = Mathf.Round(listing.Slider(Settings.dailyApprenticeXpCap, 500f, 5000f));

            listing.Gap();
            listing.Label("AB_Settings_SectionGrad".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();

            listing.Label("AB_Settings_GradDays".Translate(Settings.graduationMinDays.ToString("F0")));
            Settings.graduationMinDays = Mathf.Round(listing.Slider(Settings.graduationMinDays, 10f, 60f));

            listing.Label("AB_Settings_GradLevelGap".Translate(Settings.graduationLevelGap.ToString("F0")));
            Settings.graduationLevelGap = Mathf.RoundToInt(listing.Slider(Settings.graduationLevelGap, 0f, 4f));

            listing.CheckboxLabeled("AB_Settings_AutoSuggestGrad".Translate(), ref Settings.autoSuggestGraduation);

            listing.Gap();
            listing.Label("AB_Settings_SectionMood".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.CheckboxLabeled("AB_Settings_MoodEnabled".Translate(), ref Settings.enableMoodThoughts);
            listing.Label("AB_Settings_BondMood".Translate(Settings.bondMood.ToString("F0")));
            Settings.bondMood = listing.Slider(Settings.bondMood, 0f, 5f);
            listing.Label("AB_Settings_BreakMood".Translate(Settings.breakMood.ToString("F0")));
            Settings.breakMood = listing.Slider(Settings.breakMood, -8f, 0f);
            listing.Label("AB_Settings_DeathMood".Translate(Settings.mentorDeathMood.ToString("F0")));
            Settings.mentorDeathMood = listing.Slider(Settings.mentorDeathMood, -15f, -4f);
            listing.Label("AB_Settings_GradMood".Translate(Settings.graduationMood.ToString("F0")));
            Settings.graduationMood = listing.Slider(Settings.graduationMood, 0f, 8f);

            listing.Gap();
            listing.Label("AB_Settings_SectionLate".Translate().Colorize(ColoredText.TipSectionTitleColor));
            listing.GapLine();
            listing.Label("AB_Settings_ColonyHint".Translate(Settings.colonySizeHint.ToString("F0")));
            Settings.colonySizeHint = Mathf.RoundToInt(listing.Slider(Settings.colonySizeHint, 6f, 30f));
            listing.CheckboxLabeled("AB_Settings_ShowColonyHint".Translate(), ref Settings.showColonySizeHint);

            listing.Gap();
            if (listing.ButtonText("AB_Settings_DissolveAll".Translate()))
            {
                GameComponent_ApprenticeBond comp = GameComponent_ApprenticeBond.Get();
                if (comp != null)
                {
                    int n = comp.DissolveAll(breakMood: false);
                    Messages.Message("AB_Msg_DissolvedAll".Translate(n), MessageTypeDefOf.NeutralEvent, historical: false);
                }
                else
                {
                    Messages.Message("AB_Msg_NoGame".Translate(), MessageTypeDefOf.RejectInput, historical: false);
                }
            }

            listing.End();
            Widgets.EndScrollView();

            Rect resetRect = new Rect(inRect.x, inRect.yMax - 32f, 220f, 30f);
            if (Widgets.ButtonText(resetRect, "AB_Settings_Reset".Translate()))
            {
                Settings.ResetToDefaults();
            }

            base.DoSettingsWindowContents(inRect);
        }
    }

    public class ApprenticeBondSettings : ModSettings
    {
        public bool modEnabled = true;
        public bool easyMode;
        /// <summary>Apprentice receives this fraction of mentor XP (same room, bonded skill). Design 15–25%.</summary>
        public float apprenticeSharePercent = 0.20f;
        /// <summary>Mentor teaching bonus as fraction of their own XP gain. Design 5%.</summary>
        public float mentorTeachPercent = 0.05f;
        /// <summary>Hard daily cap on bond XP for the apprentice.</summary>
        public float dailyApprenticeXpCap = 2000f;
        public float graduationMinDays = 30f;
        public int graduationLevelGap = 2;
        public bool autoSuggestGraduation = true;
        public bool enableMoodThoughts = true;
        public float bondMood = 2f;
        public float breakMood = -3f;
        public float mentorDeathMood = -10f;
        public float graduationMood = 4f;
        public int colonySizeHint = 12;
        public bool showColonySizeHint = true;

        public float EffectiveApprenticeShare
        {
            get
            {
                float v = Mathf.Clamp(apprenticeSharePercent, 0.15f, 0.25f);
                if (easyMode)
                {
                    v = Mathf.Min(0.25f, v + 0.03f);
                }
                return v;
            }
        }

        public float EffectiveMentorTeach => Mathf.Clamp(mentorTeachPercent, 0.02f, 0.10f);

        public float EffectiveDailyCap
        {
            get
            {
                float cap = Mathf.Clamp(dailyApprenticeXpCap, 500f, 5000f);
                if (easyMode)
                {
                    cap *= 1.25f;
                }
                return cap;
            }
        }

        public int GraduationMinTicks =>
            Mathf.RoundToInt(Mathf.Clamp(graduationMinDays, 10f, 60f) * GenDate.TicksPerDay);

        public void ResetToDefaults()
        {
            modEnabled = true;
            easyMode = false;
            apprenticeSharePercent = 0.20f;
            mentorTeachPercent = 0.05f;
            dailyApprenticeXpCap = 2000f;
            graduationMinDays = 30f;
            graduationLevelGap = 2;
            autoSuggestGraduation = true;
            enableMoodThoughts = true;
            bondMood = 2f;
            breakMood = -3f;
            mentorDeathMood = -10f;
            graduationMood = 4f;
            colonySizeHint = 12;
            showColonySizeHint = true;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref modEnabled, "modEnabled", true);
            Scribe_Values.Look(ref easyMode, "easyMode", false);
            Scribe_Values.Look(ref apprenticeSharePercent, "apprenticeSharePercent", 0.20f);
            Scribe_Values.Look(ref mentorTeachPercent, "mentorTeachPercent", 0.05f);
            Scribe_Values.Look(ref dailyApprenticeXpCap, "dailyApprenticeXpCap", 2000f);
            Scribe_Values.Look(ref graduationMinDays, "graduationMinDays", 30f);
            Scribe_Values.Look(ref graduationLevelGap, "graduationLevelGap", 2);
            Scribe_Values.Look(ref autoSuggestGraduation, "autoSuggestGraduation", true);
            Scribe_Values.Look(ref enableMoodThoughts, "enableMoodThoughts", true);
            Scribe_Values.Look(ref bondMood, "bondMood", 2f);
            Scribe_Values.Look(ref breakMood, "breakMood", -3f);
            Scribe_Values.Look(ref mentorDeathMood, "mentorDeathMood", -10f);
            Scribe_Values.Look(ref graduationMood, "graduationMood", 4f);
            Scribe_Values.Look(ref colonySizeHint, "colonySizeHint", 12);
            Scribe_Values.Look(ref showColonySizeHint, "showColonySizeHint", true);
        }
    }
}
