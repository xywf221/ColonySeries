using System.Collections.Generic;
using Verse;

namespace SalvageAtlas
{
    public enum AtlasBranchId : byte
    {
        Blueprints = 0,
        FieldKit = 1,
        Teardown = 2,
        Optics = 3,
        Coolant = 4
    }

    public enum AtlasUnlockKind : byte
    {
        OneShotBlueprint = 0,
        LocalBuff = 1,
        TeardownRecipe = 2
    }

    public class AtlasRankDef
    {
        public int rank;
        public int fragmentCost;
        public AtlasUnlockKind kind;
        public string labelKey;
        public string descKey;
        public string rewardKey; // free-form for inspect
    }

    public static class AtlasBranches
    {
        public const int BranchCount = 5;

        public static string LabelKey(AtlasBranchId id)
        {
            switch (id)
            {
                case AtlasBranchId.Blueprints: return "SA_Branch_Blueprints";
                case AtlasBranchId.FieldKit: return "SA_Branch_FieldKit";
                case AtlasBranchId.Teardown: return "SA_Branch_Teardown";
                case AtlasBranchId.Optics: return "SA_Branch_Optics";
                case AtlasBranchId.Coolant: return "SA_Branch_Coolant";
                default: return "SA_Branch_Unknown";
            }
        }

        public static string DescKey(AtlasBranchId id)
        {
            switch (id)
            {
                case AtlasBranchId.Blueprints: return "SA_Branch_Blueprints_Desc";
                case AtlasBranchId.FieldKit: return "SA_Branch_FieldKit_Desc";
                case AtlasBranchId.Teardown: return "SA_Branch_Teardown_Desc";
                case AtlasBranchId.Optics: return "SA_Branch_Optics_Desc";
                case AtlasBranchId.Coolant: return "SA_Branch_Coolant_Desc";
                default: return "SA_Branch_Unknown";
            }
        }

        public static int MaxRank(AtlasBranchId id)
        {
            switch (id)
            {
                case AtlasBranchId.Blueprints: return 4;
                case AtlasBranchId.FieldKit: return 3;
                case AtlasBranchId.Teardown: return 4;
                case AtlasBranchId.Optics: return 3;
                case AtlasBranchId.Coolant: return 3;
                default: return 3;
            }
        }

        public static IReadOnlyList<AtlasRankDef> Ranks(AtlasBranchId id)
        {
            switch (id)
            {
                case AtlasBranchId.Blueprints: return Blueprints;
                case AtlasBranchId.FieldKit: return FieldKit;
                case AtlasBranchId.Teardown: return Teardown;
                case AtlasBranchId.Optics: return Optics;
                case AtlasBranchId.Coolant: return Coolant;
                default: return FieldKit;
            }
        }

        public static AtlasRankDef RankAt(AtlasBranchId id, int rank1Based)
        {
            IReadOnlyList<AtlasRankDef> list = Ranks(id);
            if (rank1Based < 1 || rank1Based > list.Count)
            {
                return null;
            }
            return list[rank1Based - 1];
        }

        // Bounded 3–4 ranks. Costs escalate. No permanent global damage tree.
        private static readonly List<AtlasRankDef> Blueprints = new List<AtlasRankDef>
        {
            new AtlasRankDef
            {
                rank = 1, fragmentCost = 8, kind = AtlasUnlockKind.OneShotBlueprint,
                labelKey = "SA_Rank_BP1", descKey = "SA_Rank_BP1_Desc", rewardKey = "schematic_components"
            },
            new AtlasRankDef
            {
                rank = 2, fragmentCost = 14, kind = AtlasUnlockKind.OneShotBlueprint,
                labelKey = "SA_Rank_BP2", descKey = "SA_Rank_BP2_Desc", rewardKey = "schematic_weapon"
            },
            new AtlasRankDef
            {
                rank = 3, fragmentCost = 22, kind = AtlasUnlockKind.OneShotBlueprint,
                labelKey = "SA_Rank_BP3", descKey = "SA_Rank_BP3_Desc", rewardKey = "schematic_armor"
            },
            new AtlasRankDef
            {
                rank = 4, fragmentCost = 32, kind = AtlasUnlockKind.OneShotBlueprint,
                labelKey = "SA_Rank_BP4", descKey = "SA_Rank_BP4_Desc", rewardKey = "schematic_core"
            }
        };

