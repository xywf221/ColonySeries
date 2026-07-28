using System.Collections.Generic;
using RimWorld;
using Verse;

namespace WatchRoster
{
    /// <summary>
    /// Tracks deferred raid early-warnings. Does not raise raid points or touch doors.
    /// </summary>
    public class MapComponent_WatchRoster : MapComponent
    {
        private List<PendingRaidWarning> pending = new List<PendingRaidWarning>();

        /// <summary>Parms currently being fired from our deferred queue (object identity).</summary>
        private readonly HashSet<IncidentParms> deferredFiring = new HashSet<IncidentParms>();

        public MapComponent_WatchRoster(Map map) : base(map)
        {
        }

        public static MapComponent_WatchRoster For(Map map)
        {
            return map?.GetComponent<MapComponent_WatchRoster>();
        }

        public override void MapComponentTick()
        {
            if (pending == null || pending.Count == 0)
            {
                return;
            }

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingRaidWarning p = pending[i];
                if (Find.TickManager.TicksGame < p.fireAtTick)
                {
                    continue;
                }

                pending.RemoveAt(i);
                FireDeferredRaid(p);
            }
        }

        public void QueueEarlyWarnedRaid(IncidentParms parms, int delayTicks)
        {
            if (parms == null)
            {
                return;
            }

            pending.Add(new PendingRaidWarning
            {
                parms = parms,
                fireAtTick = Find.TickManager.TicksGame + delayTicks,
                incidentDef = IncidentDefOf.RaidEnemy
            });
        }

        public bool IsDeferredFire(IncidentParms parms)
        {
            return parms != null && deferredFiring.Contains(parms);
        }

        private void FireDeferredRaid(PendingRaidWarning p)
        {
            if (p.parms == null || p.incidentDef == null)
            {
                return;
            }

            if (p.parms.target is Map m && m != map)
            {
                return;
            }

            p.parms.target = map;
            deferredFiring.Add(p.parms);
            try
            {
                p.incidentDef.Worker.TryExecute(p.parms);
            }
            catch (System.Exception e)
            {
                Log.Error("[WatchRoster] Deferred raid failed: " + e);
            }
            finally
            {
                deferredFiring.Remove(p.parms);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pending, "pendingRaidWarnings", LookMode.Deep);
            if (pending == null)
            {
                pending = new List<PendingRaidWarning>();
            }
        }

        private class PendingRaidWarning : IExposable
        {
            public IncidentParms parms;
            public int fireAtTick;
            public IncidentDef incidentDef;

            public void ExposeData()
            {
                Scribe_Deep.Look(ref parms, "parms");
                Scribe_Values.Look(ref fireAtTick, "fireAtTick", 0);
                Scribe_Defs.Look(ref incidentDef, "incidentDef");
            }
        }
    }
}
