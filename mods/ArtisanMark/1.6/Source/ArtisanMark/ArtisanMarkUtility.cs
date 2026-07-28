using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ArtisanMark
{
    public static class ArtisanMarkUtility
    {
        public static bool IsEligibleDef(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }

            var s = ArtisanMarkMod.Settings;
            if (s == null || !s.modEnabled)
            {
                return false;
            }

            // Never stamp one-shot consumables / food / drugs / shells that are pure ammo-ish consumables.
            if (def.IsIngestible)
            {
                return false;
            }
            if (def.IsDrug)
            {
                return false;
            }

            // Minified wrapper itself is not stamped; inner is.
            if (typeof(MinifiedThing).IsAssignableFrom(def.thingClass))
            {
                return false;
            }

            if (s.stampWeapons && def.IsWeapon)
            {
                return true;
            }

            if (s.stampApparel && def.IsApparel)
            {
                return true;
            }

            if (s.stampArt && def.IsArt)
            {
                return true;
            }

            // Sculptures / art items that carry CompArt even if not BuildingsArt category.
            if (s.stampArt && def.HasComp(typeof(CompArt)))
            {
                return true;
            }

            // Artificial body parts / prostheses (design: 人造躯体).
            if (s.stampProstheses && def.isTechHediff)
            {
                return true;
            }

            return false;
        }

        public static CompArtisanMark GetMark(Thing thing)
        {
            if (thing == null)
            {
                return null;
            }

            if (thing is MinifiedThing mini && mini.InnerThing != null)
            {
                thing = mini.InnerThing;
            }

            return thing.TryGetComp<CompArtisanMark>();
        }

        public static void EnsureCompOnDef(ThingDef def)
        {
            if (def == null || !IsEligibleDef(def))
            {
                return;
            }

            if (def.comps == null)
            {
                def.comps = new List<CompProperties>();
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

        public static void StampProduct(Thing product, Pawn worker)
        {
            if (product == null || worker == null)
            {
                return;
            }

            var s = ArtisanMarkMod.Settings;
            if (s == null || !s.modEnabled)
            {
                return;
            }

            Thing target = product;
            if (product is MinifiedThing mini && mini.InnerThing != null)
            {
                target = mini.InnerThing;
            }

            if (target?.def == null || !IsEligibleDef(target.def))
            {
                return;
            }

            // Runtime-add if def injection missed (modded defs loaded late, etc.).
            if (target is ThingWithComps twc)
            {
                CompArtisanMark existing = twc.GetComp<CompArtisanMark>();
                if (existing == null)
                {
                    // Def may lack the prop — try inject for future spawns, stamp only if we can attach.
                    EnsureCompOnDef(target.def);
                    // Cannot safely AddComp mid-flight without ThingWithComps internals;
                    // rely on StaticConstructor injection for normal path.
                    existing = twc.GetComp<CompArtisanMark>();
                }

                if (existing != null && !existing.HasMaker)
                {
                    existing.Stamp(worker);
                }
            }
        }

        /// <summary>
        /// Friend / lover / bonded for legacy mood. Opinion-based "friend" uses DirectRelations + high opinion.
        /// </summary>
        public static bool IsCloseBond(Pawn wearer, Pawn maker)
        {
            if (wearer == null || maker == null || wearer == maker)
            {
                return false;
            }

            if (wearer.relations == null)
            {
                return false;
            }

            // Lover / spouse / fiancé family
            if (LovePartnerRelationUtility.LovePartnerRelationExists(wearer, maker))
            {
                return true;
            }

            // Bonded animal/human bond relation
            if (wearer.relations.DirectRelationExists(PawnRelationDefOf.Bond, maker))
            {
                return true;
            }

            // Close friend: mutual high opinion (threshold ~40, vanilla "friend" territory)
            int opinion = wearer.relations.OpinionOf(maker);
            if (opinion >= 40)
            {
                return true;
            }

            // Sibling / parent / child as "close" for legacy relic feel
            if (wearer.relations.DirectRelationExists(PawnRelationDefOf.Sibling, maker)
                || wearer.relations.DirectRelationExists(PawnRelationDefOf.Parent, maker)
                || wearer.relations.DirectRelationExists(PawnRelationDefOf.Child, maker)
                || wearer.relations.DirectRelationExists(PawnRelationDefOf.Spouse, maker)
                || wearer.relations.DirectRelationExists(PawnRelationDefOf.ExSpouse, maker)
                || wearer.relations.DirectRelationExists(PawnRelationDefOf.ExLover, maker)
                || wearer.relations.DirectRelationExists(PawnRelationDefOf.Fiance, maker))
            {
                return true;
            }

            return false;
        }

        public static bool SameRoom(Pawn a, Pawn b)
        {
            if (a == null || b == null || a.Map == null || a.Map != b.Map)
            {
                return false;
            }

            Room ra = a.GetRoom();
            Room rb = b.GetRoom();
            if (ra == null || rb == null)
            {
                // Outdoors / no room: treat adjacent-ish as same if both outdoors and close? Spec says same room.
                // Outdoors room can be shared; if both null, not same.
                return false;
            }
            return ra == rb;
        }

        /// <summary>
        /// Combat bonus applies once if primary weapon (or any equipped weapon) was made by living maker in same room.
        /// Cap — no multi-stack from multiple items.
        /// </summary>
        public static bool TryGetCombatBonus(Pawn attacker, out float bonus)
        {
            bonus = 0f;
            var s = ArtisanMarkMod.Settings;
            if (s == null || !s.modEnabled || !s.enableCombatBonus || attacker == null)
            {
                return false;
            }

            Thing weapon = attacker.equipment?.Primary;
            CompArtisanMark mark = GetMark(weapon);
            if (mark == null || !mark.HasMaker)
            {
                return false;
            }

            Pawn maker = mark.TryResolveMaker();
            if (maker == null || maker.Dead || maker.Destroyed)
            {
                return false;
            }

            // Self-made or ally maker present in same room.
            if (maker == attacker || SameRoom(attacker, maker))
            {
                bonus = s.combatBonusPercent;
                return bonus > 0f;
            }

            return false;
        }
    }
}
