using RimWorld;
using UnityEngine;
using Verse;

namespace Landworks
{
    public class IncidentWorker_GeologicalBacklash : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            LandworksSettings settings = LandworksMod.Settings;
            if (settings != null && !settings.enableGeologicalBacklash)
            {
                return false;
            }
            Map map = (Map)parms.target;
            MapComponent_Landworks comp = MapComponent_Landworks.For(map);
            if (comp == null)
            {
                return false;
            }
            float min = settings != null ? settings.backlashMinStress : 30f;
            // O(1): only need stress + tracked engineered cells.
            return comp.geologicalStress >= min && comp.EngineeredCount > 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            MapComponent_Landworks comp = MapComponent_Landworks.For(map);
            if (comp == null)
            {
                return false;
            }

            LandworksSettings settings = LandworksMod.Settings;
            float factor = Mathf.Clamp01(comp.geologicalStress / 100f);
            float severity = settings != null ? settings.backlashSeverityMultiplier : 1f;
            if (settings != null && settings.easyMode)
            {
                severity *= 0.5f;
            }

            int count = Mathf.RoundToInt(Mathf.Lerp(8f, 45f, factor) * severity);
            count = Mathf.Clamp(count, 1, 80);
            comp.ForceDegradeRandom(count, toHardpan: false);

            IntVec3 center;
            if (!CellFinder.TryFindRandomCellNear(map.Center, map, 40, c => c.Standable(map) && !c.Fogged(map), out center))
            {
                center = map.Center;
            }

            // Mud splash is small and bounded — not a map scan.
            int mudCells = Mathf.RoundToInt(count * 0.35f);
            mudCells = Mathf.Min(mudCells, 40);
            for (int i = 0; i < mudCells; i++)
            {
                IntVec3 c = center + GenRadial.RadialPattern[i % GenRadial.RadialPattern.Length];
                if (!c.InBounds(map) || c.GetEdifice(map) != null)
                {
                    continue;
                }
                TerrainDef t = c.GetTerrain(map);
                if (t.IsWater || t.layerable)
                {
                    continue;
                }
                TerrainUtility.SetTerrainSilent(map, c, TerrainDefOf.Mud);
                FilthMaker.TryMakeFilth(c, map, ThingDefOf.Filth_Dirt, 2);
            }

            SendStandardLetter(parms, new TargetInfo(center, map));
            return true;
        }
    }

    public class IncidentWorker_SoilCollapse : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            LandworksSettings settings = LandworksMod.Settings;
            if (settings != null && !settings.enableGeologicalBacklash)
            {
                return false;
            }
            Map map = (Map)parms.target;
            MapComponent_Landworks comp = MapComponent_Landworks.For(map);
            if (comp == null)
            {
                return false;
            }
            float min = settings != null ? settings.backlashMinStress + 10f : 40f;
            // O(1): degradable tracked cells are amended/tilled only.
            return comp.geologicalStress >= min && comp.DegradableCount >= 6;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = (Map)parms.target;
            MapComponent_Landworks comp = MapComponent_Landworks.For(map);
            if (comp == null)
            {
                return false;
            }

            LandworksSettings settings = LandworksMod.Settings;
            float severity = settings != null ? settings.backlashSeverityMultiplier : 1f;
            if (settings != null && settings.easyMode)
            {
                severity *= 0.5f;
            }

            int count = Mathf.RoundToInt(Rand.RangeInclusive(10, 28) * severity);
            count = Mathf.Clamp(count, 1, 60);
            comp.ForceDegradeRandom(count, toHardpan: true);
            SendStandardLetter(parms, new TargetInfo(map.Center, map));
            return true;
        }
    }
}
