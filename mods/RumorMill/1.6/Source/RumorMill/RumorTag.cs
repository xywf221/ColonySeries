using System.Collections.Generic;
using Verse;

namespace RumorMill
{
    /// <summary>Deed-driven reputation tags. Stingy intentionally omitted (unfair to detect).</summary>
    public enum RumorTagKind : byte
    {
        Reliable = 0,
        BloodyHands = 1,
        Squeamish = 2,
        Nerved = 3,
        WarHero = 4,
        Deserter = 5
    }

    public class RumorTagEntry : IExposable
    {
        public RumorTagKind kind;
        public int strength = 1; // 1..3
        public int lastReinforcedTick;
        public int deedCount;

        public void ExposeData()
        {
            Scribe_Values.Look(ref kind, "kind", RumorTagKind.Reliable);
            Scribe_Values.Look(ref strength, "strength", 1);
            Scribe_Values.Look(ref lastReinforcedTick, "lastReinforcedTick", 0);
            Scribe_Values.Look(ref deedCount, "deedCount", 0);
        }

        public string LabelCap => ("RM_Tag_" + kind).Translate().CapitalizeFirst();

        public string Description => ("RM_TagDesc_" + kind).Translate();

        public bool IsPositive
        {
            get
            {
                switch (kind)
                {
                    case RumorTagKind.Reliable:
                    case RumorTagKind.WarHero:
                        return true;
                    default:
                        return false;
                }
            }
        }
    }

    public class PawnRumorState : IExposable
    {
        public string pawnId;
        public List<RumorTagEntry> tags = new List<RumorTagEntry>();

        // Progress counters (not tags until thresholds)
        public int tendSuccessCount;
        public int bloodyDeedCount;
        public int deathWitnessCount;
        public int raidKillCount;
        public int fleeCount;
        public bool nervedSeen;

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Collections.Look(ref tags, "tags", LookMode.Deep);
            Scribe_Values.Look(ref tendSuccessCount, "tendSuccessCount", 0);
            Scribe_Values.Look(ref bloodyDeedCount, "bloodyDeedCount", 0);
            Scribe_Values.Look(ref deathWitnessCount, "deathWitnessCount", 0);
            Scribe_Values.Look(ref raidKillCount, "raidKillCount", 0);
            Scribe_Values.Look(ref fleeCount, "fleeCount", 0);
            Scribe_Values.Look(ref nervedSeen, "nervedSeen", false);
            if (tags == null)
            {
                tags = new List<RumorTagEntry>();
            }
        }

        public RumorTagEntry Get(RumorTagKind kind)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i].kind == kind)
                {
                    return tags[i];
                }
            }
            return null;
        }

        public bool Has(RumorTagKind kind) => Get(kind) != null;
    }
}
