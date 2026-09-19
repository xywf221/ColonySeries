using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FanWheel
{
    public static class FanNameUtility
    {
        /// <summary>
        /// 把一段多行文本解析成粉丝名称列表并去重地加入池。返回新增数量。
        /// </summary>
        public static int AddBatch(List<string> names, string text)
        {
            if (names == null || text.NullOrEmpty())
            {
                return 0;
            }
            var existing = new HashSet<string>(names);
            int added = 0;
            foreach (string raw in text.Split('\n'))
            {
                string name = raw.Trim().Trim('\r');
                if (name.NullOrEmpty() || name.Length > 24)
                {
                    continue;
                }
                if (existing.Add(name))
                {
                    names.Add(name);
                    added++;
                }
            }
            return added;
        }
    }

    /// <summary>
    /// 设置窗口中的粉丝名称管理页（批量添加 / 清空 / 单个删除）。
    /// </summary>
    public static class FanNameManagerUI
    {
        private static Vector2 scrollPos;

        public static void Draw(Listing_Standard ls, List<string> names)
        {
            ls.GapLine();
            ls.Label($"粉丝名称池 / Fan Names ({names.Count})");

            Rect row = ls.GetRect(30f);
            if (Widgets.ButtonText(row.LeftHalf(), "批量添加 (一行一个) / Batch add"))
            {
                Find.WindowStack.Add(new Dialog_BatchAddNames(names));
            }
            if (Widgets.ButtonText(row.RightHalf(), "清空 / Clear all") && names.Count > 0)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    $"确定清空全部 {names.Count} 个名称？",
                    delegate { names.Clear(); FanWheelMod.WriteSettingsNow(); }));
            }

            Rect listRect = ls.GetRect(160f);
            Widgets.DrawBox(listRect);
            Rect viewRect = new Rect(0f, 0f, listRect.width - 20f, names.Count * 24f + 8f);
            Widgets.BeginScrollView(listRect.ContractedBy(4f), ref scrollPos, viewRect);
            float y = 4f;
            for (int i = names.Count - 1; i >= 0; i--)
            {
                Rect r = new Rect(4f, y, viewRect.width - 44f, 22f);
                Widgets.Label(r, names[i]);
                Rect del = new Rect(viewRect.width - 30f, y, 22f, 22f);
                if (Widgets.ButtonText(del, "×"))
                {
                    names.RemoveAt(i);
                    FanWheelMod.WriteSettingsNow();
                }
                y += 24f;
            }
            Widgets.EndScrollView();
        }
    }

    public class Dialog_BatchAddNames : Window
    {
        private readonly List<string> names;
        private string text = "";

        public override Vector2 InitialSize => new Vector2(420f, 420f);

        public Dialog_BatchAddNames(List<string> names)
        {
            this.names = names;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(inRect.TopPartPixels(24f), "粘贴粉丝名称，一行一个 / One name per line:");
            Rect textRect = new Rect(inRect.x, inRect.y + 28f, inRect.width, inRect.height - 28f - 40f);
            text = Widgets.TextArea(textRect, text);
            Rect btnRow = new Rect(inRect.x, inRect.yMax - 36f, inRect.width, 32f);
            if (Widgets.ButtonText(btnRow.LeftHalf(), "添加 / Add"))
            {
                int added = FanNameUtility.AddBatch(names, text);
                FanWheelMod.WriteSettingsNow();
                Messages.Message($"新增 {added} 个名称（重复或过长的已跳过）", MessageTypeDefOf.TaskCompletion, historical: false);
                Close();
            }
            if (Widgets.ButtonText(btnRow.RightHalf(), "取消 / Cancel"))
            {
                Close();
            }
        }
    }
}
