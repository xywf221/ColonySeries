using System.Collections.Generic;
using Verse;

namespace SalvageAtlas
{
    /// <summary>
    /// Per-map salvage bookkeeping. Idle when no designations / no mechs.
    /// Tracks which corpses/wrecks were already fragment-scavenged.
    /// </summary>
    public class MapComponent_SalvageAtlas : MapComponent
    {
        private HashSet<int> salvagedThingIds = new HashSet<int>();

        public MapComponent_SalvageAtlas(Map map) : base(map)
        {
        }

        public static MapComponent_SalvageAtlas For(Map map)
        {
            return map?.GetComponent<MapComponent_SalvageAtlas>();
        }

        public bool WasSalvaged(Thing t)
        {
            return t != null && salvagedThingIds.Contains(t.thingIDNumber);
        }

        public void MarkSalvaged(int thingId)
        {
            salvagedThingIds.Add(thingId);
        }

        public override void MapComponentTick()
        {
            // Intentionally idle — work is designation/job driven.
            // Rare cleanup of despawned IDs is unnecessary (ints are cheap).
        }

        public override void ExposeData()
        {
            List<int> list = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                list = new List<int>(salvagedThingIds);
            }
            Scribe_Collections.Look(ref list, "salvagedThingIds", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                salvagedThingIds = list != null ? new HashSet<int>(list) : new HashSet<int>();
            }
        }
    }
}
