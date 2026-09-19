using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalKit
{
    /// <summary>
    /// Searchable picker for adding an item to a loot pool.
    ///
    /// Uses vanilla's generic Dialog_Search&lt;T&gt; (RimWorld/Dialog_Search.cs) rather than
    /// rolling our own: it already does frame-sliced incremental filtering (500
    /// items/frame), a QuickSearchWidget, and auto-sizing. It has no map/world
    /// dependency, so it works fine from the mod settings window.
    ///
    /// Source list is ThingSetMakerUtility.allGeneratableItems — vanilla's
    /// precomputed "everything that could plausibly be loot" list (already filtered
    /// by ThingSetMakerUtility.CanGenerate). Zero enumeration cost, and it picks up
    /// items from other mods automatically.
    /// </summary>
    public class Dialog_SearchThingDef : Dialog_Search<ThingDef>
    {
        private readonly System.Action<ThingDef> onChosen;
        private readonly HashSet<string> alreadyInPool;

        protected override List<ThingDef> SearchSet => ThingSetMakerUtility.allGeneratableItems;

        protected override bool ShouldClose => false;

        protected override TaggedString SearchLabel => "PK_Loot_SearchItemLabel".Translate();

        public Dialog_SearchThingDef(System.Action<ThingDef> onChosen, HashSet<string> alreadyInPool = null)
        {
            this.onChosen = onChosen;
            this.alreadyInPool = alreadyInPool;
        }

        // Wider than the 350f default so full item names fit.
        public override Vector2 InitialSize => new Vector2(420f, 100f);

        protected override void DoIcon(ThingDef element, Rect iconRect)
        {
            Widgets.DefIcon(iconRect, element);
        }

        protected override void DoLabel(ThingDef element, Rect labelRect)
        {
            string label = element.LabelCap;
            if (alreadyInPool != null && alreadyInPool.Contains(element.defName))
            {
                label = label + " " + "PK_Loot_AlreadyInPool".Translate();
                GUI.color = ColoredText.SubtleGrayColor;
            }
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(labelRect, element.description ?? element.defName);
        }

        protected override void ClikedOnElement(ThingDef element)
        {
            if (element == null) return;
            onChosen?.Invoke(element);
            Close();
        }

        protected override bool ShouldSkipElement(ThingDef element) => element == null;

        protected override void OnHighlightUpdate(ThingDef element)
        {
        }

        protected override void TryAddElement(ThingDef element)
        {
            if (element == null || searchResultsSet.Contains(element)) return;
            if (!TextMatch(element.label) && !TextMatch(element.defName)) return;
            searchResults.Add(element.LabelCap.ToLower(), element);
            searchResultsSet.Add(element);
            SetInitialSizeAndPosition();
        }

        protected override void TryRemoveElement(ThingDef element)
        {
            int idx = searchResults.IndexOfValue(element);
            if (idx < 0) return;
            searchResults.RemoveAt(idx);
            searchResultsSet.Remove(element);
            SetInitialSizeAndPosition();
        }

        // Base class positions bottom-right; centred is friendlier for a settings picker.
        protected override void SetInitialSizeAndPosition()
        {
            scrollHeight = searchResults.Count * 26f;
            Vector2 size = InitialSize;
            size.y = Mathf.Clamp(size.y + scrollHeight, InitialSize.y, UI.screenHeight / 2f);
            windowRect = new Rect(
                (UI.screenWidth - size.x) / 2f,
                (UI.screenHeight - size.y) / 2f,
                size.x, size.y).Rounded();
        }
    }
}
