using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RumorMill
{
    public class GameComponent_RumorMill : GameComponent
    {
        private Dictionary<string, PawnRumorState> byId = new Dictionary<string, PawnRumorState>();
        private int nextDecayCheckTick;
        private int nextNervedScanTick;
        // once per day, not hourly mesh
        private static int DecayCheckInterval => GenDate.TicksPerDay;
        private static int NervedScanInterval => GenDate.TicksPerDay * 2;

        public GameComponent_RumorMill(Game game)
        {
        }

        public static GameComponent_RumorMill Get()
        {
            return Current.Game?.GetComponent<GameComponent_RumorMill>();
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref byId, "byId", LookMode.Value, LookMode.Deep);
            Scribe_Values.Look(ref nextDecayCheckTick, "nextDecayCheckTick", 0);
            Scribe_Values.Look(ref nextNervedScanTick, "nextNervedScanTick", 0);
            if (byId == null)
            {
                byId = new Dictionary<string, PawnRumorState>();
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            if (nextDecayCheckTick <= 0)
            {
                nextDecayCheckTick = Find.TickManager.TicksGame + DecayCheckInterval;
            }
        }

        public override void GameComponentTick()
        {
            if (!RumorUtility.Enabled || Current.ProgramState != ProgramState.Playing)
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            if (now >= nextDecayCheckTick)
            {
                nextDecayCheckTick = now + DecayCheckInterval;
                RunDecayPass();
            }
            if (now >= nextNervedScanTick)
            {
                nextNervedScanTick = now + NervedScanInterval;
                if (RumorMillMod.Settings == null || RumorMillMod.Settings.hookNerved)
                {
                    SoftLinkNervedScan();
                }
            }
        }

        public PawnRumorState GetOrCreate(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }
            string id = pawn.ThingID;
            if (!byId.TryGetValue(id, out PawnRumorState state))
            {
                state = new PawnRumorState { pawnId = id };
                byId[id] = state;
            }
            return state;
        }

        public PawnRumorState TryGet(Pawn pawn)
        {
            if (pawn == null)
            {
                return null;
            }
            byId.TryGetValue(pawn.ThingID, out PawnRumorState state);
            return state;
        }

        private void RunDecayPass()
        {
            if (byId.Count == 0)
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            int decayTicks = RumorMillMod.Settings?.DecayTicks ?? (45 * GenDate.TicksPerDay);
            // Easy mode: negatives decay faster
            float easyNeg = RumorMillMod.Settings != null && RumorMillMod.Settings.easyMode ? 0.7f : 1f;

            List<string> emptyKeys = null;
            foreach (KeyValuePair<string, PawnRumorState> kv in byId)
            {
                PawnRumorState state = kv.Value;
                if (state?.tags == null || state.tags.Count == 0)
                {
                    continue;
                }
                for (int i = state.tags.Count - 1; i >= 0; i--)
                {
                    RumorTagEntry tag = state.tags[i];
                    int threshold = decayTicks;
                    if (!tag.IsPositive)
                    {
                        threshold = (int)(threshold * easyNeg);
                    }
                    if (now - tag.lastReinforcedTick < threshold)
                    {
                        continue;
                    }
                    RumorUtility.DowngradeOrRemove(state, tag);
                }
                if (state.tags.Count == 0
                    && state.tendSuccessCount == 0
                    && state.bloodyDeedCount == 0
                    && state.deathWitnessCount == 0
                    && state.raidKillCount == 0
                    && state.fleeCount == 0
                    && !state.nervedSeen)
                {
                    if (emptyKeys == null)
                    {
                        emptyKeys = new List<string>();
                    }
                    emptyKeys.Add(kv.Key);
                }
            }
            if (emptyKeys != null)
            {
                for (int i = 0; i < emptyKeys.Count; i++)
                {
                    byId.Remove(emptyKeys[i]);
                }
            }
        }

        /// <summary>
        /// Soft-link Trait Extractor: daily-ish scan of colonists/prisoners for TE hediffs only.
        /// Bounded to player maps' humanlikes — not an hourly all-colonist rumor mesh.
        /// </summary>
        private void SoftLinkNervedScan()
        {
            List<Map> maps = Find.Maps;
            if (maps == null)
            {
                return;
            }
            for (int m = 0; m < maps.Count; m++)
            {
                Map map = maps[m];
                if (map?.mapPawns == null || !map.IsPlayerHome)
                {
                    continue;
                }
                List<Pawn> pawns = map.mapPawns.AllHumanlikeSpawned;
                if (pawns == null)
                {
                    continue;
                }
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn p = pawns[i];
                    if (!RumorUtility.IsHumanlikeTrackable(p))
                    {
                        continue;
                    }
                    PawnRumorState existing = TryGet(p);
                    if (existing != null && existing.nervedSeen)
                    {
                        continue;
                    }
                    if (HasTraitExtractorScar(p))
                    {
                        RumorUtility.NotifyNerved(p);
                    }
                }
            }
        }

        public static bool HasTraitExtractorScar(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff h = hediffs[i];
                if (h?.def == null)
                {
                    continue;
                }
                string n = h.def.defName;
                // Soft-link by defName — no hard assembly reference
                if (n == "TE_NeuralScar" || n == "TE_NeuralTrauma" || n == "TE_ExtractionCooldown")
                {
                    return true;
                }
            }
            return false;
        }
    }
}
