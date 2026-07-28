using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace TraitExtractor
{
    public static class TraitUtility
    {
        public static bool HasStory(Pawn pawn)
        {
            return pawn?.story?.traits != null;
        }

        public static bool IsOnCooldown(Pawn pawn)
        {
            return pawn.health?.hediffSet?.HasHediff(TraitExtractorDefOf.TE_ExtractionCooldown) == true;
        }

        public static bool CanExtractTrait(Trait trait, out string reason)
        {
            reason = null;
            if (trait == null)
            {
                reason = "TE_Reason_NoTrait".Translate();
                return false;
            }
            TraitExtractorSettings s = TraitExtractorMod.Settings;
            if (s != null && s.blockScenarioForced && trait.ScenForced)
            {
                reason = "TE_Reason_ScenForced".Translate(trait.LabelCap);
                return false;
            }
            if (s != null && s.blockGeneBoundTraits && trait.sourceGene != null)
            {
                reason = "TE_Reason_GeneBound".Translate(trait.LabelCap);
                return false;
            }
            if (trait.Suppressed)
            {
                reason = "TE_Reason_Suppressed".Translate(trait.LabelCap);
                return false;
            }
            return true;
        }

        public static List<Trait> ExtractableTraits(Pawn pawn)
        {
            List<Trait> result = new List<Trait>();
            if (!HasStory(pawn))
            {
                return result;
            }
            foreach (Trait t in pawn.story.traits.allTraits)
            {
                if (CanExtractTrait(t, out _))
                {
                    result.Add(t);
                }
            }
            return result;
        }

        public static bool HasSameTrait(Pawn pawn, TraitDef def, int degree)
        {
            if (!HasStory(pawn))
            {
                return false;
            }
            return pawn.story.traits.HasTrait(def, degree);
        }

        public static List<Trait> ConflictingTraits(Pawn pawn, TraitDef def, int degree)
        {
            List<Trait> list = new List<Trait>();
            if (!HasStory(pawn))
            {
                return list;
            }
            Trait probe = new Trait(def, degree);
            foreach (Trait t in pawn.story.traits.allTraits)
            {
                if (t.def == def && t.Degree != degree)
                {
                    list.Add(t);
                    continue;
                }
                if (t.def.ConflictsWith(probe))
                {
                    list.Add(t);
                }
            }
            return list;
        }

        public static void GiveCooldown(Pawn pawn)
        {
            if (pawn?.health == null || TraitExtractorDefOf.TE_ExtractionCooldown == null)
            {
                return;
            }
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(TraitExtractorDefOf.TE_ExtractionCooldown);
            if (existing != null)
            {
                existing.Severity = 1f;
                return;
            }
            Hediff h = HediffMaker.MakeHediff(TraitExtractorDefOf.TE_ExtractionCooldown, pawn);
            // SeverityPerDay drives duration; scale initial severity by settings via severityPerDay is fixed in XML.
            // Approximate: base ~14 days at -0.07/day from severity 1. For longer/shorter, adjust severity.
            float days = TraitExtractorMod.Settings?.CooldownDays ?? 12f;
            // severity / 0.07 ≈ days  => severity = days * 0.07, clamp
            h.Severity = UnityEngine.Mathf.Clamp(days * 0.07f, 0.2f, 1f);
            pawn.health.AddHediff(h);
        }

        public static void GiveThought(Pawn pawn, ThoughtDef def)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null || def == null)
            {
                return;
            }
            pawn.needs.mood.thoughts.memories.TryGainMemory(def);
        }

        public static Thing MakeSerum(TraitDef def, int degree, string sourceName)
        {
            Thing serum = ThingMaker.MakeThing(TraitExtractorDefOf.TE_TraitSerum);
            CompTraitSerum comp = serum.TryGetComp<CompTraitSerum>();
            if (comp != null)
            {
                comp.traitDef = def;
                comp.degree = degree;
                comp.sourceName = sourceName ?? string.Empty;
            }
            return serum;
        }

        public static string SerumLabel(TraitDef def, int degree)
        {
            if (def == null)
            {
                return "TE_SerumEmpty".Translate();
            }
            Trait t = new Trait(def, degree);
            return "TE_SerumLabel".Translate(t.LabelCap);
        }

        public static string SerumDescription(CompTraitSerum comp)
        {
            if (comp?.traitDef == null)
            {
                return "TE_SerumEmptyDesc".Translate();
            }
            Trait t = new Trait(comp.traitDef, comp.degree);
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("TE_SerumDesc".Translate(t.LabelCap, t.CurrentData.description));
            if (!comp.sourceName.NullOrEmpty())
            {
                sb.AppendLine("TE_SerumSource".Translate(comp.sourceName));
            }
            return sb.ToString().TrimEnd();
        }

        public static void NotifyNearbyWitnesses(Pawn patient, Map map)
        {
            if (map == null || patient == null)
            {
                return;
            }
            foreach (Pawn p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p == patient || p.Dead || p.Downed)
                {
                    continue;
                }
                if (p.Position.DistanceTo(patient.Position) > 12f)
                {
                    continue;
                }
                if (p.needs?.mood?.thoughts?.memories == null)
                {
                    continue;
                }
                // Bloodlust / psychopath less bothered
                if (p.story?.traits != null && (p.story.traits.HasTrait(TraitDefOf.Psychopath) || p.story.traits.HasTrait(TraitDefOf.Bloodlust)))
                {
                    continue;
                }
                GiveThought(p, TraitExtractorDefOf.TE_WitnessedExtraction);
            }
        }
    }
}
