using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SalvageAtlas
{
    public class CompProperties_AtlasSchematic : CompProperties
    {
        public CompProperties_AtlasSchematic()
        {
            compClass = typeof(CompAtlasSchematic);
        }
    }

    /// <summary>
    /// One-shot blueprint item. Use from inventory / float menu to spawn a small loot package.
    /// Not a permanent global buff.
    /// </summary>
    public class CompAtlasSchematic : ThingComp
    {
        public int blueprintRank = 1;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref blueprintRank, "blueprintRank", 1);
        }

        public override string CompInspectStringExtra()
        {
            return "SA_Schematic_Rank".Translate(blueprintRank);
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            if (selPawn == null || !selPawn.IsColonistPlayerControlled)
            {
                yield break;
            }
            yield return new FloatMenuOption(
                "SA_Float_DecodeSchematic".Translate(parent.LabelCap),
                () => Decode(selPawn));
        }

        public void Decode(Pawn user)
        {
            if (parent.Destroyed)
            {
                return;
            }
            Map map = user?.Map ?? parent.Map;
            IntVec3 cell = parent.PositionHeld;
            if (map == null)
            {
                return;
            }

            List<ThingDefCountClass> loot = BuildLoot(blueprintRank);
            foreach (ThingDefCountClass d in loot)
            {
                if (d?.thingDef == null || d.count <= 0)
                {
                    continue;
                }
                Thing t = ThingMaker.MakeThing(d.thingDef);
                t.stackCount = Mathf.Min(d.count, d.thingDef.stackLimit);
                GenPlace.TryPlaceThing(t, cell, map, ThingPlaceMode.Near);
            }

            Messages.Message(
                "SA_Msg_SchematicDecoded".Translate(user?.LabelShort ?? "?", blueprintRank),
                new TargetInfo(cell, map),
                MessageTypeDefOf.PositiveEvent);

            parent.SplitOff(1).Destroy(DestroyMode.Vanish);
        }

        private static List<ThingDefCountClass> BuildLoot(int rank)
        {
            var list = new List<ThingDefCountClass>();
            rank = Mathf.Clamp(rank, 1, 4);
            switch (rank)
            {
                case 1:
                    list.Add(new ThingDefCountClass(ThingDefOf.ComponentIndustrial, Rand.RangeInclusive(2, 4)));
                    list.Add(new ThingDefCountClass(ThingDefOf.Steel, Rand.RangeInclusive(20, 40)));
                    break;
                case 2:
                    list.Add(new ThingDefCountClass(ThingDefOf.ComponentIndustrial, Rand.RangeInclusive(3, 5)));
                    ThingDef gun = DefDatabase<ThingDef>.GetNamedSilentFail("Gun_ChargeRifle")
                                    ?? DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle")
                                    ?? DefDatabase<ThingDef>.GetNamedSilentFail("Gun_Revolver")
                                    ?? DefDatabase<ThingDef>.GetNamedSilentFail("Gun_Autopistol");
                    if (gun != null && Rand.Chance(0.55f))
                    {
                        list.Add(new ThingDefCountClass(gun, 1));
                    }
                    else
                    {
                        list.Add(new ThingDefCountClass(ThingDefOf.Steel, Rand.RangeInclusive(30, 55)));
                    }
                    break;
                case 3:
                    ThingDef armor = DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_FlakJacket")
                                     ?? DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_FlakVest")
                                     ?? DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_JacketSimple");
                    if (armor != null)
                    {
                        list.Add(new ThingDefCountClass(armor, 1));
                    }
                    if (ThingDefOf.Plasteel != null)
                    {
                        list.Add(new ThingDefCountClass(ThingDefOf.Plasteel, Rand.RangeInclusive(10, 25)));
                    }
                    list.Add(new ThingDefCountClass(ThingDefOf.ComponentIndustrial, Rand.RangeInclusive(2, 4)));
                    break;
                case 4:
                    if (ThingDefOf.ComponentSpacer != null)
                    {
                        list.Add(new ThingDefCountClass(ThingDefOf.ComponentSpacer, Rand.RangeInclusive(1, 2)));
                    }
                    // AI persona core is too strong as free drop — use power focus or uranium.
                    ThingDef rare = DefDatabase<ThingDef>.GetNamedSilentFail("Uranium")
                                    ?? ThingDefOf.Plasteel
                                    ?? ThingDefOf.Steel;
                    if (rare != null)
                    {
                        list.Add(new ThingDefCountClass(rare, Rand.RangeInclusive(15, 30)));
                    }
                    break;
            }
            return list;
        }
    }
}
