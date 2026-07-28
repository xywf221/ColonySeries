using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SalvageAtlas
{
    public static class SalvageUtility
    {
        public static bool ModActive
        {
            get
            {
                SalvageAtlasSettings s = SalvageAtlasMod.Settings;
                return s != null && s.masterEnabled;
            }
        }

        public static bool ResearchDone
        {
            get
            {
                if (DebugSettings.godMode)
                {
                    return true;
                }
                return SalvageAtlasDefOf.SA_SalvageBasics == null
                       || SalvageAtlasDefOf.SA_SalvageBasics.IsFinished;
            }
        }

        public static bool IsMechCorpse(Thing t)
        {
            if (t is Corpse corpse)
            {
                return corpse.InnerPawn?.RaceProps?.IsMechanoid == true;
            }
            return false;
        }

        public static bool IsMechSalvageTarget(Thing t)
        {
            if (t == null || t.Destroyed)
            {
                return false;
            }
            if (IsMechCorpse(t))
            {
                return true;
            }
            return IsWreckOrChunk(t);
        }

        /// <summary>Risky teardown targets (wrecks/chunks). Corpses use normal salvage.</summary>
        public static bool IsTeardownTarget(Thing t)
        {
            if (t == null || t.Destroyed || t is Corpse)
            {
                return false;
            }
            return IsWreckOrChunk(t);
        }

        public static bool IsWreckOrChunk(Thing t)
        {
            if (t?.def == null)
            {
                return false;
            }
            // Ship chunks / mech slag wrecks — name-based so Core-only still works.
            string n = t.def.defName;
            if (n == "ShipChunk" || n == "ChunkMechanoidSlag" || n == "ShipSegment"
                || n == "ShipChunk_Mech" || n == "AncientSpacerJunk")
            {
                return true;
            }
            if (n.StartsWith("Mech") && t.def.category == ThingCategory.Building)
            {
                return true;
            }
            if (t.def.building != null)
            {
                if (n.Contains("Mech") && (n.Contains("Chunk") || n.Contains("Slag") || n.Contains("Wreck")))
                {
                    return true;
                }
                if (n.Contains("ShipChunk") || n.Contains("ShipPart") || n.Contains("ShipSegment"))
                {
                    return true;
                }
            }
            return false;
        }

        public static int EstimateFragmentYield(Thing target)
        {
            if (target == null)
            {
                return 0;
            }
            float mult = SalvageAtlasMod.Settings?.YieldMult ?? 1f;
            int raw;
            if (target is Corpse corpse && corpse.InnerPawn != null)
            {
                Pawn p = corpse.InnerPawn;
                float bodySize = p.BodySize;
                // Centipede ~3, scyther ~1, lancer ~1 → 2–6 base
                raw = Mathf.Clamp(Mathf.RoundToInt(2f + bodySize * 1.5f), 1, 8);
                // Heavier combat mechs give a bit more.
                if (p.kindDef != null && p.kindDef.combatPower >= 200f)
                {
                    raw += 1;
                }
            }
            else
            {
                // Wrecks / chunks
                float mv = target.MarketValue;
                raw = Mathf.Clamp(Mathf.RoundToInt(1f + mv / 120f), 1, 5);
            }
            int result = Mathf.Max(1, Mathf.RoundToInt(raw * mult));
            return result;
        }

        public static void CompleteSalvage(Thing target, Pawn worker, int yield)
        {
            if (target == null || target.Destroyed || yield <= 0)
            {
                return;
            }
            Map map = target.Map;
            IntVec3 cell = target.Position;
            ThingDef fragDef = SalvageAtlasDefOf.SA_MechFragment;
            if (fragDef == null || map == null)
            {
                return;
            }

            int left = yield;
            while (left > 0)
            {
                int stack = Mathf.Min(left, fragDef.stackLimit);
                Thing frag = ThingMaker.MakeThing(fragDef);
                frag.stackCount = stack;
                left -= stack;
                GenPlace.TryPlaceThing(frag, cell, map, ThingPlaceMode.Near);
            }

            GameComponent_SalvageAtlas.Get()?.NotifySalvaged(yield);

            // Light steel/slag drip so salvage feels physical, not pure meta-currency.
            MaybeDropScrap(cell, map, target);

            if (target is Corpse)
            {
                // Leave corpse for normal butchering of plasteel; mark with flag via destroy? Keep corpse.
                // Tag so WorkGiver skips: use designation removal only; set hit points low? Use comp-less:
                // Destroy corpse after salvage to avoid double-dip — but that removes butcher products.
                // Better: set a custom hediff-less flag via thing ID hash set in map component.
                MapComponent_SalvageAtlas mapComp = MapComponent_SalvageAtlas.For(map);
                mapComp?.MarkSalvaged(target.thingIDNumber);
            }
            else
            {
                // Wrecks consumed by teardown path, not here.
                MapComponent_SalvageAtlas mapComp = MapComponent_SalvageAtlas.For(map);
                mapComp?.MarkSalvaged(target.thingIDNumber);
            }

            if (worker?.skills != null)
            {
                worker.skills.Learn(SkillDefOf.Intellectual, 40f);
                worker.skills.Learn(SkillDefOf.Crafting, 25f);
            }

            Messages.Message(
                "SA_Msg_Salvaged".Translate(worker?.LabelShort ?? "?", yield, target.LabelShort),
                new TargetInfo(cell, map),
                MessageTypeDefOf.TaskCompletion);
        }

        private static void MaybeDropScrap(IntVec3 cell, Map map, Thing target)
        {
            if (Rand.Chance(0.55f))
            {
                Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
                steel.stackCount = Rand.RangeInclusive(1, 4);
                GenPlace.TryPlaceThing(steel, cell, map, ThingPlaceMode.Near);
            }
            if (Rand.Chance(0.25f))
            {
                ThingDef slag = ThingDefOf.ChunkSlagSteel;
                if (slag != null)
                {
                    GenPlace.TryPlaceThing(ThingMaker.MakeThing(slag), cell, map, ThingPlaceMode.Near);
                }
            }
        }

        public static void ApplyTeardownSuccess(Thing wreck, Pawn worker, int tier)
        {
            Map map = wreck.Map;
            IntVec3 cell = wreck.Position;
            List<ThingDefCountClass> drops = BuildTeardownDrops(tier, wreck);
            foreach (ThingDefCountClass d in drops)
            {
                if (d?.thingDef == null || d.count <= 0)
                {
                    continue;
                }
                Thing t = ThingMaker.MakeThing(d.thingDef);
                t.stackCount = d.count;
                GenPlace.TryPlaceThing(t, cell, map, ThingPlaceMode.Near);
            }

            // Bonus fragments on success (do not re-enter CompleteSalvage destroy path on wreck twice).
            int bonus = Mathf.Max(1, Mathf.RoundToInt((1 + tier) * (SalvageAtlasMod.Settings?.YieldMult ?? 1f)));
            ThingDef fragDef = SalvageAtlasDefOf.SA_MechFragment;
            if (fragDef != null && map != null)
            {
                int left = bonus;
                while (left > 0)
                {
                    int stack = Mathf.Min(left, fragDef.stackLimit);
                    Thing frag = ThingMaker.MakeThing(fragDef);
                    frag.stackCount = stack;
                    left -= stack;
                    GenPlace.TryPlaceThing(frag, cell, map, ThingPlaceMode.Near);
                }
                GameComponent_SalvageAtlas.Get()?.NotifySalvaged(bonus);
            }

            string wreckLabel = wreck.LabelShort;
            MapComponent_SalvageAtlas.For(map)?.MarkSalvaged(wreck.thingIDNumber);
            if (!wreck.Destroyed)
            {
                wreck.Destroy(DestroyMode.KillFinalize);
            }
            GameComponent_SalvageAtlas.Get()?.NotifyTeardown(true);
            Messages.Message(
                "SA_Msg_TeardownOK".Translate(worker?.LabelShort ?? "?", wreckLabel),
                new TargetInfo(cell, map),
                MessageTypeDefOf.PositiveEvent);
        }

        public static void ApplyTeardownFailure(Thing wreck, Pawn worker, int tier)
        {
            Map map = wreck.Map;
            IntVec3 cell = wreck.Position;
            SalvageAtlasSettings s = SalvageAtlasMod.Settings;

            // Waste some banked or nearby fragments — readable cost.
            GameComponent_SalvageAtlas atlas = GameComponent_SalvageAtlas.Get();
            if (atlas != null && atlas.analyzedFragments > 0)
            {
                int waste = Mathf.Clamp(2 + tier, 1, Mathf.Min(6, atlas.analyzedFragments));
                atlas.analyzedFragments -= waste;
                Messages.Message(
                    "SA_Msg_FragmentsWasted".Translate(waste),
                    MessageTypeDefOf.NegativeEvent);
            }

            // Small fire / explosion — survivable, readable.
            float risk = s?.RiskMult ?? 1f;
            bool didHazard = false;

            float fireChance = 0.35f * risk;
            float boomChance = 0.18f * risk;
            if (s != null && s.easyMode)
            {
                fireChance *= 0.5f;
                boomChance *= 0.35f;
            }

            if ((s == null || s.FireAllowed) && Rand.Chance(fireChance))
            {
                // 1.6 signature includes optional thingDef; pass null like WorkshopWear.
                FireUtility.TryStartFireIn(cell, map, Rand.Range(0.25f, 0.55f), worker, null);
                didHazard = true;
            }

            if ((s == null || s.ExplosionAllowed) && Rand.Chance(boomChance))
            {
                // Tiny radius — survivable, not a raid-wiper.
                GenExplosion.DoExplosion(
                    cell,
                    map,
                    1.9f,
                    DamageDefOf.Bomb,
                    worker,
                    Rand.RangeInclusive(4, 10),
                    0.1f);
                didHazard = true;
            }

            // Wreck is damaged / sometimes destroyed.
            if (!wreck.Destroyed)
            {
                int dmg = Rand.RangeInclusive(20, 60);
                wreck.TakeDamage(new DamageInfo(DamageDefOf.Bomb, dmg, 0.2f, -1f, worker));
                if (!wreck.Destroyed && Rand.Chance(0.4f + 0.1f * tier))
                {
                    wreck.Destroy(DestroyMode.KillFinalize);
                }
            }

            if (worker != null && !worker.Dead && Rand.Chance(0.25f * risk))
            {
                worker.TakeDamage(new DamageInfo(DamageDefOf.Burn, Rand.RangeInclusive(3, 8), 0f, -1f, wreck));
            }

            GameComponent_SalvageAtlas.Get()?.NotifyTeardown(false);
            Messages.Message(
                didHazard
                    ? "SA_Msg_TeardownFailHazard".Translate(worker?.LabelShort ?? "?", wreck.Label)
                    : "SA_Msg_TeardownFail".Translate(worker?.LabelShort ?? "?", wreck.Label),
                new TargetInfo(cell, map),
                MessageTypeDefOf.NegativeEvent);
        }

        private static List<ThingDefCountClass> BuildTeardownDrops(int tier, Thing wreck)
        {
            var list = new List<ThingDefCountClass>();
            // Rank 1: steel + components
            list.Add(new ThingDefCountClass(ThingDefOf.Steel, Rand.RangeInclusive(5, 10 + tier * 3)));
            if (tier >= 1)
            {
                list.Add(new ThingDefCountClass(ThingDefOf.ComponentIndustrial, Rand.RangeInclusive(1, 1 + tier / 2)));
            }
            if (tier >= 2)
            {
                ThingDef plasteel = ThingDefOf.Plasteel;
                if (plasteel != null)
                {
                    list.Add(new ThingDefCountClass(plasteel, Rand.RangeInclusive(1, 2 + tier)));
                }
            }
            if (tier >= 3)
            {
                ThingDef spacer = ThingDefOf.ComponentSpacer;
                if (spacer != null && Rand.Chance(0.45f + 0.1f * tier))
                {
                    list.Add(new ThingDefCountClass(spacer, 1));
                }
            }
            if (tier >= 4 && Rand.Chance(0.35f))
            {
                // Rare chemfuel drip from coolant lines
                if (ThingDefOf.Chemfuel != null)
                {
                    list.Add(new ThingDefCountClass(ThingDefOf.Chemfuel, Rand.RangeInclusive(5, 15)));
                }
            }
            return list;
        }

        public static void ApplyFieldPatch(Pawn target, AtlasBranchId branch)
        {
            if (target == null || target.Dead || target.health?.hediffSet == null)
            {
                return;
            }
            GameComponent_SalvageAtlas atlas = GameComponent_SalvageAtlas.Get();
            if (atlas == null)
            {
                return;
            }

            HediffDef def = null;
            int rank = 0;
            switch (branch)
            {
                case AtlasBranchId.FieldKit:
                    def = SalvageAtlasDefOf.SA_FieldPatch_Armor;
                    rank = atlas.GetRank(AtlasBranchId.FieldKit);
                    break;
                case AtlasBranchId.Optics:
                    def = SalvageAtlasDefOf.SA_FieldPatch_Optics;
                    rank = atlas.GetRank(AtlasBranchId.Optics);
                    break;
                case AtlasBranchId.Coolant:
                    def = SalvageAtlasDefOf.SA_FieldPatch_Coolant;
                    rank = atlas.GetRank(AtlasBranchId.Coolant);
                    break;
            }
            if (def == null || rank <= 0)
            {
                Messages.Message("SA_Msg_PatchLocked".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }

            Hediff existing = target.health.hediffSet.GetFirstHediffOfDef(def);
            if (existing != null)
            {
                target.health.RemoveHediff(existing);
            }

            Hediff h = HediffMaker.MakeHediff(def, target);
            // Severity encodes rank (0.2 / 0.5 / 0.8) — stages in XML.
            h.Severity = rank <= 1 ? 0.25f : rank == 2 ? 0.55f : 0.85f;
            target.health.AddHediff(h);
            Messages.Message(
                "SA_Msg_PatchApplied".Translate(target.LabelShort, def.label),
                target,
                MessageTypeDefOf.PositiveEvent);
        }

        public static void PrintSchematic(Map map, IntVec3 cell)
        {
            GameComponent_SalvageAtlas atlas = GameComponent_SalvageAtlas.Get();
            if (atlas == null || !atlas.TryConsumeSchematicCredit())
            {
                Messages.Message("SA_Msg_NoSchematicCredit".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            ThingDef schematic = SalvageAtlasDefOf.SA_AtlasSchematic;
            if (schematic == null)
            {
                return;
            }
            Thing t = ThingMaker.MakeThing(schematic);
            t.stackCount = 1;
            int bp = Mathf.Clamp(atlas.GetRank(AtlasBranchId.Blueprints), 1, 4);
            var comp = t.TryGetComp<CompAtlasSchematic>();
            if (comp != null)
            {
                comp.blueprintRank = bp;
            }
            GenPlace.TryPlaceThing(t, cell, map, ThingPlaceMode.Near);
            Messages.Message("SA_Msg_SchematicPrinted".Translate(bp), MessageTypeDefOf.PositiveEvent);
        }
    }
}
