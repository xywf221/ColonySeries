using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FanWheel
{
    public class FanWheelSettings : ModSettings
    {
        public bool masterEnabled = true;
        // 粉丝名称池 (大转盘奖池)
        public List<string> fanNames = new List<string>();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref masterEnabled, "masterEnabled", true);
            Scribe_Collections.Look(ref fanNames, "fanNames", LookMode.Value);
            if (fanNames == null)
            {
                fanNames = new List<string>();
            }
        }
    }

    public class FanWheelMod : Mod
    {
        public static FanWheelSettings Settings { get; private set; }

        public FanWheelMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<FanWheelSettings>();
        }

        /// <summary>立即把设置(含奖池)写入磁盘, 防止崩溃丢失。</summary>
        public static void WriteSettingsNow()
        {
            LoadedModManager.GetMod<FanWheelMod>()?.WriteSettings();
        }

        public override string SettingsCategory()
        {
            return "粉丝转盘 FanWheel";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.CheckboxLabeled("启用粉丝大转盘 / Enable fan wheel", ref Settings.masterEnabled);
            FanNameManagerUI.Draw(ls, Settings.fanNames);
            ls.End();
        }
    }
}
