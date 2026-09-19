using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalKit
{
    /// <summary>
    /// Per-psycast parameter tweaks. Each AbilityDef under PsycastBase gets
    /// multipliers + absolute overrides for its six player-facing knobs:
    /// entropy gain, psyfocus cost, duration, effect radius, cast range,
    /// warmup time. Vanilla values are cached once so toggling the feature
    /// off restores the original def state exactly (no compounding).
    ///
    /// Design: settings UI is a scrollable list grouped by level. Every
    /// field is an absolute value; leave the box empty to keep vanilla.
    /// </summary>
    public static class PsycastTweaks
    {
        public class Entry : IExposable
        {
            public string defName;
            /// <summary>Per-skill master switch. Off = vanilla for this skill.</summary>
            public bool enabled;
            // Absolute overrides; NaN = keep vanilla even when enabled.
            public float entropyGain = float.NaN;
            public float psyfocusCost = float.NaN;
            public float duration = float.NaN;
            public float effectRadius = float.NaN;
            public float range = float.NaN;
            public float warmupTime = float.NaN;

            public bool HasAnyOverride =>
                enabled && (!float.IsNaN(entropyGain) || !float.IsNaN(psyfocusCost) ||
                !float.IsNaN(duration) || !float.IsNaN(effectRadius) ||
                !float.IsNaN(range) || !float.IsNaN(warmupTime));

            public void ExposeData()
            {
                Scribe_Values.Look(ref defName, "defName");
                Scribe_Values.Look(ref enabled, "enabled", false);
                // Scribe can't serialize NaN meaningfully; use -9999 as sentinel.
                float e = entropyGain, p = psyfocusCost, d = duration,
                      r = effectRadius, ra = range, w = warmupTime;
                if (Scribe.mode == LoadSaveMode.Saving)
                {
                    e = float.IsNaN(entropyGain) ? -9999f : entropyGain;
                    p = float.IsNaN(psyfocusCost) ? -9999f : psyfocusCost;
                    d = float.IsNaN(duration) ? -9999f : duration;
                    r = float.IsNaN(effectRadius) ? -9999f : effectRadius;
                    ra = float.IsNaN(range) ? -9999f : range;
                    w = float.IsNaN(warmupTime) ? -9999f : warmupTime;
                }
                Scribe_Values.Look(ref e, "entropyGain", -9999f);
                Scribe_Values.Look(ref p, "psyfocusCost", -9999f);
                Scribe_Values.Look(ref d, "duration", -9999f);
                Scribe_Values.Look(ref r, "effectRadius", -9999f);
                Scribe_Values.Look(ref ra, "range", -9999f);
                Scribe_Values.Look(ref w, "warmupTime", -9999f);
                if (Scribe.mode == LoadSaveMode.LoadingVars)
                {
                    entropyGain = e < -9998f ? float.NaN : e;
                    psyfocusCost = p < -9998f ? float.NaN : p;
                    duration = d < -9998f ? float.NaN : d;
                    effectRadius = r < -9998f ? float.NaN : r;
                    range = ra < -9998f ? float.NaN : ra;
                    warmupTime = w < -9998f ? float.NaN : w;
                }
            }
        }

        /// <summary>Vanilla stat cache: defName → {stat → baseValue}.</summary>
        private static readonly Dictionary<string, Dictionary<string, float>> vanilla =
            new Dictionary<string, Dictionary<string, float>>();
        private static readonly Dictionary<string, float> vanillaVerbRange =
            new Dictionary<string, float>();
        private static readonly Dictionary<string, float> vanillaVerbWarmup =
            new Dictionary<string, float>();

        private static bool captured;

        public static void CaptureVanilla()
        {
            if (captured) return;
            captured = true;
            foreach (AbilityDef def in DefDatabase<AbilityDef>.AllDefsListForReading)
            {
                if (def.abilityClass != typeof(Psycast)) continue;
                var stats = new Dictionary<string, float>();
                if (def.statBases != null)
                {
                    foreach (StatModifier sm in def.statBases)
                    {
                        if (sm?.stat == null) continue;
                        stats[sm.stat.defName] = sm.value;
                    }
                }
                vanilla[def.defName] = stats;
                if (def.verbProperties != null)
                {
                    vanillaVerbRange[def.defName] = def.verbProperties.range;
                    vanillaVerbWarmup[def.defName] = def.verbProperties.warmupTime;
                }
            }
        }

        /// <summary>Apply or restore all tweaks. Called from settings + mod init.</summary>
        public static void ApplyAll(List<Entry> entries)
        {
            if (!ModsConfig.RoyaltyActive) return;
            CaptureVanilla();

            foreach (AbilityDef def in DefDatabase<AbilityDef>.AllDefsListForReading)
            {
                if (def.abilityClass != typeof(Psycast)) continue;
                RestoreDef(def);

                Entry e = entries?.Find(x => x.defName == def.defName);
                if (e == null || !e.HasAnyOverride) continue;

                // Stats
                SetStat(def, "Ability_EntropyGain", e.entropyGain);
                SetStat(def, "Ability_PsyfocusCost", e.psyfocusCost);
                SetStat(def, "Ability_Duration", e.duration);
                SetStat(def, "Ability_EffectRadius", e.effectRadius);
                // Verb
                if (def.verbProperties != null)
                {
                    if (!float.IsNaN(e.range))
                        def.verbProperties.range = Mathf.Max(0f, e.range);
                    if (!float.IsNaN(e.warmupTime))
                        def.verbProperties.warmupTime = Mathf.Max(0f, e.warmupTime);
                }
            }
        }

        private static void SetStat(AbilityDef def, string statDefName, float value)
        {
            if (float.IsNaN(value)) return;
            StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(statDefName);
            if (stat == null) return;
            if (def.statBases == null) def.statBases = new List<StatModifier>();
            StatModifier sm = def.statBases.FirstOrDefault(s => s?.stat == stat);
            if (sm == null)
            {
                sm = new StatModifier { stat = stat, value = 0f };
                def.statBases.Add(sm);
            }
            sm.value = Mathf.Max(0f, value);
        }

        private static void RestoreDef(AbilityDef def)
        {
            if (!vanilla.TryGetValue(def.defName, out var stats)) return;
            // Remove stats we added that weren't in vanilla, restore vanilla values.
            if (def.statBases != null)
            {
                for (int i = def.statBases.Count - 1; i >= 0; i--)
                {
                    var sm = def.statBases[i];
                    if (sm?.stat == null) continue;
                    if (stats.TryGetValue(sm.stat.defName, out float v))
                    {
                        sm.value = v;
                    }
                    else if (IsManagedStat(sm.stat.defName))
                    {
                        def.statBases.RemoveAt(i);
                    }
                }
            }
            if (def.verbProperties != null)
            {
                if (vanillaVerbRange.TryGetValue(def.defName, out float vr))
                    def.verbProperties.range = vr;
                if (vanillaVerbWarmup.TryGetValue(def.defName, out float vw))
                    def.verbProperties.warmupTime = vw;
            }
        }

        private static bool IsManagedStat(string defName) =>
            defName == "Ability_EntropyGain" || defName == "Ability_PsyfocusCost" ||
            defName == "Ability_Duration" || defName == "Ability_EffectRadius";

        // ─── Settings UI ───────────────────────────────────────────

        private static Vector2 scrollPos;
        private static readonly Dictionary<string, string> editBuffers =
            new Dictionary<string, string>();

        /// <param name="listing">The OUTER settings Listing_Standard. Rows are taken
        /// from it; see LootTableUI for why nested BeginScrollView must be avoided.</param>
        public static void DoSettingsWindow(Listing_Standard listing, List<Entry> entries)
        {
            if (!ModsConfig.RoyaltyActive)
            {
                listing.Label("PK_Psycast_NeedRoyalty".Translate());
                return;
            }

            // Collect defs grouped by level
            var byLevel = new SortedDictionary<int, List<AbilityDef>>();
            foreach (AbilityDef def in DefDatabase<AbilityDef>.AllDefsListForReading)
            {
                if (def.abilityClass != typeof(Psycast)) continue;
                if (!byLevel.TryGetValue(def.level, out var list))
                {
                    list = new List<AbilityDef>();
                    byLevel[def.level] = list;
                }
                list.Add(def);
            }

            float width = listing.ColumnWidth;

            foreach (var kv in byLevel)
            {
                // Level header
                Widgets.Label(listing.GetRect(26f), "PK_Psycast_Level".Translate(kv.Key));

                foreach (AbilityDef def in kv.Value)
                {
                    Entry e = entries.Find(x => x.defName == def.defName);
                    if (e == null)
                    {
                        e = new Entry { defName = def.defName };
                        entries.Add(e);
                    }
                    DrawEntry(listing, def, e, width);
                    listing.Gap(4f);
                }
                listing.Gap(8f);
            }
        }

        private static void DrawEntry(Listing_Standard listing, AbilityDef def, Entry entry, float width)
        {
            const float rowH = 24f;
            const int cols = 6;
            float checkW = 24f;
            float labelW = 110f;
            float fieldW = (width - labelW - checkW - 8f) / cols;

            Rect first = listing.GetRect(rowH);
            float y = first.y;

            // Per-skill enable checkbox
            Rect checkRect = new Rect(first.x, y + 2f, checkW, rowH - 4f);
            Widgets.Checkbox(checkRect.x, checkRect.y, ref entry.enabled, rowH - 6f);

            // Name row
            Rect nameRect = new Rect(first.x + checkW, y, labelW - checkW, rowH);
            string tip = def.description ?? "";
            Widgets.Label(nameRect, def.LabelCap);
            if (!tip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(nameRect, tip);
            }

            // Vanilla values as tooltip on the name
            string vanillaTip = GetVanillaTip(def);
            if (!vanillaTip.NullOrEmpty())
            {
                TooltipHandler.TipRegion(nameRect, vanillaTip);
            }

            Rect second = listing.GetRect(rowH + 16f);
            y = second.y;

            // Field row (grayed when this skill's toggle is off)
            string[] keys = { "entropyGain", "psyfocusCost", "duration", "effectRadius", "range", "warmupTime" };
            float[] vals = { entry.entropyGain, entry.psyfocusCost, entry.duration,
                             entry.effectRadius, entry.range, entry.warmupTime };
            float[] vanillaVals = GetVanillaValues(def);

            for (int i = 0; i < cols; i++)
            {
                Rect r = new Rect(second.x + labelW + i * fieldW, y, fieldW - 4f, rowH);
                string key = $"PK_Psycast_{keys[i]}";
                string label = key.Translate();
                string bufferKey = def.defName + ":" + keys[i];
                if (!editBuffers.TryGetValue(bufferKey, out string buf))
                {
                    // Show override if set, otherwise show vanilla value.
                    buf = !float.IsNaN(vals[i])
                        ? vals[i].ToString("F2")
                        : vanillaVals[i].ToString("F2");
                    editBuffers[bufferKey] = buf;
                }
                if (!entry.enabled) GUI.color = Color.gray;
                string newBuf = Widgets.TextField(r, buf);
                if (newBuf != buf)
                {
                    editBuffers[bufferKey] = newBuf;
                    if (string.IsNullOrWhiteSpace(newBuf))
                    {
                        SetField(entry, keys[i], float.NaN);
                    }
                    else if (float.TryParse(newBuf, out float v))
                    {
                        SetField(entry, keys[i], v);
                    }
                }
                GUI.color = Color.white;
                // tiny label above field
                Rect lbl = new Rect(second.x + labelW + i * fieldW, y - 14f, fieldW - 4f, 14f);
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                Widgets.Label(lbl, label);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }

        private static float[] GetVanillaValues(AbilityDef def)
        {
            float e = 0f, p = 0f, d = 0f, r = 0f, ra = 0f, w = 0f;
            if (def.statBases != null)
            {
                foreach (var sm in def.statBases)
                {
                    if (sm?.stat == null) continue;
                    switch (sm.stat.defName)
                    {
                        case "Ability_EntropyGain": e = sm.value; break;
                        case "Ability_PsyfocusCost": p = sm.value; break;
                        case "Ability_Duration": d = sm.value; break;
                        case "Ability_EffectRadius": r = sm.value; break;
                    }
                }
            }
            if (def.verbProperties != null)
            {
                ra = def.verbProperties.range;
                w = def.verbProperties.warmupTime;
            }
            return new[] { e, p, d, r, ra, w };
        }

        private static string GetVanillaTip(AbilityDef def)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PK_Psycast_Vanilla".Translate());
            if (def.statBases != null)
            {
                foreach (var sm in def.statBases)
                {
                    if (sm?.stat == null) continue;
                    sb.AppendLine($"  {sm.stat.LabelCap}: {sm.value}");
                }
            }
            if (def.verbProperties != null)
            {
                sb.AppendLine($"  {"PK_Psycast_range".Translate()}: {def.verbProperties.range}");
                sb.AppendLine($"  {"PK_Psycast_warmupTime".Translate()}: {def.verbProperties.warmupTime}");
            }
            return sb.ToString().TrimEnd();
        }

        private static void SetField(Entry e, string key, float v)
        {
            switch (key)
            {
                case "entropyGain": e.entropyGain = v; break;
                case "psyfocusCost": e.psyfocusCost = v; break;
                case "duration": e.duration = v; break;
                case "effectRadius": e.effectRadius = v; break;
                case "range": e.range = v; break;
                case "warmupTime": e.warmupTime = v; break;
            }
        }
    }
}
