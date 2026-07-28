using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FordRights
{
    /// <summary>
    /// Per-map ford economy. Rare ticks only.
    /// Sleeps when master off, no water, or no ford buildings.
    /// </summary>
    public class MapComponent_FordRights : MapComponent
    {
        public int silverToday;
        public int tollsToday;
        public int silverThisSeason;
        public int dayWindowStartTick = -1;
        public int seasonWindowStartTick = -1;
        public int totalTollsCollected;
        public int totalSilverCollected;
        public int totalFishCaught;
        public int totalFloods;
        public bool cachedHasWater;
        public int nextWaterRecheckTick;
        public int nextFloodCheckTick;

        private const int RareInterval = 2500;

        public MapComponent_FordRights(Map map) : base(map)
        {
        }

        public static MapComponent_FordRights For(Map map)
        {
            return map?.GetComponent<MapComponent_FordRights>();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref silverToday, "silverToday", 0);
            Scribe_Values.Look(ref tollsToday, "tollsToday", 0);
            Scribe_Values.Look(ref silverThisSeason, "silverThisSeason", 0);
            Scribe_Values.Look(ref dayWindowStartTick, "dayWindowStartTick", -1);
            Scribe_Values.Look(ref seasonWindowStartTick, "seasonWindowStartTick", -1);
            Scribe_Values.Look(ref totalTollsCollected, "totalTollsCollected", 0);
            Scribe_Values.Look(ref totalSilverCollected, "totalSilverCollected", 0);
            Scribe_Values.Look(ref totalFishCaught, "totalFishCaught", 0);
            Scribe_Values.Look(ref totalFloods, "totalFloods", 0);
            Scribe_Values.Look(ref cachedHasWater, "cachedHasWater", false);
            Scribe_Values.Look(ref nextWaterRecheckTick, "nextWaterRecheckTick", 0);
            Scribe_Values.Look(ref nextFloodCheckTick, "nextFloodCheckTick", 0);
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            RecheckWater(force: true);
            if (nextFloodCheckTick <= 0)
            {
                ScheduleNextFloodCheck(initial: true);
            }
            EnsureWindows();
        }

        public override void MapComponentTick()
        {
            FordRightsSettings settings = FordRightsMod.Settings;
            if (settings == null || !settings.modEnabled)
            {
                return;
            }

            if (!map.IsHashIntervalTick(RareInterval))
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (now >= nextWaterRecheckTick)
            {
                RecheckWater(force: true);
            }

            if (!cachedHasWater)
            {
                return;
            }

            List<Building_FordCrossing> fords = FordUtility.GetSpawnedFords(map);
            if (fords.Count == 0)
            {
                return;
            }

            EnsureWindows();

            for (int i = 0; i < fords.Count; i++)
            {
                fords[i].RefreshWaterStatus();
            }
        }

        public void RecheckWater(bool force = false)
        {
            cachedHasWater = FordUtility.MapHasUsableWater(map);
            nextWaterRecheckTick = Find.TickManager.TicksGame + GenDate.TicksPerDay;
        }

        public void EnsureWindows()
        {
            int now = Find.TickManager.TicksGame;
            if (dayWindowStartTick < 0 || now - dayWindowStartTick >= GenDate.TicksPerDay)
            {
                dayWindowStartTick = now;
                silverToday = 0;
                tollsToday = 0;
            }

            // "Season" cap = 15-day rolling window (one quadrum-ish), no GenDate lat math.
            if (seasonWindowStartTick < 0 || now - seasonWindowStartTick >= GenDate.TicksPerDay * 15)
            {
                seasonWindowStartTick = now;
                silverThisSeason = 0;
            }
        }

        public bool CanCollectToll(out string reason)
        {
            reason = null;
            FordRightsSettings s = FordRightsMod.Settings;
            if (s == null || !s.modEnabled || !s.tollsEnabled)
            {
                reason = "FR_Toll_Disabled".Translate();
                return false;
            }

            EnsureWindows();

            if (tollsToday >= s.DailyTollCountCap)
            {
                reason = "FR_Toll_DailyCountCap".Translate(s.DailyTollCountCap);
                return false;
            }

            if (silverToday >= s.DailySilverCap)
            {
                reason = "FR_Toll_DailySilverCap".Translate(s.DailySilverCap);
                return false;
            }

            if (silverThisSeason >= s.SeasonalSilverCap)
            {
                reason = "FR_Toll_SeasonalSilverCap".Translate(s.SeasonalSilverCap);
                return false;
            }

            return true;
        }

        public int TryCollectToll(Building_FordCrossing ford, string travelerLabel, Faction travelerFaction)
        {
            if (ford == null || !ford.IsOperational)
            {
                return 0;
            }

            if (!CanCollectToll(out _))
            {
                return 0;
            }

            FordRightsSettings s = FordRightsMod.Settings;
            int want = s != null ? s.RollTollSilver() : Rand.RangeInclusive(50, 150);
            int roomDaily = s != null ? Mathf.Max(0, s.DailySilverCap - silverToday) : want;
            int roomSeason = s != null ? Mathf.Max(0, s.SeasonalSilverCap - silverThisSeason) : want;
            int paid = Mathf.Min(want, roomDaily, roomSeason);
            if (paid <= 0)
            {
                return 0;
            }

            Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
            silver.stackCount = paid;
            GenPlace.TryPlaceThing(silver, ford.Position, map, ThingPlaceMode.Near);

            silverToday += paid;
            silverThisSeason += paid;
            tollsToday++;
            totalTollsCollected++;
            totalSilverCollected += paid;

            Messages.Message(
                "FR_Message_TollCollected".Translate(paid, travelerLabel ?? "FR_Traveler".Translate()),
                ford,
                MessageTypeDefOf.PositiveEvent);

            return paid;
        }

        public void NotifyFishCaught(int count)
        {
            totalFishCaught += Mathf.Max(0, count);
        }

        public void ScheduleNextFloodCheck(bool initial = false)
        {
            float days = initial ? Rand.Range(8f, 16f) : Rand.Range(12f, 22f);
            if (FordRightsMod.Settings != null && FordRightsMod.Settings.easyMode)
            {
                days *= 1.6f;
            }
            nextFloodCheckTick = Find.TickManager.TicksGame + Mathf.RoundToInt(days * GenDate.TicksPerDay);
        }

        public string InspectSummary(Building_FordCrossing ford)
        {
            FordRightsSettings s = FordRightsMod.Settings;
            List<string> lines = new List<string>();

            if (!cachedHasWater)
            {
                lines.Add("FR_Inspect_NoMapWater".Translate());
            }
            else if (ford != null)
            {
                lines.Add(ford.HasAdjacentWater
                    ? "FR_Inspect_WaterOk".Translate()
                    : "FR_Inspect_NoAdjacentWater".Translate());
            }

            if (s != null && s.tollsEnabled)
            {
                lines.Add("FR_Inspect_TollToday".Translate(tollsToday, s.DailyTollCountCap, silverToday, s.DailySilverCap));
                lines.Add("FR_Inspect_TollSeason".Translate(silverThisSeason, s.SeasonalSilverCap));
            }

            lines.Add("FR_Inspect_Stats".Translate(totalTollsCollected, totalSilverCollected, totalFishCaught, totalFloods));
            return string.Join("\n", lines);
        }
    }
}
