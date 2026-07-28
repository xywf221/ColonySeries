using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ArtisanMark
{
    /// <summary>
    /// Wearing gear you crafted yourself. Stage 0 only; mood magnitude from settings via Thought_ArtisanDynamic.
    /// </summary>
    public class ThoughtWorker_ArtisanSelfMade : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var s = ArtisanMarkMod.Settings;
            if (s == null || !s.modEnabled || !s.enableMoodThoughts || p?.apparel == null)
            {
                return ThoughtState.Inactive;
            }

            List<Apparel> worn = p.apparel.WornApparel;
            if (worn == null || worn.Count == 0)
            {
                return ThoughtState.Inactive;
            }

            string makerId = p.GetUniqueLoadID();
            for (int i = 0; i < worn.Count; i++)
            {
                CompArtisanMark mark = ArtisanMarkUtility.GetMark(worn[i]);
                if (mark == null || !mark.HasMaker)
                {
                    continue;
                }
                if (mark.makerLoadId == makerId)
                {
                    return ThoughtState.ActiveAtStage(0);
                }
            }

            return ThoughtState.Inactive;
        }
    }

    /// <summary>
    /// Wearing gear from a dead friend / lover / bonded. One stack only (single stage).
    /// </summary>
    public class ThoughtWorker_ArtisanLegacy : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var s = ArtisanMarkMod.Settings;
            if (s == null || !s.modEnabled || !s.enableMoodThoughts || p?.apparel == null)
            {
                return ThoughtState.Inactive;
            }

            List<Apparel> worn = p.apparel.WornApparel;
            if (worn == null || worn.Count == 0)
            {
                return ThoughtState.Inactive;
            }

            string bestName = null;
            for (int i = 0; i < worn.Count; i++)
            {
                CompArtisanMark mark = ArtisanMarkUtility.GetMark(worn[i]);
                if (mark == null || !mark.HasMaker)
                {
                    continue;
                }

                // Skip self-made — that's the other thought.
                if (mark.makerLoadId == p.GetUniqueLoadID())
                {
                    continue;
                }

                Pawn maker = mark.TryResolveMaker();
                bool dead;
                if (maker != null)
                {
                    dead = maker.Dead;
                    if (!dead)
                    {
                        continue;
                    }
                    if (!ArtisanMarkUtility.IsCloseBond(p, maker))
                    {
                        continue;
                    }
                    bestName = maker.LabelShort;
                }
                else
                {
                    // Fully gone from world: only count if we can still identify closeness via name? Skip —
                    // without the pawn we cannot verify friend/lover. Spec: dead friend — needs relation.
                    continue;
                }

                // Cap one stack: first matching piece wins (reason shows maker name).
                if (!string.IsNullOrEmpty(bestName))
                {
                    return ThoughtState.ActiveAtStage(0, bestName);
                }
            }

            return ThoughtState.Inactive;
        }
    }

    /// <summary>
    /// Situational thought that reads mood magnitude from mod settings.
    /// </summary>
    public class Thought_ArtisanSelfMade : Thought_Situational
    {
        public override float MoodOffset()
        {
            var s = ArtisanMarkMod.Settings;
            if (s == null)
            {
                return base.MoodOffset();
            }
            return s.selfMadeMood;
        }
    }

    public class Thought_ArtisanLegacy : Thought_Situational
    {
        public override float MoodOffset()
        {
            var s = ArtisanMarkMod.Settings;
            if (s == null)
            {
                return base.MoodOffset();
            }
            return s.legacyMood;
        }
    }
}
