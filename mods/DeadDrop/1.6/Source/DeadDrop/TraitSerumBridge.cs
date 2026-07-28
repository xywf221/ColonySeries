using Verse;

namespace DeadDrop
{
    /// <summary>
    /// Soft-link to Trait Extractor serums. Silent if TE is absent.
    /// </summary>
    public static class TraitSerumBridge
    {
        private static bool resolved;
        private static ThingDef serumDef;

        public static ThingDef SerumDef
        {
            get
            {
                if (!resolved)
                {
                    resolved = true;
                    serumDef = DefDatabase<ThingDef>.GetNamedSilentFail("TE_TraitSerum");
                }
                return serumDef;
            }
        }

        public static bool Available => SerumDef != null;
    }
}
