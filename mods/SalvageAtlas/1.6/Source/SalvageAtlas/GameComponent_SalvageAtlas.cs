using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace SalvageAtlas
{
    /// <summary>
    /// Colony-wide atlas progress. Fragment inventory is physical items;
    /// analyzed fragments bank into this component for branch unlocks.
    /// Parallel to vanilla research — never grants ResearchManager points.
    /// </summary>
    public class GameComponent_SalvageAtlas : GameComponent
    {
        public int analyzedFragments;
        public int totalSalvaged;
        public int totalAnalyzed;
        public int totalFailedTeardowns;
        public int totalSuccessfulTeardowns;
        public int schematicsPending;

        // ranks[branch] = unlocked rank (0 = none)
        private int[] ranks = new int[AtlasBranches.BranchCount];

        public GameComponent_SalvageAtlas(Game game)
        {
        }

        public static GameComponent_SalvageAtlas Get()
        {
            return Current.Game?.GetComponent<GameComponent_SalvageAtlas>();
        }

        public int GetRank(AtlasBranchId id)
        {
            EnsureRanks();
            int i = (int)id;
            if (i < 0 || i >= ranks.Length)
            {
                return 0;
            }
            return ranks[i];
        }

        public bool IsMaxed(AtlasBranchId id) => GetRank(id) >= AtlasBranches.MaxRank(id);

        public AtlasRankDef NextRank(AtlasBranchId id)
        {
            int cur = GetRank(id);
            return AtlasBranches.RankAt(id, cur + 1);
        }

        public bool CanUnlock(AtlasBranchId id, out string failReason)
        {
            failReason = null;
            SalvageAtlasSettings s = SalvageAtlasMod.Settings;
            if (s == null || !s.masterEnabled)
            {
                failReason = "SA_Disabled".Translate();
                return false;
            }
            if (SalvageAtlasDefOf.SA_SalvageBasics != null
                && !SalvageAtlasDefOf.SA_SalvageBasics.IsFinished
                && !DebugSettings.godMode)
            {
                failReason = "SA_NeedResearch".Translate();
                return false;
            }
            if (IsMaxed(id))
            {
                failReason = "SA_BranchMaxed".Translate();
                return false;
            }
            AtlasRankDef next = NextRank(id);
            if (next == null)
            {
                failReason = "SA_BranchMaxed".Translate();
                return false;
            }
            if (analyzedFragments < next.fragmentCost)
            {
                failReason = "SA_NeedFragments".Translate(next.fragmentCost, analyzedFragments);
                return false;
            }
            return true;
        }

        public bool TryUnlock(AtlasBranchId id)
        {
            if (!CanUnlock(id, out _))
            {
                return false;
            }
            AtlasRankDef next = NextRank(id);
            analyzedFragments -= next.fragmentCost;
            EnsureRanks();
            ranks[(int)id] = next.rank;
            ApplyUnlockReward(id, next);

            if (SalvageAtlasMod.Settings != null && SalvageAtlasMod.Settings.letterOnUnlock)
            {
                Find.LetterStack.ReceiveLetter(
                    "SA_Letter_UnlockTitle".Translate(next.labelKey.Translate()),
                    "SA_Letter_UnlockBody".Translate(
                        AtlasBranches.LabelKey(id).Translate(),
                        next.rank,
                        next.descKey.Translate()),
                    LetterDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message(
                    "SA_Msg_Unlocked".Translate(next.labelKey.Translate()),
                    MessageTypeDefOf.PositiveEvent);
            }
            return true;
        }

        public void AddAnalyzed(int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            analyzedFragments += amount;
            totalAnalyzed += amount;
        }

        public void NotifySalvaged(int fragments)
        {
            totalSalvaged += Mathf.Max(0, fragments);
        }

        public void NotifyTeardown(bool success)
        {
            if (success)
            {
                totalSuccessfulTeardowns++;
            }
            else
            {
                totalFailedTeardowns++;
            }
        }

        public int TeardownTier => GetRank(AtlasBranchId.Teardown);

        public bool HasTeardownUnlocked => TeardownTier >= 1;

        public float TeardownSuccessChance(Pawn worker)
        {
            int tier = TeardownTier;
            if (tier <= 0)
            {
                return 0f;
            }
            // Base 55% at rank1 → ~85% at rank4, plus intellect skill, minus risk mult.
            float baseChance = 0.45f + 0.10f * tier;
            if (worker?.skills != null)
            {
                int intel = worker.skills.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
                baseChance += intel * 0.012f;
                int craft = worker.skills.GetSkill(SkillDefOf.Crafting)?.Level ?? 0;
                baseChance += craft * 0.008f;
            }
            SalvageAtlasSettings s = SalvageAtlasMod.Settings;
            if (s != null)
            {
                // Higher risk mult lowers success slightly (riskier teardowns).
                baseChance -= (s.RiskMult - 1f) * 0.08f;
                if (s.easyMode)
                {
                    baseChance += 0.12f;
                }
            }
            return Mathf.Clamp(baseChance, 0.25f, 0.95f);
        }

        public string InspectSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("SA_Inspect_Bank".Translate(analyzedFragments));
            sb.AppendLine("SA_Inspect_Stats".Translate(totalSalvaged, totalAnalyzed, totalSuccessfulTeardowns, totalFailedTeardowns));
            for (int i = 0; i < AtlasBranches.BranchCount; i++)
            {
                AtlasBranchId id = (AtlasBranchId)i;
                int r = GetRank(id);
                int max = AtlasBranches.MaxRank(id);
                sb.AppendLine("SA_Inspect_BranchLine".Translate(
                    AtlasBranches.LabelKey(id).Translate(),
                    r,
                    max));
            }
            if (schematicsPending > 0)
            {
                sb.AppendLine("SA_Inspect_Schematics".Translate(schematicsPending));
            }
            return sb.ToString().TrimEnd();
        }

        private void ApplyUnlockReward(AtlasBranchId id, AtlasRankDef rank)
        {
            switch (rank.kind)
            {
                case AtlasUnlockKind.OneShotBlueprint:
                    // Bank one consumable schematic craft credit; player prints at bench.
                    schematicsPending++;
                    break;
                case AtlasUnlockKind.LocalBuff:
                    // Buffs are applied on demand via bench gizmo (Field Kit / Optics / Coolant).
                    // Unlock merely enables higher severity / longer duration.
                    break;
                case AtlasUnlockKind.TeardownRecipe:
                    // Rank itself gates teardown yield tables.
                    break;
            }
        }

        public bool TryConsumeSchematicCredit()
        {
            if (schematicsPending <= 0)
            {
                return false;
            }
            schematicsPending--;
            return true;
        }

        private void EnsureRanks()
        {
            if (ranks == null || ranks.Length != AtlasBranches.BranchCount)
            {
                int[] next = new int[AtlasBranches.BranchCount];
                if (ranks != null)
                {
                    for (int i = 0; i < Mathf.Min(ranks.Length, next.Length); i++)
                    {
                        next[i] = ranks[i];
                    }
                }
                ranks = next;
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref analyzedFragments, "analyzedFragments", 0);
            Scribe_Values.Look(ref totalSalvaged, "totalSalvaged", 0);
            Scribe_Values.Look(ref totalAnalyzed, "totalAnalyzed", 0);
            Scribe_Values.Look(ref totalFailedTeardowns, "totalFailedTeardowns", 0);
            Scribe_Values.Look(ref totalSuccessfulTeardowns, "totalSuccessfulTeardowns", 0);
            Scribe_Values.Look(ref schematicsPending, "schematicsPending", 0);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                EnsureRanks();
                List<int> list = new List<int>(ranks);
                Scribe_Collections.Look(ref list, "ranks", LookMode.Value);
            }
            else
            {
                List<int> list = null;
                Scribe_Collections.Look(ref list, "ranks", LookMode.Value);
                ranks = new int[AtlasBranches.BranchCount];
                if (list != null)
                {
                    for (int i = 0; i < Mathf.Min(list.Count, ranks.Length); i++)
                    {
                        ranks[i] = list[i];
                    }
                }
            }
        }
    }
}
