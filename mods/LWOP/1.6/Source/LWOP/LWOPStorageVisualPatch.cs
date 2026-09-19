using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LWOP.Buildings
{
    [StaticConstructorOnStartup]
    public static class LWOPStorageVisualPatch
    {
        static LWOPStorageVisualPatch()
        {
            new Harmony("com.colonyseries.lwop.StorageVisualStackLimit").PatchAll();
        }
    }

    public static class LWOPStorageRackUtility
    {
        public static bool IsLWOPStorageRack(Thing thing)
        {
            return thing != null &&
                thing.def != null &&
                (thing.def.defName == "LWOPStorageRack" ||
                    thing.def.defName == "LWOPStorageRackSmall" ||
                    thing.def.defName == "LWOPCorpseCollector" ||
                    thing.def.defName == "LWOPDimensionalStorageBox");
        }

        public static bool IsOnLWOPStorageRack(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Map == null)
            {
                return false;
            }

            return IsLWOPStorageRack(thing.Position.GetEdifice(thing.Map));
        }

        public static bool ShouldHideStoredThingGraphic(Thing thing)
        {
            if (thing == null || thing.def == null || IsLWOPStorageRack(thing) || !IsOnLWOPStorageRack(thing))
            {
                return false;
            }

            if (thing is Pawn)
            {
                return false;
            }

            return thing.def.category == ThingCategory.Item ||
                thing is Corpse ||
                thing is MinifiedThing ||
                thing.def.EverStorable(false);
        }

        public static bool ShouldHideStoredThingGraphicSafe(Thing thing)
        {
            try
            {
                return ShouldHideStoredThingGraphic(thing);
            }
            catch (Exception ex)
            {
                string thingLabel = thing == null || thing.def == null ? "null" : thing.def.defName;
                Log.WarningOnce("[LWOP] Failed to evaluate storage rack visual hiding for " + thingLabel + ": " + ex.Message, 92736141);
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(Thing), "DrawAt", new Type[] { typeof(Vector3), typeof(bool) })]
    public static class LWOPStorageRackThingDrawAtPatch
    {
        public static bool Prefix(Thing __instance)
        {
            return !LWOPStorageRackUtility.ShouldHideStoredThingGraphicSafe(__instance);
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), "DrawAt", new Type[] { typeof(Vector3), typeof(bool) })]
    public static class LWOPStorageRackThingWithCompsDrawAtPatch
    {
        public static bool Prefix(Thing __instance)
        {
            return !LWOPStorageRackUtility.ShouldHideStoredThingGraphicSafe(__instance);
        }
    }

    [HarmonyPatch(typeof(MinifiedThing), "DrawAt", new Type[] { typeof(Vector3), typeof(bool) })]
    public static class LWOPStorageRackMinifiedThingDrawAtPatch
    {
        public static bool Prefix(MinifiedThing __instance)
        {
            return !LWOPStorageRackUtility.ShouldHideStoredThingGraphicSafe(__instance);
        }
    }

    [HarmonyPatch(typeof(Thing), "DynamicDrawPhaseAt", new Type[] { typeof(DrawPhase), typeof(Vector3), typeof(bool) })]
    public static class LWOPStorageRackThingDynamicDrawPhaseAtPatch
    {
        public static bool Prefix(Thing __instance)
        {
            return !LWOPStorageRackUtility.ShouldHideStoredThingGraphicSafe(__instance);
        }
    }

    [HarmonyPatch(typeof(Corpse), "DynamicDrawPhaseAt", new Type[] { typeof(DrawPhase), typeof(Vector3), typeof(bool) })]
    public static class LWOPStorageRackCorpseDynamicDrawPhaseAtPatch
    {
        public static bool Prefix(Corpse __instance)
        {
            return !LWOPStorageRackUtility.ShouldHideStoredThingGraphicSafe(__instance);
        }
    }

    [HarmonyPatch(typeof(Thing), "DrawGUIOverlay")]
    public static class LWOPStorageRackThingDrawGUIOverlayPatch
    {
        public static bool Prefix(Thing __instance)
        {
            return !LWOPStorageRackUtility.ShouldHideStoredThingGraphicSafe(__instance);
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), "DrawGUIOverlay")]
    public static class LWOPStorageRackThingWithCompsDrawGUIOverlayPatch
    {
        public static bool Prefix(Thing __instance)
        {
            return !LWOPStorageRackUtility.ShouldHideStoredThingGraphicSafe(__instance);
        }
    }

    [HarmonyPatch(typeof(Thing), "Print", new Type[] { typeof(SectionLayer) })]
    public static class LWOPStorageRackThingPrintPatch
    {
        public static bool Prefix(Thing __instance)
        {
            return !LWOPStorageRackUtility.ShouldHideStoredThingGraphicSafe(__instance);
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), "Print", new Type[] { typeof(SectionLayer) })]
    public static class LWOPStorageRackThingWithCompsPrintPatch
    {
        public static bool Prefix(Thing __instance)
        {
            return !LWOPStorageRackUtility.ShouldHideStoredThingGraphicSafe(__instance);
        }
    }

    [HarmonyPatch(typeof(MinifiedThing), "Print", new Type[] { typeof(SectionLayer) })]
    public static class LWOPStorageRackMinifiedThingPrintPatch
    {
        public static bool Prefix(MinifiedThing __instance)
        {
            return !LWOPStorageRackUtility.ShouldHideStoredThingGraphicSafe(__instance);
        }
    }

    [HarmonyPatch(typeof(GenThing), "ItemCenterAt")]
    public static class GenThingItemCenterAtPatch
    {
        private const int VisualSlots = 5;

        public static void Postfix(Thing thing, ref Vector3 __result)
        {
            if (thing == null || !thing.Spawned || thing.Map == null || thing.def == null || thing.def.category != ThingCategory.Item)
            {
                return;
            }

            if (!LWOPStorageRackUtility.IsOnLWOPStorageRack(thing))
            {
                return;
            }

            int slot = Math.Abs(thing.thingIDNumber) % VisualSlots;
            Vector3 basePos = thing.Position.ToVector3Shifted();
            Vector2 offset = VisualOffset(slot);
            __result = new Vector3(
                basePos.x + offset.x,
                thing.def.Altitude + 0.003f * slot,
                basePos.z + offset.y);
        }

        private static Vector2 VisualOffset(int slot)
        {
            switch (slot)
            {
                case 0:
                    return new Vector2(-0.20f, -0.14f);
                case 1:
                    return new Vector2(0.20f, -0.12f);
                case 2:
                    return new Vector2(-0.14f, 0.14f);
                case 3:
                    return new Vector2(0.16f, 0.16f);
                default:
                    return new Vector2(0.00f, 0.02f);
            }
        }
    }

    [HarmonyPatch(typeof(GenDraw), "DrawRadiusRing", new Type[] { typeof(IntVec3), typeof(float), typeof(Color), typeof(Func<IntVec3, bool>) })]
    public static class GenDrawDrawRadiusRingPatch
    {
        private const float MaxDrawableRadius = 90f;

        public static bool Prefix(float radius)
        {
            return radius <= MaxDrawableRadius;
        }
    }

    [HarmonyPatch(typeof(CompRottable), "CompTickRare")]
    public static class LWOPStorageRackRottableRarePatch
    {
        public static bool Prefix(CompRottable __instance)
        {
            return __instance == null || !LWOPStorageRackUtility.IsOnLWOPStorageRack(__instance.parent);
        }
    }

    [HarmonyPatch(typeof(CompRottable), "CompTickInterval")]
    public static class LWOPStorageRackRottableIntervalPatch
    {
        public static bool Prefix(CompRottable __instance)
        {
            return __instance == null || !LWOPStorageRackUtility.IsOnLWOPStorageRack(__instance.parent);
        }
    }

    [HarmonyPatch(typeof(CompRottable), "CompInspectStringExtra")]
    public static class LWOPStorageRackRottableInspectPatch
    {
        public static void Postfix(CompRottable __instance, ref string __result)
        {
            if (__instance == null || !__instance.Active || !LWOPStorageRackUtility.IsOnLWOPStorageRack(__instance.parent))
            {
                return;
            }

            switch (__instance.Stage)
            {
                case RotStage.Fresh:
                    __result = "RotStateFresh".Translate() + string.Format(" ({0})", "CurrentlyFrozen".Translate());
                    break;
                case RotStage.Rotting:
                    __result = "RotStateRotting".Translate() + ".";
                    break;
                case RotStage.Dessicated:
                    __result = "RotStateDessicated".Translate() + ".";
                    break;
            }
        }
    }

    public class CompProperties_LWOPCorpseCollector : CompProperties
    {
        public int collectDelayTicks = 30;
        public int maxPendingTicks = 60000;
        public float collectRadius = 12f;

        public CompProperties_LWOPCorpseCollector()
        {
            compClass = typeof(CompLWOPCorpseCollector);
        }
    }

    public class CompLWOPCorpseCollector : ThingComp
    {
        public CompProperties_LWOPCorpseCollector Props
        {
            get { return (CompProperties_LWOPCorpseCollector)props; }
        }

        public bool Active
        {
            get
            {
                return parent != null &&
                    parent.Spawned &&
                    parent.Map != null &&
                    parent.Faction == Faction.OfPlayer &&
                    !parent.IsForbidden(Faction.OfPlayer) &&
                    FlickUtility.WantsToBeOn(parent);
            }
        }

        public bool StoreThing(Thing thing)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map != parent.Map)
            {
                return false;
            }

            if (!Allows(thing))
            {
                return false;
            }

            if (thing.Position == parent.Position)
            {
                TryUnforbid(thing);
                return true;
            }

            Map map = thing.Map;
            IntVec3 originalPosition = thing.Position;
            thing.DeSpawn();

            Thing placedThing;
            if (GenPlace.TryPlaceThing(thing, parent.Position, map, ThingPlaceMode.Direct, out placedThing))
            {
                TryUnforbid(placedThing ?? thing);
                return true;
            }

            GenSpawn.Spawn(thing, originalPosition, map);
            return false;
        }

        private bool Allows(Thing thing)
        {
            Building_Storage storage = parent as Building_Storage;
            return storage == null || storage.Accepts(thing);
        }

        private static void TryUnforbid(Thing thing)
        {
            if (thing == null)
            {
                return;
            }

            thing.SetForbidden(false, false);
        }
    }

    public class CompProperties_LWOPDimensionalStorageBox : CompProperties
    {
        public int defaultChannel = 1;
        public int absorbIntervalTicks = 60;
        public int ejectGraceTicks = 2500;

        public CompProperties_LWOPDimensionalStorageBox()
        {
            compClass = typeof(CompLWOPDimensionalStorageBox);
        }
    }

    public class CompLWOPDimensionalStorageBox : ThingComp
    {
        private int channel = 1;
        private bool isHost;
        private bool setupConfigured;
        private bool detachedForRemoval;

        public CompProperties_LWOPDimensionalStorageBox Props
        {
            get { return (CompProperties_LWOPDimensionalStorageBox)props; }
        }

        public int Channel
        {
            get { return Math.Max(1, channel); }
        }

        public bool IsHost
        {
            get { return isHost; }
        }

        private Texture2D CommandIcon
        {
            get
            {
                if (parent != null && parent.def != null && parent.def.uiIcon != null)
                {
                    return parent.def.uiIcon;
                }

                return BaseContent.BadTex;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            detachedForRemoval = false;
            if (channel < 1)
            {
                channel = Math.Max(1, Props.defaultChannel);
            }

            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            if (component != null)
            {
                if (respawningAfterLoad && !setupConfigured)
                {
                    setupConfigured = true;
                }

                if (!component.HasHost(Channel))
                {
                    component.SetChannelHost(Channel, this);
                }
                else if (isHost)
                {
                    component.SetChannelHost(Channel, this);
                }

                if (!isHost)
                {
                    component.SyncBoxStorageSettingsFromHost(Channel, parent);
                }
            }

            if (!respawningAfterLoad &&
                !setupConfigured &&
                parent != null &&
                parent.Faction == Faction.OfPlayer)
            {
                Find.WindowStack.Add(new Dialog_LWOPDimensionalStorageSetup(this, true));
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            DetachContentsForRemoval(map);
            base.PostDeSpawn(map, mode);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            DetachContentsForRemoval(previousMap);
            base.PostDestroy(mode, previousMap);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref channel, "lwopDimensionalStorageChannel", Math.Max(1, Props.defaultChannel));
            Scribe_Values.Look(ref isHost, "lwopDimensionalStorageIsHost", false);
            Scribe_Values.Look(ref setupConfigured, "lwopDimensionalStorageSetupConfigured", false);
            if (channel < 1)
            {
                channel = Math.Max(1, Props.defaultChannel);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent == null ||
                !parent.Spawned ||
                parent.Map == null ||
                !parent.IsHashIntervalTick(Math.Max(1, Props.absorbIntervalTicks)))
            {
                return;
            }

            UploadLocalContents();
        }

        public override string CompInspectStringExtra()
        {
            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            int stacks = 0;
            int totalCount = 0;
            if (component != null)
            {
                component.GetCounts(Channel, out stacks, out totalCount);
            }

            string role = (isHost ? "LWOPDimensionalStorageRoleHost" : "LWOPDimensionalStorageRoleTerminal").Translate();
            string hostState = component != null && component.HasHost(Channel) ? "LWOPDimensionalStorageHostOnline".Translate() : "LWOPDimensionalStorageHostMissing".Translate();
            return "LWOPDimensionalStorageInspect".Translate(Channel.ToString(), stacks.ToString(), totalCount.ToString()) +
                "\n" + "LWOPDimensionalStorageRoleInspect".Translate(role, hostState);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            yield return new Command_Action
            {
                defaultLabel = "LWOPDimensionalStorageSetChannel".Translate(Channel.ToString()),
                defaultDesc = "LWOPDimensionalStorageSetChannelDesc".Translate(),
                icon = CommandIcon,
                action = delegate
                {
                    Find.WindowStack.Add(new Dialog_LWOPDimensionalStorageSetup(this, false));
                }
            };

            if (!isHost)
            {
                yield return new Command_Action
                {
                    defaultLabel = "LWOPDimensionalStorageMakeHost".Translate(),
                    defaultDesc = "LWOPDimensionalStorageMakeHostDesc".Translate(Channel.ToString()),
                    icon = CommandIcon,
                    action = delegate
                    {
                        ApplyChannelAndRole(Channel, true, true);
                    }
                };
            }

            yield return new Command_Action
            {
                defaultLabel = "LWOPDimensionalStorageOpen".Translate(),
                defaultDesc = "LWOPDimensionalStorageOpenDesc".Translate(Channel.ToString()),
                icon = CommandIcon,
                action = delegate
                {
                    Find.WindowStack.Add(new Dialog_LWOPDimensionalStorage(this));
                }
            };
        }

        public void SetChannel(int newChannel)
        {
            ApplyChannelAndRole(newChannel, isHost, true);
        }

        public void ApplyChannelAndRole(int newChannel, bool newIsHost, bool askForHostReplacement)
        {
            int normalizedChannel = Math.Max(1, newChannel);
            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            if (newIsHost &&
                askForHostReplacement &&
                component != null &&
                component.HasDifferentHost(normalizedChannel, this))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "LWOPDimensionalStorageReplaceHostConfirm".Translate(normalizedChannel.ToString()),
                    delegate
                    {
                        ApplyChannelAndRole(normalizedChannel, true, false);
                    },
                    false,
                    "LWOPDimensionalStorageReplaceHostTitle".Translate()));
                return;
            }

            int oldChannel = Channel;
            bool wasHost = isHost;
            if (component != null && parent != null && parent.Spawned && parent.Map != null)
            {
                component.AbsorbBoxContentsToChannel(oldChannel, parent, false);
            }

            channel = normalizedChannel;
            isHost = newIsHost;
            setupConfigured = true;

            if (component != null)
            {
                if (wasHost && (!newIsHost || oldChannel != normalizedChannel))
                {
                    component.PromoteFallbackHost(oldChannel, parent);
                }

                if (newIsHost)
                {
                    component.SetChannelHost(normalizedChannel, this);
                }
                else
                {
                    component.SyncBoxStorageSettingsFromHost(normalizedChannel, parent);
                }
            }

            UploadLocalContents();
        }

        public bool TryEjectThing(Thing thing)
        {
            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            return component != null &&
                component.TryEjectOrMoveThingToBox(Channel, thing, parent.Position, parent.Map, Props.ejectGraceTicks);
        }

        public void SyncChannelToThisBox(bool includeSpawnedThings)
        {
            UploadLocalContents();
        }

        public void SetHostDirect(bool host)
        {
            isHost = host;
            setupConfigured = true;
        }

        public void UploadLocalContents()
        {
            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            if (component != null && parent != null && parent.Spawned && parent.Map != null)
            {
                if (!isHost)
                {
                    component.AbsorbBoxContentsToChannel(Channel, parent, false);
                }

                component.MaterializeChannelToHost(Channel, Props.ejectGraceTicks, true);
            }
        }

        public void NotifyStorageSettingsChanged()
        {
            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            if (component != null)
            {
                component.SyncChannelStorageSettingsFrom(this);
            }
        }

        private void DetachContentsForRemoval(Map map)
        {
            if (detachedForRemoval || parent == null || map == null)
            {
                return;
            }

            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            if (component == null)
            {
                return;
            }

            detachedForRemoval = true;
            if (isHost)
            {
                component.DropChannelContentsAt(Channel, parent.Position, map, Props.ejectGraceTicks);
            }
            else
            {
                component.AbsorbBoxContentsToChannel(Channel, parent, parent.Position, map, true);
                component.MaterializeChannelToHost(Channel, Props.ejectGraceTicks, true);
            }
        }
    }

    [HarmonyPatch(typeof(Building_Storage), "Notify_ReceivedThing")]
    public static class LWOPDimensionalStorageReceivedThingPatch
    {
        public static void Postfix(Building_Storage __instance)
        {
            CompLWOPDimensionalStorageBox box = __instance == null ? null : __instance.TryGetComp<CompLWOPDimensionalStorageBox>();
            if (box != null)
            {
                box.UploadLocalContents();
            }
        }
    }

    [HarmonyPatch]
    public static class LWOPDimensionalStorageSettingsChangedPatch
    {
        public static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            System.Reflection.MethodInfo method = AccessTools.Method(typeof(Building_Storage), "Notify_SettingsChanged");
            if (method != null)
            {
                yield return method;
            }
        }

        public static void Postfix(Building_Storage __instance)
        {
            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            if (component != null && component.IsSyncingStorageSettings)
            {
                return;
            }

            CompLWOPDimensionalStorageBox box = __instance == null ? null : __instance.TryGetComp<CompLWOPDimensionalStorageBox>();
            if (box != null)
            {
                box.NotifyStorageSettingsChanged();
            }
        }
    }

    [HarmonyPatch]
    public static class LWOPDimensionalStorageClosestThingReachablePatch
    {
        public static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (System.Reflection.MethodInfo method in AccessTools.GetDeclaredMethods(typeof(GenClosest)))
            {
                if (method.Name == "ClosestThingReachable" &&
                    typeof(Thing).IsAssignableFrom(method.ReturnType))
                {
                    yield return method;
                }
            }
        }

        public static void Postfix(object[] __args, ref Thing __result)
        {
            IntVec3 root;
            Map map;
            ThingRequest request;
            TraverseParms traverseParms;
            bool hasTraverseParms;
            float maxDistance;
            Predicate<Thing> validator;
            if (!TryReadArgs(__args, out root, out map, out request, out traverseParms, out hasTraverseParms, out maxDistance, out validator))
            {
                return;
            }

            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            Thing ejectedThing;
            if (component != null &&
                component.TryEjectMatchingThing(root, map, request, validator, traverseParms, hasTraverseParms, maxDistance, __result, out ejectedThing))
            {
                __result = ejectedThing;
            }
        }

        private static bool TryReadArgs(
            object[] args,
            out IntVec3 root,
            out Map map,
            out ThingRequest request,
            out TraverseParms traverseParms,
            out bool hasTraverseParms,
            out float maxDistance,
            out Predicate<Thing> validator)
        {
            root = IntVec3.Invalid;
            map = null;
            request = default(ThingRequest);
            traverseParms = default(TraverseParms);
            hasTraverseParms = false;
            maxDistance = 9999f;
            validator = null;

            bool hasRoot = false;
            bool hasRequest = false;
            bool hasMaxDistance = false;
            if (args == null)
            {
                return false;
            }

            for (int index = 0; index < args.Length; index++)
            {
                object arg = args[index];
                if (!hasRoot && arg is IntVec3)
                {
                    root = (IntVec3)arg;
                    hasRoot = root.IsValid;
                    continue;
                }

                if (map == null)
                {
                    map = arg as Map;
                    if (map != null)
                    {
                        continue;
                    }
                }

                if (!hasRequest && arg is ThingRequest)
                {
                    request = (ThingRequest)arg;
                    hasRequest = true;
                    continue;
                }

                if (!hasTraverseParms && arg is TraverseParms)
                {
                    traverseParms = (TraverseParms)arg;
                    hasTraverseParms = true;
                    continue;
                }

                if (!hasMaxDistance && arg is float)
                {
                    maxDistance = (float)arg;
                    hasMaxDistance = true;
                    continue;
                }

                if (validator == null)
                {
                    validator = arg as Predicate<Thing>;
                }
            }

            return hasRoot && map != null && hasRequest;
        }
    }

    [HarmonyPatch]
    public static class LWOPDimensionalStorageBillIngredientsPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(WorkGiver_DoBill),
                "TryFindBestBillIngredients",
                new Type[]
                {
                    typeof(Bill),
                    typeof(Pawn),
                    typeof(Thing),
                    typeof(List<ThingCount>),
                    typeof(List<IngredientCount>)
                });
        }

        public static void Prefix(Bill bill, Pawn pawn, Thing billGiver)
        {
            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            if (component != null)
            {
                component.PrepareBillIngredients(bill, pawn, billGiver);
            }
        }
    }

    [HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestFixedIngredients")]
    public static class LWOPDimensionalStorageFixedIngredientsPatch
    {
        public static void Prefix(List<IngredientCount> ingredients, Pawn pawn, Thing ingredientDestination, float searchRadius)
        {
            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            if (component != null)
            {
                component.PrepareFixedIngredients(ingredients, pawn, ingredientDestination, searchRadius);
            }
        }
    }

    [HarmonyPatch]
    public static class LWOPDimensionalStorageStartOrResumeBillPatch
    {
        public static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(WorkGiver_DoBill),
                "StartOrResumeBillJob",
                new Type[] { typeof(Pawn), typeof(IBillGiver), typeof(bool) });
        }

        public static void Prefix(Pawn pawn, IBillGiver giver)
        {
            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            if (component != null)
            {
                component.PrimeBillGiverForDimensionalIngredients(pawn, giver);
            }
        }
    }

    public class LWOPDimensionalStorageGameComponent : GameComponent, IThingHolder
    {
        private List<LWOPDimensionalStorageChannel> channels = new List<LWOPDimensionalStorageChannel>();
        private Dictionary<int, int> ejectedUntilTick = new Dictionary<int, int>();
        private readonly List<Thing> matchingThings = new List<Thing>();
        private readonly List<Thing> spawnedChannelThings = new List<Thing>();
        private readonly List<Thing> hiddenChannelThings = new List<Thing>();
        private bool syncingStorageSettings;
        private bool materializingChannel;
        private ThingOwner<Thing> emptyHolder;

        public LWOPDimensionalStorageGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref channels, "lwopDimensionalStorageChannels", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (channels == null)
                {
                    channels = new List<LWOPDimensionalStorageChannel>();
                }

                if (ejectedUntilTick == null)
                {
                    ejectedUntilTick = new Dictionary<int, int>();
                }
            }
        }

        public static LWOPDimensionalStorageGameComponent CurrentComponent
        {
            get
            {
                Game game = Current.Game;
                return game == null ? null : game.GetComponent<LWOPDimensionalStorageGameComponent>();
            }
        }

        public bool IsSyncingStorageSettings
        {
            get { return syncingStorageSettings; }
        }

        public bool PrepareBillIngredients(Bill bill, Pawn pawn, Thing billGiver)
        {
            if (bill == null || billGiver == null || !billGiver.Spawned || billGiver.Map == null)
            {
                return false;
            }

            return PrepareChannelItemsForWork(
                billGiver.Map,
                billGiver.Position,
                bill.ingredientSearchRadius,
                delegate(Thing thing)
                {
                    return BillAllowsThing(bill, thing);
                });
        }

        public bool PrepareFixedIngredients(List<IngredientCount> ingredients, Pawn pawn, Thing ingredientDestination, float searchRadius)
        {
            if (ingredients == null ||
                ingredients.Count == 0 ||
                ingredientDestination == null ||
                !ingredientDestination.Spawned ||
                ingredientDestination.Map == null)
            {
                return false;
            }

            return PrepareChannelItemsForWork(
                ingredientDestination.Map,
                ingredientDestination.Position,
                searchRadius,
                delegate(Thing thing)
                {
                    return IngredientCountsAllow(ingredients, thing);
                });
        }

        public void PrimeBillGiverForDimensionalIngredients(Pawn pawn, IBillGiver giver)
        {
            Thing billGiverThing = giver as Thing;
            if (pawn == null ||
                giver == null ||
                billGiverThing == null ||
                !billGiverThing.Spawned ||
                billGiverThing.Map == null ||
                giver.BillStack == null)
            {
                return;
            }

            for (int index = 0; index < giver.BillStack.Count; index++)
            {
                Bill bill = giver.BillStack[index];
                if (bill == null ||
                    bill.recipe == null ||
                    bill.recipe.ingredients == null ||
                    bill.recipe.ingredients.Count == 0 ||
                    !SafeBillShouldDoNow(bill))
                {
                    continue;
                }

                if (PrepareBillIngredients(bill, pawn, billGiverThing))
                {
                    bill.nextTickToSearchForIngredients = 0;
                }
            }
        }

        public bool TryAddThing(int channelId, Thing thing)
        {
            if (thing == null || thing.Destroyed)
            {
                return false;
            }

            return ChannelFor(channelId, true).TryAdd(thing);
        }

        public bool HasHost(int channelId)
        {
            return FindHostBox(channelId, null) != null;
        }

        public bool EnsureHost(int channelId)
        {
            if (HasHost(channelId))
            {
                return true;
            }

            PromoteFallbackHost(channelId, null);
            return HasHost(channelId);
        }

        public bool HasDifferentHost(int channelId, CompLWOPDimensionalStorageBox requester)
        {
            Thing requesterThing = requester == null ? null : requester.parent;
            return FindHostBox(channelId, requesterThing) != null;
        }

        public void SetChannelHost(int channelId, CompLWOPDimensionalStorageBox newHost)
        {
            int normalizedChannel = Math.Max(1, channelId);
            Thing newHostThing = newHost == null ? null : newHost.parent;
            if (newHost != null)
            {
                newHost.SetHostDirect(true);
            }

            List<Thing> boxes = new List<Thing>();
            AddChannelBoxesTo(normalizedChannel, boxes);

            for (int index = 0; index < boxes.Count; index++)
            {
                Thing boxThing = boxes[index];
                CompLWOPDimensionalStorageBox box = boxThing == null ? null : boxThing.TryGetComp<CompLWOPDimensionalStorageBox>();
                if (box != null && box.IsHost && boxThing != newHostThing)
                {
                    AbsorbBoxContentsToChannel(normalizedChannel, boxThing, false);
                }
            }

            for (int index = 0; index < boxes.Count; index++)
            {
                Thing boxThing = boxes[index];
                CompLWOPDimensionalStorageBox box = boxThing == null ? null : boxThing.TryGetComp<CompLWOPDimensionalStorageBox>();
                if (box != null)
                {
                    box.SetHostDirect(boxThing == newHostThing);
                }
            }

            if (newHostThing != null)
            {
                SyncChannelStorageSettingsFrom(newHost);
                MaterializeChannelToBox(normalizedChannel, newHostThing.Position, newHostThing.Map, newHost.Props.ejectGraceTicks, true);
            }
        }

        public void MaterializeChannelToHost(int channelId, int graceTicks, bool includeSpawnedThings)
        {
            Thing hostBox = FindHostBox(channelId, null);
            if (hostBox == null || !hostBox.Spawned || hostBox.Map == null)
            {
                return;
            }

            MaterializeChannelToBox(channelId, hostBox.Position, hostBox.Map, graceTicks, includeSpawnedThings);
        }

        public void SyncChannelStorageSettingsFrom(CompLWOPDimensionalStorageBox sourceBox)
        {
            if (sourceBox == null || sourceBox.parent == null || syncingStorageSettings)
            {
                return;
            }

            List<Thing> boxes = new List<Thing>();
            AddChannelBoxesTo(sourceBox.Channel, boxes);
            if (boxes.Count <= 1)
            {
                return;
            }

            try
            {
                syncingStorageSettings = true;
                for (int index = 0; index < boxes.Count; index++)
                {
                    Thing targetBox = boxes[index];
                    if (targetBox != null && targetBox != sourceBox.parent)
                    {
                        CopyStorageSettings(sourceBox.parent, targetBox);
                    }
                }
            }
            finally
            {
                syncingStorageSettings = false;
            }
        }

        public void SyncBoxStorageSettingsFromHost(int channelId, Thing targetBox)
        {
            if (targetBox == null || syncingStorageSettings)
            {
                return;
            }

            Thing hostBox = FindHostBox(channelId, null);
            if (hostBox == null || hostBox == targetBox)
            {
                return;
            }

            try
            {
                syncingStorageSettings = true;
                CopyStorageSettings(hostBox, targetBox);
            }
            finally
            {
                syncingStorageSettings = false;
            }
        }

        public void PromoteFallbackHost(int channelId, Thing excludedBox)
        {
            Thing fallbackHost = FindAlternativeChannelBox(channelId, excludedBox, null);
            CompLWOPDimensionalStorageBox box = fallbackHost == null ? null : fallbackHost.TryGetComp<CompLWOPDimensionalStorageBox>();
            if (box != null)
            {
                SetChannelHost(channelId, box);
            }
        }

        private bool PrepareChannelItemsForWork(Map map, IntVec3 rootCell, float searchRadius, Predicate<Thing> matcher)
        {
            if (map == null || matcher == null)
            {
                return false;
            }

            List<Thing> boxes = new List<Thing>();
            AddDimensionalBoxesOnMap(map, boxes);
            if (boxes.Count == 0)
            {
                return false;
            }

            boxes.Sort(delegate(Thing firstBox, Thing secondBox)
            {
                return rootCell.DistanceToSquared(firstBox.Position).CompareTo(rootCell.DistanceToSquared(secondBox.Position));
            });

            HashSet<int> preparedChannels = new HashSet<int>();
            bool preparedAny = false;
            for (int index = 0; index < boxes.Count; index++)
            {
                Thing boxThing = boxes[index];
                CompLWOPDimensionalStorageBox box = boxThing == null ? null : boxThing.TryGetComp<CompLWOPDimensionalStorageBox>();
                if (box == null ||
                    preparedChannels.Contains(box.Channel) ||
                    !WithinIngredientSearchRadius(rootCell, boxThing.Position, searchRadius))
                {
                    continue;
                }

                if (PrepareChannelItemsAtBox(box.Channel, boxThing, box.Props.ejectGraceTicks, matcher))
                {
                    preparedAny = true;
                }

                preparedChannels.Add(box.Channel);
            }

            return preparedAny;
        }

        private bool PrepareChannelItemsAtBox(int channelId, Thing accessBox, int graceTicks, Predicate<Thing> matcher)
        {
            if (accessBox == null || !accessBox.Spawned || accessBox.Map == null)
            {
                return false;
            }

            int normalizedChannel = Math.Max(1, channelId);
            bool preparedAny = false;
            LWOPDimensionalStorageChannel channel = ChannelFor(normalizedChannel, false);
            if (channel != null)
            {
                hiddenChannelThings.Clear();
                channel.AddThingsTo(hiddenChannelThings);
                for (int index = 0; index < hiddenChannelThings.Count; index++)
                {
                    Thing thing = hiddenChannelThings[index];
                    if (MatchesWorkIngredient(matcher, thing))
                    {
                        Thing ejectedThing;
                        if (TryEjectThing(normalizedChannel, thing, accessBox.Position, accessBox.Map, graceTicks, out ejectedThing))
                        {
                            preparedAny = true;
                        }
                    }
                }
            }

            spawnedChannelThings.Clear();
            AddSpawnedChannelThingsTo(normalizedChannel, spawnedChannelThings);
            for (int index = 0; index < spawnedChannelThings.Count; index++)
            {
                Thing thing = spawnedChannelThings[index];
                if (thing == null ||
                    !thing.Spawned ||
                    !MatchesWorkIngredient(matcher, thing))
                {
                    continue;
                }

                if (thing.Map == accessBox.Map && thing.Position == accessBox.Position)
                {
                    preparedAny = true;
                    continue;
                }

                Thing movedThing;
                if (MoveSpawnedThingToBox(thing, accessBox.Position, accessBox.Map, graceTicks, out movedThing))
                {
                    preparedAny = true;
                }
            }

            return preparedAny;
        }

        public bool AbsorbBoxContentsToChannel(int channelId, Thing boxThing, bool force)
        {
            if (boxThing == null || !boxThing.Spawned || boxThing.Map == null)
            {
                return false;
            }

            return AbsorbBoxContentsToChannel(channelId, boxThing, boxThing.Position, boxThing.Map, force);
        }

        public bool AbsorbBoxContentsToChannel(int channelId, Thing boxThing, IntVec3 boxCell, Map map, bool force)
        {
            if (boxThing == null || map == null || !boxCell.InBounds(map))
            {
                return false;
            }

            int normalizedChannel = Math.Max(1, channelId);
            List<Thing> cellThings = boxCell.GetThingList(map);
            List<Thing> thingsToAbsorb = new List<Thing>();
            for (int index = 0; index < cellThings.Count; index++)
            {
                Thing thing = cellThings[index];
                if (IsDimensionalStoredThingOnBox(thing, boxThing))
                {
                    thingsToAbsorb.Add(thing);
                }
            }

            bool absorbedAny = false;
            for (int index = 0; index < thingsToAbsorb.Count; index++)
            {
                Thing thing = thingsToAbsorb[index];
                if (thing == null || thing.Destroyed || !thing.Spawned)
                {
                    continue;
                }

                if (!force && IsEjectedThingProtected(thing))
                {
                    continue;
                }

                if (!force && !BoxAcceptsForStorage(boxThing, thing))
                {
                    continue;
                }

                Map originalMap = thing.Map;
                IntVec3 originalPosition = thing.Position;
                thing.DeSpawn();
                UnregisterEjectedThing(thing);
                if (TryAddThing(normalizedChannel, thing))
                {
                    absorbedAny = true;
                    continue;
                }

                if (!thing.Destroyed && !thing.Spawned && originalMap != null)
                {
                    GenSpawn.Spawn(thing, originalPosition, originalMap);
                }
            }

            return absorbedAny;
        }

        public void DropChannelContentsAt(int channelId, IntVec3 cell, Map map, int graceTicks)
        {
            if (map == null || !cell.InBounds(map))
            {
                return;
            }

            int normalizedChannel = Math.Max(1, channelId);
            spawnedChannelThings.Clear();
            AddSpawnedChannelThingsTo(normalizedChannel, spawnedChannelThings);
            for (int index = 0; index < spawnedChannelThings.Count; index++)
            {
                TryHideSpawnedThingInChannel(normalizedChannel, spawnedChannelThings[index]);
            }

            LWOPDimensionalStorageChannel channel = ChannelFor(normalizedChannel, false);
            if (channel == null)
            {
                return;
            }

            hiddenChannelThings.Clear();
            channel.AddThingsTo(hiddenChannelThings);
            for (int index = 0; index < hiddenChannelThings.Count; index++)
            {
                Thing ejectedThing;
                TryEjectThing(normalizedChannel, hiddenChannelThings[index], cell, map, graceTicks, out ejectedThing);
            }
        }

        public bool TryEjectThing(int channelId, Thing thing, IntVec3 cell, Map map, int graceTicks)
        {
            Thing ejectedThing;
            return TryEjectThing(channelId, thing, cell, map, graceTicks, out ejectedThing);
        }

        public bool TryEjectOrMoveThingToBox(int channelId, Thing thing, IntVec3 cell, Map map, int graceTicks)
        {
            Thing movedThing;
            if (TryEjectThing(channelId, thing, cell, map, graceTicks, out movedThing))
            {
                return true;
            }

            if (thing == null || !thing.Spawned)
            {
                return false;
            }

            spawnedChannelThings.Clear();
            AddSpawnedChannelThingsTo(channelId, spawnedChannelThings);
            if (!spawnedChannelThings.Contains(thing))
            {
                return false;
            }

            return MoveSpawnedThingToBox(thing, cell, map, graceTicks, out movedThing);
        }

        public bool TryEjectThing(int channelId, Thing thing, IntVec3 cell, Map map, int graceTicks, out Thing ejectedThing)
        {
            ejectedThing = null;
            if (thing == null || map == null)
            {
                return false;
            }

            LWOPDimensionalStorageChannel channel = ChannelFor(channelId, false);
            if (channel == null || !channel.Contains(thing))
            {
                return false;
            }

            channel.Remove(thing);
            RegisterEjectedThing(thing, graceTicks);
            Thing placedThing;
            if (!GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Direct, out placedThing) &&
                !GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near, out placedThing))
            {
                UnregisterEjectedThing(thing);
                channel.TryAdd(thing);
                return false;
            }

            ejectedThing = placedThing ?? thing;
            RegisterEjectedThing(ejectedThing, graceTicks);
            return true;
        }

        public bool TryEjectMatchingThing(
            IntVec3 root,
            Map map,
            ThingRequest request,
            Predicate<Thing> validator,
            TraverseParms traverseParms,
            bool hasTraverseParms,
            float maxDistance,
            Thing existingResult,
            out Thing ejectedThing)
        {
            ejectedThing = null;
            ThingDef boxDef = DefDatabase<ThingDef>.GetNamedSilentFail("LWOPDimensionalStorageBox");
            if (boxDef == null || map == null)
            {
                return false;
            }

            if (!CanUseDimensionalNetwork(traverseParms, hasTraverseParms))
            {
                return false;
            }

            List<Thing> listedBoxes = map.listerThings.ThingsOfDef(boxDef);
            if (listedBoxes == null || listedBoxes.Count == 0)
            {
                return false;
            }

            List<Thing> boxes = new List<Thing>(listedBoxes);
            boxes.Sort(delegate(Thing firstBox, Thing secondBox)
            {
                return root.DistanceToSquared(firstBox.Position).CompareTo(root.DistanceToSquared(secondBox.Position));
            });

            for (int boxIndex = 0; boxIndex < boxes.Count; boxIndex++)
            {
                Thing boxThing = boxes[boxIndex];
                if (existingResult != null &&
                    existingResult.Spawned &&
                    root.DistanceToSquared(boxThing.Position) >= root.DistanceToSquared(existingResult.Position))
                {
                    break;
                }

                CompLWOPDimensionalStorageBox box = boxThing.TryGetComp<CompLWOPDimensionalStorageBox>();
                if (box == null ||
                    !EnsureHost(box.Channel) ||
                    !CanSupplyFromBox(root, map, boxThing, traverseParms, hasTraverseParms, maxDistance))
                {
                    continue;
                }

                LWOPDimensionalStorageChannel channel = ChannelFor(box.Channel, false);
                matchingThings.Clear();
                if (channel != null)
                {
                    channel.AddMatchingThingsTo(request, matchingThings);
                }

                AddMatchingSpawnedThingsTo(box.Channel, request, matchingThings);
                for (int thingIndex = 0; thingIndex < matchingThings.Count; thingIndex++)
                {
                    Thing thing = matchingThings[thingIndex];
                    if (channel != null && channel.Contains(thing))
                    {
                        if (!TryEjectThing(box.Channel, thing, boxThing.Position, map, box.Props.ejectGraceTicks, out ejectedThing))
                        {
                            continue;
                        }

                        if (validator == null || SafeValidatorPasses(validator, ejectedThing))
                        {
                            return true;
                        }

                        ReturnEjectedThing(box.Channel, ejectedThing);
                        ejectedThing = null;
                        continue;
                    }

                    if (!MoveSpawnedThingToBox(thing, boxThing.Position, map, box.Props.ejectGraceTicks, out ejectedThing))
                    {
                        continue;
                    }

                    if (validator == null || SafeValidatorPasses(validator, ejectedThing))
                    {
                        return true;
                    }

                    ReturnEjectedThing(box.Channel, ejectedThing);
                    ejectedThing = null;
                }
            }

            return false;
        }

        public void GetThings(int channelId, List<Thing> outThings)
        {
            outThings.Clear();
            LWOPDimensionalStorageChannel channel = ChannelFor(channelId, false);
            if (channel != null)
            {
                channel.AddThingsTo(outThings);
            }

            AddSpawnedChannelThingsTo(channelId, outThings);
        }

        public void GetCounts(int channelId, out int stacks, out int totalCount)
        {
            stacks = 0;
            totalCount = 0;
            LWOPDimensionalStorageChannel channel = ChannelFor(channelId, false);
            if (channel != null)
            {
                channel.GetCounts(out stacks, out totalCount);
            }

            spawnedChannelThings.Clear();
            AddSpawnedChannelThingsTo(channelId, spawnedChannelThings);
            for (int index = 0; index < spawnedChannelThings.Count; index++)
            {
                Thing thing = spawnedChannelThings[index];
                if (thing == null)
                {
                    continue;
                }

                stacks++;
                totalCount += Math.Max(0, thing.stackCount);
            }
        }

        public bool IsEjectedThingProtected(Thing thing)
        {
            if (thing == null || ejectedUntilTick == null)
            {
                return false;
            }

            int untilTick;
            if (!ejectedUntilTick.TryGetValue(thing.thingIDNumber, out untilTick))
            {
                return false;
            }

            if (Find.TickManager.TicksGame <= untilTick)
            {
                return true;
            }

            ejectedUntilTick.Remove(thing.thingIDNumber);
            return false;
        }

        public IThingHolder ParentHolder
        {
            get { return null; }
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            if (emptyHolder == null)
            {
                emptyHolder = new ThingOwner<Thing>(this, false, LookMode.Deep);
            }

            return emptyHolder;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            for (int i = 0; i < channels.Count; i++)
            {
                outChildren.Add(channels[i]);
            }
        }

        public void MaterializeChannelToBox(int channelId, IntVec3 cell, Map map, int graceTicks, bool includeSpawnedThings)
        {
            if (map == null || materializingChannel)
            {
                return;
            }

            try
            {
                materializingChannel = true;
                LWOPDimensionalStorageChannel channel = ChannelFor(channelId, false);
                if (channel != null)
                {
                    hiddenChannelThings.Clear();
                    channel.AddThingsTo(hiddenChannelThings);
                    for (int index = 0; index < hiddenChannelThings.Count; index++)
                    {
                        Thing ejectedThing;
                        TryEjectThing(channelId, hiddenChannelThings[index], cell, map, graceTicks, out ejectedThing);
                    }
                }

                if (!includeSpawnedThings)
                {
                    return;
                }

                spawnedChannelThings.Clear();
                AddSpawnedChannelThingsTo(channelId, spawnedChannelThings);
                for (int index = 0; index < spawnedChannelThings.Count; index++)
                {
                    Thing thing = spawnedChannelThings[index];
                    if (thing == null ||
                        !thing.Spawned ||
                        IsEjectedThingProtected(thing) ||
                        thing.Map == map && thing.Position == cell)
                    {
                        continue;
                    }

                    Thing movedThing;
                    MoveSpawnedThingToBox(thing, cell, map, graceTicks, out movedThing);
                }
            }
            finally
            {
                materializingChannel = false;
            }
        }

        public void DetachBoxContentsFromChannel(int channelId, Thing boxThing, int graceTicks)
        {
            if (boxThing == null || !boxThing.Spawned || boxThing.Map == null)
            {
                return;
            }

            DetachBoxContentsFromChannel(channelId, boxThing, boxThing.Position, boxThing.Map, graceTicks, true);
        }

        public void DetachBoxContentsForRemovingBox(int channelId, Thing boxThing, IntVec3 boxCell, Map map, int graceTicks)
        {
            if (boxThing == null || map == null || !boxCell.InBounds(map))
            {
                return;
            }

            int normalizedChannel = Math.Max(1, channelId);
            bool hasAlternativeBox = FindAlternativeChannelBox(normalizedChannel, boxThing, null) != null;
            if (!hasAlternativeBox)
            {
                return;
            }

            DetachBoxContentsFromChannel(normalizedChannel, boxThing, boxCell, map, graceTicks, true);
        }

        private void DetachBoxContentsFromChannel(int channelId, Thing boxThing, IntVec3 boxCell, Map map, int graceTicks, bool hideIfNoAlternative)
        {
            if (boxThing == null || map == null || !boxCell.InBounds(map))
            {
                return;
            }

            int normalizedChannel = Math.Max(1, channelId);
            List<Thing> cellThings = boxCell.GetThingList(map);
            List<Thing> thingsToDetach = new List<Thing>();
            for (int index = 0; index < cellThings.Count; index++)
            {
                Thing thing = cellThings[index];
                if (IsDimensionalStoredThingOnBox(thing, boxThing))
                {
                    thingsToDetach.Add(thing);
                }
            }

            for (int index = 0; index < thingsToDetach.Count; index++)
            {
                Thing thing = thingsToDetach[index];
                if (thing == null || thing.Destroyed || !thing.Spawned)
                {
                    continue;
                }

                Thing targetBox = FindAlternativeChannelBox(normalizedChannel, boxThing, thing);
                Thing movedThing;
                if (targetBox != null &&
                    TryMoveSpawnedThingDirectToBox(thing, targetBox.Position, targetBox.Map, graceTicks, out movedThing))
                {
                    continue;
                }

                if (hideIfNoAlternative)
                {
                    TryHideSpawnedThingInChannel(normalizedChannel, thing);
                }
            }
        }

        private void RegisterEjectedThing(Thing thing, int graceTicks)
        {
            if (thing == null)
            {
                return;
            }

            if (ejectedUntilTick == null)
            {
                ejectedUntilTick = new Dictionary<int, int>();
            }

            ejectedUntilTick[thing.thingIDNumber] = Find.TickManager.TicksGame + Math.Max(1, graceTicks);
            thing.SetForbidden(false, false);
        }

        private void UnregisterEjectedThing(Thing thing)
        {
            if (thing != null && ejectedUntilTick != null)
            {
                ejectedUntilTick.Remove(thing.thingIDNumber);
            }
        }

        private void ReturnEjectedThing(int channelId, Thing thing)
        {
            if (thing == null)
            {
                return;
            }

            if (ejectedUntilTick != null)
            {
                ejectedUntilTick.Remove(thing.thingIDNumber);
            }

            if (thing.Spawned)
            {
                thing.DeSpawn();
            }

            LWOPDimensionalStorageChannel channel = ChannelFor(channelId, true);
            if (channel != null)
            {
                channel.TryAdd(thing);
            }
        }

        private void AddMatchingSpawnedThingsTo(int channelId, ThingRequest request, List<Thing> outThings)
        {
            spawnedChannelThings.Clear();
            AddSpawnedChannelThingsTo(channelId, spawnedChannelThings);
            for (int index = 0; index < spawnedChannelThings.Count; index++)
            {
                Thing thing = spawnedChannelThings[index];
                if (thing == null ||
                    thing.def == null ||
                    !IsDimensionalStoredThing(thing) ||
                    !request.Accepts(thing) ||
                    outThings.Contains(thing))
                {
                    continue;
                }

                outThings.Add(thing);
            }
        }

        private void AddDimensionalBoxesOnMap(Map map, List<Thing> outBoxes)
        {
            if (map == null || outBoxes == null)
            {
                return;
            }

            ThingDef boxDef = DefDatabase<ThingDef>.GetNamedSilentFail("LWOPDimensionalStorageBox");
            if (boxDef == null)
            {
                return;
            }

            List<Thing> boxes = map.listerThings.ThingsOfDef(boxDef);
            for (int index = 0; index < boxes.Count; index++)
            {
                Thing boxThing = boxes[index];
                if (boxThing == null || !boxThing.Spawned || boxThing.Destroyed || outBoxes.Contains(boxThing))
                {
                    continue;
                }

                if (boxThing.TryGetComp<CompLWOPDimensionalStorageBox>() != null)
                {
                    outBoxes.Add(boxThing);
                }
            }
        }

        private void AddChannelBoxesTo(int channelId, List<Thing> outBoxes)
        {
            if (outBoxes == null)
            {
                return;
            }

            int normalizedChannel = Math.Max(1, channelId);
            ThingDef boxDef = DefDatabase<ThingDef>.GetNamedSilentFail("LWOPDimensionalStorageBox");
            if (boxDef == null || Current.Game == null)
            {
                return;
            }

            List<Map> maps = Find.Maps;
            for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                Map map = maps[mapIndex];
                if (map == null)
                {
                    continue;
                }

                List<Thing> boxes = map.listerThings.ThingsOfDef(boxDef);
                for (int boxIndex = 0; boxIndex < boxes.Count; boxIndex++)
                {
                    Thing boxThing = boxes[boxIndex];
                    if (boxThing == null || !boxThing.Spawned || boxThing.Destroyed || outBoxes.Contains(boxThing))
                    {
                        continue;
                    }

                    CompLWOPDimensionalStorageBox box = boxThing.TryGetComp<CompLWOPDimensionalStorageBox>();
                    if (box != null && box.Channel == normalizedChannel)
                    {
                        outBoxes.Add(boxThing);
                    }
                }
            }
        }

        private void AddSpawnedChannelThingsTo(int channelId, List<Thing> outThings)
        {
            int normalizedChannel = Math.Max(1, channelId);
            ThingDef boxDef = DefDatabase<ThingDef>.GetNamedSilentFail("LWOPDimensionalStorageBox");
            if (boxDef == null || Current.Game == null)
            {
                return;
            }

            List<Map> maps = Find.Maps;
            for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                Map map = maps[mapIndex];
                if (map == null)
                {
                    continue;
                }

                List<Thing> boxes = map.listerThings.ThingsOfDef(boxDef);
                for (int boxIndex = 0; boxIndex < boxes.Count; boxIndex++)
                {
                    Thing boxThing = boxes[boxIndex];
                    CompLWOPDimensionalStorageBox box = boxThing.TryGetComp<CompLWOPDimensionalStorageBox>();
                    if (box == null || box.Channel != normalizedChannel)
                    {
                        continue;
                    }

                    List<Thing> cellThings = boxThing.Position.GetThingList(map);
                    for (int thingIndex = 0; thingIndex < cellThings.Count; thingIndex++)
                    {
                        Thing thing = cellThings[thingIndex];
                        if (thing == null ||
                            thing == boxThing ||
                            thing.def == null ||
                            !IsDimensionalStoredThing(thing) ||
                            outThings.Contains(thing))
                        {
                            continue;
                        }

                        outThings.Add(thing);
                    }
                }
            }
        }

        private bool MoveSpawnedThingToBox(Thing thing, IntVec3 cell, Map map, int graceTicks, out Thing movedThing)
        {
            movedThing = null;
            if (thing == null || map == null)
            {
                return false;
            }

            if (!thing.Spawned)
            {
                return TryEjectHiddenOrUnspawnedThing(thing, cell, map, graceTicks, out movedThing);
            }

            if (thing.Map == map && thing.Position == cell)
            {
                movedThing = thing;
                RegisterEjectedThing(movedThing, graceTicks);
                return true;
            }

            Map originalMap = thing.Map;
            IntVec3 originalPosition = thing.Position;
            thing.DeSpawn();
            RegisterEjectedThing(thing, graceTicks);

            Thing placedThing;
            if (GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Direct, out placedThing) ||
                GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near, out placedThing))
            {
                movedThing = placedThing ?? thing;
                RegisterEjectedThing(movedThing, graceTicks);
                return true;
            }

            UnregisterEjectedThing(thing);
            GenSpawn.Spawn(thing, originalPosition, originalMap);
            return false;
        }

        private bool TryMoveSpawnedThingDirectToBox(Thing thing, IntVec3 cell, Map map, int graceTicks, out Thing movedThing)
        {
            movedThing = null;
            if (thing == null || map == null)
            {
                return false;
            }

            if (!thing.Spawned)
            {
                return false;
            }

            if (thing.Map == map && thing.Position == cell)
            {
                movedThing = thing;
                RegisterEjectedThing(movedThing, graceTicks);
                return true;
            }

            Map originalMap = thing.Map;
            IntVec3 originalPosition = thing.Position;
            thing.DeSpawn();
            RegisterEjectedThing(thing, graceTicks);

            Thing placedThing;
            if (GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Direct, out placedThing))
            {
                movedThing = placedThing ?? thing;
                RegisterEjectedThing(movedThing, graceTicks);
                return true;
            }

            UnregisterEjectedThing(thing);
            GenSpawn.Spawn(thing, originalPosition, originalMap);
            return false;
        }

        private bool TryHideSpawnedThingInChannel(int channelId, Thing thing)
        {
            if (thing == null || thing.Destroyed)
            {
                return false;
            }

            Map originalMap = thing.Map;
            IntVec3 originalPosition = thing.Position;
            bool wasSpawned = thing.Spawned;
            if (wasSpawned)
            {
                thing.DeSpawn();
            }

            UnregisterEjectedThing(thing);
            if (TryAddThing(channelId, thing))
            {
                return true;
            }

            if (wasSpawned && !thing.Spawned && !thing.Destroyed && originalMap != null)
            {
                GenSpawn.Spawn(thing, originalPosition, originalMap);
            }

            return false;
        }

        private Thing FindAlternativeChannelBox(int channelId, Thing excludedBox, Thing storedThing)
        {
            int normalizedChannel = Math.Max(1, channelId);
            ThingDef boxDef = DefDatabase<ThingDef>.GetNamedSilentFail("LWOPDimensionalStorageBox");
            if (boxDef == null || Current.Game == null)
            {
                return null;
            }

            Thing bestBox = null;
            float bestDistance = float.MaxValue;
            List<Map> maps = Find.Maps;
            for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                Map map = maps[mapIndex];
                if (map == null)
                {
                    continue;
                }

                List<Thing> boxes = map.listerThings.ThingsOfDef(boxDef);
                for (int boxIndex = 0; boxIndex < boxes.Count; boxIndex++)
                {
                    Thing boxThing = boxes[boxIndex];
                    if (boxThing == null || boxThing == excludedBox || !boxThing.Spawned || boxThing.Destroyed)
                    {
                        continue;
                    }

                    CompLWOPDimensionalStorageBox box = boxThing.TryGetComp<CompLWOPDimensionalStorageBox>();
                    if (box == null || box.Channel != normalizedChannel)
                    {
                        continue;
                    }

                    Building_Storage storage = boxThing as Building_Storage;
                    if (storage != null && storedThing != null && !storage.Accepts(storedThing))
                    {
                        continue;
                    }

                    float distance = 1000000f;
                    if (excludedBox != null && excludedBox.Map == boxThing.Map)
                    {
                        distance = excludedBox.Position.DistanceToSquared(boxThing.Position);
                    }

                    if (bestBox == null || distance < bestDistance)
                    {
                        bestBox = boxThing;
                        bestDistance = distance;
                    }
                }
            }

            return bestBox;
        }

        private Thing FindHostBox(int channelId, Thing excludedBox)
        {
            List<Thing> boxes = new List<Thing>();
            AddChannelBoxesTo(channelId, boxes);
            for (int index = 0; index < boxes.Count; index++)
            {
                Thing boxThing = boxes[index];
                if (boxThing == null || boxThing == excludedBox)
                {
                    continue;
                }

                CompLWOPDimensionalStorageBox box = boxThing.TryGetComp<CompLWOPDimensionalStorageBox>();
                if (box != null && box.IsHost)
                {
                    return boxThing;
                }
            }

            return null;
        }

        private static bool WithinIngredientSearchRadius(IntVec3 rootCell, IntVec3 boxCell, float searchRadius)
        {
            if (searchRadius <= 0f || searchRadius >= 999f)
            {
                return true;
            }

            return rootCell.DistanceToSquared(boxCell) <= searchRadius * searchRadius;
        }

        private static bool MatchesWorkIngredient(Predicate<Thing> matcher, Thing thing)
        {
            if (thing == null || thing.Destroyed || matcher == null)
            {
                return false;
            }

            try
            {
                return matcher(thing);
            }
            catch
            {
                return false;
            }
        }

        private static bool BillAllowsThing(Bill bill, Thing thing)
        {
            if (bill == null || thing == null || thing.def == null)
            {
                return false;
            }

            try
            {
                return bill.IsFixedOrAllowedIngredient(thing);
            }
            catch
            {
                return false;
            }
        }

        private static bool SafeBillShouldDoNow(Bill bill)
        {
            if (bill == null)
            {
                return false;
            }

            try
            {
                return bill.ShouldDoNow();
            }
            catch
            {
                return false;
            }
        }

        private static bool IngredientCountsAllow(List<IngredientCount> ingredients, Thing thing)
        {
            if (ingredients == null || thing == null || thing.def == null)
            {
                return false;
            }

            for (int index = 0; index < ingredients.Count; index++)
            {
                IngredientCount ingredient = ingredients[index];
                if (ingredient != null && ingredient.filter != null && ingredient.filter.Allows(thing))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BoxAcceptsForStorage(Thing boxThing, Thing thing)
        {
            Building_Storage storage = boxThing as Building_Storage;
            return storage == null || thing == null || storage.Accepts(thing);
        }

        private static void CopyStorageSettings(Thing sourceBox, Thing targetBox)
        {
            Building_Storage sourceStorage = sourceBox as Building_Storage;
            Building_Storage targetStorage = targetBox as Building_Storage;
            if (sourceStorage == null || targetStorage == null)
            {
                return;
            }

            StorageSettings sourceSettings = sourceStorage.GetStoreSettings();
            StorageSettings targetSettings = targetStorage.GetStoreSettings();
            if (sourceSettings == null || targetSettings == null || ReferenceEquals(sourceSettings, targetSettings))
            {
                return;
            }

            targetSettings.CopyFrom(sourceSettings);
        }

        private static bool IsDimensionalStoredThingOnBox(Thing thing, Thing boxThing)
        {
            return thing != null &&
                thing != boxThing &&
                IsDimensionalStoredThing(thing);
        }

        private static bool IsDimensionalStoredThing(Thing thing)
        {
            if (thing == null || thing.Destroyed || thing.def == null || LWOPStorageRackUtility.IsLWOPStorageRack(thing))
            {
                return false;
            }

            if (thing is Pawn)
            {
                return false;
            }

            return thing.def.category == ThingCategory.Item ||
                thing is Corpse ||
                thing is MinifiedThing ||
                thing.def.EverStorable(false);
        }

        private bool TryEjectHiddenOrUnspawnedThing(Thing thing, IntVec3 cell, Map map, int graceTicks, out Thing movedThing)
        {
            movedThing = null;
            for (int channelIndex = 0; channelIndex < channels.Count; channelIndex++)
            {
                LWOPDimensionalStorageChannel channel = channels[channelIndex];
                if (channel == null || !channel.Contains(thing))
                {
                    continue;
                }

                return TryEjectThing(channel.ChannelId, thing, cell, map, graceTicks, out movedThing);
            }

            return false;
        }

        private static bool CanUseDimensionalNetwork(TraverseParms traverseParms, bool hasTraverseParms)
        {
            if (!hasTraverseParms || traverseParms.pawn == null)
            {
                return true;
            }

            Faction pawnFaction = traverseParms.pawn.Faction;
            return pawnFaction == null ||
                Faction.OfPlayer == null ||
                !pawnFaction.HostileTo(Faction.OfPlayer);
        }

        private static bool CanSupplyFromBox(
            IntVec3 root,
            Map map,
            Thing boxThing,
            TraverseParms traverseParms,
            bool hasTraverseParms,
            float maxDistance)
        {
            if (boxThing == null ||
                !boxThing.Spawned ||
                boxThing.Map != map ||
                boxThing.IsForbidden(Faction.OfPlayer))
            {
                return false;
            }

            if (maxDistance > 0f && maxDistance < 9999f && root.DistanceToSquared(boxThing.Position) > maxDistance * maxDistance)
            {
                return false;
            }

            if (!hasTraverseParms)
            {
                return true;
            }

            return map.reachability.CanReach(root, new LocalTargetInfo(boxThing), PathEndMode.Touch, traverseParms);
        }

        private static bool SafeValidatorPasses(Predicate<Thing> validator, Thing thing)
        {
            try
            {
                return validator == null || validator(thing);
            }
            catch
            {
                return false;
            }
        }

        private LWOPDimensionalStorageChannel ChannelFor(int channelId, bool createIfMissing)
        {
            int normalizedChannel = Math.Max(1, channelId);
            for (int i = 0; i < channels.Count; i++)
            {
                if (channels[i].ChannelId == normalizedChannel)
                {
                    return channels[i];
                }
            }

            if (!createIfMissing)
            {
                return null;
            }

            LWOPDimensionalStorageChannel channel = new LWOPDimensionalStorageChannel(normalizedChannel);
            channels.Add(channel);
            return channel;
        }
    }

    public class LWOPDimensionalStorageChannel : IExposable, IThingHolder
    {
        private int channelId = 1;
        private ThingOwner<Thing> innerContainer;

        public LWOPDimensionalStorageChannel()
        {
            EnsureContainer();
        }

        public LWOPDimensionalStorageChannel(int channelId)
        {
            this.channelId = Math.Max(1, channelId);
            EnsureContainer();
        }

        public int ChannelId
        {
            get { return Math.Max(1, channelId); }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref channelId, "channelId", 1);
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                channelId = Math.Max(1, channelId);
                EnsureContainer();
            }
        }

        public bool TryAdd(Thing thing)
        {
            EnsureContainer();
            if (thing == null)
            {
                return false;
            }

            if (innerContainer.Contains(thing))
            {
                return true;
            }

            return innerContainer.TryAdd(thing, true);
        }

        public bool Contains(Thing thing)
        {
            EnsureContainer();
            return thing != null && innerContainer.Contains(thing);
        }

        public void Remove(Thing thing)
        {
            EnsureContainer();
            if (thing != null)
            {
                innerContainer.Remove(thing);
            }
        }

        public void AddThingsTo(List<Thing> outThings)
        {
            EnsureContainer();
            foreach (Thing thing in innerContainer)
            {
                if (!IsDimensionalStoredThing(thing))
                {
                    continue;
                }

                outThings.Add(thing);
            }
        }

        public void AddMatchingThingsTo(ThingRequest request, List<Thing> outThings)
        {
            EnsureContainer();
            foreach (Thing thing in innerContainer)
            {
                if (thing == null ||
                    thing.def == null ||
                    !IsDimensionalStoredThing(thing) ||
                    !request.Accepts(thing))
                {
                    continue;
                }

                outThings.Add(thing);
            }
        }

        private static bool IsDimensionalStoredThing(Thing thing)
        {
            if (thing == null || thing.Destroyed || thing.def == null || LWOPStorageRackUtility.IsLWOPStorageRack(thing))
            {
                return false;
            }

            if (thing is Pawn)
            {
                return false;
            }

            return thing.def.category == ThingCategory.Item ||
                thing is Corpse ||
                thing is MinifiedThing ||
                thing.def.EverStorable(false);
        }

        public void GetCounts(out int stacks, out int totalCount)
        {
            stacks = 0;
            totalCount = 0;
            EnsureContainer();
            foreach (Thing thing in innerContainer)
            {
                if (!IsDimensionalStoredThing(thing))
                {
                    continue;
                }

                stacks++;
                totalCount += Math.Max(0, thing.stackCount);
            }
        }

        public IThingHolder ParentHolder
        {
            get { return null; }
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            EnsureContainer();
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            EnsureContainer();
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, innerContainer);
        }

        private void EnsureContainer()
        {
            if (innerContainer == null)
            {
                innerContainer = new ThingOwner<Thing>(this, false, LookMode.Deep);
            }
        }
    }

    public class Dialog_LWOPDimensionalStorageSetup : Window
    {
        private readonly CompLWOPDimensionalStorageBox box;
        private string channelBuffer;
        private bool makeHost;
        private readonly bool initialSetup;

        public Dialog_LWOPDimensionalStorageSetup(CompLWOPDimensionalStorageBox box, bool initialSetup)
        {
            this.box = box;
            this.initialSetup = initialSetup;
            channelBuffer = box.Channel.ToString();
            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            makeHost = box.IsHost || component != null && !component.HasHost(box.Channel);
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            forcePause = false;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(500f, 260f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "LWOPDimensionalStorageSetupTitle".Translate());
            Text.Font = GameFont.Small;

            Widgets.Label(new Rect(0f, 40f, inRect.width, 54f), "LWOPDimensionalStorageSetupDesc".Translate());
            Widgets.Label(new Rect(0f, 102f, inRect.width, 24f), "LWOPDimensionalStorageChannelCurrent".Translate(box.Channel.ToString()));
            Widgets.Label(new Rect(0f, 130f, 120f, 30f), "LWOPDimensionalStorageChannelLabel".Translate());
            channelBuffer = Widgets.TextField(new Rect(126f, 128f, inRect.width - 126f, 32f), channelBuffer);
            Widgets.CheckboxLabeled(new Rect(0f, 170f, inRect.width, 30f), "LWOPDimensionalStorageHostCheckbox".Translate(), ref makeHost);

            Rect confirmRect = new Rect(inRect.width - 250f, inRect.height - 38f, 120f, 32f);
            if (Widgets.ButtonText(confirmRect, "ConfirmButton".Translate()))
            {
                int newChannel;
                if (int.TryParse(channelBuffer, out newChannel) && newChannel >= 1)
                {
                    box.ApplyChannelAndRole(newChannel, makeHost, true);
                    Close();
                }
                else
                {
                    Messages.Message("LWOPDimensionalStorageInvalidChannel".Translate(), MessageTypeDefOf.RejectInput, false);
                }
            }

            Rect closeRect = new Rect(inRect.width - 120f, inRect.height - 38f, 120f, 32f);
            if (Widgets.ButtonText(closeRect, "CloseButton".Translate()))
            {
                Close();
            }
        }
    }

    public class Dialog_LWOPDimensionalStorage : Window
    {
        private readonly CompLWOPDimensionalStorageBox box;
        private readonly List<Thing> things = new List<Thing>();
        private readonly List<Thing> filteredThings = new List<Thing>();
        private string searchBuffer = "";
        private Vector2 scrollPosition;

        public Dialog_LWOPDimensionalStorage(CompLWOPDimensionalStorageBox box)
        {
            this.box = box;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            forcePause = false;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(620f, 520f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "LWOPDimensionalStorageTitle".Translate(box.Channel.ToString()));
            Text.Font = GameFont.Small;

            LWOPDimensionalStorageGameComponent component = LWOPDimensionalStorageGameComponent.CurrentComponent;
            if (component == null)
            {
                Widgets.Label(new Rect(0f, 82f, inRect.width, 32f), "LWOPDimensionalStorageEmpty".Translate());
                return;
            }

            Widgets.Label(new Rect(0f, 43f, 78f, 28f), "LWOPDimensionalStorageSearch".Translate());
            searchBuffer = Widgets.TextField(new Rect(82f, 40f, inRect.width - 82f, 30f), searchBuffer);

            component.GetThings(box.Channel, things);
            if (things.Count == 0)
            {
                Widgets.Label(new Rect(0f, 82f, inRect.width, 32f), "LWOPDimensionalStorageEmpty".Translate());
                return;
            }

            BuildFilteredThings();
            if (filteredThings.Count == 0)
            {
                Widgets.Label(new Rect(0f, 82f, inRect.width, 32f), "LWOPDimensionalStorageNoSearchResults".Translate());
                return;
            }

            Rect scrollRect = new Rect(0f, 80f, inRect.width, inRect.height - 124f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, Math.Max(scrollRect.height, filteredThings.Count * 36f));
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            for (int i = 0; i < filteredThings.Count; i++)
            {
                Thing thing = filteredThings[i];
                if (thing == null)
                {
                    continue;
                }

                Rect rowRect = new Rect(0f, i * 36f, viewRect.width, 32f);
                Rect iconRect = new Rect(rowRect.x, rowRect.y + 2f, 28f, 28f);
                try
                {
                    Widgets.ThingIcon(iconRect, thing, 1f, null, true, 1f, false);
                }
                catch
                {
                    Widgets.Label(iconRect, "?");
                }

                Rect buttonRect = new Rect(rowRect.xMax - 118f, rowRect.y, 118f, 30f);
                Rect countRect = new Rect(buttonRect.x - 86f, rowRect.y + 4f, 76f, 28f);
                Rect labelRect = new Rect(iconRect.xMax + 8f, rowRect.y + 4f, countRect.x - iconRect.xMax - 16f, 28f);
                Widgets.Label(labelRect, SafeThingLabel(thing));

                TextAnchor oldAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(countRect, "x" + thing.stackCount);
                Text.Anchor = oldAnchor;

                if (Widgets.ButtonText(buttonRect, "LWOPDimensionalStorageRetrieve".Translate()))
                {
                    if (box.TryEjectThing(thing))
                    {
                        Messages.Message("LWOPDimensionalStorageRetrieved".Translate(thing.LabelCap), box.parent, MessageTypeDefOf.PositiveEvent, false);
                    }
                    else
                    {
                        Messages.Message("LWOPDimensionalStorageRetrieveFailed".Translate(), box.parent, MessageTypeDefOf.RejectInput, false);
                    }

                    break;
                }
            }

            Widgets.EndScrollView();

            Rect closeRect = new Rect(inRect.width - 120f, inRect.height - 34f, 120f, 32f);
            if (Widgets.ButtonText(closeRect, "CloseButton".Translate()))
            {
                Close();
            }
        }

        private void BuildFilteredThings()
        {
            filteredThings.Clear();
            string query = searchBuffer == null ? "" : searchBuffer.Trim();
            for (int index = 0; index < things.Count; index++)
            {
                Thing thing = things[index];
                if (thing == null)
                {
                    continue;
                }

                if (query.Length == 0 || ThingMatchesSearch(thing, query))
                {
                    filteredThings.Add(thing);
                }
            }
        }

        private static bool ThingMatchesSearch(Thing thing, string query)
        {
            if (thing == null || string.IsNullOrEmpty(query))
            {
                return false;
            }

            string label = SafeThingLabel(thing);
            if (!string.IsNullOrEmpty(label) &&
                label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return thing.def != null &&
                !string.IsNullOrEmpty(thing.def.label) &&
                thing.def.label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string SafeThingLabel(Thing thing)
        {
            try
            {
                if (thing != null)
                {
                    return thing.LabelCapNoCount;
                }
            }
            catch
            {
            }

            if (thing != null && thing.def != null)
            {
                return thing.def.label.CapitalizeFirst();
            }

            return "Unknown";
        }
    }

    public class LWOPCorpseCollectorMapComponent : MapComponent
    {
        private readonly List<PendingCollection> pendingCollections = new List<PendingCollection>();

        public LWOPCorpseCollectorMapComponent(Map map) : base(map)
        {
        }

        public void Register(Pawn pawn, IntVec3 deathCell, int delayTicks, int maxPendingTicks)
        {
            if (pawn == null || map == null)
            {
                return;
            }

            int ticksGame = Find.TickManager.TicksGame;
            pendingCollections.Add(new PendingCollection
            {
                pawn = pawn,
                deathCell = deathCell,
                dueTick = ticksGame + Math.Max(1, delayTicks),
                expireTick = ticksGame + Math.Max(delayTicks + 1, maxPendingTicks)
            });
        }

        public override void MapComponentTick()
        {
            if (pendingCollections.Count == 0)
            {
                return;
            }

            int ticksGame = Find.TickManager.TicksGame;
            for (int i = pendingCollections.Count - 1; i >= 0; i--)
            {
                PendingCollection pending = pendingCollections[i];
                if (ticksGame < pending.dueTick)
                {
                    continue;
                }

                if (ticksGame > pending.expireTick || TryCollect(pending))
                {
                    pendingCollections.RemoveAt(i);
                }
                else
                {
                    pendingCollections[i] = pending.Postpone(ticksGame + 250);
                }
            }
        }

        private bool TryCollect(PendingCollection pending)
        {
            CompLWOPCorpseCollector collector = ClosestActiveCollector(pending.deathCell);
            if (collector == null)
            {
                return false;
            }

            bool collectedAny = false;
            Corpse corpse = pending.pawn == null ? null : pending.pawn.Corpse;
            if (corpse != null && corpse.Spawned && corpse.Map == map)
            {
                collectedAny |= collector.StoreThing(corpse);
            }

            foreach (Thing thing in NearbyLoot(pending.deathCell, collector.Props.collectRadius))
            {
                collectedAny |= collector.StoreThing(thing);
            }

            return collectedAny;
        }

        private CompLWOPCorpseCollector ClosestActiveCollector(IntVec3 cell)
        {
            ThingDef collectorDef = DefDatabase<ThingDef>.GetNamedSilentFail("LWOPCorpseCollector");
            if (collectorDef == null)
            {
                return null;
            }

            CompLWOPCorpseCollector best = null;
            float bestDistance = float.MaxValue;
            List<Thing> collectors = map.listerThings.ThingsOfDef(collectorDef);
            for (int i = 0; i < collectors.Count; i++)
            {
                Thing thing = collectors[i];
                CompLWOPCorpseCollector comp = thing.TryGetComp<CompLWOPCorpseCollector>();
                if (comp == null || !comp.Active)
                {
                    continue;
                }

                float distance = thing.Position.DistanceToSquared(cell);
                if (distance < bestDistance)
                {
                    best = comp;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private IEnumerable<Thing> NearbyLoot(IntVec3 center, float radius)
        {
            List<Thing> things = new List<Thing>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> cellThings = cell.GetThingList(map);
                for (int i = 0; i < cellThings.Count; i++)
                {
                    Thing thing = cellThings[i];
                    if (ShouldCollectLoot(thing))
                    {
                        things.Add(thing);
                    }
                }
            }

            return things;
        }

        private static bool ShouldCollectLoot(Thing thing)
        {
            return thing != null &&
                thing.Spawned &&
                !thing.Destroyed &&
                thing.def != null &&
                thing.def.category == ThingCategory.Item &&
                !(thing is Corpse) &&
                !IsInPlayerStorage(thing) &&
                thing.def.EverStorable(false);
        }

        private static bool IsInPlayerStorage(Thing thing)
        {
            if (thing == null || thing.Map == null)
            {
                return false;
            }

            SlotGroup slotGroup = thing.Position.GetSlotGroup(thing.Map);
            if (slotGroup == null || slotGroup.parent == null)
            {
                return false;
            }

            Thing storageThing = slotGroup.parent as Thing;
            return storageThing == null ||
                storageThing.Faction == null ||
                storageThing.Faction == Faction.OfPlayer ||
                storageThing.Faction == Faction.OfPlayerSilentFail;
        }

        private struct PendingCollection
        {
            public Pawn pawn;
            public IntVec3 deathCell;
            public int dueTick;
            public int expireTick;

            public PendingCollection Postpone(int newDueTick)
            {
                dueTick = newDueTick;
                return this;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "Kill")]
    public static class LWOPCorpseCollectorPawnKillPatch
    {
        public static void Prefix(Pawn __instance, ref DeathCollectionState __state)
        {
            __state = default(DeathCollectionState);
            if (__instance == null || !__instance.Spawned || __instance.Map == null || __instance.Dead)
            {
                return;
            }

            Faction playerFaction = Faction.OfPlayerSilentFail ?? Faction.OfPlayer;
            if (playerFaction == null || !__instance.HostileTo(playerFaction))
            {
                return;
            }

            __state.shouldCollect = true;
            __state.map = __instance.Map;
            __state.deathCell = __instance.Position;
        }

        public static void Postfix(Pawn __instance, DeathCollectionState __state)
        {
            if (!__state.shouldCollect || __state.map == null)
            {
                return;
            }

            CompProperties_LWOPCorpseCollector props = DefaultCollectorProps(__state.map);
            __state.map.GetComponent<LWOPCorpseCollectorMapComponent>()?.Register(
                __instance,
                __state.deathCell,
                props == null ? 30 : props.collectDelayTicks,
                props == null ? 60000 : props.maxPendingTicks);
        }

        private static CompProperties_LWOPCorpseCollector DefaultCollectorProps(Map map)
        {
            ThingDef collectorDef = DefDatabase<ThingDef>.GetNamedSilentFail("LWOPCorpseCollector");
            if (collectorDef == null || collectorDef.comps == null)
            {
                return null;
            }

            for (int i = 0; i < collectorDef.comps.Count; i++)
            {
                CompProperties_LWOPCorpseCollector props = collectorDef.comps[i] as CompProperties_LWOPCorpseCollector;
                if (props != null)
                {
                    return props;
                }
            }

            return null;
        }

        public struct DeathCollectionState
        {
            public bool shouldCollect;
            public Map map;
            public IntVec3 deathCell;
        }
    }

    [HarmonyPatch(typeof(StoreUtility), "IsGoodStoreCell")]
    public static class LWOPCorpseCollectorStoreCellPatch
    {
        public static bool Prefix(IntVec3 c, Map map, Thing t, ref bool __result)
        {
            if (map == null || t == null || t.Position == c)
            {
                return true;
            }

            Thing edifice = c.GetEdifice(map);
            if (edifice != null && edifice.def != null && edifice.def.defName == "LWOPCorpseCollector")
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch]
    public static class LWOPCorpseCollectorVEFConstructionSkillPatch
    {
        public static readonly string[] VEFConstructionSkillWorkGivers =
        {
            "VEF.Pawns.WorkGiver_ConstructionSkill_DeliverResourcesToBlueprints",
            "VEF.Pawns.WorkGiver_ConstructionSkill_DeliverResourcesToFrames",
            "VEF.Pawns.WorkGiver_ConstructionSkill_FinishFrames"
        };

        public static bool Prepare()
        {
            return HasVEFConstructionSkillWorkGiver();
        }

        public static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            System.Reflection.MethodBase method = AccessTools.Method(typeof(WorkGiver_ConstructDeliverResourcesToBlueprints), "HasJobOnThing");
            if (method != null)
            {
                yield return method;
            }

            method = AccessTools.Method(typeof(WorkGiver_ConstructDeliverResourcesToFrames), "HasJobOnThing");
            if (method != null)
            {
                yield return method;
            }

            method = AccessTools.Method(typeof(WorkGiver_ConstructFinishFrames), "HasJobOnThing");
            if (method != null)
            {
                yield return method;
            }

            method = AccessTools.Method(typeof(WorkGiver_Scanner), "HasJobOnThing");
            if (method != null)
            {
                yield return method;
            }
        }

        public static bool Prefix(WorkGiver_Scanner __instance, Thing t, ref bool __result)
        {
            if (IsVEFConstructionSkillWorkGiver(__instance) &&
                IsLWOPConstructionTarget(t))
            {
                __result = false;
                return false;
            }

            return true;
        }

        public static bool HasVEFConstructionSkillWorkGiver()
        {
            for (int i = 0; i < VEFConstructionSkillWorkGivers.Length; i++)
            {
                if (AccessTools.TypeByName(VEFConstructionSkillWorkGivers[i]) != null)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsVEFConstructionSkillWorkGiver(WorkGiver_Scanner scanner)
        {
            if (scanner == null)
            {
                return false;
            }

            string typeName = scanner.GetType().FullName;
            for (int i = 0; i < VEFConstructionSkillWorkGivers.Length; i++)
            {
                if (typeName == VEFConstructionSkillWorkGivers[i])
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsLWOPConstructionTarget(Thing thing)
        {
            if (thing == null || thing.def == null)
            {
                return false;
            }

            Blueprint blueprint = thing as Blueprint;
            if (blueprint != null)
            {
                return IsLWOPBuildableDef(blueprint.def.entityDefToBuild);
            }

            Frame frame = thing as Frame;
            if (frame != null)
            {
                return IsLWOPBuildableDef(frame.def.entityDefToBuild);
            }

            return false;
        }

        private static bool IsLWOPBuildableDef(BuildableDef def)
        {
            return def != null &&
                def.modContentPack != null &&
                def.modContentPack.PackageId == "LWOP.Package";
        }
    }

    [HarmonyPatch]
    public static class LWOPCorpseCollectorVEFConstructionSkillJobPatch
    {
        public static bool Prepare()
        {
            return LWOPCorpseCollectorVEFConstructionSkillPatch.HasVEFConstructionSkillWorkGiver();
        }

        public static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            Type[] parameters =
            {
                typeof(Pawn),
                typeof(Thing),
                typeof(bool)
            };

            for (int i = 0; i < LWOPCorpseCollectorVEFConstructionSkillPatch.VEFConstructionSkillWorkGivers.Length; i++)
            {
                Type type = AccessTools.TypeByName(LWOPCorpseCollectorVEFConstructionSkillPatch.VEFConstructionSkillWorkGivers[i]);
                if (type == null)
                {
                    continue;
                }

                System.Reflection.MethodBase method = AccessTools.Method(type, "JobOnThing", parameters);
                if (method != null)
                {
                    yield return method;
                }
            }
        }

        public static bool Prefix(Thing t, ref Job __result)
        {
            if (LWOPCorpseCollectorVEFConstructionSkillPatch.IsLWOPConstructionTarget(t))
            {
                __result = null;
                return false;
            }

            return true;
        }
    }
}