        private static readonly List<AtlasRankDef> FieldKit = new List<AtlasRankDef>
        {
            new AtlasRankDef
            {
                rank = 1, fragmentCost = 6, kind = AtlasUnlockKind.LocalBuff,
                labelKey = "SA_Rank_FK1", descKey = "SA_Rank_FK1_Desc", rewardKey = "patch_armor"
            },
            new AtlasRankDef
            {
                rank = 2, fragmentCost = 12, kind = AtlasUnlockKind.LocalBuff,
                labelKey = "SA_Rank_FK2", descKey = "SA_Rank_FK2_Desc", rewardKey = "patch_armor2"
            },
            new AtlasRankDef
            {
                rank = 3, fragmentCost = 20, kind = AtlasUnlockKind.LocalBuff,
                labelKey = "SA_Rank_FK3", descKey = "SA_Rank_FK3_Desc", rewardKey = "patch_armor3"
            }
        };

        private static readonly List<AtlasRankDef> Teardown = new List<AtlasRankDef>
        {
            new AtlasRankDef
            {
                rank = 1, fragmentCost = 5, kind = AtlasUnlockKind.TeardownRecipe,
                labelKey = "SA_Rank_TD1", descKey = "SA_Rank_TD1_Desc", rewardKey = "teardown_basic"
            },
            new AtlasRankDef
            {
                rank = 2, fragmentCost = 10, kind = AtlasUnlockKind.TeardownRecipe,
                labelKey = "SA_Rank_TD2", descKey = "SA_Rank_TD2_Desc", rewardKey = "teardown_plasteel"
            },
            new AtlasRankDef
            {
                rank = 3, fragmentCost = 16, kind = AtlasUnlockKind.TeardownRecipe,
                labelKey = "SA_Rank_TD3", descKey = "SA_Rank_TD3_Desc", rewardKey = "teardown_advanced"
            },
            new AtlasRankDef
            {
                rank = 4, fragmentCost = 24, kind = AtlasUnlockKind.TeardownRecipe,
                labelKey = "SA_Rank_TD4", descKey = "SA_Rank_TD4_Desc", rewardKey = "teardown_master"
            }
        };

        private static readonly List<AtlasRankDef> Optics = new List<AtlasRankDef>
        {
            new AtlasRankDef
            {
                rank = 1, fragmentCost = 7, kind = AtlasUnlockKind.LocalBuff,
                labelKey = "SA_Rank_OP1", descKey = "SA_Rank_OP1_Desc", rewardKey = "patch_optics"
            },
            new AtlasRankDef
            {
                rank = 2, fragmentCost = 13, kind = AtlasUnlockKind.LocalBuff,
                labelKey = "SA_Rank_OP2", descKey = "SA_Rank_OP2_Desc", rewardKey = "patch_optics2"
            },
            new AtlasRankDef
            {
                rank = 3, fragmentCost = 21, kind = AtlasUnlockKind.LocalBuff,
                labelKey = "SA_Rank_OP3", descKey = "SA_Rank_OP3_Desc", rewardKey = "patch_optics3"
            }
        };

        private static readonly List<AtlasRankDef> Coolant = new List<AtlasRankDef>
        {
            new AtlasRankDef
            {
                rank = 1, fragmentCost = 6, kind = AtlasUnlockKind.LocalBuff,
                labelKey = "SA_Rank_CL1", descKey = "SA_Rank_CL1_Desc", rewardKey = "patch_coolant"
            },
            new AtlasRankDef
            {
                rank = 2, fragmentCost = 12, kind = AtlasUnlockKind.LocalBuff,
                labelKey = "SA_Rank_CL2", descKey = "SA_Rank_CL2_Desc", rewardKey = "patch_coolant2"
            },
            new AtlasRankDef
            {
                rank = 3, fragmentCost = 18, kind = AtlasUnlockKind.LocalBuff,
                labelKey = "SA_Rank_CL3", descKey = "SA_Rank_CL3_Desc", rewardKey = "patch_coolant3"
            }
        };
    }
}
