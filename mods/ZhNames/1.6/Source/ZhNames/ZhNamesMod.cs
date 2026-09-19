using UnityEngine;
using Verse;

namespace ZhNames
{
    public class ZhNamesSettings : ModSettings
    {
        public bool masterEnabled = true;
        // Probability a given name is two characters (二字名) vs one.
        public float doubleCharNameChance = 0.8f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref masterEnabled, "masterEnabled", true);
            Scribe_Values.Look(ref doubleCharNameChance, "doubleCharNameChance", 0.8f);
        }
    }

    public class ZhNamesMod : Mod
    {
        public static ZhNamesSettings Settings { get; private set; }

        public ZhNamesMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<ZhNamesSettings>();
        }

        public override string SettingsCategory()
        {
            return "中文姓名 ZhNames";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.CheckboxLabeled("启用中文姓名生成 / Enable Chinese names", ref Settings.masterEnabled);
            ls.Label($"二字名概率 / Two-char given name chance: {Settings.doubleCharNameChance:P0}");
            Settings.doubleCharNameChance = ls.Slider(Settings.doubleCharNameChance, 0f, 1f);
            ls.End();
        }
    }
}
