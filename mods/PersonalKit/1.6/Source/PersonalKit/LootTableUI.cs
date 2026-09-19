using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalKit
{
    /// <summary>
    /// Settings sub-panel for the loot table editor.
    ///
    /// Layout notes (things that are easy to get wrong here):
    /// - Listing_Standard has no horizontal API at all, so each row takes a rect
    ///   from the listing and hands out sub-rects manually.
    /// - Rows come from the OUTER settings listing. Do NOT add a second
    ///   BeginScrollView here: nested scroll views swallow mouse events, which made
    ///   the whole settings page unclickable while still scrolling. The settings
    ///   page owns scrolling (and PersonalKitMod gives it a tall enough view).
    /// - The item picker opens as its own Window (Dialog_SearchThingDef).
    /// - Weight fields need a per-row string buffer, kept in a dictionary; Widgets
    ///   wants it by ref, so it is copied to a local and stored back each frame.
    /// </summary>
    public static class LootTableUI
    {
        private const float RowH = 26f;
        private const float ItemH = 24f;
        private const float BtnW = 22f;
        /// <summary>
        /// Width of the weight field. The largest weight in any vanilla pool is
        /// 10, so anything wider just wastes label room.
        /// </summary>
        private const float FieldW = 52f;
        /// <summary>Right-edge offset where the trailing delete button starts.</summary>
        private const float TrailW = BtnW + 4f;

        private static string selectedPoolDefName;
        private static readonly HashSet<int> expandedGroups = new HashSet<int>();
        private static readonly Dictionary<string, string> editBuffers = new Dictionary<string, string>();

        /// <param name="listing">The OUTER settings Listing_Standard. Rows are taken
        /// from it so scrolling is handled by the settings page itself.</param>
        public static void DoSettingsWindow(Listing_Standard listing,
                                            List<LootTableTweaks.GroupOverride> overrides)
        {
            List<ThingSetMakerDef> pools = LootTableTweaks.EditablePools();
            if (pools.Count == 0)
            {
                listing.Label("PK_Loot_NoPools".Translate());
                return;
            }

            if (selectedPoolDefName.NullOrEmpty() || !pools.Any(p => p.defName == selectedPoolDefName))
            {
                selectedPoolDefName = pools[0].defName;
                expandedGroups.Clear();
            }

            ThingSetMakerDef pool = pools.First(p => p.defName == selectedPoolDefName);
            var random = (ThingSetMaker_RandomOption)pool.root;

            DrawPoolSelector(listing.GetRect(RowH), pools);

            listing.Label("PK_Loot_Hint".Translate().Colorize(ColoredText.SubtleGrayColor));

            int count = random.options?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                DrawGroup(listing, pool, random.options[i], i, overrides);
            }
        }

        /// <summary>
        /// Display name for a pool. ThingSetMakerDef declares no label at all in
        /// vanilla (LabelCap returns null when label is empty), so fall back to the
        /// defName and keep it visible — a bare internal name is still better than
        /// a blank line. Chinese names come from our own DefInjected package, since
        /// the official one does not cover this def type.
        /// </summary>
        private static string PoolDisplayName(ThingSetMakerDef pool)
        {
            if (pool == null) return "?";
            // Read the raw label rather than LabelCap: Def.LabelCap caches its
            // capitalized value on first access, and our own DefInjected package
            // writes directly to the label field. Both work in practice (the cache
            // is lazy and cleared by Def.ClearCachedData), but going to the source
            // cannot go stale after a language hot reload.
            string lab = pool.label;
            return lab.NullOrEmpty() ? pool.defName : lab;
        }

        private static void DrawPoolSelector(Rect rect, List<ThingSetMakerDef> pools)
        {
            const float clearW = 116f;
            Rect labelRect = new Rect(rect.x, rect.y, 70f, rect.height);
            Widgets.Label(labelRect, "PK_Loot_Pool".Translate());
            Rect btnRect = new Rect(rect.x + 74f, rect.y, rect.width - 74f - clearW - 4f, rect.height);

            if (Widgets.ButtonText(btnRect, PoolDisplayName(pools.First(p => p.defName == selectedPoolDefName))))
            {
                var opts = new List<FloatMenuOption>();
                foreach (ThingSetMakerDef p in pools)
                {
                    string name = p.defName;
                    opts.Add(new FloatMenuOption(PoolDisplayName(p), () =>
                    {
                        selectedPoolDefName = name;
                        expandedGroups.Clear();
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }

            Rect clearRect = new Rect(rect.xMax - clearW, rect.y, clearW, rect.height);
            if (Widgets.ButtonText(clearRect, "PK_Loot_RestoreAll".Translate()))
            {
                PersonalKitMod.Settings?.lootOverrides?.Clear();
                expandedGroups.Clear();
            }
        }

        private static void DrawGroup(Listing_Standard listing, ThingSetMakerDef pool,
                                      ThingSetMaker_RandomOption.Option opt, int index,
                                      List<LootTableTweaks.GroupOverride> overrides)
        {
            var key = new LootTableTweaks.GroupKey(pool.defName, index);
            bool expanded = expandedGroups.Contains(index);
            float vanillaW = LootTableTweaks.VanillaWeightOf(key);

            LootTableTweaks.GroupOverride ov = overrides?
                .Find(x => x != null && x.poolDefName == pool.defName && x.optionIndex == index);

            Rect row = listing.GetRect(RowH);
            float y = row.y;
            float width = row.width;

            // expand toggle
            Rect expRect = new Rect(row.x, y, BtnW, RowH);
            if (Widgets.ButtonText(expRect, expanded ? "-" : "+"))
            {
                if (expanded) expandedGroups.Remove(index);
                else expandedGroups.Add(index);
            }

            // label + full contents as tooltip (vanilla options have no names of their own)
            List<string> names = LootTableTweaks.VanillaItemNames(opt);
            string label = LootTableTweaks.GroupLabel(key, opt);
            Rect labelRect = new Rect(row.x + BtnW + 2f, y,
                                      width - BtnW - 2f - FieldW - TrailW - 8f, RowH);
            if (ov != null && ov.HasAnyOverride)
            {
                GUI.color = Color.yellow;
            }
            Widgets.Label(labelRect, label);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(labelRect,
                "PK_Loot_GroupTip".Translate(LootTableTweaks.DescribeItems(names)));

            // group weight field
            Rect wRect = new Rect(row.x + width - FieldW - TrailW, y + 1f, FieldW, RowH - 2f);
            bool hasW = ov != null && !float.IsNaN(ov.groupWeight);
            float shown = hasW ? ov.groupWeight : vanillaW;
            if (!hasW) GUI.color = ColoredText.SubtleGrayColor;
            string bufKey = "gw:" + key;
            string buf = editBuffers.TryGetValue(bufKey, out string b) ? b : shown.ToString("0.###");
            float before = shown;
            Widgets.TextFieldNumeric(wRect, ref shown, ref buf, 0f, 9999f);
            editBuffers[bufKey] = buf;
            GUI.color = Color.white;
            if (Mathf.Abs(shown - before) > 0.0001f)
            {
                LootTableTweaks.GroupOverride target = LootTableTweaks.EnsureOverride(overrides, key);
                if (target != null) target.groupWeight = shown;
            }

            // clear this group
            Rect delRect = new Rect(row.x + width - BtnW, y + 1f, BtnW, BtnW);
            if (ov != null && ov.HasAnyOverride &&
                Widgets.ButtonImage(delRect, TexButton.Delete, Color.white, GenUI.SubtleMouseoverColor))
            {
                overrides.Remove(ov);
                editBuffers.Remove(bufKey);
            }

            if (!expanded)
            {
                listing.Gap(2f);
                return;
            }

            // ── expanded: item rows ──
            List<string> listed = names.ToList();
            if (ov != null)
            {
                foreach (string n in ov.Additions.Select(a => a.defName))
                {
                    if (!listed.Contains(n)) listed.Add(n);
                }
            }

            foreach (string defName in listed)
            {
                DrawItemRow(listing, key, defName, ov, overrides);
            }

            // A group with no filter (techprints) cannot take additions or per-item
            // weights — only its group weight. Show why instead of a button that
            // would silently do nothing.
            if (!LootTableTweaks.CanEditItems(opt))
            {
                GUI.color = ColoredText.SubtleGrayColor;
                listing.Label("PK_Loot_NoFilterGroup".Translate());
                GUI.color = Color.white;
                listing.Gap(4f);
                return;
            }

            Rect addRect = listing.GetRect(ItemH).LeftPartPixels(160f);
            if (Widgets.ButtonText(addRect, "PK_Loot_AddItem".Translate()))
            {
                var already = new HashSet<string>(listed);
                var capturedKey = key;
                Find.WindowStack.Add(new Dialog_SearchThingDef(td =>
                {
                    if (td == null) return;
                    LootTableTweaks.GroupOverride target =
                        LootTableTweaks.EnsureOverride(PersonalKitMod.Settings?.lootOverrides, capturedKey);
                    if (target == null) return;
                    LootTableTweaks.ItemWeight iw = target.items.Find(x => x.defName == td.defName);
                    if (iw == null)
                    {
                        target.items.Add(new LootTableTweaks.ItemWeight(td.defName, 1f));
                    }
                    else
                    {
                        iw.removed = false;
                        if (iw.weight <= 0f) iw.weight = 1f;
                    }
                }, already));
            }
            listing.Gap(6f);
        }

        private static void DrawItemRow(Listing_Standard listing, LootTableTweaks.GroupKey key,
                                        string defName, LootTableTweaks.GroupOverride ov,
                                        List<LootTableTweaks.GroupOverride> overrides)
        {
            ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            LootTableTweaks.ItemWeight iw = ov?.items.Find(x => x.defName == defName);
            bool removed = iw != null && iw.removed;
            float w = ov == null ? 1f : ov.WeightOf(defName);

            Rect row = listing.GetRect(ItemH);
            float y = row.y;
            float x = row.x;
            float width = row.width;

            if (removed)
            {
                Widgets.DrawBoxSolid(row, new Color(0.4f, 0.15f, 0.15f, 0.25f));
            }

            Rect iconRect = new Rect(x + 26f, y + 1f, BtnW, BtnW);
            if (td != null) Widgets.DefIcon(iconRect, td);

            Rect labelRect = new Rect(x + 50f, y, width - 50f - FieldW - TrailW, ItemH);
            string text = td != null ? td.LabelCap : defName;
            if (removed) text += " " + "PK_Loot_Removed".Translate();
            GUI.color = removed ? ColoredText.SubtleGrayColor : Color.white;
            Widgets.Label(labelRect, text);
            GUI.color = Color.white;
            if (td != null && !td.description.NullOrEmpty())
            {
                TooltipHandler.TipRegion(labelRect, defName);
            }

            Rect wRect = new Rect(x + width - FieldW - TrailW, y + 1f, FieldW, ItemH - 2f);
            GUI.color = removed ? ColoredText.SubtleGrayColor : Color.white;
            string bufKey = "iw:" + key + ":" + defName;
            string buf = editBuffers.TryGetValue(bufKey, out string b) ? b : w.ToString("0.###");
            float before = w;
            Widgets.TextFieldNumeric(wRect, ref w, ref buf, 0f, 9999f);
            editBuffers[bufKey] = buf;
            GUI.color = Color.white;
            if (Mathf.Abs(w - before) > 0.0001f)
            {
                LootTableTweaks.GroupOverride target = LootTableTweaks.EnsureOverride(overrides, key);
                if (target != null)
                {
                    LootTableTweaks.ItemWeight entry = target.items.Find(x => x.defName == defName);
                    if (entry == null)
                    {
                        entry = new LootTableTweaks.ItemWeight(defName, w);
                        target.items.Add(entry);
                    }
                    else
                    {
                        entry.weight = w;
                        if (w > 0f) entry.removed = false;
                    }
                }
            }

            // Removed items keep their row so they can be brought back; the button
            // toggles rather than permanently deleting a vanilla entry.
            Rect delRect = new Rect(x + width - BtnW, y + 1f, BtnW, BtnW);
            if (Widgets.ButtonImage(delRect, removed ? TexButton.Plus : TexButton.Delete,
                                    Color.white, GenUI.SubtleMouseoverColor))
            {
                LootTableTweaks.GroupOverride target = LootTableTweaks.EnsureOverride(overrides, key);
                if (target != null)
                {
                    LootTableTweaks.ItemWeight entry = target.items.Find(x => x.defName == defName);
                    if (entry == null)
                    {
                        target.items.Add(new LootTableTweaks.ItemWeight(defName, 1f, true));
                    }
                    else if (entry.removed && !target.vanillaDefNames.Contains(defName))
                    {
                        // An addition the player removed: drop the entry entirely.
                        target.items.Remove(entry);
                        editBuffers.Remove(bufKey);
                    }
                    else
                    {
                        entry.removed = !entry.removed;
                        if (!entry.removed && entry.weight <= 0f) entry.weight = 1f;
                    }
                    LootTableTweaks.Prune(overrides);
                }
            }

            listing.Gap(2f);
        }
    }
}
