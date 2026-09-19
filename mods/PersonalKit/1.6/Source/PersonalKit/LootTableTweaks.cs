using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PersonalKit
{
    /// <summary>
    /// Runtime-editable loot tables.
    ///
    /// Vanilla loot pools (ThingSetMakerDef) are XML-authored and fixed at load:
    /// a pool is a ThingSetMaker_RandomOption whose options each carry a weight
    /// and point at a ThingSetMaker_StackCount listing concrete item defNames.
    /// Two things are therefore impossible without a mod:
    ///   - changing how likely a whole group is
    ///   - weighting items *within* a group (StackCount picks uniformly, see
    ///     ThingSetMakerUtility.TryGetRandomThingWhichCanWeighNoMoreThan)
    ///
    /// This adds both, plus adding/removing items, driven from ModSettings.
    /// Everything is opt-in per group: untouched groups behave exactly as vanilla.
    /// </summary>
    public static class LootTableTweaks
    {
        // ══════════════════════════════════════════════════════════════
        //  Data model
        // ══════════════════════════════════════════════════════════════

        public class ItemWeight : IExposable
        {
            public string defName;
            /// <summary>Relative weight within the group. 0 or less = never rolls.</summary>
            public float weight = 1f;
            /// <summary>Item is in the vanilla list but the player removed it.</summary>
            public bool removed;

            public ItemWeight() { }

            public ItemWeight(string defName, float weight = 1f, bool removed = false)
            {
                this.defName = defName;
                this.weight = weight;
                this.removed = removed;
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref defName, "defName");
                Scribe_Values.Look(ref weight, "weight", 1f);
                Scribe_Values.Look(ref removed, "removed", false);
            }
        }

        public class GroupOverride : IExposable
        {
            public string poolDefName;
            public int optionIndex;
            /// <summary>Overrides the group's selection weight. NaN = keep vanilla.</summary>
            public float groupWeight = float.NaN;
            public List<ItemWeight> items = new List<ItemWeight>();
            /// <summary>
            /// The group's contents as authored in XML. Persisted so we can tell
            /// "player removed this" from "player added this" after a reload.
            /// </summary>
            public List<string> vanillaDefNames = new List<string>();

            public GroupOverride() { }

            public GroupOverride(string poolDefName, int optionIndex)
            {
                this.poolDefName = poolDefName;
                this.optionIndex = optionIndex;
            }

            /// <summary>True when the player changed anything for this group.</summary>
            public bool HasAnyOverride =>
                !float.IsNaN(groupWeight) ||
                items.Any(i => i.removed || Mathf.Abs(i.weight - 1f) > 0.0001f);

            /// <summary>Effective weight of an item. 0 = excluded.</summary>
            public float WeightOf(string defName)
            {
                if (defName == null) return 1f;
                ItemWeight iw = items.Find(x => x.defName == defName);
                if (iw == null) return 1f;
                if (iw.removed) return 0f;
                return Mathf.Max(0f, iw.weight);
            }

            /// <summary>Items the player added that are not in the vanilla list.</summary>
            public IEnumerable<ItemWeight> Additions =>
                items.Where(x => !x.removed && x.weight > 0f && !vanillaDefNames.Contains(x.defName));

            /// <summary>Every def name the UI should list: vanilla contents first, then additions.</summary>
            public IEnumerable<string> AllListedDefNames =>
                vanillaDefNames.Concat(Additions.Select(x => x.defName)).Distinct();

            public void ExposeData()
            {
                Scribe_Values.Look(ref poolDefName, "poolDefName");
                Scribe_Values.Look(ref optionIndex, "optionIndex", 0);
                // Scribe cannot round-trip NaN; -9999 is the "keep vanilla" sentinel
                // (same trick as PsycastTweaks.Entry).
                float w = groupWeight;
                if (Scribe.mode == LoadSaveMode.Saving) w = float.IsNaN(groupWeight) ? -9999f : groupWeight;
                Scribe_Values.Look(ref w, "groupWeight", -9999f);
                if (Scribe.mode != LoadSaveMode.Saving) groupWeight = (w <= -9999f) ? float.NaN : w;
                Scribe_Collections.Look(ref items, "items", LookMode.Deep);
                Scribe_Collections.Look(ref vanillaDefNames, "vanillaDefNames", LookMode.Value);
                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    if (items == null) items = new List<ItemWeight>();
                    if (vanillaDefNames == null) vanillaDefNames = new List<string>();
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  Pool / group discovery
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Identifies a group by pool defName + option index. We do not need an
        /// instance-identity dictionary: Option.weight is a public field we can
        /// mutate directly (and restore from a cached vanilla value), so there is
        /// no Harmony patch and therefore no "which Option is this?" question at
        /// runtime. The key exists only for settings lookup.
        /// </summary>
        public readonly struct GroupKey : IEquatable<GroupKey>
        {
            public readonly string PoolDefName;
            public readonly int OptionIndex;

            public GroupKey(string poolDefName, int optionIndex)
            {
                PoolDefName = poolDefName;
                OptionIndex = optionIndex;
            }

            public bool Equals(GroupKey other) =>
                PoolDefName == other.PoolDefName && OptionIndex == other.OptionIndex;

            public override bool Equals(object obj) => obj is GroupKey k && Equals(k);

            public override int GetHashCode() =>
                (PoolDefName?.GetHashCode() ?? 0) * 397 ^ OptionIndex;

            public override string ToString() => PoolDefName + "#" + OptionIndex;
        }

        /// <summary>Vanilla weight of each group, cached once so Apply never compounds.</summary>
        private static readonly Dictionary<GroupKey, float> vanillaGroupWeight =
            new Dictionary<GroupKey, float>();

        /// <summary>What we changed on a group, so "restore" is exact.</summary>
        private class AppliedState
        {
            public ThingSetMaker originalMaker;
            public List<string> setAllowed = new List<string>();
            public List<string> setDisallowed = new List<string>();
        }

        private static readonly Dictionary<GroupKey, AppliedState> applied =
            new Dictionary<GroupKey, AppliedState>();

        private static bool discovered;

        /// <summary>
        /// Walk every ThingSetMakerDef whose root is a ThingSetMaker_RandomOption
        /// (only those use true weights; ThingSetMaker_Sum uses per-option `chance`,
        /// a different semantic we deliberately do not touch) and cache its vanilla weights.
        /// </summary>
        public static void Discover()
        {
            if (discovered) return;
            discovered = true;

            vanillaGroupWeight.Clear();

            foreach (ThingSetMakerDef def in DefDatabase<ThingSetMakerDef>.AllDefsListForReading)
            {
                if (def?.root is not ThingSetMaker_RandomOption random) continue;
                if (random.options == null) continue;

                for (int i = 0; i < random.options.Count; i++)
                {
                    ThingSetMaker_RandomOption.Option opt = random.options[i];
                    if (opt == null) continue;
                    // Cache the value as authored. Never re-read it after we have
                    // started mutating, or repeated Apply calls would compound.
                    vanillaGroupWeight[new GroupKey(def.defName, i)] = opt.weight;
                }
            }
        }

        /// <summary>Pools offered in the settings dropdown (RandomOption roots only).</summary>
        public static List<ThingSetMakerDef> EditablePools()
        {
            Discover();
            return DefDatabase<ThingSetMakerDef>.AllDefsListForReading
                .Where(d => d?.root is ThingSetMaker_RandomOption)
                .OrderBy(d => d.defName)
                .ToList();
        }

        public static ThingSetMaker_RandomOption.Option GetOption(GroupKey key)
        {
            ThingSetMakerDef def = DefDatabase<ThingSetMakerDef>.GetNamedSilentFail(key.PoolDefName);
            if (def?.root is not ThingSetMaker_RandomOption random) return null;
            if (random.options == null || key.OptionIndex < 0 || key.OptionIndex >= random.options.Count)
                return null;
            return random.options[key.OptionIndex];
        }

        /// <summary>Vanilla weight of a group (for display and for restore).</summary>
        public static float VanillaWeightOf(GroupKey key) =>
            vanillaGroupWeight.TryGetValue(key, out float w) ? w : GetOption(key)?.weight ?? 0f;

        /// <summary>
        /// Human label for a group. Vanilla options have no name at all (all 256
        /// options in the game lack a label), so synthesise one from the contents.
        /// </summary>
        /// <summary>
        /// Human label for a group. Vanilla options have no name of their own, so
        /// synthesise one from the contents.
        ///
        /// Most groups hold exactly one item — 119 of the 189 groups in the game
        /// do, and 13 pools are single-item throughout — so for those the item's
        /// name IS the group name and no count suffix is appended. Claiming
        /// "Cooler (1 item)" would imply a distinction that does not exist.
        /// </summary>
        public static string GroupLabel(GroupKey key, ThingSetMaker_RandomOption.Option opt)
        {
            List<string> names = VanillaItemNames(opt);
            if (names.Count == 0) return $"#{key.OptionIndex}";
            // Translate defNames to display labels; keep the pluralised count so
            // groups are distinguishable even when several hold similar things.
            string head = DefDatabase<ThingDef>.GetNamedSilentFail(names[0])?.LabelCap ?? names[0];

            // Dynamic groups (no filter) have no single defining item, so name them
            // by what they generate rather than pretending one item stands in.
            if (!CanEditItems(opt))
            {
                return $"#{key.OptionIndex} {"PK_Loot_DynamicGroup".Translate()} ({names.Count}{"PK_Loot_ItemsSuffix".Translate()})";
            }

            if (names.Count == 1) return $"#{key.OptionIndex} {head}";
            return $"#{key.OptionIndex} {head} ({names.Count}{"PK_Loot_ItemsSuffix".Translate()})";
        }

        /// <summary>Cap when enumerating a maker's dynamically produced items.</summary>
        private const int MaxDebugItems = 200;

        /// <summary>
        /// True when the group's contents can actually be edited. Item add/remove
        /// and per-item weights both go through the maker's ThingFilter, so a group
        /// whose maker has no filter (ThingSetMaker_Techprints picks from research
        /// state instead) cannot support them. Its group weight still can be set.
        /// </summary>
        public static bool CanEditItems(ThingSetMaker_RandomOption.Option opt) =>
            opt?.thingSetMaker?.fixedParams.filter != null;

        /// <summary>Item defNames currently allowed by a group's filter.</summary>
        public static List<string> VanillaItemNames(ThingSetMaker_RandomOption.Option opt)
        {
            var list = new List<string>();
            ThingSetMaker maker = opt?.thingSetMaker;
            if (maker == null) return list;

            if (maker.fixedParams.filter != null)
            {
                foreach (ThingDef td in maker.fixedParams.filter.AllowedThingDefs)
                {
                    if (td != null) list.Add(td.defName);
                }
            }

            // Not every group is a StackCount listing defNames. Techprint groups
            // (ThingSetMaker_Techprints) carry no filter at all: they pick from
            // research state at roll time. Ask the maker what it can produce so
            // those groups get a readable name instead of a bare "#11".
            if (list.Count == 0 && maker.fixedParams.filter == null)
            {
                IEnumerable<ThingDef> debug = maker.AllGeneratableThingsDebug();
                if (debug != null)
                {
                    foreach (ThingDef td in debug)
                    {
                        if (td != null) list.Add(td.defName);
                        // These can be large and are only ever shown in a tooltip,
                        // so cap the enumeration rather than trusting its size.
                        if (list.Count >= MaxDebugItems) break;
                    }
                }
            }

            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        /// <summary>"A, B, … (+N more)" for tooltips.</summary>
        public static string DescribeItems(IEnumerable<string> defNames, int max = 12)
        {
            List<string> labels = defNames
                .Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n))
                .Where(d => d != null)
                .Select(d => (string)d.LabelCap)
                .ToList();
            if (labels.Count == 0) return "—";
            if (labels.Count <= max) return string.Join("、", labels);
            return string.Join("、", labels.Take(max)) +
                   "PK_Loot_MoreItems".Translate(labels.Count - max);
        }

        // ══════════════════════════════════════════════════════════════
        //  Apply / restore
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Push all player overrides into the live defs. Called from
        /// PersonalKitMod.ApplyAll (settings changed, and once at startup).
        /// </summary>
        public static void ApplyAll(List<GroupOverride> overrides)
        {
            Discover();
            PersonalKitSettings s = PersonalKitMod.Settings;
            bool enabled = s != null && s.enableLootEditor;

            // Undo everything we ever did, so turning the master switch off
            // (or deleting an override) returns to vanilla exactly.
            RestoreAll();
            if (!enabled || overrides == null) return;

            foreach (GroupOverride ov in overrides)
            {
                if (ov == null || !ov.HasAnyOverride) continue;
                var key = new GroupKey(ov.poolDefName, ov.optionIndex);
                ThingSetMaker_RandomOption.Option opt = GetOption(key);
                if (opt?.thingSetMaker == null) continue;

                var state = new AppliedState { originalMaker = opt.thingSetMaker };
                ThingFilter filter = opt.thingSetMaker.fixedParams.filter;

                // Group weight: Option.weight is a plain public field and nothing
                // else in the game writes it, so mutate it directly and restore
                // from the cached vanilla value — no Harmony needed. This works
                // for every group, including those without a filter.
                if (!float.IsNaN(ov.groupWeight))
                {
                    opt.weight = Mathf.Max(0f, ov.groupWeight);
                }

                // Everything below needs a filter to edit. Dynamic makers such as
                // ThingSetMaker_Techprints pick from research state instead, so for
                // them the group weight above is the whole story.
                if (filter == null) { applied[key] = state; continue; }

                // Add/remove items. ThingFilter.SetAllow is live: allowedDefs is
                // the source of truth and is read fresh on every Generate.
                // Additions are not in the vanilla filter at all, so they need
                // SetAllow(true) too — the union covers both directions.
                foreach (string defName in ov.AllListedDefNames)
                {
                    ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                    if (td == null) continue;
                    bool want = ov.WeightOf(defName) > 0f;
                    bool was = filter.Allows(td);
                    filter.SetAllow(td, want);
                    if (want == was) continue;
                    (want ? state.setAllowed : state.setDisallowed).Add(defName);
                }

                // Per-item weighting is behaviour vanilla does not have, so swap
                // in our own maker for this group only. Groups the player never
                // touched keep the vanilla StackCount and stay uniform.
                if (opt.thingSetMaker is not WeightedStackCount)
                {
                    opt.thingSetMaker = new WeightedStackCount(opt.thingSetMaker, ov);
                }
                else
                {
                    ((WeightedStackCount)opt.thingSetMaker).Override = ov;
                }

                applied[key] = state;
            }
        }

        /// <summary>Put every group we touched back exactly as it was.</summary>
        public static void RestoreAll()
        {
            Discover();
            foreach (var kv in applied)
            {
                ThingSetMaker_RandomOption.Option opt = GetOption(kv.Key);
                if (opt == null) continue;

                ThingFilter filter = opt.thingSetMaker?.fixedParams.filter;
                if (filter != null)
                {
                    foreach (string n in kv.Value.setAllowed)
                    {
                        ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                        if (td != null) filter.SetAllow(td, false);
                    }
                    foreach (string n in kv.Value.setDisallowed)
                    {
                        ThingDef td = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                        if (td != null) filter.SetAllow(td, true);
                    }
                }

                opt.thingSetMaker = kv.Value.originalMaker;
                if (vanillaGroupWeight.TryGetValue(kv.Key, out float w)) opt.weight = w;
            }
            applied.Clear();
        }

        /// <summary>
        /// Build (or refresh) an override entry for a group, seeding it with the
        /// group's current vanilla contents so the UI can show them.
        /// </summary>
        public static GroupOverride EnsureOverride(List<GroupOverride> overrides, GroupKey key)
        {
            if (overrides == null) return null;
            GroupOverride ov = overrides.Find(x => x.poolDefName == key.PoolDefName
                                                && x.optionIndex == key.OptionIndex);
            if (ov != null) return ov;

            ov = new GroupOverride(key.PoolDefName, key.OptionIndex);
            overrides.Add(ov);

            // Seed from the live filter, but ONLY on first creation. Once we
            // start mutating the filter (SetAllow) its contents are no longer
            // the vanilla ones, so re-seeding would let removed items quietly
            // reappear as "not overridden" and get re-enabled.
            ThingSetMaker_RandomOption.Option opt = GetOption(key);
            if (opt != null)
            {
                foreach (string n in VanillaItemNames(opt)) ov.vanillaDefNames.Add(n);
            }
            return ov;
        }

        /// <summary>Drop overrides that no longer change anything.</summary>
        public static void Prune(List<GroupOverride> overrides)
        {
            if (overrides == null) return;
            for (int i = overrides.Count - 1; i >= 0; i--)
            {
                GroupOverride ov = overrides[i];
                if (ov == null || !ov.HasAnyOverride) overrides.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Drop-in replacement for a group's ThingSetMaker that honours per-item
    /// weights. Vanilla's StackCount picks uniformly
    /// (ThingSetMakerUtility.TryGetRandomThingWhichCanWeighNoMoreThan uses plain
    /// TryRandomElement), so there is no hook to weight items without either
    /// patching that global static — which would affect every pool in the game —
    /// or swapping the maker. We swap the maker, for this group only.
    ///
    /// Count range, mass cap, quality assignment and stuff selection all follow
    /// ThingSetMaker_StackCount's original logic; only the candidate enumeration
    /// and the pick itself change.
    /// </summary>
    public class WeightedStackCount : ThingSetMaker_StackCount
    {
        public LootTableTweaks.GroupOverride Override;

        public WeightedStackCount(ThingSetMaker inner, LootTableTweaks.GroupOverride ov)
        {
            if (inner != null) fixedParams = inner.fixedParams;
            Override = ov;
        }

        /// <summary>Weight of a candidate: 0 removes it from the roll.</summary>
        private float WeightFor(ThingDef td)
        {
            if (td == null || Override == null) return 1f;
            return Override.WeightOf(td.defName);
        }

        /// <summary>
        /// Exclude zero-weight items here rather than only in Generate, so that
        /// CanGenerateSub also reports false and the parent RandomOption skips the
        /// group entirely — same as vanilla does when a filter empties out.
        /// </summary>
        protected override IEnumerable<ThingDef> AllowedThingDefs(ThingSetMakerParams parms)
        {
            IEnumerable<ThingDef> source = base.AllowedThingDefs(parms);
            if (Override == null) return source;
            return source.Where(td => td != null && WeightFor(td) > 0f);
        }

        protected override void Generate(ThingSetMakerParams parms, List<Thing> outThings)
        {
            if (Override == null || !Override.HasAnyOverride)
            {
                base.Generate(parms, outThings);
                return;
            }

            List<ThingDef> allowed = AllowedThingDefs(parms).ToList();
            if (allowed.Count == 0) return;

            TechLevel stuffTech = parms.techLevel.GetValueOrDefault();
            IntRange countRange = parms.countRange ?? IntRange.One;
            float maxMass = parms.maxTotalMass ?? float.MaxValue;
            int remaining = Mathf.Max(countRange.RandomInRange, 1);
            float mass = 0f;

            // Mirrors ThingSetMaker_StackCount.Generate: `remaining` is the count
            // budget and is decremented by however many actually went in the stack,
            // so a stackLimit of 75 still satisfies countRange in one roll.
            while (remaining > 0)
            {
                float budget = (maxMass == float.MaxValue) ? float.MaxValue : (maxMass - mass);
                ThingDef chosen = PickWeighted(allowed, stuffTech, budget);
                if (chosen == null) break;

                ThingDef stuff = null;
                if (chosen.MadeFromStuff)
                {
                    IEnumerable<ThingDef> stuffs = GenStuff.AllowedStuffsFor(chosen, stuffTech)
                        .Where(x => chosen.GetStatValueAbstract(StatDefOf.Mass, x) <= budget
                                 && !ThingSetMakerUtility.IsDerpAndDisallowed(chosen, x, parms.qualityGenerator));
                    if (!stuffs.TryRandomElementByWeight(x => x.stuffProps.commonality, out stuff))
                        break;
                }

                Thing thing = ThingMaker.MakeThing(chosen, stuff);
                ThingSetMakerUtility.AssignQuality(thing, parms.qualityGenerator);
                int n = remaining;
                if (maxMass != float.MaxValue && !(thing is Pawn))
                {
                    n = Mathf.Min(n, Mathf.FloorToInt(budget / thing.GetStatValue(StatDefOf.Mass)));
                }
                remaining -= (thing.stackCount = Mathf.Clamp(n, 1, thing.def.stackLimit));
                outThings.Add(thing);
                if (!(thing is Pawn))
                {
                    mass += thing.GetStatValue(StatDefOf.Mass) * (float)thing.stackCount;
                }
            }
        }

        private ThingDef PickWeighted(List<ThingDef> candidates, TechLevel stuffTech, float maxMass)
        {
            ThingDef result = null;
            float total = 0f;
            foreach (ThingDef td in candidates)
            {
                if (td == null) continue;
                float w = WeightFor(td);
                if (w <= 0f) continue;
                if (!ThingSetMakerUtility.PossibleToWeighNoMoreThan(
                        td, maxMass, GenStuff.AllowedStuffsFor(td, stuffTech))) continue;
                total += w;
                // Weighted reservoir sampling: P(select td) proportional to w.
                if (Rand.Value * total < w) result = td;
            }
            return result;
        }
    }
}
