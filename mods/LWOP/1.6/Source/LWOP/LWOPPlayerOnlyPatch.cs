using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LWOP.Buildings
{
    [StaticConstructorOnStartup]
    public static class LWOPPlayerOnlyPatch
    {
        static LWOPPlayerOnlyPatch()
        {
            new Harmony("com.colonyseries.lwop.PlayerOnlyUsage").PatchAll();
            LongEventHandler.ExecuteWhenFinished(LWOPOpenAllContainersUtility.AddOrdersDesignator);
        }
    }

    public static class LWOPPlayerOnlyUtility
    {
        private const string LWOPWorkerMechDefName = "Mech_LWOPWorker";
        private static readonly FieldInfo EquipmentPawnField = AccessTools.Field(typeof(Pawn_EquipmentTracker), "pawn");
        private static readonly FieldInfo ApparelPawnField = AccessTools.Field(typeof(Pawn_ApparelTracker), "pawn");
        private static readonly FieldInfo ThingStuffField = AccessTools.Field(typeof(Thing), "stuffInt");
        private static readonly FieldInfo ThingGraphicField = AccessTools.Field(typeof(Thing), "graphicInt");
        [ThreadStatic]
        private static bool replacingUnauthorizedLWOPWorkerMech;

        public static bool IsLWOPDef(Def def)
        {
            return def != null && def.defName != null &&
                def.defName.IndexOf("LWOP", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsLWOPThing(Thing thing)
        {
            return thing != null && (IsLWOPDef(thing.def) || IsLWOPDef(thing.Stuff));
        }

        public static bool IsPlayerFaction(Faction faction)
        {
            if (faction == null)
            {
                return false;
            }

            Faction playerFaction = Faction.OfPlayerSilentFail;
            return playerFaction != null && faction == playerFaction;
        }

        public static bool IsProtectedPlayerThing(Thing thing, Map map)
        {
            if (thing == null)
            {
                return true;
            }

            if (IsPlayerFaction(thing.Faction))
            {
                return true;
            }

            return thing.Faction == null && map != null && map.IsPlayerHome;
        }

        public static Pawn PawnFor(Pawn_EquipmentTracker tracker)
        {
            return tracker == null ? null : EquipmentPawnField.GetValue(tracker) as Pawn;
        }

        public static Pawn PawnFor(Pawn_ApparelTracker tracker)
        {
            return tracker == null ? null : ApparelPawnField.GetValue(tracker) as Pawn;
        }

        public static bool PawnMayUseLWOP(Pawn pawn)
        {
            return pawn == null || pawn.Faction == null || IsPlayerFaction(pawn.Faction);
        }

        public static bool PawnShouldBeSanitized(Pawn pawn)
        {
            return pawn != null && pawn.Faction != null && !IsPlayerFaction(pawn.Faction);
        }

        public static bool IsLWOPWorkerMech(Pawn pawn)
        {
            return pawn != null &&
                pawn.def != null &&
                string.Equals(pawn.def.defName, LWOPWorkerMechDefName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldRemoveUnauthorizedLWOPWorkerMech(Pawn pawn)
        {
            if (!IsLWOPWorkerMech(pawn))
            {
                return false;
            }

            if (IsPlayerFaction(pawn.Faction))
            {
                return false;
            }

            if (pawn.Faction == null)
            {
                return pawn.Spawned && pawn.Map != null && !pawn.Map.IsPlayerHome;
            }

            return true;
        }

        public static bool TryRemoveUnauthorizedLWOPWorkerMech(Pawn pawn)
        {
            if (!ShouldRemoveUnauthorizedLWOPWorkerMech(pawn))
            {
                return false;
            }

            if (!pawn.Destroyed)
            {
                pawn.Destroy(DestroyMode.Vanish);
            }

            return true;
        }

        public static void ReplaceOrRemoveUnauthorizedGeneratedLWOPWorkerMech(ref Pawn pawn)
        {
            if (!ShouldRemoveUnauthorizedLWOPWorkerMech(pawn))
            {
                return;
            }

            Pawn replacement = TryGenerateReplacementMech(pawn.Faction);
            if (replacement != null)
            {
                if (!pawn.Destroyed)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                }

                pawn = replacement;
                return;
            }

            TryRemoveUnauthorizedLWOPWorkerMech(pawn);
        }

        private static Pawn TryGenerateReplacementMech(Faction faction)
        {
            if (replacingUnauthorizedLWOPWorkerMech)
            {
                return null;
            }

            PawnKindDef replacementKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Militor");
            if (replacementKind == null)
            {
                return null;
            }

            try
            {
                replacingUnauthorizedLWOPWorkerMech = true;
                return PawnGenerator.GeneratePawn(replacementKind, faction, (PlanetTile?)null);
            }
            finally
            {
                replacingUnauthorizedLWOPWorkerMech = false;
            }
        }

        public static void SanitizePawn(Pawn pawn)
        {
            if (TryRemoveUnauthorizedLWOPWorkerMech(pawn))
            {
                return;
            }

            if (!PawnShouldBeSanitized(pawn))
            {
                return;
            }

            if (pawn.equipment != null)
            {
                List<ThingWithComps> equipment = pawn.equipment.AllEquipmentListForReading
                    .Where(IsLWOPThing)
                    .ToList();
                foreach (ThingWithComps thing in equipment)
                {
                    ThingWithComps replacement;
                    if (TryCreateSanitizedReplacement(thing, out replacement) && replacement != null)
                    {
                        pawn.equipment.Remove(thing);
                        if (!thing.Destroyed)
                        {
                            thing.Destroy(DestroyMode.Vanish);
                        }

                        pawn.equipment.AddEquipment(replacement);
                    }
                    else
                    {
                        pawn.equipment.Remove(thing);
                        if (!thing.Destroyed)
                        {
                            thing.Destroy(DestroyMode.Vanish);
                        }
                    }
                }
            }

            if (pawn.apparel != null)
            {
                List<Apparel> apparel = pawn.apparel.WornApparel
                    .Where(IsLWOPThing)
                    .ToList();
                foreach (Apparel thing in apparel)
                {
                    Apparel replacement;
                    if (TryCreateSanitizedReplacement(thing, out replacement) && replacement != null)
                    {
                        pawn.apparel.Remove(thing);
                        if (!thing.Destroyed)
                        {
                            thing.Destroy(DestroyMode.Vanish);
                        }

                        pawn.apparel.Wear(replacement, false, true);
                    }
                    else
                    {
                        pawn.apparel.Remove(thing);
                        if (!thing.Destroyed)
                        {
                            thing.Destroy(DestroyMode.Vanish);
                        }
                    }
                }
            }

        }

        public static bool TrySanitizeEquipmentForPawn(Pawn pawn, ref ThingWithComps equipment)
        {
            if (!PawnShouldBeSanitized(pawn) || !IsLWOPThing(equipment))
            {
                return true;
            }

            ThingWithComps replacement;
            if (TryCreateSanitizedReplacement(equipment, out replacement) && replacement != null)
            {
                if (equipment != null && !equipment.Destroyed)
                {
                    equipment.Destroy(DestroyMode.Vanish);
                }

                equipment = replacement;
                return true;
            }

            if (equipment != null && !equipment.Destroyed)
            {
                equipment.Destroy(DestroyMode.Vanish);
            }

            equipment = null;
            return false;
        }

        public static bool TrySanitizeApparelForPawn(Pawn pawn, ref Apparel apparel)
        {
            if (!PawnShouldBeSanitized(pawn) || !IsLWOPThing(apparel))
            {
                return true;
            }

            Apparel replacement;
            if (TryCreateSanitizedReplacement(apparel, out replacement) && replacement != null)
            {
                if (apparel != null && !apparel.Destroyed)
                {
                    apparel.Destroy(DestroyMode.Vanish);
                }

                apparel = replacement;
                return true;
            }

            if (apparel != null && !apparel.Destroyed)
            {
                apparel.Destroy(DestroyMode.Vanish);
            }

            apparel = null;
            return false;
        }

        public static void SanitizeFactionOwnedThing(Thing thing)
        {
            if (thing == null || thing.Destroyed || !IsLWOPThing(thing))
            {
                return;
            }

            if (thing.Faction == null || IsPlayerFaction(thing.Faction))
            {
                return;
            }

            if (!TrySanitizeThingForNonPlayer(thing) && !thing.Destroyed)
            {
                thing.Destroy(DestroyMode.Vanish);
            }
        }

        private static bool TrySanitizeThingForNonPlayer(Thing thing)
        {
            if (thing == null || !IsLWOPThing(thing))
            {
                return true;
            }

            if (IsLWOPDef(thing.def))
            {
                return false;
            }

            if (IsLWOPDef(thing.Stuff))
            {
                return TryReplaceLWOPStuff(thing);
            }

            return true;
        }

        private static bool TryCreateSanitizedReplacement<T>(T thing, out T replacement) where T : Thing
        {
            replacement = null;
            if (thing == null || !IsLWOPThing(thing))
            {
                return false;
            }

            if (IsLWOPDef(thing.def))
            {
                return false;
            }

            ThingDef replacementStuff = ReplacementStuffFor(thing.Stuff);
            if (replacementStuff == null)
            {
                return false;
            }

            Thing madeThing = ThingMaker.MakeThing(thing.def, replacementStuff);
            replacement = madeThing as T;
            if (replacement == null)
            {
                if (madeThing != null && !madeThing.Destroyed)
                {
                    madeThing.Destroy(DestroyMode.Vanish);
                }

                return false;
            }

            replacement.stackCount = Math.Max(1, thing.stackCount);
            CopyQuality(thing, replacement);
            ClampHitPointsToCurrentMax(replacement);
            return true;
        }

        private static void CopyQuality(Thing source, Thing destination)
        {
            ThingWithComps sourceWithComps = source as ThingWithComps;
            ThingWithComps destinationWithComps = destination as ThingWithComps;
            if (sourceWithComps == null || destinationWithComps == null)
            {
                return;
            }

            CompQuality sourceQuality = sourceWithComps.TryGetComp<CompQuality>();
            CompQuality destinationQuality = destinationWithComps.TryGetComp<CompQuality>();
            if (sourceQuality != null && destinationQuality != null)
            {
                destinationQuality.SetQuality(sourceQuality.Quality, ArtGenerationContext.Outsider);
            }
        }

        public static bool BuildingMayStay(Thing thing, Map map)
        {
            if (thing == null || thing.def == null || thing.def.category != ThingCategory.Building)
            {
                return true;
            }

            if (IsProtectedPlayerThing(thing, map))
            {
                return true;
            }

            if (IsLWOPDef(thing.def))
            {
                if (thing.Faction == null && IsLWOPTransportShipPart(thing.def))
                {
                    return true;
                }

                thing.Destroy(DestroyMode.Vanish);
                return false;
            }

            if (IsLWOPDef(thing.Stuff))
            {
                if (TryReplaceLWOPStuff(thing))
                {
                    return true;
                }

                thing.Destroy(DestroyMode.Vanish);
                return false;
            }

            ClampHitPointsToCurrentMax(thing);
            return true;
        }

        private static bool IsLWOPTransportShipPart(ThingDef def)
        {
            return def != null && def.defName != null &&
                def.defName.IndexOf("LWOPTransportship", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ThingDef ReplacementStuffFor(ThingDef stuff)
        {
            if (stuff == null || stuff.stuffProps == null || stuff.stuffProps.categories == null)
            {
                return null;
            }

            List<StuffCategoryDef> categories = stuff.stuffProps.categories;
            if (categories.Contains(StuffCategoryDefOf.Metallic))
            {
                return ThingDefOf.Steel;
            }

            if (categories.Contains(StuffCategoryDefOf.Stony))
            {
                return ThingDefOf.BlocksGranite;
            }

            if (categories.Contains(StuffCategoryDefOf.Woody))
            {
                return ThingDefOf.WoodLog;
            }

            if (categories.Contains(StuffCategoryDefOf.Fabric))
            {
                return ThingDefOf.Cloth;
            }

            if (categories.Contains(StuffCategoryDefOf.Leathery))
            {
                return DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Plain") ?? ThingDefOf.Cloth;
            }

            return null;
        }

        private static bool TryReplaceLWOPStuff(Thing thing)
        {
            if (thing == null || !IsLWOPDef(thing.Stuff))
            {
                return true;
            }

            ThingDef replacement = ReplacementStuffFor(thing.Stuff);
            if (replacement == null)
            {
                return false;
            }

            SetStuff(thing, replacement);
            ClampHitPointsToCurrentMax(thing);
            return true;
        }

        private static void SetStuff(Thing thing, ThingDef replacement)
        {
            ThingStuffField.SetValue(thing, replacement);
            ThingGraphicField.SetValue(thing, null);
            thing.Notify_ColorChanged();
        }

        private static void ClampHitPointsToCurrentMax(Thing thing)
        {
            if (thing == null || thing.Destroyed || thing.def == null || !thing.def.useHitPoints)
            {
                return;
            }

            int maxHitPoints = thing.MaxHitPoints;
            if (maxHitPoints > 0 && thing.HitPoints > maxHitPoints)
            {
                thing.HitPoints = maxHitPoints;
            }
        }
    }

    public static class LWOPShieldBypassUtility
    {
        private static readonly HashSet<string> BypassProjectileDefNames = new HashSet<string>
        {
            "Bullet_ChargeRifle",
            "Proj_lwRocket"
        };

        private static readonly HashSet<string> BypassDamageDefNames = new HashSet<string>
        {
            "LWOPBulletBypassShields"
        };

        public static bool IsLWOPShieldBypassProjectile(Projectile projectile)
        {
            if (projectile == null)
            {
                return false;
            }

            return IsBypassProjectileDef(projectile.def) ||
                IsBypassDamageDef(projectile.DamageDef) ||
                IsBypassThingDef(projectile.EquipmentDef);
        }

        public static bool IsLWOPShieldBypassVerb(Verb verb)
        {
            if (verb == null || verb.verbProps == null)
            {
                return false;
            }

            return IsBypassProjectileDef(verb.verbProps.defaultProjectile) ||
                IsBypassThingDef(verb.EquipmentSource == null ? null : verb.EquipmentSource.def);
        }

        private static bool IsBypassProjectileDef(ThingDef def)
        {
            return def != null && BypassProjectileDefNames.Contains(def.defName);
        }

        private static bool IsBypassDamageDef(DamageDef def)
        {
            return def != null && BypassDamageDefNames.Contains(def.defName);
        }

        private static bool IsBypassThingDef(ThingDef def)
        {
            return def != null &&
                (string.Equals(def.defName, "LWOPSpaceRifle", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(def.defName, "LWOPTransportshipGun", StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class LWOPCarryCapacityUtility
    {
        private const float PowerArmorCarryCapacity = 10000f;
        private const float HelmetCarryCapacity = 1000f;

        public static float ExtraCapacityFor(Pawn pawn)
        {
            if (pawn == null || pawn.apparel == null)
            {
                return 0f;
            }

            float capacity = 0f;
            List<Apparel> wornApparel = pawn.apparel.WornApparel;
            for (int i = 0; i < wornApparel.Count; i++)
            {
                Apparel apparel = wornApparel[i];
                if (apparel == null || apparel.def == null)
                {
                    continue;
                }

                if (string.Equals(apparel.def.defName, "LWOPApparel_PowerArmor", StringComparison.OrdinalIgnoreCase))
                {
                    capacity += PowerArmorCarryCapacity;
                }
                else if (string.Equals(apparel.def.defName, "lwApparel_PowerArmorHelmet", StringComparison.OrdinalIgnoreCase))
                {
                    capacity += HelmetCarryCapacity;
                }
            }

            return capacity;
        }
    }

    public static class LWOPMedicalFurnitureUtility
    {
        private static readonly HashSet<string> MedicalStuffDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "LWOPSteel",
            "LWOPwool",
            "LWOPWool",
            "LWOPFiber",
            "LWOPFibre",
            "LWOPBlockStone"
        };

        private static readonly FieldInfo NeedPawnField = AccessTools.Field(typeof(Need), "pawn");
        private static readonly FieldInfo ImmunityPawnField = AccessTools.Field(typeof(ImmunityHandler), "pawn");

        public static bool IsLWOPMedicalStuff(ThingDef stuff)
        {
            return stuff != null &&
                !stuff.defName.NullOrEmpty() &&
                (MedicalStuffDefNames.Contains(stuff.defName) ||
                 stuff.defName.IndexOf("LWOP", StringComparison.OrdinalIgnoreCase) >= 0 &&
                 (stuff.defName.IndexOf("Steel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                  stuff.defName.IndexOf("wool", StringComparison.OrdinalIgnoreCase) >= 0 ||
                  stuff.defName.IndexOf("Fiber", StringComparison.OrdinalIgnoreCase) >= 0 ||
                  stuff.defName.IndexOf("Fibre", StringComparison.OrdinalIgnoreCase) >= 0 ||
                  stuff.defName.IndexOf("BlockStone", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        public static bool IsLWOPMedicalBed(Building_Bed bed)
        {
            return bed != null && IsLWOPMedicalStuff(bed.Stuff);
        }

        public static bool PawnUsingLWOPMedicalBed(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed)
            {
                return false;
            }

            Building_Bed bed = RestUtility.CurrentBed(pawn);
            return IsLWOPMedicalBed(bed);
        }

        public static Pawn PawnFor(Need_Rest restNeed)
        {
            return restNeed == null ? null : NeedPawnField.GetValue(restNeed) as Pawn;
        }

        public static Pawn PawnFor(ImmunityHandler immunityHandler)
        {
            return immunityHandler == null ? null : ImmunityPawnField.GetValue(immunityHandler) as Pawn;
        }
    }

    [HarmonyPatch(typeof(Need_Rest), "TickResting")]
    public static class LWOPMedicalBedInstantRestPatch
    {
        public static void Postfix(Need_Rest __instance)
        {
            Pawn pawn = LWOPMedicalFurnitureUtility.PawnFor(__instance);
            if (LWOPMedicalFurnitureUtility.PawnUsingLWOPMedicalBed(pawn))
            {
                __instance.CurLevel = __instance.MaxLevel;
            }
        }
    }

    [HarmonyPatch(typeof(Recipe_Surgery), "CheckSurgeryFail")]
    public static class LWOPMedicalBedSurgerySuccessPatch
    {
        public static bool Prefix(Pawn patient, ref bool __result)
        {
            if (!LWOPMedicalFurnitureUtility.PawnUsingLWOPMedicalBed(patient))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(ImmunityHandler), "ImmunityHandlerTickInterval")]
    public static class LWOPMedicalBedImmunityPatch
    {
        public static void Prefix(ImmunityHandler __instance, out Dictionary<ImmunityRecord, float> __state)
        {
            __state = null;
            Pawn pawn = LWOPMedicalFurnitureUtility.PawnFor(__instance);
            if (!LWOPMedicalFurnitureUtility.PawnUsingLWOPMedicalBed(pawn) ||
                __instance.ImmunityListForReading == null ||
                __instance.ImmunityListForReading.Count == 0)
            {
                return;
            }

            __state = new Dictionary<ImmunityRecord, float>();
            List<ImmunityRecord> records = __instance.ImmunityListForReading;
            for (int i = 0; i < records.Count; i++)
            {
                ImmunityRecord record = records[i];
                if (record != null)
                {
                    __state[record] = record.immunity;
                }
            }
        }

        public static void Postfix(ImmunityHandler __instance, Dictionary<ImmunityRecord, float> __state)
        {
            if (__state == null || __state.Count == 0)
            {
                return;
            }

            List<ImmunityRecord> records = __instance.ImmunityListForReading;
            for (int i = 0; i < records.Count; i++)
            {
                ImmunityRecord record = records[i];
                if (record == null || !__state.TryGetValue(record, out float before))
                {
                    continue;
                }

                float gained = record.immunity - before;
                if (gained > 0f)
                {
                    record.immunity = Mathf.Min(1f, record.immunity + gained * 49f);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Hediff_Injury), "TickInterval")]
    public static class LWOPMedicalBedInjuryHealingPatch
    {
        public static void Postfix(Hediff_Injury __instance, int delta)
        {
            if (__instance == null ||
                __instance.pawn == null ||
                __instance.Severity <= 0f ||
                !LWOPMedicalFurnitureUtility.PawnUsingLWOPMedicalBed(__instance.pawn))
            {
                return;
            }

            __instance.Heal(Mathf.Max(0.01f, delta * 0.02f));
        }
    }

    public static class LWOPBedLovinUtility
    {
        public static bool TryGetAssignedLovePartnerPair(Building_Bed bed, out Pawn first, out Pawn second, out string failReasonKey)
        {
            first = null;
            second = null;
            failReasonKey = null;

            if (!LWOPMedicalFurnitureUtility.IsLWOPMedicalBed(bed) || bed.SleepingSlotsCount < 2)
            {
                failReasonKey = "LWOPForceLovinNeedLWOPDoubleBed";
                return false;
            }

            List<Pawn> owners = bed.OwnersForReading;
            if (owners == null || owners.Count < 2)
            {
                failReasonKey = "LWOPForceLovinNeedTwoOwners";
                return false;
            }

            for (int i = 0; i < owners.Count; i++)
            {
                Pawn a = owners[i];
                if (!ValidPawnForForcedLovin(a, bed))
                {
                    continue;
                }

                for (int j = i + 1; j < owners.Count; j++)
                {
                    Pawn b = owners[j];
                    if (ValidPawnForForcedLovin(b, bed) &&
                        LovePartnerRelationUtility.LovePartnerRelationExists(a, b))
                    {
                        first = a;
                        second = b;
                        return true;
                    }
                }
            }

            failReasonKey = "LWOPForceLovinNeedLovePartners";
            return false;
        }

        public static bool ShouldShowCommand(Building_Bed bed)
        {
            return bed != null &&
                bed.Spawned &&
                bed.Faction == Faction.OfPlayer &&
                LWOPMedicalFurnitureUtility.IsLWOPMedicalBed(bed) &&
                bed.SleepingSlotsCount >= 2;
        }

        public static void StartForcedLovin(Building_Bed bed)
        {
            if (!TryGetAssignedLovePartnerPair(bed, out Pawn first, out Pawn second, out string failReasonKey))
            {
                Messages.Message(failReasonKey.Translate(), bed, MessageTypeDefOf.RejectInput, false);
                return;
            }

            int now = Find.TickManager == null ? 0 : Find.TickManager.TicksGame;
            first.mindState.canLovinTick = now - 1;
            second.mindState.canLovinTick = now - 1;

            LovePartnerRelationUtility.TryToShareBed(first, second);

            Job partnerJob = JobMaker.MakeJob(JobDefOf.LayDown, bed);
            partnerJob.forceSleep = false;
            partnerJob.ignoreJoyTimeAssignment = true;
            second.jobs.TryTakeOrderedJob(partnerJob, JobTag.Misc);

            Job lovinJob = JobMaker.MakeJob(JobDefOf.Lovin, second, bed);
            lovinJob.ignoreJoyTimeAssignment = true;
            first.jobs.TryTakeOrderedJob(lovinJob, JobTag.Misc);

            Messages.Message("LWOPForceLovinStarted".Translate(first.LabelShortCap, second.LabelShortCap), bed, MessageTypeDefOf.PositiveEvent, false);
        }

        private static bool ValidPawnForForcedLovin(Pawn pawn, Building_Bed bed)
        {
            return pawn != null &&
                !pawn.Dead &&
                !pawn.Downed &&
                pawn.Spawned &&
                pawn.Map == bed.Map &&
                pawn.Faction == Faction.OfPlayer &&
                pawn.jobs != null &&
                pawn.mindState != null &&
                pawn.health != null &&
                pawn.health.capacities != null &&
                pawn.health.capacities.CanBeAwake;
        }
    }

    [HarmonyPatch(typeof(Building_Bed), "GetGizmos")]
    public static class LWOPMedicalBedLovinGizmoPatch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Bed __instance)
        {
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
            }

            if (!LWOPBedLovinUtility.ShouldShowCommand(__instance))
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "LWOPForceLovin".Translate().ToString(),
                defaultDesc = "LWOPForceLovinDesc".Translate().ToString(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LWOPBedRomance"),
                action = delegate
                {
                    LWOPBedLovinUtility.StartForcedLovin(__instance);
                }
            };
        }
    }

    [HarmonyPatch(typeof(MassUtility), "Capacity")]
    public static class LWOPMassUtilityCapacityPatch
    {
        public static void Postfix(Pawn p, StringBuilder explanation, ref float __result)
        {
            float extraCapacity = LWOPCarryCapacityUtility.ExtraCapacityFor(p);
            if (extraCapacity <= 0f)
            {
                return;
            }

            __result += extraCapacity;
            if (explanation != null)
            {
                if (explanation.Length > 0)
                {
                    explanation.AppendLine();
                }

                explanation.Append("  - LWOP equipment: " + extraCapacity.ToStringMassOffset());
            }
        }
    }

    [HarmonyPatch(typeof(CompProjectileInterceptor), "InterceptsProjectile")]
    public static class LWOPProjectileInterceptorBypassPatch
    {
        public static bool Prefix(Projectile projectile, ref bool __result)
        {
            if (!LWOPShieldBypassUtility.IsLWOPShieldBypassProjectile(projectile))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompShield), "CompAllowVerbCast")]
    public static class LWOPPersonalShieldAllowVerbPatch
    {
        public static void Postfix(Verb verb, ref bool __result)
        {
            if (!__result && LWOPShieldBypassUtility.IsLWOPShieldBypassVerb(verb))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new Type[] { typeof(PawnGenerationRequest) })]
    public static class PawnGeneratorGeneratePawnPatch
    {
        public static void Postfix(ref Pawn __result)
        {
            LWOPPlayerOnlyUtility.ReplaceOrRemoveUnauthorizedGeneratedLWOPWorkerMech(ref __result);
            LWOPPlayerOnlyUtility.SanitizePawn(__result);
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new Type[] { typeof(PawnKindDef), typeof(Faction), typeof(RimWorld.Planet.PlanetTile?) })]
    public static class PawnGeneratorGeneratePawnKindPatch
    {
        public static void Postfix(ref Pawn __result)
        {
            LWOPPlayerOnlyUtility.ReplaceOrRemoveUnauthorizedGeneratedLWOPWorkerMech(ref __result);
            LWOPPlayerOnlyUtility.SanitizePawn(__result);
        }
    }

    [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
    public static class PawnSpawnSetupPlayerOnlyPatch
    {
        public static void Postfix(Pawn __instance)
        {
            LWOPPlayerOnlyUtility.SanitizePawn(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn), "TickInterval")]
    public static class PawnTickIntervalPlayerOnlyPatch
    {
        public static void Postfix(Pawn __instance)
        {
            if (__instance != null && __instance.Spawned && __instance.IsHashIntervalTick(250))
            {
                LWOPPlayerOnlyUtility.SanitizePawn(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), "AddEquipment")]
    public static class PawnEquipmentAddEquipmentPlayerOnlyPatch
    {
        public static bool Prefix(Pawn_EquipmentTracker __instance, ref ThingWithComps newEq)
        {
            Pawn pawn = LWOPPlayerOnlyUtility.PawnFor(__instance);
            return LWOPPlayerOnlyUtility.TrySanitizeEquipmentForPawn(pawn, ref newEq);
        }
    }

    [HarmonyPatch(typeof(Pawn_ApparelTracker), "Wear")]
    public static class PawnApparelWearPlayerOnlyPatch
    {
        public static bool Prefix(Pawn_ApparelTracker __instance, ref Apparel newApparel)
        {
            Pawn pawn = LWOPPlayerOnlyUtility.PawnFor(__instance);
            return LWOPPlayerOnlyUtility.TrySanitizeApparelForPawn(pawn, ref newApparel);
        }
    }

    [HarmonyPatch(typeof(Thing), "SpawnSetup")]
    public static class ThingSpawnSetupPlayerOnlyPatch
    {
        public static void Postfix(Thing __instance, Map map)
        {
            LWOPPlayerOnlyUtility.BuildingMayStay(__instance, map);
            LWOPPlayerOnlyUtility.SanitizeFactionOwnedThing(__instance);
        }
    }

    [HarmonyPatch(typeof(ThingMaker), "MakeThing")]
    public static class ThingMakerDefaultStuffPatch
    {
        public static void Prefix(ThingDef def, ref ThingDef stuff)
        {
            if (def != null && def.MadeFromStuff && stuff == null)
            {
                stuff = def.defaultStuff ?? GenStuff.DefaultStuffFor(def);
            }
        }
    }

    [HarmonyPatch(typeof(CompFoodPoisonable), "Notify_RecipeProduced")]
    public static class LWOPFoodPoisonableRecipeProducedPatch
    {
        public static bool Prefix(CompFoodPoisonable __instance, Pawn pawn)
        {
            Thing parent = __instance == null ? null : __instance.parent;
            LWOPFoodSafetyUtility.RecordRecipeProduct(parent, pawn);
            return !LWOPFoodSafetyUtility.IsPackagedSurvivalMeal(parent) &&
                !LWOPFoodSafetyUtility.IsSafeCook(pawn);
        }
    }

    [HarmonyPatch(typeof(GenRecipe), "MakeRecipeProducts")]
    public static class LWOPRecipeProductsFoodSafetyPatch
    {
        public static IEnumerable<Thing> Postfix(IEnumerable<Thing> __result, Pawn worker)
        {
            if (__result == null)
            {
                yield break;
            }

            foreach (Thing thing in __result)
            {
                LWOPFoodSafetyUtility.RecordRecipeProduct(thing, worker);
                yield return thing;
            }
        }
    }

    [HarmonyPatch(typeof(FoodUtility), "AddFoodPoisoningHediff")]
    public static class LWOPFoodPoisoningMakerMessagePatch
    {
        public static void Postfix(Pawn pawn, Thing ingestible)
        {
            if (pawn == null)
            {
                return;
            }

            string foodLabel = ingestible == null ? "Unknown".Translate().ToString() : ingestible.LabelShort;
            string maker = LWOPFoodMakerGameComponent.MakerFor(ingestible);
            if (!maker.NullOrEmpty())
            {
                Messages.Message("LWOPFoodPoisoningMakerKnown".Translate(pawn.LabelShortCap, foodLabel, maker), pawn, MessageTypeDefOf.NegativeHealthEvent, false);
            }
            else
            {
                Messages.Message("LWOPFoodPoisoningMakerUnknown".Translate(foodLabel), pawn, MessageTypeDefOf.NegativeHealthEvent, false);
            }
        }
    }

    public class IngestionOutcomeDoer_LWOPNutrientHeal : IngestionOutcomeDoer
    {
        private const string NutrientHediffDefName = "LWOPNutrientAgentHigh";

        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return;
            }

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            if (hediffs == null || hediffs.Count == 0)
            {
                EnsureNutrientHigh(pawn);
                return;
            }

            List<Hediff> toRemove = new List<Hediff>();
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (ShouldRemoveHediff(hediff))
                {
                    toRemove.Add(hediff);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                if (pawn.health.hediffSet.hediffs.Contains(toRemove[i]))
                {
                    pawn.health.RemoveHediff(toRemove[i]);
                }
            }

            EnsureNutrientHigh(pawn);
        }

        private static bool ShouldRemoveHediff(Hediff hediff)
        {
            if (hediff == null || hediff.def == null)
            {
                return false;
            }

            if (string.Equals(hediff.def.defName, NutrientHediffDefName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsProtectedAbilityHediff(hediff))
            {
                return false;
            }

            if (hediff is Hediff_MissingPart ||
                hediff.def.countsAsAddedPartOrImplant ||
                hediff.def.addedPartProps != null)
            {
                return false;
            }

            if (hediff.def.IsAddiction ||
                hediff.def.chemicalNeed != null ||
                hediff.def.pregnant)
            {
                return false;
            }

            if (hediff is Hediff_Injury)
            {
                return true;
            }

            string defName = hediff.def.defName ?? string.Empty;
            if (defName.IndexOf("Metalhorror", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return hediff.def.isInfection ||
                hediff.def.chronic ||
                hediff.def.everCurableByItem ||
                hediff.def.tendable ||
                hediff.def.makesSickThought;
        }

        private static bool IsProtectedAbilityHediff(Hediff hediff)
        {
            string defName = hediff.def.defName ?? string.Empty;
            string label = hediff.def.label ?? string.Empty;
            string hediffClassName = hediff.GetType().FullName ?? string.Empty;
            string packageId = hediff.def.modContentPack == null || hediff.def.modContentPack.PackageId == null
                ? string.Empty
                : hediff.def.modContentPack.PackageId;

            if (ContainsToken(defName, "PsychicAmplifier") ||
                ContainsToken(defName, "Psylink") ||
                ContainsToken(defName, "PsycastAbility") ||
                ContainsToken(label, "psylink") ||
                ContainsToken(hediffClassName, "Hediff_Psylink") ||
                ContainsToken(hediffClassName, "Hediff_PsycastAbilities"))
            {
                return true;
            }

            return ContainsToken(packageId, "VanillaExpanded.VPsycastsE") &&
                (ContainsToken(defName, "PsycastAbility") ||
                    ContainsToken(label, "psylink") ||
                    ContainsToken(hediffClassName, "PsycastAbilities"));
        }

        private static bool ContainsToken(string value, string token)
        {
            return value != null && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void EnsureNutrientHigh(Pawn pawn)
        {
            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(NutrientHediffDefName);
            if (hediffDef == null || pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return;
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (existing != null)
            {
                existing.Severity = Math.Max(existing.Severity, 1f);
                return;
            }

            Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
            hediff.Severity = 1f;
            pawn.health.AddHediff(hediff);
        }
    }

    [HarmonyPatch(typeof(Thing), "SplitOff")]
    public static class LWOPFoodMakerSplitOffPatch
    {
        public static void Postfix(Thing __instance, Thing __result)
        {
            LWOPFoodMakerGameComponent.CopyMaker(__instance, __result);
        }
    }

    [HarmonyPatch(typeof(Thing), "TryAbsorbStack")]
    public static class LWOPFoodMakerAbsorbStackPatch
    {
        public static void Prefix(Thing __instance, Thing other)
        {
            LWOPFoodMakerGameComponent.CopyMakerIfMissing(other, __instance);
        }
    }

    public static class LWOPFoodSafetyUtility
    {
        private static readonly HashSet<string> LWOPSafeCookApparelDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "lwApparel_PowerArmorHelmet",
            "LWOPApparel_PowerArmor",
            "LWOPApparel_PowerArmorHelmetChild",
            "LWOPApparel_PowerArmorChild"
        };

        private static readonly FieldInfo PoisonPercentField = AccessTools.Field(typeof(CompFoodPoisonable), "poisonPct");
        private static readonly FieldInfo PoisonCauseField = AccessTools.Field(typeof(CompFoodPoisonable), "cause");

        public static bool IsSafeCook(Pawn pawn)
        {
            return IsLWOPWorkerMech(pawn) || WearsLWOPApparel(pawn);
        }

        public static bool IsPackagedSurvivalMeal(Thing thing)
        {
            return thing != null &&
                thing.def != null &&
                string.Equals(thing.def.defName, "MealSurvivalPack", StringComparison.OrdinalIgnoreCase);
        }

        public static void RecordRecipeProduct(Thing thing, Pawn worker)
        {
            CompFoodPoisonable foodPoisonable = FoodPoisonableComp(thing);
            if (foodPoisonable == null)
            {
                return;
            }

            LWOPFoodMakerGameComponent.SetMaker(thing, worker);
            if (IsSafeCook(worker) || IsPackagedSurvivalMeal(thing))
            {
                ClearPoison(foodPoisonable);
            }
        }

        private static void ClearPoison(CompFoodPoisonable comp)
        {
            if (comp == null)
            {
                return;
            }

            PoisonPercentField?.SetValue(comp, 0f);
            PoisonCauseField?.SetValue(comp, FoodPoisonCause.Unknown);
        }

        private static bool IsLWOPWorkerMech(Pawn pawn)
        {
            return pawn != null &&
                pawn.def != null &&
                string.Equals(pawn.def.defName, "Mech_LWOPWorker", StringComparison.OrdinalIgnoreCase);
        }

        private static bool WearsLWOPApparel(Pawn pawn)
        {
            if (pawn == null || pawn.apparel == null)
            {
                return false;
            }

            List<Apparel> wornApparel = pawn.apparel.WornApparel;
            for (int i = 0; i < wornApparel.Count; i++)
            {
                Apparel apparel = wornApparel[i];
                string defName = apparel == null || apparel.def == null ? null : apparel.def.defName;
                if (defName.NullOrEmpty())
                {
                    continue;
                }

                if (LWOPSafeCookApparelDefNames.Contains(defName) ||
                    defName.IndexOf("LWOP", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static CompFoodPoisonable FoodPoisonableComp(Thing thing)
        {
            ThingWithComps thingWithComps = thing as ThingWithComps;
            return thingWithComps == null ? null : thingWithComps.GetComp<CompFoodPoisonable>();
        }
    }

    public class LWOPFoodMakerGameComponent : GameComponent
    {
        private Dictionary<int, string> foodMakers = new Dictionary<int, string>();

        public LWOPFoodMakerGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref foodMakers, "lwopFoodMakers", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && foodMakers == null)
            {
                foodMakers = new Dictionary<int, string>();
            }
        }

        public static void SetMaker(Thing thing, Pawn worker)
        {
            if (thing == null || worker == null)
            {
                return;
            }

            string maker = worker.LabelShortCap.ToString();
            if (maker.NullOrEmpty() && worker.def != null)
            {
                maker = worker.def.label;
            }

            if (!maker.NullOrEmpty())
            {
                SetMaker(thing, maker);
            }
        }

        public static void SetMaker(Thing thing, string maker)
        {
            LWOPFoodMakerGameComponent component = CurrentComponent;
            if (component == null || thing == null || maker.NullOrEmpty())
            {
                return;
            }

            component.foodMakers[thing.thingIDNumber] = maker;
        }

        public static string MakerFor(Thing thing)
        {
            LWOPFoodMakerGameComponent component = CurrentComponent;
            if (component == null || thing == null || component.foodMakers == null)
            {
                return null;
            }

            string maker;
            return component.foodMakers.TryGetValue(thing.thingIDNumber, out maker) ? maker : null;
        }

        public static void CopyMaker(Thing from, Thing to)
        {
            string maker = MakerFor(from);
            if (!maker.NullOrEmpty())
            {
                SetMaker(to, maker);
            }
        }

        public static void CopyMakerIfMissing(Thing from, Thing to)
        {
            if (!MakerFor(to).NullOrEmpty())
            {
                return;
            }

            CopyMaker(from, to);
        }

        private static LWOPFoodMakerGameComponent CurrentComponent
        {
            get
            {
                Game game = Current.Game;
                return game == null ? null : game.GetComponent<LWOPFoodMakerGameComponent>();
            }
        }
    }

    [HarmonyPatch(typeof(CompLaunchable), "MaxLaunchDistanceAtFuelLevel", new Type[] { typeof(float), typeof(PlanetLayer) })]
    public static class LWOPPodLauncherMaxDistancePatch
    {
        public static void Postfix(CompLaunchable __instance, ref int __result)
        {
            if (__result > 0 && __result < int.MaxValue && UsesLWOPFuelingPort(__instance))
            {
                __result = (int)Mathf.Min(int.MaxValue, (long)__result * 20L);
            }
        }

        private static bool UsesLWOPFuelingPort(CompLaunchable launchable)
        {
            CompLaunchable_TransportPod transportPod = launchable as CompLaunchable_TransportPod;
            return transportPod != null &&
                transportPod.FuelingPortSource != null &&
                transportPod.FuelingPortSource.parent != null &&
                transportPod.FuelingPortSource.parent.def != null &&
                transportPod.FuelingPortSource.parent.def.defName == "LWOPPodLauncher";
        }
    }

    [HarmonyPatch(typeof(CompLaunchable), "FuelNeededToLaunchAtDist", new Type[] { typeof(float), typeof(PlanetLayer) })]
    public static class LWOPPodLauncherFuelNeededPatch
    {
        public static void Postfix(CompLaunchable __instance, ref float __result)
        {
            if (__result > 0f && UsesLWOPFuelingPort(__instance))
            {
                __result /= 20f;
            }
        }

        private static bool UsesLWOPFuelingPort(CompLaunchable launchable)
        {
            CompLaunchable_TransportPod transportPod = launchable as CompLaunchable_TransportPod;
            return transportPod != null &&
                transportPod.FuelingPortSource != null &&
                transportPod.FuelingPortSource.parent != null &&
                transportPod.FuelingPortSource.parent.def != null &&
                transportPod.FuelingPortSource.parent.def.defName == "LWOPPodLauncher";
        }
    }

    public static class LWOPPodLauncherUtility
    {
        private static ThingDef lwopTransportPod;

        public static bool IsLWOPPodLauncher(Building_PodLauncher launcher)
        {
            return launcher != null && launcher.def != null && launcher.def.defName == "LWOPPodLauncher";
        }

        public static void PlaceLWOPTransportPodBlueprint(Building_PodLauncher launcher)
        {
            ThingDef podDef = LWOPTransportPodDef;
            if (launcher == null || podDef == null)
            {
                return;
            }

            IntVec3 cell = FuelingPortUtility.GetFuelingPortCell(launcher);
            GenConstruct.PlaceBlueprintForBuild(podDef, cell, launcher.Map, podDef.defaultPlacingRot, Faction.OfPlayer, null, null, null, true);
        }

        public static void CheckPlaceLWOPTransportPod(Building_PodLauncher launcher)
        {
            ThingDef podDef = LWOPTransportPodDef;
            if (launcher == null || podDef == null || !launcher.autoPlacePods)
            {
                return;
            }

            IntVec3 cell = FuelingPortUtility.GetFuelingPortCell(launcher);
            AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(podDef, cell, podDef.defaultPlacingRot, launcher.Map, false, null, null, null, false, false, false);
            if (report.Accepted)
            {
                PlaceLWOPTransportPodBlueprint(launcher);
            }
        }

        private static ThingDef LWOPTransportPodDef
        {
            get
            {
                if (lwopTransportPod == null)
                {
                    lwopTransportPod = DefDatabase<ThingDef>.GetNamedSilentFail("LWOPTransportPod");
                }

                return lwopTransportPod;
            }
        }
    }

    [HarmonyPatch]
    public static class LWOPPodLauncherManualBuildPatch
    {
        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Building_PodLauncher), "<GetGizmos>b__2_0");
        }

        public static bool Prefix(Building_PodLauncher __instance)
        {
            if (!LWOPPodLauncherUtility.IsLWOPPodLauncher(__instance))
            {
                return true;
            }

            LWOPPodLauncherUtility.PlaceLWOPTransportPodBlueprint(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(Building_PodLauncher), "CheckPlacePod")]
    public static class LWOPPodLauncherAutoBuildPatch
    {
        public static bool Prefix(Building_PodLauncher __instance)
        {
            if (!LWOPPodLauncherUtility.IsLWOPPodLauncher(__instance))
            {
                return true;
            }

            LWOPPodLauncherUtility.CheckPlaceLWOPTransportPod(__instance);
            return false;
        }
    }

    public static class LWOPOpenAllContainersUtility
    {
        private static DesignationDef openDesignation;

        public static void AddOrdersDesignator()
        {
            DesignationCategoryDef orders = DefDatabase<DesignationCategoryDef>.GetNamedSilentFail("Orders");
            if (orders == null)
            {
                return;
            }

            Type designatorType = typeof(Designator_LWOPOpenAllContainers);
            if (orders.specialDesignatorClasses == null)
            {
                orders.specialDesignatorClasses = new List<Type>();
            }

            if (!orders.specialDesignatorClasses.Contains(designatorType))
            {
                orders.specialDesignatorClasses.Add(designatorType);
            }

            List<Designator> designators = orders.AllResolvedDesignators;
            if (designators != null && !designators.Any(designator => designator != null && designator.GetType() == designatorType))
            {
                designators.Add(new Designator_LWOPOpenAllContainers());
            }
        }

        public static void DesignateAllOpenables(Map map)
        {
            if (map == null || OpenDesignation == null)
            {
                Messages.Message("LWOPOpenAllContainersNone".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            int count = 0;
            List<Thing> things = map.listerThings.AllThings.ToList();
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (!CanDesignateOpen(thing, map))
                {
                    continue;
                }

                map.designationManager.AddDesignation(new Designation(thing, OpenDesignation));
                count++;
            }

            if (count > 0)
            {
                Messages.Message("LWOPOpenAllContainersDone".Translate(count), MessageTypeDefOf.TaskCompletion, false);
            }
            else
            {
                Messages.Message("LWOPOpenAllContainersNone".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        public static bool HasDesignatableOpenables(Map map)
        {
            if (map == null || OpenDesignation == null)
            {
                return false;
            }

            List<Thing> things = map.listerThings.AllThings;
            for (int i = 0; i < things.Count; i++)
            {
                if (CanDesignateOpen(things[i], map))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanDesignateOpen(Thing thing, Map map)
        {
            return IsOpenable(thing) &&
                thing.Map == map &&
                map.designationManager.DesignationOn(thing, OpenDesignation) == null;
        }

        private static bool IsOpenable(Thing thing)
        {
            IOpenable openable = thing as IOpenable;
            return openable != null &&
                !IsExcludedOpenable(thing) &&
                openable.CanOpen &&
                thing.Spawned &&
                !thing.Destroyed &&
                !thing.Discarded;
        }

        private static bool IsExcludedOpenable(Thing thing)
        {
            if (thing is Building_CryptosleepCasket)
            {
                return true;
            }

            string defName = thing == null || thing.def == null ? null : thing.def.defName;
            return !defName.NullOrEmpty() &&
                defName.IndexOf("Cryptosleep", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static DesignationDef OpenDesignation
        {
            get
            {
                if (openDesignation == null)
                {
                    openDesignation = DefDatabase<DesignationDef>.GetNamedSilentFail("Open");
                }

                return openDesignation;
            }
        }

    }

    public class Designator_LWOPOpenAllContainers : Designator
    {
        public Designator_LWOPOpenAllContainers()
        {
            defaultLabel = "LWOPOpenAllContainers".Translate().ToString();
            defaultDesc = "LWOPOpenAllContainersDesc".Translate().ToString();
            icon = ContentFinder<Texture2D>.Get("Designations/Open");
            soundSucceeded = SoundDefOf.Designate_Claim;
            soundFailed = SoundDefOf.ClickReject;
            useMouseIcon = false;
            Order = -10f;
        }

        public override bool Visible
        {
            get { return Find.CurrentMap != null && LWOPOpenAllContainersUtility.HasDesignatableOpenables(Find.CurrentMap); }
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 loc)
        {
            return false;
        }

        public override bool CanRemainSelected()
        {
            return false;
        }

        public override void ProcessInput(Event ev)
        {
            if (Find.CurrentMap != null)
            {
                LWOPOpenAllContainersUtility.DesignateAllOpenables(Find.CurrentMap);
            }
        }
    }

}
