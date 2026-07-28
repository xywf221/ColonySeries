using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Ledger
{
    public class LedgerMod : Mod
    {
        public static LedgerSettings Settings;
        private Vector2 scroll;

        public LedgerMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<LedgerSettings>();
            new Harmony("ledger.colonybooks").PatchAll(Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "LD_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var outer = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 36f);
            var view = new Rect(0f, 0f, outer.width - 20f, 640f);
            Widgets.BeginScrollView(outer, ref scroll, view);
            var L = new Listing_Standard();
            L.Begin(view);

            L.CheckboxLabeled("LD_Settings_Enabled".Translate(), ref Settings.modEnabled);
            L.CheckboxLabeled("LD_Settings_Easy".Translate(), ref Settings.easyMode);
            L.CheckboxLabeled("LD_Settings_EnableDraw".Translate(), ref Settings.enableDrawCredit);
            L.GapLine();
            L.Label("LD_Settings_DebtMult".Translate(Settings.debtCapMult.ToString("F0")));
            Settings.debtCapMult = L.Slider(Settings.debtCapMult, 5f, 30f);
            L.Label("LD_Settings_DueDays".Translate(Settings.debtDueDays.ToString("F0")));
            Settings.debtDueDays = L.Slider(Settings.debtDueDays, 3f, 21f);
            L.Label("LD_Settings_MinCreditCredit".Translate(Settings.minCreditToBorrow.ToString("F0")));
            Settings.minCreditToBorrow = L.Slider(Settings.minCreditToBorrow, 10f, 50f);
            L.CheckboxLabeled("LD_Settings_ForceWeekly".Translate(), ref Settings.forceWeeklyLetter);
            L.CheckboxLabeled("LD_Settings_ReserveMood".Translate(), ref Settings.enableReserveMood);
            L.CheckboxLabeled("LD_Settings_AllowCollection".Translate(), ref Settings.allowCollectionIncident);
            L.Gap();
            L.Label("LD_Settings_V2Hint".Translate());
            L.Label("LD_Settings_DrawHint".Translate().Colorize(ColoredText.SubtleGrayColor));

            L.End();
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 32f, 200f, 30f), "LD_Settings_Reset".Translate()))
                Settings.Reset();
            base.DoSettingsWindowContents(inRect);
        }
    }

    public class LedgerSettings : ModSettings
    {
        public bool modEnabled = true;
        public bool easyMode;
        public bool enableDrawCredit = true;
        public float debtCapMult = 15f;
        public float debtDueDays = 7f;
        public float minCreditToBorrow = 25f;
        public bool forceWeeklyLetter;
        public bool enableReserveMood; // default OFF — v2
        public bool allowCollectionIncident = true;

        public float Easy => easyMode ? 0.5f : 1f;
        public int DebtCap(float credit) => Mathf.RoundToInt(Mathf.Clamp(credit, 0f, 100f) * debtCapMult);
        public int DueTicks => Mathf.RoundToInt(debtDueDays * GenDate.TicksPerDay);

        public void Reset()
        {
            modEnabled = true;
            easyMode = false;
            enableDrawCredit = true;
            debtCapMult = 15f;
            debtDueDays = 7f;
            minCreditToBorrow = 25f;
            forceWeeklyLetter = false;
            enableReserveMood = false;
            allowCollectionIncident = true;
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref modEnabled, "modEnabled", true);
            Scribe_Values.Look(ref easyMode, "easyMode", false);
            Scribe_Values.Look(ref enableDrawCredit, "enableDrawCredit", true);
            Scribe_Values.Look(ref debtCapMult, "debtCapMult", 15f);
            Scribe_Values.Look(ref debtDueDays, "debtDueDays", 7f);
            Scribe_Values.Look(ref minCreditToBorrow, "minCreditToBorrow", 25f);
            Scribe_Values.Look(ref forceWeeklyLetter, "forceWeeklyLetter", false);
            Scribe_Values.Look(ref enableReserveMood, "enableReserveMood", false);
            Scribe_Values.Look(ref allowCollectionIncident, "allowCollectionIncident", true);
        }
    }
}
