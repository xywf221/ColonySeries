using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace QuarantineLine
{
    /// <summary>
    /// Per-map quarantine cells + isolation policy.
    /// Fully asleep when no infectious disease on player-relevant pawns:
    /// no exposure jobs, no meaningful work beyond a rare cheap presence check.
    /// </summary>
    public class MapComponent_Quarantine : MapComponent
    {
        private HashSet<IntVec3> quarantineCells = new HashSet<IntVec3>();
        private bool strictIsolation;
        private bool awake;
        private int nextPulseTick;
        private int carrierCountCache;
        private bool notifiedOutbreak;

        public MapComponent_Quarantine(Map map) : base(map)
        {
        }

        public static MapComponent_Quarantine For(Map map)
        {
            return map?.GetComponent<MapComponent_Quarantine>();
        }

        public bool IsAwake => awake;
        public bool StrictIsolation => strictIsolation;
        public int QuarantineCellCount => quarantineCells.Count;
        public int CarrierCount => carrierCountCache;

        public bool IsQuarantineCell(IntVec3 c) => quarantineCells.Contains(c);

        public IEnumerable<IntVec3> AllQuarantineCells => quarantineCells;

        public override void ExposeData()
        {
            base.ExposeData();
            List<IntVec3> cells = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                cells = new List<IntVec3>(quarantineCells);
            }
            Scribe_Collections.Look(ref cells, "quarantineCells", LookMode.Value);
            Scribe_Values.Look(ref strictIsolation, "strictIsolation", false);
            Scribe_Values.Look(ref awake, "awake", false);
            Scribe_Values.Look(ref nextPulseTick, "nextPulseTick", 0);
            Scribe_Values.Look(ref notifiedOutbreak, "notifiedOutbreak", false);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                quarantineCells = new HashSet<IntVec3>();
                if (cells != null)
                {
                    for (int i = 0; i < cells.Count; i++)
                    {
                        quarantineCells.Add(cells[i]);
                    }
                }
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            ResyncDesignations();
            if (nextPulseTick <= 0)
            {
                nextPulseTick = Find.TickManager.TicksGame
                                + (QuarantineLineMod.Settings?.RareTickIntervalTicks ?? 2500);
            }
        }

        public override void MapComponentTick()
        {
            if (!QuarantineUtility.Enabled || Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (now < nextPulseTick)
            {
                return;
            }

            int interval = QuarantineLineMod.Settings?.RareTickIntervalTicks ?? 2500;
            nextPulseTick = now + interval;

            // Cheap presence check — only player-relevant pawn lists.
            bool hasDisease = QuarantineUtility.MapHasInfectiousDisease(map);
            if (!hasDisease)
            {
                if (awake)
                {
                    GoToSleep();
                }
                // Fully asleep: no exposure, no carrier cache rebuild, no jobs.
                return;
            }

            if (!awake)
            {
                WakeForOutbreak();
            }

            RefreshCarrierCount();
            if (QuarantineUtility.SpreadEnabled)
            {
                RunExposurePulse();
            }
        }

        public void NotifyPossibleInfection()
        {
            // Called from Harmony when an infectious hediff is added — wake ASAP.
            if (!QuarantineUtility.Enabled || awake)
            {
                return;
            }
            if (QuarantineUtility.MapHasInfectiousDisease(map))
            {
                WakeForOutbreak();
                nextPulseTick = Find.TickManager.TicksGame + 60;
            }
        }

        private void WakeForOutbreak()
        {
            awake = true;
            RefreshCarrierCount();
            if (!notifiedOutbreak)
            {
                notifiedOutbreak = true;
                string iso = strictIsolation
                    ? "QL_Letter_OutbreakBodyStrict".Translate()
                    : "QL_Letter_OutbreakBody".Translate();
                Find.LetterStack.ReceiveLetter(
                    "QL_Letter_OutbreakTitle".Translate(),
                    iso,
                    LetterDefOf.NegativeEvent,
                    new TargetInfo(map.Center, map));
            }
        }

        private void GoToSleep()
        {
            awake = false;
            carrierCountCache = 0;
            notifiedOutbreak = false;
            // Keep quarantine cells — player may pre-draw wards; storage is cheap.
        }

        private void RefreshCarrierCount()
        {
            int n = 0;
            CountCarriers(map.mapPawns.FreeColonistsSpawned, ref n);
            CountCarriers(map.mapPawns.PrisonersOfColonySpawned, ref n);
            CountCarriers(map.mapPawns.SpawnedColonyAnimals, ref n);
            carrierCountCache = n;
        }

        private static void CountCarriers(List<Pawn> pawns, ref int n)
        {
            if (pawns == null)
            {
                return;
            }
            for (int i = 0; i < pawns.Count; i++)
            {
                if (QuarantineUtility.IsInfectiousCarrier(pawns[i]))
                {
                    n++;
                }
            }
        }

        /// <summary>
        /// Bounded exposure pulse: healthy colonists near infectious carriers.
        /// Visit risk (in ward) vs isolation containment tradeoff.
        /// </summary>
        private void RunExposurePulse()
        {
            QuarantineLineSettings settings = QuarantineLineMod.Settings;
            if (settings == null)
            {
                return;
            }

            List<Pawn> healthy = map.mapPawns.FreeColonistsSpawned;
            if (healthy == null || healthy.Count == 0)
            {
                return;
            }

            List<Pawn> carriers = new List<Pawn>(8);
            CollectCarriers(map.mapPawns.FreeColonistsSpawned, carriers);
            CollectCarriers(map.mapPawns.PrisonersOfColonySpawned, carriers);
            CollectCarriers(map.mapPawns.SpawnedColonyAnimals, carriers);
            if (carriers.Count == 0)
            {
                return;
            }

            bool allSickContained = true;
            for (int i = 0; i < carriers.Count; i++)
            {
                Pawn c = carriers[i];
                if (c.RaceProps.Humanlike && !IsQuarantineCell(c.Position))
                {
                    allSickContained = false;
                    break;
                }
            }

            for (int h = 0; h < healthy.Count; h++)
            {
                Pawn target = healthy[h];
                if (target == null || target.Dead || QuarantineUtility.IsInfectiousCarrier(target))
                {
                    continue;
                }

                float chance = 0f;
                HediffDef seedDef = null;
                bool visiting = IsQuarantineCell(target.Position);

                for (int i = 0; i < carriers.Count; i++)
                {
                    Pawn carrier = carriers[i];
                    if (carrier == target)
                    {
                        continue;
                    }
                    Hediff src = QuarantineUtility.FirstInfectiousHediff(carrier);
                    if (src?.def == null || !QuarantineUtility.CanCatch(target, src.def))
                    {
                        continue;
                    }

                    bool carrierInWard = IsQuarantineCell(carrier.Position);
                    bool close = QuarantineUtility.SameRoomOrClose(target, carrier, visiting ? 8f : 5f);
                    if (!close && !(visiting && carrierInWard))
                    {
                        continue;
                    }

                    float local;
                    if (visiting && carrierInWard)
                    {
                        // Visiting the ward raises spread risk.
                        local = settings.VisitRisk01;
                    }
                    else if (carrierInWard && strictIsolation && !visiting)
                    {
                        // Strict isolation: contained sick barely seed outsiders.
                        local = settings.BaseExposure01 * settings.IsolationKeepFactor * 0.35f;
                    }
                    else if (carrierInWard && !strictIsolation && !visiting)
                    {
                        local = settings.BaseExposure01 * 0.55f;
                    }
                    else
                    {
                        // Free-roaming sick.
                        local = settings.BaseExposure01;
                        if (strictIsolation && !allSickContained)
                        {
                            // Protocol on but sick not contained — protocol stress, slightly worse.
                            local *= 1.15f;
                        }
                    }

                    if (local > chance)
                    {
                        chance = local;
                        seedDef = src.def;
                    }
                }

                if (seedDef == null || chance <= 0f)
                {
                    continue;
                }

                // Cap per pulse so multi-carrier maps don't become RNG death.
                chance = Mathf.Min(chance, settings.easyMode ? 0.08f : 0.18f);
                if (Rand.Chance(chance))
                {
                    if (QuarantineUtility.TrySeedInfection(target, seedDef))
                    {
                        Messages.Message(
                            "QL_Msg_CaughtDisease".Translate(target.LabelShort, seedDef.label),
                            target,
                            MessageTypeDefOf.NegativeHealthEvent);
                    }
                }
            }
        }

        private static void CollectCarriers(List<Pawn> source, List<Pawn> into)
        {
            if (source == null)
            {
                return;
            }
            for (int i = 0; i < source.Count; i++)
            {
                Pawn p = source[i];
                if (p != null && QuarantineUtility.IsInfectiousCarrier(p))
                {
                    into.Add(p);
                }
            }
        }

        public void MarkCell(IntVec3 c)
        {
            if (!c.InBounds(map) || quarantineCells.Contains(c))
            {
                return;
            }
            quarantineCells.Add(c);
            EnsureDesignation(c);
        }

        public void ReleaseCell(IntVec3 c)
        {
            if (!quarantineCells.Remove(c))
            {
                return;
            }
            Designation des = map.designationManager.DesignationAt(c, QuarantineLineDefOf.QL_Quarantine);
            if (des != null)
            {
                map.designationManager.RemoveDesignation(des);
            }
        }

        public void ToggleStrictIsolation()
        {
            strictIsolation = !strictIsolation;
            Messages.Message(
                strictIsolation
                    ? "QL_Msg_StrictOn".Translate()
                    : "QL_Msg_StrictOff".Translate(),
                MessageTypeDefOf.TaskCompletion,
                historical: false);
        }

        public void SetStrictIsolation(bool value)
        {
            if (strictIsolation == value)
            {
                return;
            }
            strictIsolation = value;
            Messages.Message(
                strictIsolation
                    ? "QL_Msg_StrictOn".Translate()
                    : "QL_Msg_StrictOff".Translate(),
                MessageTypeDefOf.TaskCompletion,
                historical: false);
        }

        private void EnsureDesignation(IntVec3 c)
        {
            if (QuarantineLineDefOf.QL_Quarantine == null)
            {
                return;
            }
            if (map.designationManager.DesignationAt(c, QuarantineLineDefOf.QL_Quarantine) != null)
            {
                return;
            }
            map.designationManager.AddDesignation(new Designation(c, QuarantineLineDefOf.QL_Quarantine));
        }

        private void ResyncDesignations()
        {
            if (QuarantineLineDefOf.QL_Quarantine == null)
            {
                return;
            }
            // Drop stale designations not in set.
            List<Designation> existing = new List<Designation>();
            foreach (Designation d in map.designationManager.SpawnedDesignationsOfDef(QuarantineLineDefOf.QL_Quarantine))
            {
                existing.Add(d);
            }
            for (int i = 0; i < existing.Count; i++)
            {
                if (!quarantineCells.Contains(existing[i].target.Cell))
                {
                    map.designationManager.RemoveDesignation(existing[i]);
                }
            }
            foreach (IntVec3 c in quarantineCells)
            {
                EnsureDesignation(c);
            }
        }

        public string StatusSummary()
        {
            if (!QuarantineUtility.Enabled)
            {
                return "QL_Status_Disabled".Translate();
            }
            if (!awake)
            {
                return "QL_Status_Sleeping".Translate(quarantineCells.Count);
            }
            return "QL_Status_Awake".Translate(
                carrierCountCache,
                quarantineCells.Count,
                strictIsolation
                    ? "QL_Status_Strict".Translate()
                    : "QL_Status_Open".Translate());
        }
    }
}
