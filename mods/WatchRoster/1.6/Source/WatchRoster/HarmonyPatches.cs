using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WatchRoster
{
    /// <summary>
    /// 1) Manned watch → early raid letter + delay actual raid (never touch points).
    /// 2) FireWatcher cadence: manned faster, empty night slower.
    /// Never auto-unlock doors. Never raise raid points.
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryExecuteWorker")]
    public static class Patch_RaidEnemy_TryExecuteWorker
    {
        public static bool Prefix(IncidentParms parms, ref bool __result)
        {
            WatchRosterSettings s = WatchRosterMod.Settings;
            if (s == null || !s.masterEnabled || !s.enableRaidWarning)
            {
                return true;
            }

            if (!(parms?.target is Map map))
            {
                return true;
            }

            if (parms.forced)
            {
                return true;
            }

            MapComponent_WatchRoster comp = MapComponent_WatchRoster.For(map);
            if (comp != null && comp.IsDeferredFire(parms))
            {
                return true;
            }

            if (!WatchUtility.HasActiveWatch(map))
            {
                return true;
            }

            if (comp == null)
            {
                return true;
            }

            int delay = s.RollWarningDelayTicks();
            string hours = ((float)delay / GenDate.TicksPerHour).ToString("F1");
            Find.LetterStack.ReceiveLetter(
                "WR_Letter_EarlyRaidLabel".Translate(),
                "WR_Letter_EarlyRaidText".Translate(hours, parms.faction?.Name ?? "WR_UnknownFaction".Translate()),
                LetterDefOf.ThreatBig,
                new TargetInfo(map.Center, map));

            // Clone via ExposeData round-trip would be heavy; copy common fields only.
            // Points copied explicitly and never raised (design §2.9).
            IncidentParms copy = new IncidentParms();
            copy.target = parms.target;
            copy.points = parms.points;
            copy.faction = parms.faction;
            copy.raidStrategy = parms.raidStrategy;
            copy.raidArrivalMode = parms.raidArrivalMode;
            copy.spawnCenter = parms.spawnCenter;
            copy.spawnRotation = parms.spawnRotation;
            copy.canTimeoutOrFlee = parms.canTimeoutOrFlee;
            copy.biocodeWeaponsChance = parms.biocodeWeaponsChance;
            copy.biocodeApparelChance = parms.biocodeApparelChance;
            // Best-effort extra fields if present on this RW build
            TryCopyField(parms, copy, "pawnGroupKindDef");
            TryCopyField(parms, copy, "pawnGroupKind");
            TryCopyField(parms, copy, "raidArrivalModeForQuickMilitaryAid");
            TryCopyField(parms, copy, "raidAgeRestriction");
            TryCopyField(parms, copy, "quest");
            TryCopyField(parms, copy, "questTag");

            comp.QueueEarlyWarnedRaid(copy, delay);
            __result = true;
            return false;
        }

        private static void TryCopyField(IncidentParms from, IncidentParms to, string name)
        {
            var fi = AccessTools.Field(typeof(IncidentParms), name);
            if (fi == null || fi.IsStatic) return;
            try { fi.SetValue(to, fi.GetValue(from)); }
            catch { /* ignore missing/incompatible */ }
        }
    }

    [HarmonyPatch(typeof(FireWatcher), nameof(FireWatcher.FireWatcherTick))]
    public static class Patch_FireWatcher_FireWatcherTick
    {
        private static readonly FieldInfo MapField =
            AccessTools.Field(typeof(FireWatcher), "map");

        private static readonly MethodInfo UpdateObservationsMethod =
            AccessTools.Method(typeof(FireWatcher), "UpdateObservations");

        private static int lastForcedObserveTick = -99999;

        public static bool Prefix(FireWatcher __instance)
        {
            WatchRosterSettings s = WatchRosterMod.Settings;
            if (s == null || !s.masterEnabled || !s.enableFireWatch)
            {
                return true;
            }

            Map map = MapField?.GetValue(__instance) as Map;
            if (map == null)
            {
                return true;
            }

            float mult = WatchUtility.FireObserveIntervalMult(map);
            if (System.Math.Abs(mult - 1f) < 0.01f)
            {
                return true;
            }

            int ticks = Find.TickManager.TicksGame;

            if (mult < 1f)
            {
                int mannedInterval = System.Math.Max(60, (int)(426f * mult));
                if (ticks - lastForcedObserveTick >= mannedInterval && ticks % mannedInterval == 0)
                {
                    lastForcedObserveTick = ticks;
                    UpdateObservationsMethod?.Invoke(__instance, null);
                }
                return true;
            }

            int stretched = System.Math.Max(426, (int)(426f * mult));
            if (ticks % stretched == 0)
            {
                UpdateObservationsMethod?.Invoke(__instance, null);
            }
            return false;
        }
    }
}
