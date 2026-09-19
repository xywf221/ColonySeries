using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LWOP.Buildings
{
    [StaticConstructorOnStartup]
    public static class LWOPTransferableDragPatch
    {
        static LWOPTransferableDragPatch()
        {
            Harmony harmony = new Harmony("com.colonyseries.lwop.TransferableAndWorkDragSelect");
            if (DragSelectIsActive())
            {
                LWOPDeadmanApparelSelector.PatchOnly(harmony);
                return;
            }

            harmony.PatchAll();
        }

        private static bool DragSelectIsActive()
        {
            try
            {
                foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
                {
                    if (mod == null)
                    {
                        continue;
                    }

                    if (IsDragSelectPackage(mod.PackageId) ||
                        IsDragSelectPackage(mod.PackageIdPlayerFacing) ||
                        string.Equals(mod.Name, "DragSelect", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool IsDragSelectPackage(string packageId)
        {
            return string.Equals(packageId, "telardo.dragselect", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static class LWOPDeadmanApparelSelector
    {
        private const float ButtonX = 540f;
        private const float ButtonY = 0f;
        private const float ButtonWidth = 170f;
        private const float ButtonHeight = 27f;

        private static readonly FieldInfo SectionsField = AccessTools.Field(typeof(TransferableOneWayWidget), "sections");
        private static readonly MethodInfo DialogTradeCountChangedMethod = AccessTools.Method(typeof(Dialog_Trade), "CountToTransferChanged");

        public static void PatchOnly(Harmony harmony)
        {
            harmony.Patch(
                AccessTools.Method(typeof(TransferableOneWayWidget), "OnGUI", new Type[] { typeof(Rect), typeof(bool).MakeByRefType() }),
                postfix: new HarmonyMethod(typeof(LWOPTransferableOneWayWidgetDeadmanPatch), "Postfix"));
            harmony.Patch(
                AccessTools.Method(typeof(Dialog_Trade), "DoWindowContents"),
                postfix: new HarmonyMethod(typeof(LWOPDialogTradeDeadmanPatch), "Postfix"));
        }

        public static bool DrawButton(Rect inRect)
        {
            if (inRect.width < ButtonX + ButtonWidth)
            {
                return false;
            }

            Rect buttonRect = new Rect(inRect.x + ButtonX, inRect.y + ButtonY, ButtonWidth, ButtonHeight);
            bool clicked = Widgets.ButtonText(buttonRect, "LWOPSelectDeadmanApparel".Translate());
            TooltipHandler.TipRegion(buttonRect, "LWOPSelectDeadmanApparelDesc".Translate());
            return clicked;
        }

        public static bool SelectTransferableDeadmanApparel(TransferableOneWayWidget widget)
        {
            bool changed = false;
            foreach (TransferableOneWay transferable in AllOneWayTransferables(widget))
            {
                if (transferable == null || !transferable.Interactive || !IsDeadmanApparelGroup(transferable.things))
                {
                    continue;
                }

                int target = transferable.GetMaximumToTransfer();
                if (transferable.CountToTransfer != target)
                {
                    transferable.AdjustTo(target);
                    changed = true;
                }
            }

            return changed;
        }

        public static bool SelectTradeDeadmanApparel()
        {
            if (!TradeSession.Active || TradeSession.deal == null)
            {
                return false;
            }

            bool changed = false;
            List<Tradeable> tradeables = TradeSession.deal.AllTradeables;
            for (int i = 0; i < tradeables.Count; i++)
            {
                Tradeable tradeable = tradeables[i];
                if (tradeable == null || !tradeable.Interactive || !tradeable.TraderWillTrade || !IsDeadmanApparelGroup(tradeable.thingsColony))
                {
                    continue;
                }

                int colonyCount = tradeable.CountHeldBy(Transactor.Colony);
                if (colonyCount <= 0)
                {
                    continue;
                }

                int target = tradeable.PositiveCountDirection == TransferablePositiveCountDirection.Source ? -colonyCount : colonyCount;
                if (tradeable.CountToTransfer != target)
                {
                    tradeable.AdjustTo(target);
                    changed = true;
                }
            }

            return changed;
        }

        public static void NotifyTradeChanged(Dialog_Trade dialog)
        {
            if (DialogTradeCountChangedMethod != null)
            {
                DialogTradeCountChangedMethod.Invoke(dialog, null);
            }
        }

        private static IEnumerable<TransferableOneWay> AllOneWayTransferables(TransferableOneWayWidget widget)
        {
            IList sections = SectionsField == null ? null : SectionsField.GetValue(widget) as IList;
            if (sections == null)
            {
                yield break;
            }

            for (int i = 0; i < sections.Count; i++)
            {
                object section = sections[i];
                if (section == null)
                {
                    continue;
                }

                FieldInfo transferablesField = AccessTools.Field(section.GetType(), "transferables");
                IEnumerable<TransferableOneWay> transferables = transferablesField == null ? null : transferablesField.GetValue(section) as IEnumerable<TransferableOneWay>;
                if (transferables == null)
                {
                    continue;
                }

                foreach (TransferableOneWay transferable in transferables)
                {
                    yield return transferable;
                }
            }
        }

        private static bool IsDeadmanApparelGroup(List<Thing> things)
        {
            if (things == null || things.Count == 0)
            {
                return false;
            }

            bool found = false;
            for (int i = 0; i < things.Count; i++)
            {
                Apparel apparel = things[i] as Apparel;
                if (apparel == null || !apparel.WornByCorpse)
                {
                    return false;
                }

                found = true;
            }

            return found;
        }
    }

    [HarmonyPatch]
    public static class LWOPTransferableOneWayWidgetDeadmanPatch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(TransferableOneWayWidget), "OnGUI", new Type[] { typeof(Rect), typeof(bool).MakeByRefType() });
        }

        public static void Postfix(TransferableOneWayWidget __instance, Rect inRect, ref bool anythingChanged)
        {
            if (__instance == null || __instance.readOnly || !LWOPDeadmanApparelSelector.DrawButton(inRect))
            {
                return;
            }

            if (LWOPDeadmanApparelSelector.SelectTransferableDeadmanApparel(__instance))
            {
                anythingChanged = true;
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            else
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
            }
        }
    }

    [HarmonyPatch(typeof(Dialog_Trade), "DoWindowContents")]
    public static class LWOPDialogTradeDeadmanPatch
    {
        public static void Postfix(Dialog_Trade __instance, Rect inRect)
        {
            if (__instance == null || !LWOPDeadmanApparelSelector.DrawButton(inRect))
            {
                return;
            }

            if (LWOPDeadmanApparelSelector.SelectTradeDeadmanApparel())
            {
                LWOPDeadmanApparelSelector.NotifyTradeChanged(__instance);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }
            else
            {
                SoundDefOf.ClickReject.PlayOneShotOnCamera();
            }
        }
    }

    public static class LWOPDragSelectButtonUtility
    {
        private const float BrushPadding = 3f;

        private static readonly HashSet<string> ActiveBrushRects = new HashSet<string>();

        private static string activeContext;
        private static bool suppressGlobalButtonPatch;
        private static Vector2 lastMousePosition;
        private static Vector2 currentMousePosition;
        private static EventType lastEventType = EventType.Ignore;

        public static bool TransferableButtonInvisible(Rect rect, bool doMouseoverSound)
        {
            return ButtonInvisibleDraggable(rect, doMouseoverSound, "transferable");
        }

        public static bool TransferableButtonText(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor)
        {
            return ButtonTextDraggable(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor, "transferable");
        }

        public static bool WorkButtonInvisible(Rect rect, bool doMouseoverSound)
        {
            return ButtonInvisibleDraggable(rect, doMouseoverSound, "checkbox");
        }

        public static bool SuppressGlobalButtonPatch
        {
            get { return suppressGlobalButtonPatch; }
        }

        private static int workBoxDrawDepth;

        public static bool InWorkBoxDraw
        {
            get { return workBoxDrawDepth > 0; }
        }

        public static void EnterWorkBoxDraw()
        {
            workBoxDrawDepth++;
        }

        public static void ExitWorkBoxDraw()
        {
            if (workBoxDrawDepth > 0)
            {
                workBoxDrawDepth--;
            }
        }

        public static bool IsTransferArrowLabel(string label)
        {
            return label == "<" || label == "<<" || label == ">" || label == ">>";
        }

        public static bool IsDraggingContext(string context)
        {
            return activeContext == context && IsLeftMouseDrag(Event.current);
        }

        public static bool WasSweptByMouse(Rect rect)
        {
            Event current = Event.current;
            if (current == null)
            {
                return false;
            }

            UpdateMouseSample(current);
            return ButtonWasSwept(rect);
        }

        public static void StopContext(string context)
        {
            if (activeContext == context)
            {
                StopBrush();
            }
        }

        public static void StartContext(Rect rect, string context, bool shouldLockColumn, Vector2 mousePosition)
        {
            StartBrush(context);
        }

        private static bool ButtonInvisibleDraggable(Rect rect, bool doMouseoverSound, string context)
        {
            Event current = Event.current;
            if (current == null)
            {
                return Widgets.ButtonInvisible(rect, doMouseoverSound);
            }

            UpdateMouseSample(current);

            if (current.rawType == EventType.MouseUp)
            {
                StopBrush();
                return NativeButtonInvisible(rect, doMouseoverSound);
            }

            if (ShouldBrushTrigger(rect, context))
            {
                return true;
            }

            return NativeButtonInvisible(rect, doMouseoverSound);
        }

        private static bool ButtonTextDraggable(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor, string context)
        {
            Event current = Event.current;
            if (current == null)
            {
                return NativeButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
            }

            UpdateMouseSample(current);

            if (current.rawType == EventType.MouseUp)
            {
                StopBrush();
                return NativeButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
            }

            if (ShouldBrushTrigger(rect, context))
            {
                return true;
            }

            return NativeButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
        }

        private static bool NativeButtonInvisible(Rect rect, bool doMouseoverSound)
        {
            suppressGlobalButtonPatch = true;
            try
            {
                return Widgets.ButtonInvisible(rect, doMouseoverSound);
            }
            finally
            {
                suppressGlobalButtonPatch = false;
            }
        }

        private static bool NativeButtonText(Rect rect, string label, bool drawBackground, bool doMouseoverSound, bool active, TextAnchor? overrideTextAnchor)
        {
            suppressGlobalButtonPatch = true;
            try
            {
                return Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, overrideTextAnchor);
            }
            finally
            {
                suppressGlobalButtonPatch = false;
            }
        }

        public static bool ShouldBrushTrigger(Rect rect, string context)
        {
            Event current = Event.current;
            if (!IsLeftMouseDrag(current))
            {
                return false;
            }

            if (activeContext != context)
            {
                StartBrush(context);
            }

            string key = context + ":" + ButtonKey(rect);
            if (!ButtonWasSwept(rect))
            {
                ActiveBrushRects.Remove(key);
                return false;
            }

            if (ActiveBrushRects.Contains(key))
            {
                return false;
            }

            ActiveBrushRects.Add(key);
            return true;
        }

        private static bool IsLeftMouseDrag(Event current)
        {
            if (current == null || (current.rawType != EventType.MouseDrag && current.type != EventType.MouseDrag))
            {
                return false;
            }

            return current.button == 0;
        }

        private static void StartBrush(string context)
        {
            activeContext = context;
            ActiveBrushRects.Clear();
        }

        private static void StopBrush()
        {
            activeContext = null;
            lastMousePosition = Vector2.zero;
            currentMousePosition = Vector2.zero;
            lastEventType = EventType.Ignore;
            ActiveBrushRects.Clear();
        }

        private static void UpdateMouseSample(Event current)
        {
            if (current.rawType == EventType.MouseDrag || current.type == EventType.MouseDrag)
            {
                currentMousePosition = current.mousePosition;
                lastMousePosition = current.mousePosition - current.delta;
                lastEventType = EventType.MouseDrag;
                return;
            }

            if (current.type != lastEventType || current.mousePosition != currentMousePosition)
            {
                lastMousePosition = currentMousePosition;
                currentMousePosition = current.mousePosition;
                lastEventType = current.type;
            }
        }

        private static bool ButtonWasSwept(Rect rect)
        {
            rect = rect.ExpandedBy(BrushPadding);

            if (rect.Contains(currentMousePosition) || rect.Contains(lastMousePosition))
            {
                return true;
            }

            return SegmentIntersectsRect(lastMousePosition, currentMousePosition, rect);
        }

        private static bool SegmentIntersectsRect(Vector2 a, Vector2 b, Rect rect)
        {
            float minX = Math.Min(a.x, b.x);
            float maxX = Math.Max(a.x, b.x);
            float minY = Math.Min(a.y, b.y);
            float maxY = Math.Max(a.y, b.y);

            if (maxX < rect.xMin || minX > rect.xMax || maxY < rect.yMin || minY > rect.yMax)
            {
                return false;
            }

            if (Math.Abs(a.x - b.x) < 0.001f)
            {
                return a.x >= rect.xMin && a.x <= rect.xMax;
            }

            if (Math.Abs(a.y - b.y) < 0.001f)
            {
                return a.y >= rect.yMin && a.y <= rect.yMax;
            }

            return LineIntersectsLine(a, b, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin)) ||
                LineIntersectsLine(a, b, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax)) ||
                LineIntersectsLine(a, b, new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax)) ||
                LineIntersectsLine(a, b, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin));
        }

        private static bool LineIntersectsLine(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
        {
            float denominator = ((a2.x - a1.x) * (b2.y - b1.y)) - ((a2.y - a1.y) * (b2.x - b1.x));
            if (Math.Abs(denominator) < 0.001f)
            {
                return false;
            }

            float numeratorA = ((a1.y - b1.y) * (b2.x - b1.x)) - ((a1.x - b1.x) * (b2.y - b1.y));
            float numeratorB = ((a1.y - b1.y) * (a2.x - a1.x)) - ((a1.x - b1.x) * (a2.y - a1.y));
            float ua = numeratorA / denominator;
            float ub = numeratorB / denominator;

            return ua >= 0f && ua <= 1f && ub >= 0f && ub <= 1f;
        }

        private static string ButtonKey(Rect rect)
        {
            return Mathf.RoundToInt(rect.x).ToString() + ":" +
                Mathf.RoundToInt(rect.y).ToString() + ":" +
                Mathf.RoundToInt(rect.width).ToString() + ":" +
                Mathf.RoundToInt(rect.height).ToString();
        }
    }

    [HarmonyPatch(typeof(TransferableUIUtility), "DoCountAdjustInterfaceInternal", new Type[]
    {
        typeof(Rect),
        typeof(Transferable),
        typeof(int),
        typeof(int),
        typeof(int),
        typeof(bool),
        typeof(bool)
    })]
    public static class LWOPTransferableCountDragPatch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReplaceButtonInvisible(
                instructions,
                AccessTools.Method(typeof(LWOPDragSelectButtonUtility), "TransferableButtonInvisible"),
                AccessTools.Method(typeof(LWOPDragSelectButtonUtility), "TransferableButtonText"));
        }

        private static IEnumerable<CodeInstruction> ReplaceButtonInvisible(IEnumerable<CodeInstruction> instructions, System.Reflection.MethodInfo invisibleReplacement, System.Reflection.MethodInfo textReplacement)
        {
            System.Reflection.MethodInfo originalInvisible = AccessTools.Method(typeof(Widgets), "ButtonInvisible", new Type[] { typeof(Rect), typeof(bool) });
            System.Reflection.MethodInfo originalText = AccessTools.Method(typeof(Widgets), "ButtonText", new Type[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });

            foreach (CodeInstruction instruction in instructions)
            {
                System.Reflection.MethodInfo method = instruction.operand as System.Reflection.MethodInfo;
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                    method != null &&
                    method == originalInvisible)
                {
                    instruction.operand = invisibleReplacement;
                }
                else if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                    method != null &&
                    method == originalText)
                {
                    instruction.operand = textReplacement;
                }

                yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(Widgets), "ButtonText", new Type[]
    {
        typeof(Rect),
        typeof(string),
        typeof(bool),
        typeof(bool),
        typeof(bool),
        typeof(TextAnchor?)
    })]
    public static class LWOPGlobalTransferButtonTextDragPatch
    {
        public static bool Prefix(Rect rect, string label, ref bool __result)
        {
            if (LWOPDragSelectButtonUtility.SuppressGlobalButtonPatch ||
                !LWOPDragSelectButtonUtility.IsTransferArrowLabel(label) ||
                !LWOPDragSelectButtonUtility.ShouldBrushTrigger(rect, "transferable"))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Widgets), "ButtonInvisible", new Type[] { typeof(Rect), typeof(bool) })]
    public static class LWOPGlobalWorkButtonInvisibleDragPatch
    {
        public static bool Prefix(Rect butRect, ref bool __result)
        {
            if (LWOPDragSelectButtonUtility.SuppressGlobalButtonPatch ||
                !LWOPDragSelectButtonUtility.InWorkBoxDraw ||
                !LWOPDragSelectButtonUtility.ShouldBrushTrigger(butRect, "checkbox"))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch]
    public static class LWOPCheckboxVectorDragPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Widgets), "Checkbox", new Type[]
            {
                typeof(Vector2),
                typeof(bool).MakeByRefType(),
                typeof(float),
                typeof(bool),
                typeof(bool),
                typeof(Texture2D),
                typeof(Texture2D)
            });
        }

        public static void Prefix(Vector2 topLeft, ref bool checkOn, float size)
        {
            if (LWOPDragSelectButtonUtility.ShouldBrushTrigger(new Rect(topLeft.x, topLeft.y, size, size), "checkbox"))
            {
                checkOn = !checkOn;
            }
        }
    }

    [HarmonyPatch]
    public static class LWOPCheckboxFloatDragPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Widgets), "Checkbox", new Type[]
            {
                typeof(float),
                typeof(float),
                typeof(bool).MakeByRefType(),
                typeof(float),
                typeof(bool),
                typeof(bool),
                typeof(Texture2D),
                typeof(Texture2D)
            });
        }

        public static void Prefix(float x, float y, ref bool checkOn, float size)
        {
            if (LWOPDragSelectButtonUtility.ShouldBrushTrigger(new Rect(x, y, size, size), "checkbox"))
            {
                checkOn = !checkOn;
            }
        }
    }

    [HarmonyPatch]
    public static class LWOPCheckboxLabeledDragPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Widgets), "CheckboxLabeled", new Type[]
            {
                typeof(Rect),
                typeof(string),
                typeof(bool).MakeByRefType(),
                typeof(bool),
                typeof(Texture2D),
                typeof(Texture2D),
                typeof(bool),
                typeof(bool)
            });
        }

        public static void Prefix(Rect rect, ref bool checkOn)
        {
            if (LWOPDragSelectButtonUtility.ShouldBrushTrigger(rect, "checkbox"))
            {
                checkOn = !checkOn;
            }
        }
    }

    [HarmonyPatch(typeof(WidgetsWork), "DrawWorkBoxFor", new Type[]
    {
        typeof(float),
        typeof(float),
        typeof(Pawn),
        typeof(WorkTypeDef),
        typeof(bool)
    })]
    public static class LWOPWorkBoxDragPatch
    {
        public static void Prefix()
        {
            LWOPDragSelectButtonUtility.EnterWorkBoxDraw();
        }

        public static void Finalizer()
        {
            LWOPDragSelectButtonUtility.ExitWorkBoxDraw();
        }
    }
}
