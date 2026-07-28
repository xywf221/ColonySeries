using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FordRights
{
    public static class FordUtility
    {
        public const int WaterAdjacencyRadius = 2;

        public static bool IsWaterTerrain(TerrainDef terrain)
        {
            return terrain != null && terrain.IsWater;
        }

        public static bool CellIsWater(Map map, IntVec3 c)
        {
            if (map == null || !c.InBounds(map))
            {
                return false;
            }
            return IsWaterTerrain(c.GetTerrain(map));
        }

        /// <summary>
        /// True if any cell within radius of center is water (cheap local ring, no AllCells).
        /// </summary>
        public static bool IsNearWater(Map map, IntVec3 center, int radius = WaterAdjacencyRadius)
        {
            if (map == null || !center.InBounds(map))
            {
                return false;
            }

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    IntVec3 c = new IntVec3(center.x + dx, 0, center.z + dz);
                    if (CellIsWater(map, c))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Coarse water presence for map-level sleep. Never scans AllCells.
        /// </summary>
        public static bool MapHasUsableWater(Map map)
        {
            if (map == null)
            {
                return false;
            }

            int step = 10;
            for (int x = 0; x < map.Size.x; x += step)
            {
                for (int z = 0; z < map.Size.z; z += step)
                {
                    if (CellIsWater(map, new IntVec3(x, 0, z)))
                    {
                        return true;
                    }
                }
            }

            // Edge strip sample (rivers often cross edges).
            int edgeStep = 6;
            for (int x = 0; x < map.Size.x; x += edgeStep)
            {
                if (CellIsWater(map, new IntVec3(x, 0, 0)) ||
                    CellIsWater(map, new IntVec3(x, 0, map.Size.z - 1)))
                {
                    return true;
                }
            }
            for (int z = 0; z < map.Size.z; z += edgeStep)
            {
                if (CellIsWater(map, new IntVec3(0, 0, z)) ||
                    CellIsWater(map, new IntVec3(map.Size.x - 1, 0, z)))
                {
                    return true;
                }
            }
            return false;
        }

        public static List<Building_FordCrossing> GetSpawnedFords(Map map)
        {
            List<Building_FordCrossing> result = new List<Building_FordCrossing>();
            if (map == null || FordRightsDefOf.FR_FordCrossing == null)
            {
                return result;
            }

            List<Thing> list = map.listerThings.ThingsOfDef(FordRightsDefOf.FR_FordCrossing);
            if (list == null)
            {
                return result;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Building_FordCrossing ford && ford.Spawned)
                {
                    result.Add(ford);
                }
            }
            return result;
        }

        public static Building_FordCrossing FindBestOperationalFord(Map map)
        {
            List<Building_FordCrossing> fords = GetSpawnedFords(map);
            Building_FordCrossing best = null;
            for (int i = 0; i < fords.Count; i++)
            {
                Building_FordCrossing f = fords[i];
                if (!f.IsOperational)
                {
                    continue;
                }
                if (best == null || f.HitPoints > best.HitPoints)
                {
                    best = f;
                }
            }
            return best;
        }

        public static bool ResearchDone()
        {
            return FordRightsDefOf.FR_RiverRights == null ||
                   FordRightsDefOf.FR_RiverRights.IsFinished;
        }
    }
}
