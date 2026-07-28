using RimWorld;
using Verse;

namespace ArtisanMark
{
    /// <summary>
    /// Inject CompArtisanMark onto eligible ThingDefs at startup (weapons, apparel, art, prostheses).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ThingCompInjector
    {
        static ThingCompInjector()
        {
            int count = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null)
                {
                    continue;
                }

                // Eligibility uses settings; if settings not ready yet, inject broadly for weapon/apparel/art/prosthesis.
                if (IsStructurallyEligible(def))
                {
                    ArtisanMarkUtility.EnsureCompOnDef(def);
                    // EnsureCompOnDef re-checks settings; force-add structural eligibles even if settings null.
                    ForceEnsure(def);
                    count++;
                }
            }

            Log.Message($"[ArtisanMark] Injected CompArtisanMark onto {count} eligible ThingDefs.");
        }

        private static bool IsStructurallyEligible(ThingDef def)
        {
            if (def.IsIngestible || def.IsDrug)
            {
                return false;
            }
            if (typeof(MinifiedThing).IsAssignableFrom(def.thingClass))
            {
                return false;
            }
            if (def.IsWeapon || def.IsApparel || def.IsArt || def.isTechHediff)
            {
                return true;
            }
            if (def.HasComp(typeof(RimWorld.CompArt)))
            {
                return true;
            }
            return false;
        }

        private static void ForceEnsure(ThingDef def)
        {
            if (def.comps == null)
            {
                def.comps = new System.Collections.Generic.List<CompProperties>();
            }
            for (int i = 0; i < def.comps.Count; i++)
            {
                if (def.comps[i] is CompProperties_ArtisanMark)
                {
                    return;
                }
            }
            def.comps.Add(new CompProperties_ArtisanMark());
        }
    }
}
