using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FanWheel
{
    [StaticConstructorOnStartup]
    public static class FanWheelInit
    {
        static FanWheelInit()
        {
            new Harmony("fanwheel.colonyseries").PatchAll();
        }
    }

    /// <summary>
    /// 在「重命名小人」对话框的「随机」按钮上方加一个「转盘」按钮。
    /// 点击后弹出粉丝名称大转盘, 抽中即把名称写回昵称输入框并从奖池移除。
    /// </summary>
    [HarmonyPatch(typeof(Dialog_NamePawn), "DoWindowContents")]
    public static class Patch_DialogNamePawn_WheelButton
    {
        public static void Postfix(Dialog_NamePawn __instance, Rect inRect)
        {
            FanWheelSettings s = FanWheelMod.Settings;
            if (s == null || !s.masterEnabled || s.fanNames.Count == 0)
            {
                return;
            }
            // 「随机」按钮: 右缘距内容右边 ~17px, 宽 114, 位于底部按钮行(35)再上一行(30)。
            // 「转盘」与它完全同列同宽, 放在正上方隔 4px。
            float randomBtnY = inRect.yMax - 35f - 4f - 30f;
            Rect btn = new Rect(inRect.xMax - 114f, randomBtnY - 4f - 30f, 114f, 30f);
            if (Widgets.ButtonText(btn, "转盘"))
            {
                Find.WindowStack.Add(new Dialog_LuckyWheel(__instance, s.fanNames));
            }
            TooltipHandler.TipRegion(btn, "粉丝大转盘：随机抽取一个名称填入昵称输入框，并移出奖池");
        }
    }
}
