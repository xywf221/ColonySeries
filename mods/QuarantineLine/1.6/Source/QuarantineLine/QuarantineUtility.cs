using System.Collections.Generic;
using RimWorld;
using Verse;

namespace QuarantineLine
{
    public static class QuarantineUtility
    {
        public static bool Enabled =>
            QuarantineLineMod.Settings == null || QuarantineLineMod.Settings.modEnabled;

        public static bool SpreadEnabled =>
            Enabled
            && QuarantineLineMod.Settings != null
            && QuarantineLineMod.Settings.enableSpreadModifiers;

        /// <summary>
        /// True for hediffs that are infections or carry HediffComp_Immunizable
        /// (flu, plague, malaria, wound infection, etc.). Not food poisoning / pregnancy.
        /// </summary>
        public static bool IsInfectiousHediff(Hediff hediff)
        {
            if (hediff?.def == null || hediff.def.isBad == false)
            {
                return false;
            }
            if (hediff.def.isInfection)
            {
                return true;
            }
            return hediff.TryGetComp<HediffComp_Immunizable>() != null;
        }

        public static bool IsInfectiousCarrier(Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null || pawn.Dead)
            {
                return false;
            }
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (IsInfectiousHediff(hediffs[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public static Hediff FirstInfectiousHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null)
            {
                return null;
            }
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (IsInfectiousHediff(hediffs[i]))
                {
                    return hediffs[i];
                }
            }
            return null;
        }

        public static bool MapHasInfectiousDisease(Map map)
        {
            if (map?.mapPawns == null)
            {
                return false;
            }
            // Player-relevant only: colonists, prisoners, colony animals (bounded lists).
            if (AnyCarrier(map.mapPawns.FreeColonistsSpawned))
            {
                return true;
            }
            if (AnyCarrier(map.mapPawns.PrisonersOfColonySpawned))
            {
                return true;
            }
            List<Pawn> animals = map.mapPawns.SpawnedColonyAnimals;
            if (animals != null && AnyCarrier(animals))
            {
                return true;
            }
            return false;
        }

        private static bool AnyCarrier(List<Pawn> pawns)
        {
            if (pawns == null)
            {
                return false;
            }
            for (int i = 0; i < pawns.Count; i++)
            {
                if (IsInfectiousCarrier(pawns[i]))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool CanCatch(Pawn pawn, HediffDef disease)
        {
            if (pawn == null || disease == null || pawn.Dead || !pawn.RaceProps.IsFlesh)
            {
                return false;
            }
            if (pawn.health?.hediffSet == null)
            {
                return false;
            }
            if (pawn.health.hediffSet.HasHediff(disease))
            {
                return false;
            }
            // Already immune / high immunity blocks new case.
            if (pawn.health.immunity != null)
            {
                float imm = pawn.health.immunity.GetImmunity(disease);
                if (imm >= 0.6f)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool TrySeedInfection(Pawn pawn, HediffDef disease, float initialSeverity = 0.001f)
        {
            if (!CanCatch(pawn, disease))
            {
                return false;
            }
            Hediff hediff = HediffMaker.MakeHediff(disease, pawn);
            hediff.Severity = initialSeverity;
            pawn.health.AddHediff(hediff);
            return true;
        }

        public static bool SameRoomOrClose(Pawn a, Pawn b, float maxDist)
        {
            if (a?.Map == null || b?.Map == null || a.Map != b.Map)
            {
                return false;
            }
            if (!a.Position.InHorDistOf(b.Position, maxDist))
            {
                return false;
            }
            Room ra = a.GetRoom();
            Room rb = b.GetRoom();
            if (ra != null && rb != null && ra == rb)
            {
                return true;
            }
            // Open air / no room: distance alone.
            return ra == null || rb == null;
        }

        public static string InspectLine(Pawn pawn)
        {
            if (!Enabled || pawn?.Map == null)
            {
                return null;
            }
            MapComponent_Quarantine comp = MapComponent_Quarantine.For(pawn.Map);
            if (comp == null || !comp.IsAwake)
            {
                return null;
            }
            bool inZone = comp.IsQuarantineCell(pawn.Position);
            bool sick = IsInfectiousCarrier(pawn);
            if (!inZone && !sick)
            {
                return null;
            }
            if (inZone && sick)
            {
                return comp.StrictIsolation
                    ? "QL_Inspect_IsolatedStrict".Translate(pawn.LabelShort).ToString()
                    : "QL_Inspect_Quarantined".Translate(pawn.LabelShort).ToString();
            }
            if (inZone && !sick)
            {
                return "QL_Inspect_VisitingWard".Translate(pawn.LabelShort).ToString();
            }
            if (sick && !inZone)
            {
                return "QL_Inspect_SickUncontained".Translate(pawn.LabelShort).ToString();
            }
            return null;
        }
    }
}
