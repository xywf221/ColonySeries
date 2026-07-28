using RimWorld;
using Verse;

namespace WorkshopWear
{
    [DefOf]
    public static class WorkshopWearDefOf
    {
        public static JobDef WW_SlapdashRepair;
        public static JobDef WW_ProperRepair;

        // WorkGiver defs share defNames with jobs; access via DefDatabase when needed.
        // Do not duplicate field names here.

        static WorkshopWearDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WorkshopWearDefOf));
        }
    }
}
