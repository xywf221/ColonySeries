using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace QuarantineLine
{
    /// <summary>
    /// Rare: a caravan return / visitor seeds a mild infectious disease onto one colonist.
    /// Low baseChance; settings toggle.
    /// </summary>
    public class IncidentWorker_CaravanDisease : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            QuarantineLineSettings s = QuarantineLineMod.Settings;
            if (s == null || !s.modEnabled || !s.enableCaravanDisease)
            {
                return false;
            }
            Map map = parms.target as Map;
            if (map == null)
            {
                return false;
            }
            return map.mapPawns.FreeColonistsSpawned.Count > 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            QuarantineLineSettings s = QuarantineLineMod.Settings;
            if (s == null || !s.modEnabled || !s.enableCaravanDisease)
            {
                return false;
            }

            Map map = (Map)parms.target;
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            if (colonists == null || colonists.Count == 0)
            {
                return false;
            }

            HediffDef disease = PickHumanDisease();
            if (disease == null)
            {
                return false;
            }

            Pawn victim = colonists.Where(p => QuarantineUtility.CanCatch(p, disease)).RandomElementWithFallback();
            if (victim == null)
            {
                return false;
            }

            if (!QuarantineUtility.TrySeedInfection(victim, disease, initialSeverity: 0.05f))
            {
                return false;
            }

            MapComponent_Quarantine.For(map)?.NotifyPossibleInfection();

            SendStandardLetter(
                "QL_Letter_CaravanDiseaseTitle".Translate(),
                "QL_Letter_CaravanDiseaseBody".Translate(victim.LabelShort, disease.label),
                LetterDefOf.NegativeEvent,
                parms,
                victim);
            return true;
        }

        internal static HediffDef PickHumanDisease()
        {
            // Prefer common immunizable infections; fall back gracefully.
            string[] names = { "Flu", "Malaria", "GutWorms", "Plague", "SleepingSickness" };
            List<HediffDef> pool = new List<HediffDef>();
            for (int i = 0; i < names.Length; i++)
            {
                HediffDef d = DefDatabase<HediffDef>.GetNamedSilentFail(names[i]);
                if (d != null)
                {
                    // Weight flu/malaria higher via duplicates
                    int w = (names[i] == "Flu" || names[i] == "Malaria") ? 3 : 1;
                    if (names[i] == "Plague")
                    {
                        w = QuarantineLineMod.Settings != null && QuarantineLineMod.Settings.easyMode ? 0 : 1;
                    }
                    for (int k = 0; k < w; k++)
                    {
                        pool.Add(d);
                    }
                }
            }
            return pool.Count > 0 ? pool.RandomElement() : null;
        }
    }

    /// <summary>
    /// Rare: a colony animal is found with animal flu/plague — zoonotic flavor seed.
    /// </summary>
    public class IncidentWorker_AnimalSource : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            QuarantineLineSettings s = QuarantineLineMod.Settings;
            if (s == null || !s.modEnabled || !s.enableAnimalSource)
            {
                return false;
            }
            Map map = parms.target as Map;
            if (map == null)
            {
                return false;
            }
            List<Pawn> animals = map.mapPawns.SpawnedColonyAnimals;
            return animals != null && animals.Count > 0;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            QuarantineLineSettings s = QuarantineLineMod.Settings;
            if (s == null || !s.modEnabled || !s.enableAnimalSource)
            {
                return false;
            }

            Map map = (Map)parms.target;
            List<Pawn> animals = map.mapPawns.SpawnedColonyAnimals;
            if (animals == null || animals.Count == 0)
            {
                return false;
            }

            HediffDef disease = DefDatabase<HediffDef>.GetNamedSilentFail("Animal_Flu")
                                ?? DefDatabase<HediffDef>.GetNamedSilentFail("Animal_Plague")
                                ?? DefDatabase<HediffDef>.GetNamedSilentFail("Flu");
            if (disease == null)
            {
                return false;
            }

            Pawn animal = animals.Where(p => p != null && !p.Dead && QuarantineUtility.CanCatch(p, disease))
                .RandomElementWithFallback();
            if (animal == null)
            {
                return false;
            }

            if (!QuarantineUtility.TrySeedInfection(animal, disease, initialSeverity: 0.08f))
            {
                return false;
            }

            MapComponent_Quarantine.For(map)?.NotifyPossibleInfection();

            SendStandardLetter(
                "QL_Letter_AnimalSourceTitle".Translate(),
                "QL_Letter_AnimalSourceBody".Translate(animal.LabelShort, disease.label),
                LetterDefOf.NegativeEvent,
                parms,
                animal);
            return true;
        }
    }
}
