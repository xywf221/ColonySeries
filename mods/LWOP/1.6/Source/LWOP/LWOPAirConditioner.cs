using RimWorld;
using UnityEngine;
using Verse;

namespace LWOP.Buildings
{
    public class CompProperties_LWOPAirConditioner : CompProperties_TempControl
    {
        public CompProperties_LWOPAirConditioner()
        {
            compClass = typeof(CompLWOPAirConditioner);
        }
    }

    public class CompLWOPAirConditioner : CompTempControl
    {
        private CompPowerTrader powerTrader;

        public override void CompTickRare()
        {
            CompPowerTrader power = PowerTraderCached;
            if (power == null || !power.PowerOn)
            {
                return;
            }

            Room room = RegionAndRoomQuery.GetRoom(parent, RegionType.Set_Passable);
            bool active = false;
            if (room != null && !room.UsesOutdoorTemperature)
            {
                float current = room.Temperature;
                float target = TargetTemperature;
                active = !Mathf.Approximately(current, target);
                if (active)
                {
                    room.Temperature = target;
                }
            }

            CompProperties_Power powerProps = power.Props;
            float factor = active ? 1f : Props.lowPowerConsumptionFactor;
            power.PowerOutput = -powerProps.PowerConsumption * factor;
            operatingAtHighPower = active;
        }

        public override string CompInspectStringExtra()
        {
            Room room = RegionAndRoomQuery.GetRoom(parent, RegionType.Set_Passable);
            if (room == null || room.UsesOutdoorTemperature)
            {
                return "LWOP air conditioner inactive: room is not sealed.";
            }

            return null;
        }

        private CompPowerTrader PowerTraderCached
        {
            get
            {
                if (powerTrader == null)
                {
                    powerTrader = parent.GetComp<CompPowerTrader>();
                }

                return powerTrader;
            }
        }
    }
}
