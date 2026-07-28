using RimWorld;
using Verse;

namespace TraitExtractor
{
    public class CompProperties_TraitSerum : CompProperties
    {
        public CompProperties_TraitSerum()
        {
            compClass = typeof(CompTraitSerum);
        }
    }

    public class CompTraitSerum : ThingComp
    {
        public TraitDef traitDef;
        public int degree;
        public string sourceName = string.Empty;

        public bool HasTrait => traitDef != null;

        public override void PostExposeData()
        {
            Scribe_Defs.Look(ref traitDef, "traitDef");
            Scribe_Values.Look(ref degree, "degree", 0);
            Scribe_Values.Look(ref sourceName, "sourceName", string.Empty);
        }

        public override bool AllowStackWith(Thing other)
        {
            return false;
        }

        public override string CompInspectStringExtra()
        {
            if (!HasTrait)
            {
                return "TE_SerumEmpty".Translate();
            }
            Trait t = new Trait(traitDef, degree);
            string s = "TE_SerumInspect".Translate(t.LabelCap);
            if (!sourceName.NullOrEmpty())
            {
                s += "\n" + "TE_SerumSource".Translate(sourceName);
            }
            return s;
        }

        public override string TransformLabel(string label)
        {
            if (!HasTrait)
            {
                return label;
            }
            Trait t = new Trait(traitDef, degree);
            return "TE_SerumLabel".Translate(t.LabelCap);
        }
    }
}
