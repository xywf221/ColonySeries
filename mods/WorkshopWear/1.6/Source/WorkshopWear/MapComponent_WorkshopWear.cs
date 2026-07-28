using System.Collections.Generic;
using Verse;

namespace WorkshopWear
{
    /// <summary>
    /// Work tables are usually tickerType Never, so CompTickRare never runs.
    /// This component pulses overwork countdowns on a rare interval without AllCells.
    /// </summary>
    public class MapComponent_WorkshopWear : MapComponent
    {
        private const int PulseInterval = 250; // matches rare tick step used by Comp
        private int ticksToPulse;

        public MapComponent_WorkshopWear(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            var settings = WorkshopWearMod.Settings;
            if (settings != null && !settings.modEnabled)
            {
                return;
            }

            ticksToPulse--;
            if (ticksToPulse > 0)
            {
                return;
            }
            ticksToPulse = PulseInterval;

            List<Building> buildings = map.listerBuildings?.allBuildingsColonist;
            if (buildings == null)
            {
                return;
            }

            for (int i = 0; i < buildings.Count; i++)
            {
                CompWorkshopWear comp = buildings[i].GetComp<CompWorkshopWear>();
                if (comp == null || !comp.overworkActive)
                {
                    continue;
                }
                comp.TickOverworkPulse(PulseInterval);
            }
        }
    }
}
