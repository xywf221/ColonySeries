using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FordRights
{
    /// <summary>
    /// Rare flood damages operational fords. Readable letter; repair with vanilla.
    /// </summary>
    public class IncidentWorker_FordFlood : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            FordRightsSettings s = FordRightsMod.Settings;
            if (s == null || !s.modEnabled || !s.floodEnabled)
            {
                return false;
            }

            Map map = parms.target as Map;
            if (map == null)
            {
                return false;
            }

            MapComponent_FordRights comp = MapComponent_FordRights.For(map);
            if (comp != null && !comp.cachedHasWater)
            {
                // Lazy recheck once.
                comp.RecheckWater(force: true);
                if (!comp.cachedHasWater)
                {
                    return false;
                }
            }

            List<Building_FordCrossing> fords = FordUtility.GetSpawnedFords(map);
            for (int i = 0; i < fords.Count; i++)
            {
                if (fords[i].HasAdjacentWater)
                {
                    return true;
                }
            }
            return false;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            FordRightsSettings s = FordRightsMod.Settings;
            if (s == null || !s.modEnabled || !s.floodEnabled)
            {
                return false;
            }

            Map map = (Map)parms.target;
            List<Building_FordCrossing> fords = FordUtility.GetSpawnedFords(map);
            List<Building_FordCrossing> targets = new List<Building_FordCrossing>();
            for (int i = 0; i < fords.Count; i++)
            {
                if (fords[i].HasAdjacentWater || FordUtility.IsNearWater(map, fords[i].Position))
                {
                    targets.Add(fords[i]);
                }
            }

            if (targets.Count == 0)
            {
                return false;
            }

            int damaged = 0;
            Building_FordCrossing letterTarget = null;
            for (int i = 0; i < targets.Count; i++)
            {
                Building_FordCrossing ford = targets[i];
                if (ApplyFloodDamage(ford, s))
                {
                    damaged++;
                    letterTarget = ford;
                }
            }

            if (damaged <= 0)
            {
                return false;
            }

            MapComponent_FordRights comp = MapComponent_FordRights.For(map);
            if (comp != null)
            {
                comp.totalFloods++;
                comp.ScheduleNextFloodCheck();
            }

            SendStandardLetter(
                "FR_Letter_FloodTitle".Translate(),
                "FR_Letter_FloodBody".Translate(damaged),
                LetterDefOf.NegativeEvent,
                parms,
                letterTarget);

            return true;
        }

        public static bool ApplyFloodDamage(Building_FordCrossing ford, FordRightsSettings settings)
        {
            if (ford == null || !ford.Spawned || ford.Destroyed)
            {
                return false;
            }

            float fraction = settings != null ? settings.FloodDamageFraction : 0.85f;
            fraction *= settings != null ? settings.EasyFloodMult : 1f;
            // Always leave a repairable stump — never free-delete the building.
            fraction = Mathf.Clamp(fraction, 0.25f, 0.92f);

            int damage = Mathf.RoundToInt(ford.MaxHitPoints * fraction);
            // Keep at least 1 HP so it stays as a repair target.
            damage = Mathf.Min(damage, ford.HitPoints - 1);
            if (damage <= 0)
            {
                return false;
            }

            ford.TakeDamage(new DamageInfo(DamageDefOf.Crush, damage, 0f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown));
            ford.RefreshWaterStatus();
            ford.fishingQueued = false;
            // Flood also resets fishing for a short while.
            ford.nextFishReadyTick = Find.TickManager.TicksGame + Mathf.RoundToInt(6f * GenDate.TicksPerHour);
            return true;
        }
    }
}
