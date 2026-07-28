using RimWorld;
using Verse;

namespace Landworks
{
    public class CompProperties_SoilStabilizer : CompProperties
    {
        // XML defaults; live values come from mod settings.
        public float radius = 9.9f;
        public float stressReliefPerDay = 4f;

        public CompProperties_SoilStabilizer()
        {
            compClass = typeof(CompSoilStabilizer);
        }
    }

    public class CompSoilStabilizer : ThingComp
    {
        public CompProperties_SoilStabilizer Props => (CompProperties_SoilStabilizer)props;

        public float ActiveRadius
        {
            get
            {
                if (LandworksMod.Settings != null)
                {
                    return LandworksMod.Settings.stabilizerRadius;
                }
                return Props.radius;
            }
        }

        public bool Active
        {
            get
            {
                CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
                if (power != null && !power.PowerOn)
                {
                    return false;
                }
                CompFlickable flick = parent.TryGetComp<CompFlickable>();
                if (flick != null && !flick.SwitchIsOn)
                {
                    return false;
                }
                return true;
            }
        }

        public override string CompInspectStringExtra()
        {
            MapComponent_Landworks comp = MapComponent_Landworks.For(parent.Map);
            string stress = comp != null ? comp.geologicalStress.ToString("F1") : "?";
            if (!Active)
            {
                return "LW_Stabilizer_Offline".Translate(stress);
            }
            return "LW_Stabilizer_Online".Translate(ActiveRadius.ToString("F0"), stress);
        }
    }
}
