using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WorkshopWear
{
    /// <summary>
    /// Inject CompWorkshopWear onto a production-bench whitelist at load.
    /// Research / sculpting are opt-in via settings (re-evaluated each game load).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ThingCompInjector
    {
        /// <summary>Default production benches — no research, no sculpting.</summary>
        private static readonly HashSet<string> DefaultWhitelist = new HashSet<string>
        {
            // Cooking
            "ElectricStove",
            "FueledStove",
            // Smithing
            "ElectricSmithy",
            "FueledSmithy",
            // Machining / fabrication family
            "TableMachining",
            "FabricationBench",
            // Tailoring
            "ElectricTailoringBench",
            "HandTailoringBench",
            // Chemistry
            "DrugLab",
            // Related production (fabrication-like)
            "Brewery",
            "ElectricSmelter",
            "BiofuelRefinery",
            // Butchery / basic craft spots that produce goods
            "TableButcher",
            "ButcherSpot",
            "CraftingSpot"
            // Note: TableStonecutter intentionally omitted from default list
        };

        private static readonly HashSet<string> ResearchBenchNames = new HashSet<string>
        {
            "SimpleResearchBench",
            "HiTechResearchBench"
        };

        private static readonly HashSet<string> SculptingNames = new HashSet<string>
        {
            "TableSculpting"
        };

        /// <summary>
        /// Substring patterns for modded fabrication-like benches.
        /// Applied only when the def already looks like a work table with recipes.
        /// Explicitly never matches Research / Sculpting unless settings allow.
        /// </summary>
        private static readonly string[] ProductionNameHints =
        {
            // Tight hints — avoid "Table" alone (would grab sculpting/stonecutter/etc.)
            "Stove", "Smithy", "Machining", "Tailor", "DrugLab", "Fabrication",
            "Brewery", "Smelter", "Refinery"
        };

        static ThingCompInjector()
        {
            InjectAll();
            InjectStatPart();
        }

        public static void InjectAll()
        {
            var settings = WorkshopWearMod.Settings;
            bool includeResearch = settings != null && settings.includeResearchBenches;
            bool includeSculpting = settings != null && settings.includeSculptingTables;

            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null || def.building == null)
                {
                    continue;
                }

                if (!ShouldInject(def, includeResearch, includeSculpting))
                {
                    continue;
                }

                EnsureComp(def);
            }
        }

        private static bool ShouldInject(ThingDef def, bool includeResearch, bool includeSculpting)
        {
            string name = def.defName;
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (ResearchBenchNames.Contains(name))
            {
                return includeResearch;
            }

            if (SculptingNames.Contains(name) || name.IndexOf("Sculpt", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return includeSculpting;
            }

            // Never inject pure research via fuzzy match
            if (name.IndexOf("Research", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return includeResearch;
            }

            if (DefaultWhitelist.Contains(name))
            {
                return true;
            }

            // Fuzzy: only work tables that have recipes (production)
            if (def.recipes == null || def.recipes.Count == 0)
            {
                return false;
            }

            if (!def.hasInteractionCell)
            {
                return false;
            }

            for (int i = 0; i < ProductionNameHints.Length; i++)
            {
                if (name.IndexOf(ProductionNameHints[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureComp(ThingDef def)
        {
            if (def.comps == null)
            {
                def.comps = new List<CompProperties>();
            }

            for (int i = 0; i < def.comps.Count; i++)
            {
                if (def.comps[i] is CompProperties_WorkshopWear)
                {
                    return;
                }
            }

            def.comps.Add(new CompProperties_WorkshopWear { wearMultiplier = 1f });
        }

        /// <summary>
        /// Attach StatPart_WorkshopWear to WorkTableWorkSpeedFactor so bills actually slow down.
        /// </summary>
        private static void InjectStatPart()
        {
            StatDef stat = StatDefOf.WorkTableWorkSpeedFactor;
            if (stat == null)
            {
                return;
            }

            if (stat.parts == null)
            {
                stat.parts = new List<StatPart>();
            }

            for (int i = 0; i < stat.parts.Count; i++)
            {
                if (stat.parts[i] is StatPart_WorkshopWear)
                {
                    return;
                }
            }

            stat.parts.Add(new StatPart_WorkshopWear());
        }
    }
}
