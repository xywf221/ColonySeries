using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FanWheel
{
    /// <summary>
    /// 打赏 ID 大转盘(第一版 GL 轮盘)。指针固定在正上方(角 -90°)。
    /// GL 画彩色扇面轮盘; 中奖后把 ID 写回重命名对话框的昵称输入框并移出奖池。
    /// </summary>
    public class Dialog_LuckyWheel : Window
    {
        private enum Phase { Idle, Spinning, Done }

        private readonly Dialog_NamePawn renameDialog;
        private readonly List<string> pool;   // 引用设置里的真实列表
        private readonly List<string> ids;    // 快照(转盘期间不变)

        private Phase phase = Phase.Idle;

        private float segAngle;
        private float startAngle;
        private float targetAngle;
        private float curAngle;
        private float spinDuration;
        private float spinTimer;
        private int winIndex = -1;
        private string winner;
        private int lastTickSeg = -1;

        private float doneTimer;
        private const float DoneHoldSeconds = 1.8f;

        private const float WheelRadius = 168f;

        private static Material _lineMat;

        public override Vector2 InitialSize => new Vector2(480f, ContentHeight);

        // 布局: center.y = 56 + R + 14; 按钮在 center.y + R + 12, 高 38; 底部留白 24。
        // 总内容高 = (56 + R + 14) + R + 12 + 38 + 24 = 144 + 2R; 另加窗口上下 Margin。
        private float ContentHeight => 144f + WheelRadius * 2f + Margin * 2f;

        public Dialog_LuckyWheel(Dialog_NamePawn renameDialog, List<string> pool)
        {
            this.renameDialog = renameDialog;
            this.pool = pool;
            ids = new List<string>(pool);
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        private void BeginSpin()
        {
            if (ids.Count == 0)
            {
                return;
            }
            segAngle = 360f / ids.Count;
            winIndex = Rand.Range(0, ids.Count);
            float winnerCenter = winIndex * segAngle + segAngle * 0.5f;
            float jitter = Rand.Range(-segAngle * 0.32f, segAngle * 0.32f);
            int fullTurns = Rand.Range(4, 7);
            startAngle = curAngle;
            targetAngle = -90f - winnerCenter + jitter + 360f * fullTurns;
            while (targetAngle < startAngle + 360f * 3f)
            {
                targetAngle += 360f;
            }
            spinDuration = 4.2f + fullTurns * 0.4f;
            spinTimer = 0f;
            lastTickSeg = -1;
            phase = Phase.Spinning;
            SoundDefOf.ExecuteTrade.PlayOneShotOnCamera();
        }

        private void FinishSpin()
        {
            phase = Phase.Done;
            doneTimer = 0f;
            winner = ids[winIndex];
            SetNickInRenameDialog(renameDialog, winner);
            pool.Remove(winner);
            // 立即把奖池变化写盘 —— 否则崩溃/报错退出时这次删除会丢失。
            FanWheelMod.WriteSettingsNow();
            SoundDefOf.Quest_Succeded.PlayOneShotOnCamera();
        }

        private static void SetNickInRenameDialog(Dialog_NamePawn dialog, string nick)
        {
            var prop = AccessTools.Property(typeof(Dialog_NamePawn), "CurPawnNick");
            prop?.SetValue(dialog, nick);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (phase == Phase.Spinning)
            {
                spinTimer += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(spinTimer / spinDuration);
                curAngle = Mathf.Lerp(startAngle, targetAngle, EaseOutQuint(t));

                int seg = Mathf.FloorToInt(curAngle / segAngle);
                if (seg != lastTickSeg)
                {
                    lastTickSeg = seg;
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                }
                if (t >= 1f)
                {
                    FinishSpin();
                }
            }
            else if (phase == Phase.Done)
            {
                doneTimer += Time.unscaledDeltaTime;
                if (doneTimer >= DoneHoldSeconds)
                {
                    Close();
                    return;
                }
            }

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            string title = phase == Phase.Done ? $"🎉 {winner}" : $"粉丝大转盘 ({ids.Count} 个名称)";
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), title);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            // GL 在 IMGUI Repaint 期间与 GUI 共用同一(虚拟 GUI)坐标系 —— 直接用即可。
            // 轮盘整体下移, 与标题拉开距离
            Vector2 center = new Vector2(inRect.x + inRect.width / 2f, inRect.y + 56f + WheelRadius + 14f);

            if (ids.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(inRect.x, center.y - 15f, inRect.width, 30f), "名称池为空，请先在 mod 设置里添加。");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            DrawPointer(center + new Vector2(0f, -WheelRadius - 2f));

            Rect btn = new Rect(inRect.x + inRect.width / 2f - 70f, center.y + WheelRadius + 14f, 140f, 38f);
            if (phase == Phase.Idle)
            {
                if (Widgets.ButtonText(btn, "开 转"))
                {
                    BeginSpin();
                }
            }
            else if (phase == Phase.Spinning)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(btn, "……");
                Text.Anchor = TextAnchor.UpperLeft;
            }

            if (Event.current.type == EventType.Repaint)
            {
                DrawWheel(center);
                DrawWheelLabels(center);
            }
        }

        // ---------- GL wheel ----------

        private void DrawWheel(Vector2 c)
        {
            EnsureMaterial();
            _lineMat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.TRIANGLES);

            int n = ids.Count;
            float seg = 360f / n;
            int sub = Mathf.Max(2, Mathf.RoundToInt(seg / 4f));

            float R = WheelRadius;
            for (int i = 0; i < n; i++)
            {
                float a0 = curAngle + i * seg;
                Color fill = (i % 2 == 0)
                    ? new Color(0.24f, 0.36f, 0.55f)
                    : new Color(0.48f, 0.30f, 0.24f);
                if (phase == Phase.Done && i == winIndex)
                {
                    float pulse = 0.75f + 0.25f * Mathf.Sin(doneTimer * 8f);
                    fill = new Color(0.85f * pulse, 0.65f * pulse, 0.18f * pulse);
                }
                Sector(c, R, a0, a0 + seg, sub, fill);
            }

            Sector(c, 22f, 0f, 360f, 28, new Color(0.12f, 0.12f, 0.14f));
            Sector(c, 12f, 0f, 360f, 20, new Color(0.85f, 0.7f, 0.3f));

            GL.End();

            GL.Begin(GL.LINES);
            GL.Color(new Color(0.08f, 0.08f, 0.1f));
            for (int i = 0; i < n; i++)
            {
                float a = (curAngle + i * seg) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                GL.Vertex3(c.x + dir.x * 22f, c.y + dir.y * 22f, 0f);
                GL.Vertex3(c.x + dir.x * R, c.y + dir.y * R, 0f);
            }
            Circle(c, R, 72);
            GL.Color(new Color(0.85f, 0.7f, 0.3f));
            Circle(c, R - 3f, 72);
            GL.End();
            GL.PopMatrix();
        }

        private void DrawWheelLabels(Vector2 c)
        {
            int n = ids.Count;
            float seg = 360f / n;
            float R = WheelRadius;
            GameFont oldFont = Text.Font;
            Text.Font = n > 20 ? GameFont.Tiny : GameFont.Small;
            GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

            // 与 GL 同一虚拟 GUI 坐标系, 直接按虚拟坐标画。
            // 不旋转文字 —— 水平文字对中文/ID 更清晰, 且彻底避开 RotateAroundPivot 偶发的
            // 矩阵未复位导致文字飞到屏幕角落的问题。
            for (int i = 0; i < n; i++)
            {
                float a0 = curAngle + i * seg;
                float mid = a0 + seg * 0.5f;
                float rad = mid * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                Vector2 pos = c + dir * (R * 0.63f);

                string shown = ids[i].Length > 8 ? ids[i].Substring(0, 8) + "…" : ids[i];
                Vector2 size = Text.CalcSize(shown);

                bool hot = phase == Phase.Done && i == winIndex;
                style.normal.textColor = hot ? Color.black : new Color(0.95f, 0.95f, 0.98f);
                Color oc = GUI.color;
                GUI.color = Color.white;
                GUI.Label(new Rect(pos.x - size.x / 2f, pos.y - size.y / 2f, size.x, size.y), shown, style);
                GUI.color = oc;
            }
            Text.Font = oldFont;
        }

        private void DrawPointer(Vector2 tip)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }
            EnsureMaterial();
            _lineMat.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.TRIANGLES);
            GL.Color(new Color(0.95f, 0.78f, 0.25f));
            GL.Vertex3(tip.x - 11f, tip.y - 20f, 0f);
            GL.Vertex3(tip.x + 11f, tip.y - 20f, 0f);
            GL.Vertex3(tip.x, tip.y, 0f);
            GL.End();
            GL.Begin(GL.LINES);
            GL.Color(new Color(0.4f, 0.3f, 0.08f));
            GL.Vertex3(tip.x - 11f, tip.y - 20f, 0f); GL.Vertex3(tip.x + 11f, tip.y - 20f, 0f);
            GL.Vertex3(tip.x + 11f, tip.y - 20f, 0f); GL.Vertex3(tip.x, tip.y, 0f);
            GL.Vertex3(tip.x, tip.y, 0f); GL.Vertex3(tip.x - 11f, tip.y - 20f, 0f);
            GL.End();
            GL.PopMatrix();
        }

        private static void Sector(Vector2 c, float r, float fromDeg, float toDeg, int subdiv, Color color)
        {
            float step = (toDeg - fromDeg) / subdiv;
            GL.Color(color);
            for (int i = 0; i < subdiv; i++)
            {
                float a0 = (fromDeg + step * i) * Mathf.Deg2Rad;
                float a1 = (fromDeg + step * (i + 1)) * Mathf.Deg2Rad;
                GL.Vertex3(c.x, c.y, 0f);
                GL.Vertex3(c.x + Mathf.Cos(a0) * r, c.y + Mathf.Sin(a0) * r, 0f);
                GL.Vertex3(c.x + Mathf.Cos(a1) * r, c.y + Mathf.Sin(a1) * r, 0f);
            }
        }

        private static void Circle(Vector2 c, float r, int subdiv)
        {
            float step = 360f / subdiv;
            for (int i = 0; i < subdiv; i++)
            {
                float a0 = (step * i) * Mathf.Deg2Rad;
                float a1 = (step * (i + 1)) * Mathf.Deg2Rad;
                GL.Vertex3(c.x + Mathf.Cos(a0) * r, c.y + Mathf.Sin(a0) * r, 0f);
                GL.Vertex3(c.x + Mathf.Cos(a1) * r, c.y + Mathf.Sin(a1) * r, 0f);
            }
        }

        private static void EnsureMaterial()
        {
            if (_lineMat == null)
            {
                Shader shader = Shader.Find("Hidden/Internal-Colored");
                _lineMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _lineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                _lineMat.SetInt("_ZWrite", 0);
            }
        }

        private static float EaseOutQuint(float t)
        {
            return 1f - Mathf.Pow(1f - t, 5f);
        }
    }
}
