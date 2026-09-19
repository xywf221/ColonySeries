using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LWOP.Buildings
{
    [StaticConstructorOnStartup]
    public static class LWOPMechSupport
    {
        static LWOPMechSupport()
        {
            new Harmony("com.colonyseries.lwop.MechSupport").PatchAll();
        }
    }

    public class CompProperties_LWOPWirelessMechCharger : CompProperties
    {
        public int chargeIntervalTicks = 15;
        public bool chargeToFull = true;

        public CompProperties_LWOPWirelessMechCharger()
        {
            compClass = typeof(CompLWOPWirelessMechCharger);
        }
    }

    public class CompLWOPWirelessMechCharger : ThingComp
    {
        private CompPowerTrader powerComp;
        private CompFlickable flickableComp;
        private readonly HashSet<Pawn> chargedPawns = new HashSet<Pawn>();

        private CompProperties_LWOPWirelessMechCharger Props
        {
            get { return (CompProperties_LWOPWirelessMechCharger)props; }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.GetComp<CompPowerTrader>();
            flickableComp = parent.GetComp<CompFlickable>();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent == null || parent.Map == null || !parent.Spawned)
            {
                return;
            }

            int interval = Math.Max(1, Props.chargeIntervalTicks);
            if (!parent.IsHashIntervalTick(interval) || !CanCharge())
            {
                return;
            }

            chargedPawns.Clear();
            foreach (Pawn pawn in GlobalPlayerMechs())
            {
                ChargePawn(pawn);
            }
        }

        private IEnumerable<Pawn> GlobalPlayerMechs()
        {
            if (Find.Maps != null)
            {
                for (int i = 0; i < Find.Maps.Count; i++)
                {
                    Map map = Find.Maps[i];
                    if (map == null || map.mapPawns == null)
                    {
                        continue;
                    }

                    foreach (Pawn pawn in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
                    {
                        if (IsChargeableMech(pawn) && chargedPawns.Add(pawn))
                        {
                            yield return pawn;
                        }
                    }
                }
            }

            if (Find.WorldPawns != null)
            {
                List<Pawn> worldPawns = Find.WorldPawns.AllPawnsAlive;
                for (int i = 0; i < worldPawns.Count; i++)
                {
                    Pawn pawn = worldPawns[i];
                    if (IsChargeableMech(pawn) && chargedPawns.Add(pawn))
                    {
                        yield return pawn;
                    }
                }
            }

            if (Find.WorldObjects == null || Find.WorldObjects.Caravans == null)
            {
                yield break;
            }

            for (int i = 0; i < Find.WorldObjects.Caravans.Count; i++)
            {
                Caravan caravan = Find.WorldObjects.Caravans[i];
                if (caravan == null || caravan.Faction != Faction.OfPlayer)
                {
                    continue;
                }

                List<Pawn> caravanPawns = caravan.PawnsListForReading;
                for (int j = 0; j < caravanPawns.Count; j++)
                {
                    Pawn pawn = caravanPawns[j];
                    if (IsChargeableMech(pawn) && chargedPawns.Add(pawn))
                    {
                        yield return pawn;
                    }
                }
            }
        }

        private static bool IsChargeableMech(Pawn pawn)
        {
            return pawn != null &&
                !pawn.Dead &&
                pawn.Faction == Faction.OfPlayer &&
                pawn.needs != null &&
                pawn.RaceProps != null &&
                pawn.RaceProps.IsMechanoid;
        }

        private void ChargePawn(Pawn pawn)
        {
            Need_MechEnergy energy = pawn.needs.TryGetNeed<Need_MechEnergy>();
            if (energy == null)
            {
                return;
            }

            if (Props.chargeToFull)
            {
                energy.CurLevelPercentage = 1f;
            }
            else
            {
                energy.CurLevelPercentage = Math.Min(1f, energy.CurLevelPercentage + 0.5f);
            }
        }

        public override string CompInspectStringExtra()
        {
            return CanCharge() ? "LWOPWirelessChargingActive".Translate() : "LWOPWirelessChargingOffline".Translate();
        }

        private bool CanCharge()
        {
            if (flickableComp != null && !flickableComp.SwitchIsOn)
            {
                return false;
            }

            return powerComp == null || powerComp.PowerOn;
        }
    }

    public class Building_LWOPAutoMortar : Building
    {
        private Graphic topGraphic;

        private Graphic TopGraphic
        {
            get
            {
                if (topGraphic == null)
                {
                    topGraphic = GraphicDatabase.Get<Graphic_Single>(
                        "Things/Building/Security/TurretMortar_Top",
                        ShaderDatabase.Cutout,
                        new Vector2(4f, 4f),
                        DrawColor);
                }

                return topGraphic;
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            Vector3 topLoc = drawLoc;
            topLoc.y += 0.1f;
            TopGraphic.Draw(topLoc, Rotation, this);
        }
    }

    public static class LWOPGravshipUtility
    {
        private const string LWOPThrusterDefName = "LWOPThruster";

        public static bool IsLWOPThruster(Thing thing)
        {
            return thing != null &&
                thing.def != null &&
                thing.def.defName == LWOPThrusterDefName;
        }

        public static bool HasLWOPThruster(Building_GravEngine engine)
        {
            if (engine == null)
            {
                return false;
            }

            List<CompGravshipFacility> components = engine.GravshipComponents;
            if (HasLWOPThruster(components))
            {
                return true;
            }

            if (HasLWOPThrusterOnMap(engine.Map))
            {
                return true;
            }

            return false;
        }

        public static bool HasLWOPThrusterOnMap(Map map)
        {
            if (map != null && map.listerThings != null)
            {
                ThingDef thrusterDef = DefDatabase<ThingDef>.GetNamedSilentFail(LWOPThrusterDefName);
                if (thrusterDef != null)
                {
                    List<Thing> thrusters = map.listerThings.ThingsOfDef(thrusterDef);
                    if (thrusters != null)
                    {
                        for (int i = 0; i < thrusters.Count; i++)
                        {
                            Thing thruster = thrusters[i];
                            if (thruster != null && thruster.Spawned)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool HasLWOPThruster(List<CompGravshipFacility> components)
        {
            if (components == null)
            {
                return false;
            }

            for (int i = 0; i < components.Count; i++)
            {
                CompGravshipFacility component = components[i];
                if (component != null &&
                    component.parent != null &&
                    IsLWOPThruster(component.parent) &&
                    component.CanBeActive)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(CompGravshipThruster), "CanBeActive", MethodType.Getter)]
    public static class LWOPGravshipThrusterCanBeActivePatch
    {
        public static bool Prefix(CompGravshipThruster __instance, ref bool __result)
        {
            if (__instance == null || !LWOPGravshipUtility.IsLWOPThruster(__instance.parent))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompGravshipThruster), "CompInspectStringExtra")]
    public static class LWOPGravshipThrusterInspectPatch
    {
        public static bool Prefix(CompGravshipThruster __instance, ref string __result)
        {
            if (__instance == null || !LWOPGravshipUtility.IsLWOPThruster(__instance.parent))
            {
                return true;
            }

            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompBreakdownable), "CanBreakdownNow")]
    public static class LWOPGravshipThrusterNoBreakdownPatch
    {
        public static bool Prefix(CompBreakdownable __instance, ref bool __result)
        {
            if (__instance == null || !LWOPGravshipUtility.IsLWOPThruster(__instance.parent))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompBreakdownable), "DoBreakdown")]
    public static class LWOPGravshipThrusterDoBreakdownPatch
    {
        public static bool Prefix(CompBreakdownable __instance)
        {
            return __instance == null || !LWOPGravshipUtility.IsLWOPThruster(__instance.parent);
        }
    }

    [HarmonyPatch(typeof(Building_GravEngine), "FuelPerTile", MethodType.Getter)]
    public static class LWOPGravshipFuelPerTilePatch
    {
        public static void Postfix(Building_GravEngine __instance, ref float __result)
        {
            if (LWOPGravshipUtility.HasLWOPThruster(__instance))
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(Building_GravEngine), "FuelUseageFactor", MethodType.Getter)]
    public static class LWOPGravshipFuelUsagePatch
    {
        public static void Postfix(Building_GravEngine __instance, ref float __result)
        {
            if (LWOPGravshipUtility.HasLWOPThruster(__instance))
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(Building_GravEngine), "FuelSavingsPercent", MethodType.Getter)]
    public static class LWOPGravshipFuelSavingsPatch
    {
        public static void Postfix(Building_GravEngine __instance, ref float __result)
        {
            if (LWOPGravshipUtility.HasLWOPThruster(__instance))
            {
                __result = 1f;
            }
        }
    }

    [HarmonyPatch(typeof(Building_GravEngine), "MaxLaunchDistance", MethodType.Getter)]
    public static class LWOPGravshipMaxLaunchDistancePatch
    {
        public static void Postfix(Building_GravEngine __instance, ref int __result)
        {
            if (LWOPGravshipUtility.HasLWOPThruster(__instance))
            {
                __result = int.MaxValue / 4;
            }
        }
    }

    [HarmonyPatch(typeof(CompPilotConsole), "GetMaxLaunchDistance")]
    public static class LWOPPilotConsoleMaxLaunchDistancePatch
    {
        public static void Postfix(CompPilotConsole __instance, ref int __result)
        {
            if (__instance != null &&
                (LWOPGravshipUtility.HasLWOPThruster(__instance.engine) ||
                LWOPGravshipUtility.HasLWOPThrusterOnMap(__instance.parent?.Map)))
            {
                __result = int.MaxValue / 4;
            }
        }
    }

    [HarmonyPatch(typeof(Building_GravEngine), "MaxFuel", MethodType.Getter)]
    public static class LWOPGravshipMaxFuelPatch
    {
        public static void Postfix(Building_GravEngine __instance, ref float __result)
        {
            if (LWOPGravshipUtility.HasLWOPThruster(__instance))
            {
                __result = float.MaxValue;
            }
        }
    }

    [HarmonyPatch(typeof(Building_GravEngine), "TotalFuel", MethodType.Getter)]
    public static class LWOPGravshipTotalFuelPatch
    {
        public static void Postfix(Building_GravEngine __instance, ref float __result)
        {
            if (LWOPGravshipUtility.HasLWOPThruster(__instance))
            {
                __result = float.MaxValue;
            }
        }
    }

    [HarmonyPatch(typeof(Building_GravEngine), "MissingComponents", MethodType.Getter)]
    public static class LWOPGravshipMissingComponentsPatch
    {
        public static void Postfix(Building_GravEngine __instance, List<GravshipComponentTypeDef> __result)
        {
            if (__result == null || !LWOPGravshipUtility.HasLWOPThruster(__instance))
            {
                return;
            }

            for (int i = __result.Count - 1; i >= 0; i--)
            {
                GravshipComponentTypeDef def = __result[i];
                if (def != null && def.defName == "FuelStorage")
                {
                    __result.RemoveAt(i);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Building_GravEngine), "ConsumeFuel")]
    public static class LWOPGravshipConsumeFuelPatch
    {
        public static bool Prefix(Building_GravEngine __instance)
        {
            return !LWOPGravshipUtility.HasLWOPThruster(__instance);
        }
    }

    [HarmonyPatch(typeof(GravshipUtility), "MaxDistForFuel")]
    public static class LWOPGravshipMaxDistForFuelPatch
    {
        public static bool Prefix(float fuel, float fuelPerTile, float fuelFactor, ref int __result)
        {
            if (fuel >= 1E+20f || fuelPerTile <= 0f || fuelFactor <= 0f)
            {
                __result = int.MaxValue;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(GravshipUtility), "TryGetPathFuelCost")]
    public static class LWOPGravshipPathFuelCostPatch
    {
        public static bool Prefix(float fuelPerTile, float fuelFactor, ref float cost, ref int distance, ref bool __result)
        {
            if (fuelPerTile > 0f && fuelFactor > 0f)
            {
                return true;
            }

            cost = 0f;
            distance = 0;
            __result = true;
            return false;
        }
    }

    public static class LWOPSpaceWallUtility
    {
        private static readonly string[] AirtightDefNames = { "Wall", "Door", "Autodoor" };
        private const string LWOPSteelDefName = "LWOPSteel";
        private const string LWOPBlockStoneDefName = "LWOPBlockStone";

        public static bool IsLWOPSpaceBoundary(Building building)
        {
            return building != null &&
                building.def != null &&
                IsAirtightBuildingDef(building.def.defName) &&
                IsLWOPSpaceStuff(building.Stuff);
        }

        public static bool IsLWOPSpaceStuff(ThingDef stuff)
        {
            return stuff != null &&
                (stuff.defName == LWOPSteelDefName || stuff.defName == LWOPBlockStoneDefName);
        }

        public static IEnumerable<string> AirtightBuildingDefNames
        {
            get { return AirtightDefNames; }
        }

        private static bool IsAirtightBuildingDef(string defName)
        {
            for (int i = 0; i < AirtightDefNames.Length; i++)
            {
                if (AirtightDefNames[i] == defName)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Building), "IsAirtight", MethodType.Getter)]
    public static class LWOPSpaceWallAirtightPatch
    {
        public static void Postfix(Building __instance, ref bool __result)
        {
            if (!__result && LWOPSpaceWallUtility.IsLWOPSpaceBoundary(__instance))
            {
                __result = true;
            }
        }
    }

    public class LWOPSpaceWallOxygenMapComponent : MapComponent
    {
        private const int TickInterval = 250;
        private const float VacuumReductionPerWall = 0.04f;
        private readonly Dictionary<Room, int> roomWallCounts = new Dictionary<Room, int>();
        private readonly HashSet<Room> roomsSeenForWall = new HashSet<Room>();

        public LWOPSpaceWallOxygenMapComponent(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            if (map == null || Find.TickManager.TicksGame % TickInterval != 0)
            {
                return;
            }

            roomWallCounts.Clear();
            foreach (string defName in LWOPSpaceWallUtility.AirtightBuildingDefNames)
            {
                ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null)
                {
                    continue;
                }

                List<Thing> buildings = map.listerThings.ThingsOfDef(def);
                for (int i = 0; i < buildings.Count; i++)
                {
                    Building building = buildings[i] as Building;
                    if (!LWOPSpaceWallUtility.IsLWOPSpaceBoundary(building))
                    {
                        continue;
                    }

                    RegisterAdjacentRooms(building);
                }
            }

            foreach (KeyValuePair<Room, int> entry in roomWallCounts)
            {
                Room room = entry.Key;
                if (room == null || !room.ProperRoom || room.UsesOutdoorTemperature || room.Vacuum <= 0f)
                {
                    continue;
                }

                float reduction = Mathf.Min(0.5f, entry.Value * VacuumReductionPerWall);
                room.Vacuum = Mathf.Max(0f, room.Vacuum - reduction);
            }
        }

        private void RegisterAdjacentRooms(Building building)
        {
            roomsSeenForWall.Clear();
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(building))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                Room room = cell.GetRoom(map);
                if (room != null && roomsSeenForWall.Add(room))
                {
                    int count;
                    roomWallCounts.TryGetValue(room, out count);
                    roomWallCounts[room] = count + 1;
                }
            }
        }
    }

    public class CompProperties_LWOPPlayerOnlyBuilding : CompProperties
    {
        public CompProperties_LWOPPlayerOnlyBuilding()
        {
            compClass = typeof(CompLWOPPlayerOnlyBuilding);
        }
    }

    public class CompLWOPPlayerOnlyBuilding : ThingComp
    {
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (respawningAfterLoad || parent == null || !parent.Spawned || parent.Faction == null)
            {
                return;
            }

            if (parent.Faction != Faction.OfPlayer)
            {
                parent.Destroy(DestroyMode.Vanish);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent == null || !parent.Spawned || parent.Faction == null || parent.Faction == Faction.OfPlayer || !parent.IsHashIntervalTick(250))
            {
                return;
            }

            parent.Destroy(DestroyMode.Vanish);
        }
    }

    public class CompProperties_LWOPAutoMortar : CompProperties
    {
        public int scanIntervalTicks = 1;
        public int cooldownTicks = 60;
        public float radius = 5f;
        public int damageAmount = 80;
        public float armorPenetration = 2f;
        public int empDamageAmount = 100;
        public bool ignoreDownedTargets = true;
        public DamageDef damageDef;
        public DamageDef empDamageDef;

        public CompProperties_LWOPAutoMortar()
        {
            compClass = typeof(CompLWOPAutoMortar);
        }
    }

    [StaticConstructorOnStartup]
    public class CompLWOPAutoMortar : ThingComp
    {
        private const float UnlimitedRange = -1f;
        private static Texture2D rangeCommandIcon;

        private int cooldownTicksLeft;
        private float attackRange = UnlimitedRange;
        private CompFlickable flickableComp;
        private CompForbiddable forbiddableComp;

        private CompProperties_LWOPAutoMortar Props
        {
            get { return (CompProperties_LWOPAutoMortar)props; }
        }

        private static Texture2D RangeCommandIcon
        {
            get
            {
                if (rangeCommandIcon == null)
                {
                    rangeCommandIcon = ContentFinder<Texture2D>.Get("UI/Commands/LWOPAutoMortarRange", false) ?? BaseContent.BadTex;
                }

                return rangeCommandIcon;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            flickableComp = parent.GetComp<CompFlickable>();
            forbiddableComp = parent.GetComp<CompForbiddable>();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref cooldownTicksLeft, "cooldownTicksLeft", 0);
            Scribe_Values.Look(ref attackRange, "attackRange", UnlimitedRange);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (cooldownTicksLeft > 0)
            {
                cooldownTicksLeft--;
                return;
            }

            if (parent == null || parent.Map == null || !parent.Spawned || !parent.IsHashIntervalTick(Math.Max(1, Props.scanIntervalTicks)) || !CanOperate())
            {
                return;
            }

            Pawn target = FindTarget();
            if (target == null)
            {
                return;
            }

            FireAt(target);
            cooldownTicksLeft = Math.Max(1, Props.cooldownTicks);
        }

        public override string CompInspectStringExtra()
        {
            string rangeLine = "LWOPAutoMortarRangeInspect".Translate(GetAttackRangeLabel());
            if (!CanOperate())
            {
                return "LWOPAutoMortarOffline".Translate() + "\n" + rangeLine;
            }

            if (cooldownTicksLeft > 0)
            {
                return "LWOPAutoMortarCooldown".Translate((cooldownTicksLeft / 60f).ToString("0.0")) + "\n" + rangeLine;
            }

            return "LWOPAutoMortarReady".Translate() + "\n" + rangeLine;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            yield return new Command_Action
            {
                defaultLabel = "LWOPAutoMortarRangeCommand".Translate(),
                defaultDesc = "LWOPAutoMortarRangeCommandDesc".Translate(GetAttackRangeLabel()),
                icon = RangeCommandIcon,
                action = delegate
                {
                    Find.WindowStack.Add(new Dialog_LWOPAutoMortarRange(this));
                }
            };
        }

        private bool CanOperate()
        {
            if (parent.Destroyed || parent.Faction != Faction.OfPlayer || forbiddableComp != null && forbiddableComp.Forbidden)
            {
                return false;
            }

            if (flickableComp != null && !flickableComp.SwitchIsOn)
            {
                return false;
            }

            return true;
        }

        private Pawn FindTarget()
        {
            Faction launcherFaction = parent.Faction ?? Faction.OfPlayer;
            Pawn bestTarget = null;
            float bestDistance = float.MaxValue;
            IReadOnlyList<Pawn> pawns = parent.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!IsValidTarget(pawn, launcherFaction))
                {
                    continue;
                }

                float distance = parent.Position.DistanceToSquared(pawn.Position);
                if (!UsesUnlimitedRange && distance > attackRange * attackRange)
                {
                    continue;
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = pawn;
                }
            }

            return bestTarget;
        }

        private bool IsValidTarget(Pawn pawn, Faction launcherFaction)
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != parent.Map || !pawn.HostileTo(launcherFaction))
            {
                return false;
            }

            return !Props.ignoreDownedTargets || !pawn.Downed;
        }

        private void FireAt(Pawn target)
        {
            DamageDef damageDef = Props.damageDef ?? DefDatabase<DamageDef>.GetNamedSilentFail("LWOPWardingDamage") ?? DamageDefOf.Bullet;
            DamageDef empDamageDef = Props.empDamageDef ?? DefDatabase<DamageDef>.GetNamedSilentFail("EMP");
            Faction launcherFaction = parent.Faction ?? Faction.OfPlayer;
            IReadOnlyList<Pawn> pawns = parent.Map.mapPawns.AllPawnsSpawned;
            List<Pawn> pawnsToDamage = new List<Pawn>();
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != parent.Map || !pawn.HostileTo(launcherFaction))
                {
                    continue;
                }

                if (pawn.Position.DistanceTo(target.Position) > Props.radius)
                {
                    continue;
                }

                pawnsToDamage.Add(pawn);
            }

            for (int i = 0; i < pawnsToDamage.Count; i++)
            {
                Pawn pawn = pawnsToDamage[i];
                DamageInfo dinfo = new DamageInfo(damageDef, Props.damageAmount, Props.armorPenetration, -1f, null, null, parent.def);
                pawn.TakeDamage(dinfo);
                if (empDamageDef != null && Props.empDamageAmount > 0 && pawn != null && !pawn.Destroyed && !pawn.Dead)
                {
                    DamageInfo empDinfo = new DamageInfo(empDamageDef, Props.empDamageAmount, 0f, -1f, null, null, parent.def);
                    pawn.TakeDamage(empDinfo);
                }
            }

            MoteMaker.ThrowText(target.DrawPos, parent.Map, "LWOPWardingStrikeMote".Translate(), 3f);
        }

        public bool UsesUnlimitedRange
        {
            get { return attackRange < 0f; }
        }

        public float MaxAttackRange
        {
            get
            {
                if (parent == null || parent.Map == null)
                {
                    return 200f;
                }

                int x = parent.Map.Size.x;
                int z = parent.Map.Size.z;
                return Mathf.Max(1f, Mathf.Ceil(Mathf.Sqrt(x * x + z * z)));
            }
        }

        public float RangeForSlider
        {
            get { return UsesUnlimitedRange ? MaxAttackRange : Mathf.Clamp(attackRange, 1f, MaxAttackRange); }
        }

        public void SetAttackRangeFromSlider(float value)
        {
            float maxRange = MaxAttackRange;
            if (value >= maxRange - 0.5f)
            {
                attackRange = UnlimitedRange;
                return;
            }

            attackRange = Mathf.Clamp(Mathf.Round(value), 1f, Mathf.Max(1f, maxRange - 1f));
        }

        public string GetAttackRangeLabel()
        {
            if (UsesUnlimitedRange)
            {
                return "LWOPAutoMortarRangeUnlimited".Translate();
            }

            return "LWOPAutoMortarRangeCells".Translate(Mathf.RoundToInt(attackRange).ToString());
        }
    }

    public class Dialog_LWOPAutoMortarRange : Window
    {
        private readonly CompLWOPAutoMortar mortar;
        private float sliderValue;

        public Dialog_LWOPAutoMortarRange(CompLWOPAutoMortar mortar)
        {
            this.mortar = mortar;
            sliderValue = mortar.RangeForSlider;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            forcePause = false;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(460f, 190f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "LWOPAutoMortarRangeTitle".Translate());
            Text.Font = GameFont.Small;

            Rect labelRect = new Rect(0f, 40f, inRect.width, 28f);
            Widgets.Label(labelRect, "LWOPAutoMortarRangeCurrent".Translate(mortar.GetAttackRangeLabel()));

            float maxRange = mortar.MaxAttackRange;
            Rect sliderRect = new Rect(0f, 78f, inRect.width, 32f);
            sliderValue = Widgets.HorizontalSlider(
                sliderRect,
                sliderValue,
                1f,
                maxRange,
                false,
                null,
                "LWOPAutoMortarRangeCells".Translate("1"),
                "LWOPAutoMortarRangeUnlimited".Translate(),
                1f);
            mortar.SetAttackRangeFromSlider(sliderValue);
            sliderValue = mortar.RangeForSlider;
            if (!mortar.UsesUnlimitedRange && mortar.parent != null && mortar.parent.Spawned)
            {
                GenDraw.DrawRadiusRing(mortar.parent.Position, mortar.RangeForSlider);
            }

            Rect closeRect = new Rect(inRect.width - 120f, inRect.height - 38f, 120f, 32f);
            if (Widgets.ButtonText(closeRect, "CloseButton".Translate()))
            {
                Close();
            }
        }
    }

    public class CompProperties_LWOPMeleeBlast : CompProperties_AbilityEffect
    {
        public float radius = 8f;
        public int damageAmount = 120;
        public float armorPenetration = 999f;
        public float chanceToStartFire = 1f;
        public DamageDef damageDef;

        public CompProperties_LWOPMeleeBlast()
        {
            compClass = typeof(CompAbilityEffect_LWOPMeleeBlast);
        }
    }

    public class CompAbilityEffect_LWOPMeleeBlast : CompAbilityEffect
    {
        private CompProperties_LWOPMeleeBlast BlastProps
        {
            get { return (CompProperties_LWOPMeleeBlast)props; }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent == null ? null : parent.pawn;
            if (caster == null || caster.Map == null || !caster.Spawned)
            {
                return;
            }

            DamageDef damageDef = BlastProps.damageDef ?? DamageDefOf.Flame;
            GenExplosion.DoExplosion(
                caster.Position,
                caster.Map,
                BlastProps.radius,
                damageDef,
                caster,
                BlastProps.damageAmount,
                BlastProps.armorPenetration,
                null,
                null,
                null,
                null,
                null,
                0f,
                1,
                null,
                null,
                255,
                true,
                null,
                0f,
                1,
                BlastProps.chanceToStartFire,
                false,
                null,
                new List<Thing> { caster },
                null,
                true,
                1f,
                0f,
                true,
                null,
                2f);
        }
    }

    public class CompProperties_LWOPRevealMap : CompProperties_AbilityEffect
    {
        public CompProperties_LWOPRevealMap()
        {
            compClass = typeof(CompAbilityEffect_LWOPRevealMap);
        }
    }

    public class CompAbilityEffect_LWOPRevealMap : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent == null ? null : parent.pawn;
            Map map = caster == null ? null : caster.Map;
            if (map == null || map.fogGrid == null)
            {
                return;
            }

            int foggedCells = 0;
            foreach (IntVec3 cell in map.AllCells)
            {
                if (map.fogGrid.IsFogged(cell))
                {
                    foggedCells++;
                }
            }

            map.fogGrid.ClearAllFog();
            Messages.Message("LWOPRevealMapComplete".Translate(foggedCells.ToString()), caster, MessageTypeDefOf.PositiveEvent, false);
        }
    }

    public class Verb_LWOPPinpointBlast : Verb
    {
        private const float DefaultRadius = 4.9f;
        private const int DefaultDamage = 250;

        public override bool MultiSelect
        {
            get { return true; }
        }

        private float BlastRadius
        {
            get { return verbProps.beamWidth > 0f ? verbProps.beamWidth : DefaultRadius; }
        }

        public override float HighlightFieldRadiusAroundTarget(out bool needLOSToCenter)
        {
            needLOSToCenter = false;
            return BlastRadius;
        }

        public override void OrderForceTarget(LocalTargetInfo target)
        {
            Job job = JobMaker.MakeJob(JobDefOf.UseVerbOnThingStatic, target);
            job.verbToUse = this;
            CasterPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        protected override bool TryCastShot()
        {
            if (caster == null || caster.Map == null || !currentTarget.IsValid)
            {
                return false;
            }

            IntVec3 cell = currentTarget.Cell;
            if (!cell.InBounds(caster.Map))
            {
                return false;
            }

            int damage = verbProps.beamTotalDamage > 0f ? Mathf.RoundToInt(verbProps.beamTotalDamage) : DefaultDamage;

            GenExplosion.DoExplosion(
                cell,
                caster.Map,
                BlastRadius,
                DamageDefOf.Bomb,
                caster,
                damage,
                -1f,
                null,
                EquipmentSource == null ? null : EquipmentSource.def,
                null,
                currentTarget.Thing,
                applyDamageToExplosionCellsNeighbors: true,
                chanceToStartFire: verbProps.beamChanceToStartFire,
                damageFalloff: false,
                doVisualEffects: true,
                propagationSpeed: 9999f,
                screenShakeFactor: 1.4f);

            return true;
        }
    }

    public class CompTargetEffect_LWOPPsychicShock : CompTargetEffect
    {
        public override void DoEffectOn(Pawn user, Thing target)
        {
            Pawn pawn = target as Pawn;
            HediffDef psychicShockDef = DefDatabase<HediffDef>.GetNamedSilentFail("PsychicShock");
            if (pawn == null || pawn.Dead || pawn.health == null || psychicShockDef == null)
            {
                return;
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(psychicShockDef);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }

            pawn.health.AddHediff(psychicShockDef);
        }
    }

    public static class LWOPVerbTargetUtility
    {
        public static bool IsValidLWOPMapTarget(Verb verb, LocalTargetInfo target)
        {
            if (verb == null || verb.Caster == null || verb.Caster.Map == null || !target.IsValid)
            {
                return false;
            }

            IntVec3 cell = target.Cell;
            return cell.IsValid && cell.InBounds(verb.Caster.Map);
        }

        public static bool IsValidLWOPPsychicShockTarget(Verb verb, LocalTargetInfo target)
        {
            if (!IsValidLWOPMapTarget(verb, target))
            {
                return false;
            }

            Pawn pawn = target.Thing as Pawn;
            return pawn != null && !pawn.Dead && !pawn.Downed;
        }
    }

    public class Verb_LWOPPsychicShock : Verb_CastTargetEffectLances
    {
        public override bool Available()
        {
            return true;
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            return LWOPVerbTargetUtility.IsValidLWOPPsychicShockTarget(this, targ);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            return LWOPVerbTargetUtility.IsValidLWOPPsychicShockTarget(this, targ);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            return LWOPVerbTargetUtility.IsValidLWOPPsychicShockTarget(this, target);
        }
    }

    public class Verb_LWOPJump : Verb_Jump
    {
        public override bool Available()
        {
            return true;
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, targ);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, targ);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, target);
        }
    }

    public class Verb_LWOPLaunchProjectileStatic : Verb_LaunchProjectileStatic
    {
        public override bool Available()
        {
            return true;
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, targ);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, targ);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, target);
        }
    }

    public class Verb_LWOPPowerBeam : Verb_PowerBeam
    {
        public override bool Available()
        {
            return true;
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, targ);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, targ);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, target);
        }
    }

    public class Verb_LWOPBombardment : Verb_Bombardment
    {
        public override bool Available()
        {
            return true;
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, targ);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, targ);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, target);
        }
    }

    public class Verb_LWOPSpawn : Verb_Spawn
    {
        public override bool Available()
        {
            return true;
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, targ);
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, targ);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            return LWOPVerbTargetUtility.IsValidLWOPMapTarget(this, target);
        }
    }

    public class Verb_LWOPRevealMap : Verb
    {
        public override bool Available()
        {
            return true;
        }

        public override bool CanHitTarget(LocalTargetInfo targ)
        {
            return true;
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo targ)
        {
            return true;
        }

        protected override bool TryCastShot()
        {
            Map map = caster == null ? null : caster.Map;
            if (map == null || map.fogGrid == null)
            {
                return false;
            }

            int foggedCells = 0;
            foreach (IntVec3 cell in map.AllCells)
            {
                if (map.fogGrid.IsFogged(cell))
                {
                    foggedCells++;
                }
            }

            map.fogGrid.ClearAllFog();

            if (CasterPawn != null)
            {
                Messages.Message("LWOPRevealMapComplete".Translate(foggedCells.ToString()), CasterPawn, MessageTypeDefOf.PositiveEvent, false);
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(JobDriver_InteractThing), "WaitForActivate")]
    public static class LWOPGrayDoorFastForceOpenPatch
    {
        public static void Prefix(JobDriver_InteractThing __instance, ref int remainingTicks, ref int totalTicks)
        {
            if (__instance == null ||
                __instance.pawn == null ||
                __instance.job == null ||
                !LWOPMechUtility.WearsLWOPMechanitorCommandApparel(__instance.pawn))
            {
                return;
            }

            Thing target = __instance.job.GetTarget(TargetIndex.A).Thing;
            if (target == null ||
                target.def == null ||
                !string.Equals(target.def.defName, "GrayDoor", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            remainingTicks = 1;
            totalTicks = 1;
        }
    }

    public static class LWOPMechUtility
    {
        private static readonly FieldInfo WorkSettingsPawnField = AccessTools.Field(typeof(Pawn_WorkSettings), "pawn");
        private static readonly FieldInfo SkillTrackerPawnField = AccessTools.Field(typeof(Pawn_SkillTracker), "pawn");
        private static readonly MethodInfo TrainingGetStepsMethod = AccessTools.Method(typeof(Pawn_TrainingTracker), "GetSteps");
        private static readonly FieldInfo AbilityInCooldownField = AccessTools.Field(typeof(Ability), "inCooldown");
        private static readonly FieldInfo AbilityCooldownDurationField = AccessTools.Field(typeof(Ability), "cooldownDuration");
        private static readonly FieldInfo AbilityCooldownEndTickField = AccessTools.Field(typeof(Ability), "cooldownEndTick");
        private const string LWOPHelmetDefName = "lwApparel_PowerArmorHelmet";
        private const string LWOPPowerArmorDefName = "LWOPApparel_PowerArmor";
        private const string LWOPChildHelmetDefName = "LWOPApparel_PowerArmorHelmetChild";
        private const string LWOPChildPowerArmorDefName = "LWOPApparel_PowerArmorChild";
        private const string LWOPMeleeBlastAbilityDefName = "LWOPMeleeBlast";
        private const string LWOPRevealMapAbilityDefName = "LWOPRevealMap";
        private const int LWOPMechAnimalSkillLevel = 20;
        private static readonly HashSet<string> LWOPMechBlockedWorkGiverDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ChatWithPrisoner",
            "EnslavePrisoner",
            "ConvertPrisoner",
            "SuppressSlave",
            "EmancipateSlave",
            "InterrogatePrisoner",
            "ActivitySuppression",
            "PlayWithBaby",
            "BreastfeedBaby",
            "CarryToBreastfeed",
            "ChildcarerTeach"
        };

        private static readonly string[] LWOPMechBlockedWorkGiverClassFragments =
        {
            "WorkGiver_Warden_Chat",
            "WorkGiver_Warden_Convert",
            "WorkGiver_Warden_Enslave",
            "WorkGiver_Warden_Suppress",
            "WorkGiver_Warden_Interrogate",
            "WorkGiver_Warden_Emancipate",
            "WorkGiver_PlayWithBaby",
            "WorkGiver_Teach",
            "WorkGiver_Breastfeed"
        };

        [ThreadStatic]
        public static bool SuppressIdeoAbilityCooldown;

        public static bool IsLWOPWorkerMech(Pawn pawn)
        {
            return pawn != null &&
                pawn.def != null &&
                string.Equals(pawn.def.defName, "Mech_LWOPWorker", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPlayerLWOPWorkerMech(Pawn pawn)
        {
            return IsLWOPWorkerMech(pawn) && pawn.Faction == Faction.OfPlayer;
        }

        public static void EnsureLWOPMechAbilities(Pawn pawn)
        {
            if (!IsLWOPWorkerMech(pawn) || pawn.abilities == null)
            {
                return;
            }

            bool changed = false;
            changed |= EnsureLWOPMechAbility(pawn, LWOPMeleeBlastAbilityDefName);
            changed |= EnsureLWOPMechAbility(pawn, LWOPRevealMapAbilityDefName);
            if (changed)
            {
                pawn.abilities.Notify_TemporaryAbilitiesChanged();
            }
        }

        private static bool EnsureLWOPMechAbility(Pawn pawn, string abilityDefName)
        {
            AbilityDef abilityDef = DefDatabase<AbilityDef>.GetNamedSilentFail(abilityDefName);
            if (abilityDef == null || pawn.abilities.GetAbility(abilityDef, true) != null)
            {
                return false;
            }

            pawn.abilities.abilities.Add(new Ability(pawn, abilityDef));
            return true;
        }

        public static void EnsureLWOPMechWorkTrackers(Pawn pawn)
        {
            if (!IsLWOPWorkerMech(pawn))
            {
                return;
            }

            EnsureLWOPMechSkills(pawn);
            if (pawn.records == null)
            {
                pawn.records = new Pawn_RecordsTracker(pawn);
            }
        }

        private static void EnsureLWOPMechSkills(Pawn pawn)
        {
            if (pawn.skills == null)
            {
                pawn.skills = new Pawn_SkillTracker(pawn);
            }

            if (pawn.skills.skills == null)
            {
                pawn.skills.skills = new List<SkillRecord>();
            }

            List<SkillDef> skillDefs = DefDatabase<SkillDef>.AllDefsListForReading;
            for (int i = 0; i < skillDefs.Count; i++)
            {
                SkillDef skillDef = skillDefs[i];
                if (skillDef == null)
                {
                    continue;
                }

                SkillRecord record = pawn.skills.GetSkill(skillDef);
                if (record == null)
                {
                    record = new SkillRecord(pawn, skillDef);
                    pawn.skills.skills.Add(record);
                }

                if (record.Level < LWOPMechAnimalSkillLevel)
                {
                    record.Level = LWOPMechAnimalSkillLevel;
                }
            }
        }

        public static void EnsureLWOPMechAnimalInteractionTracker(Pawn pawn)
        {
            if (IsLWOPWorkerMech(pawn) && pawn.interactions == null)
            {
                pawn.interactions = new Pawn_InteractionsTracker(pawn);
            }
        }

        public static bool IsLWOPMechAnimalInteraction(Pawn pawn, Pawn recipient, InteractionDef interactionDef)
        {
            if (!IsLWOPWorkerMech(pawn) || recipient == null || interactionDef == null || !recipient.AnimalOrWildMan())
            {
                return false;
            }

            return interactionDef == InteractionDefOf.AnimalChat ||
                interactionDef == InteractionDefOf.TrainAttempt ||
                interactionDef == InteractionDefOf.TameAttempt;
        }

        public static bool CanLWOPMechUseWorkGiver(WorkGiverDef def)
        {
            if (def == null)
            {
                return false;
            }

            if (!def.defName.NullOrEmpty() && LWOPMechBlockedWorkGiverDefNames.Contains(def.defName))
            {
                return false;
            }

            if (def.requiredCapacities != null && def.requiredCapacities.Contains(PawnCapacityDefOf.Talking))
            {
                return false;
            }

            string giverClassName = def.giverClass == null ? null : def.giverClass.FullName;
            if (!giverClassName.NullOrEmpty())
            {
                for (int i = 0; i < LWOPMechBlockedWorkGiverClassFragments.Length; i++)
                {
                    if (giverClassName.IndexOf(LWOPMechBlockedWorkGiverClassFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool WearsLWOPHelmet(Pawn pawn)
        {
            return WearsLWOPMechanitorCommandApparel(pawn);
        }

        public static bool WearsLWOPMechanitorCommandApparel(Pawn pawn)
        {
            if (pawn == null || pawn.apparel == null)
            {
                return false;
            }

            List<Apparel> wornApparel = pawn.apparel.WornApparel;
            for (int i = 0; i < wornApparel.Count; i++)
            {
                Apparel apparel = wornApparel[i];
                if (apparel == null || apparel.def == null)
                {
                    continue;
                }

                string defName = apparel.def.defName;
                if (string.Equals(defName, LWOPHelmetDefName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(defName, LWOPPowerArmorDefName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(defName, LWOPChildHelmetDefName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(defName, LWOPChildPowerArmorDefName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasFreeAbilityApparel(Pawn pawn)
        {
            return WearsLWOPMechanitorCommandApparel(pawn);
        }

        public static bool HasLWOPWeaponMasteryApparel(Pawn pawn)
        {
            return WearsLWOPMechanitorCommandApparel(pawn);
        }

        public static bool IsLWOPFixedColorVerbCommand(Command_VerbTarget command)
        {
            if (command == null || command.verb == null || command.verb.verbProps == null)
            {
                return false;
            }

            string iconPath = command.verb.verbProps.commandIcon;
            if (iconPath.NullOrEmpty() || iconPath.IndexOf("UI/Commands/LWOP", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            Thing owner = command.ownerThing ?? command.verb.EquipmentSource;
            return IsLWOPCommandIconSource(owner);
        }

        private static bool IsLWOPCommandIconSource(Thing owner)
        {
            if (owner == null || owner.def == null || owner.def.defName.NullOrEmpty())
            {
                return false;
            }

            string defName = owner.def.defName;
            return string.Equals(defName, LWOPPowerArmorDefName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(defName, LWOPChildPowerArmorDefName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool AbilityCasterHasFreeAbilityApparel(Ability ability)
        {
            return ability != null && HasFreeAbilityApparel(ability.pawn);
        }

        public static void MakeAbilityFreeAndReady(Ability ability)
        {
            if (ability == null)
            {
                return;
            }

            AbilityInCooldownField?.SetValue(ability, false);
            AbilityCooldownDurationField?.SetValue(ability, 0);
            AbilityCooldownEndTickField?.SetValue(ability, 0);

            if (ability.maxCharges > 0)
            {
                ability.RemainingCharges = ability.maxCharges;
            }
        }

        public static Pawn PawnForWorkSettings(Pawn_WorkSettings settings)
        {
            return WorkSettingsPawnField == null ? null : WorkSettingsPawnField.GetValue(settings) as Pawn;
        }

        public static Pawn PawnForSkillTracker(Pawn_SkillTracker skills)
        {
            return SkillTrackerPawnField == null ? null : SkillTrackerPawnField.GetValue(skills) as Pawn;
        }

        public static List<WorkGiverDef> AllowAllWorkGiversForLWOPMech(Pawn pawn)
        {
            if (!IsLWOPWorkerMech(pawn))
            {
                return null;
            }

            List<WorkGiverDef> changed = null;
            foreach (WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefsListForReading)
            {
                if (def == null || def.canBeDoneByMechs || !CanLWOPMechUseWorkGiver(def))
                {
                    continue;
                }

                if (changed == null)
                {
                    changed = new List<WorkGiverDef>();
                }

                def.canBeDoneByMechs = true;
                changed.Add(def);
            }

            return changed;
        }

        public static bool CanInteractWithAnimalAsLWOPMech(
            Pawn pawn,
            Pawn animal,
            out string jobFailReason,
            bool forced,
            bool canInteractWhileSleeping,
            bool ignoreSkillRequirements,
            bool canInteractWhileRoaming)
        {
            jobFailReason = null;
            EnsureLWOPMechWorkTrackers(pawn);
            EnsureLWOPMechAnimalInteractionTracker(pawn);
            if (pawn == null || animal == null)
            {
                return false;
            }

            if (!pawn.CanReserve(animal, 1, -1, null, forced))
            {
                return false;
            }

            if (animal.Downed)
            {
                jobFailReason = "CantInteractAnimalDowned".Translate();
                return false;
            }

            if (!animal.Awake() && !canInteractWhileSleeping)
            {
                jobFailReason = "CantInteractAnimalAsleep".Translate();
                return false;
            }

            if (!animal.CanCasuallyInteractNow(false, canInteractWhileSleeping, canInteractWhileRoaming))
            {
                jobFailReason = "CantInteractAnimalBusy".Translate();
                return false;
            }

            int minimumSkill = TrainableUtility.MinimumHandlingSkill(animal);
            if (!ignoreSkillRequirements && minimumSkill > LWOPMechAnimalSkillLevel)
            {
                jobFailReason = "AnimalsSkillTooLow".Translate(minimumSkill);
                return false;
            }

            return true;
        }

        public static void TryTrainAnimalAsLWOPMech(Pawn actor, Pawn animal)
        {
            if (!IsLWOPWorkerMech(actor) || animal == null || !animal.Spawned || !animal.Awake() || animal.training == null)
            {
                return;
            }

            EnsureLWOPMechWorkTrackers(actor);
            EnsureLWOPMechAnimalInteractionTracker(actor);
            if (actor.interactions != null)
            {
                actor.interactions.TryInteractWith(animal, InteractionDefOf.TrainAttempt);
            }

            TrainableDef trainableDef = animal.training.NextTrainableToTrain();
            if (trainableDef == null)
            {
                Log.ErrorOnce("Attempted to train untrainable animal", 7842936);
                return;
            }

            float chance = 1f;
            animal.training.Train(trainableDef, actor);
            ClearLWOPMechAnimalMaster(animal);
            if (animal.caller != null)
            {
                animal.caller.DoCall();
            }

            string text = "TextMote_TrainSuccess".Translate(trainableDef.LabelCap, chance.ToStringPercent());
            if (TrainingGetStepsMethod != null)
            {
                text = text + "\n" + TrainingGetStepsMethod.Invoke(animal.training, new object[] { trainableDef }) + " / " + trainableDef.steps;
            }
            MoteMaker.ThrowText((actor.DrawPos + animal.DrawPos) / 2f, actor.Map, text, 5f);
        }

        public static void TryTameAnimalAsLWOPMech(Pawn actor, Pawn animal)
        {
            if (!IsLWOPWorkerMech(actor) || animal == null || !animal.Spawned || !animal.Awake() || !animal.AnimalOrWildMan())
            {
                return;
            }

            EnsureLWOPMechWorkTrackers(actor);
            EnsureLWOPMechAnimalInteractionTracker(actor);
            if (actor.interactions != null)
            {
                actor.interactions.TryInteractWith(animal, InteractionDefOf.TameAttempt);
            }

            string oldLabel = animal.LabelIndefinite();
            RecruitUtility.Recruit(animal, actor.Faction ?? Faction.OfPlayer, actor);
            ClearLWOPMechAnimalMaster(animal);
            if (animal.caller != null)
            {
                animal.caller.DoCall();
            }

            if (actor.Spawned && animal.Spawned)
            {
                MoteMaker.ThrowText((actor.DrawPos + animal.DrawPos) / 2f, actor.Map, "TextMote_TameSuccess".Translate(), 8f);
            }

            Messages.Message("MessageTameSuccess".Translate(actor.LabelShort, oldLabel, actor.Named("RECRUITER")), animal, MessageTypeDefOf.PositiveEvent);
        }

        public static void ClearLWOPMechAnimalMaster(Pawn animal)
        {
            if (animal == null || animal.playerSettings == null)
            {
                return;
            }

            Pawn master = animal.playerSettings.Master;
            if (IsLWOPWorkerMech(master))
            {
                animal.playerSettings.Master = BestReplacementAnimalMaster(animal);
            }
        }

        private static Pawn BestReplacementAnimalMaster(Pawn animal)
        {
            if (animal == null || animal.Map == null || animal.Map.mapPawns == null)
            {
                return null;
            }

            List<Pawn> colonists = animal.Map.mapPawns.FreeColonistsSpawned;
            Pawn best = null;
            int bestSkill = -1;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn colonist = colonists[i];
                if (!CanBeSafeAnimalMaster(colonist, animal))
                {
                    continue;
                }

                int skill = colonist.skills == null ? 0 : colonist.skills.GetSkill(SkillDefOf.Animals).Level;
                if (best == null ||
                    skill > bestSkill ||
                    (skill == bestSkill && colonist.Position.DistanceToSquared(animal.Position) < best.Position.DistanceToSquared(animal.Position)))
                {
                    best = colonist;
                    bestSkill = skill;
                }
            }

            return best;
        }

        private static bool CanBeSafeAnimalMaster(Pawn colonist, Pawn animal)
        {
            if (colonist == null || colonist.Dead || colonist.Destroyed || colonist.RaceProps == null || colonist.RaceProps.IsMechanoid)
            {
                return false;
            }

            if (colonist.Faction == null || colonist.Faction != Faction.OfPlayer)
            {
                return false;
            }

            if (animal != null && colonist.Map != animal.Map)
            {
                return false;
            }

            return TrainableUtility.CanBeMaster(colonist, animal, false);
        }

        public static void RestoreMechWorkGivers(List<WorkGiverDef> changed)
        {
            if (changed == null)
            {
                return;
            }

            foreach (WorkGiverDef def in changed)
            {
                if (def != null)
                {
                    def.canBeDoneByMechs = false;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Ability), "get_CanCast")]
    public static class LWOPAbilityCanCastReadyPatch
    {
        public static void Prefix(Ability __instance)
        {
            if (LWOPMechUtility.AbilityCasterHasFreeAbilityApparel(__instance))
            {
                LWOPMechUtility.MakeAbilityFreeAndReady(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(ApparelGraphicRecordGetter), "TryGetGraphicApparel")]
    public static class LWOPChildPowerArmorAdultGraphicPatch
    {
        public static bool Prefix(Apparel apparel, BodyTypeDef bodyType, bool forStatue, ref ApparelGraphicRecord rec, ref bool __result)
        {
            if (apparel == null ||
                apparel.def == null ||
                !string.Equals(apparel.def.defName, "LWOPApparel_PowerArmorChild", StringComparison.OrdinalIgnoreCase) ||
                bodyType == null ||
                !string.Equals(bodyType.defName, "Child", StringComparison.OrdinalIgnoreCase) ||
                apparel.WornGraphicPath.NullOrEmpty())
            {
                return true;
            }

            string path = apparel.WornGraphicPath + "_" + BodyTypeDefOf.Thin.defName;
            Shader shader = ShaderDatabase.Cutout;
            if (!forStatue)
            {
                if (apparel.StyleDef != null && apparel.StyleDef.graphicData != null && apparel.StyleDef.graphicData.shaderType != null)
                {
                    shader = apparel.StyleDef.graphicData.shaderType.Shader;
                }
                else if ((apparel.StyleDef == null && apparel.def.apparel.useWornGraphicMask) ||
                    (apparel.StyleDef != null && apparel.StyleDef.UseWornGraphicMask))
                {
                    shader = ShaderDatabase.CutoutComplex;
                }
            }

            Graphic graphic = GraphicDatabase.Get<Graphic_Multi>(path, shader, apparel.def.graphicData.drawSize, apparel.DrawColor);
            rec = new ApparelGraphicRecord(graphic, apparel);
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Ability), "CooldownTicksRemaining", MethodType.Getter)]
    public static class LWOPAbilityCooldownRemainingPatch
    {
        public static void Postfix(Ability __instance, ref int __result)
        {
            if (LWOPMechUtility.AbilityCasterHasFreeAbilityApparel(__instance))
            {
                __result = 0;
            }
        }
    }

    [HarmonyPatch(typeof(Ability), "StartCooldown")]
    public static class LWOPAbilityStartCooldownPatch
    {
        public static bool Prefix(Ability __instance)
        {
            if (!LWOPMechUtility.AbilityCasterHasFreeAbilityApparel(__instance))
            {
                return true;
            }

            LWOPMechUtility.MakeAbilityFreeAndReady(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(Ability), "PreActivate")]
    public static class LWOPAbilityPreActivatePatch
    {
        public static void Prefix(Ability __instance)
        {
            if (LWOPMechUtility.AbilityCasterHasFreeAbilityApparel(__instance))
            {
                LWOPMechUtility.SuppressIdeoAbilityCooldown = true;
            }
        }

        public static void Postfix(Ability __instance)
        {
            LWOPMechUtility.SuppressIdeoAbilityCooldown = false;
            if (LWOPMechUtility.AbilityCasterHasFreeAbilityApparel(__instance))
            {
                LWOPMechUtility.MakeAbilityFreeAndReady(__instance);
            }
        }

        public static void Finalizer()
        {
            LWOPMechUtility.SuppressIdeoAbilityCooldown = false;
        }
    }

    [HarmonyPatch(typeof(Precept_Ritual), "Notify_CooldownFromAbilityStarted")]
    public static class LWOPIdeoAbilityCooldownPatch
    {
        public static bool Prefix()
        {
            return !LWOPMechUtility.SuppressIdeoAbilityCooldown;
        }
    }

    [HarmonyPatch(typeof(Ability), "GizmoDisabled")]
    public static class LWOPAbilityGizmoDisabledPatch
    {
        public static void Prefix(Ability __instance)
        {
            if (LWOPMechUtility.AbilityCasterHasFreeAbilityApparel(__instance))
            {
                LWOPMechUtility.MakeAbilityFreeAndReady(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Ability), "FinalPsyfocusCost")]
    public static class LWOPAbilityPsyfocusCostPatch
    {
        public static void Postfix(Ability __instance, ref float __result)
        {
            if (LWOPMechUtility.AbilityCasterHasFreeAbilityApparel(__instance))
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(Ability), "HemogenCost")]
    public static class LWOPAbilityHemogenCostPatch
    {
        public static void Postfix(Ability __instance, ref float __result)
        {
            if (LWOPMechUtility.AbilityCasterHasFreeAbilityApparel(__instance))
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(CompAbilityEffect_HemogenCost), "GizmoDisabled")]
    public static class LWOPAbilityHemogenGizmoPatch
    {
        public static bool Prefix(CompAbilityEffect_HemogenCost __instance, ref bool __result, ref string reason)
        {
            if (__instance == null || !LWOPMechUtility.AbilityCasterHasFreeAbilityApparel(__instance.parent))
            {
                return true;
            }

            reason = null;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompAbilityEffect_HemogenCost), "Apply", new Type[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) })]
    public static class LWOPAbilityHemogenApplyPatch
    {
        public static bool Prefix(CompAbilityEffect_HemogenCost __instance)
        {
            return __instance == null || !LWOPMechUtility.AbilityCasterHasFreeAbilityApparel(__instance.parent);
        }
    }

    [HarmonyPatch(typeof(Pawn_PsychicEntropyTracker), "CurrentPsyfocus", MethodType.Getter)]
    public static class LWOPPsyfocusLevelPatch
    {
        public static void Postfix(Pawn_PsychicEntropyTracker __instance, ref float __result)
        {
            if (__instance != null && LWOPMechUtility.HasFreeAbilityApparel(__instance.Pawn))
            {
                __result = 1f;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_PsychicEntropyTracker), "MaxAbilityLevel", MethodType.Getter)]
    public static class LWOPPsyfocusBandPatch
    {
        public static void Postfix(Pawn_PsychicEntropyTracker __instance, ref int __result)
        {
            if (__instance != null && LWOPMechUtility.HasFreeAbilityApparel(__instance.Pawn))
            {
                __result = int.MaxValue;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_PsychicEntropyTracker), "WouldOverflowEntropy")]
    public static class LWOPPsychicEntropyOverflowPatch
    {
        public static void Postfix(Pawn_PsychicEntropyTracker __instance, ref bool __result)
        {
            if (__instance != null && LWOPMechUtility.HasFreeAbilityApparel(__instance.Pawn))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_PsychicEntropyTracker), "TryAddEntropy")]
    public static class LWOPPsychicEntropyAddPatch
    {
        public static bool Prefix(Pawn_PsychicEntropyTracker __instance, float value, ref bool __result)
        {
            if (__instance == null || value <= 0f || !LWOPMechUtility.HasFreeAbilityApparel(__instance.Pawn))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_PsychicEntropyTracker), "OffsetPsyfocusDirectly")]
    public static class LWOPPsyfocusSpendPatch
    {
        public static bool Prefix(Pawn_PsychicEntropyTracker __instance, float offset)
        {
            return __instance == null || offset >= 0f || !LWOPMechUtility.HasFreeAbilityApparel(__instance.Pawn);
        }
    }

    [HarmonyPatch(typeof(Verb), "WarmupTime", MethodType.Getter)]
    public static class LWOPWeaponWarmupTimePatch
    {
        public static void Postfix(Verb __instance, ref float __result)
        {
            if (__instance != null && LWOPMechUtility.HasLWOPWeaponMasteryApparel(__instance.CasterPawn))
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(VerbProperties), "AdjustedCooldown", new Type[] { typeof(Verb), typeof(Pawn) })]
    public static class LWOPWeaponAdjustedCooldownVerbPatch
    {
        public static void Postfix(Pawn attacker, ref float __result)
        {
            if (LWOPMechUtility.HasLWOPWeaponMasteryApparel(attacker))
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(VerbProperties), "AdjustedCooldown", new Type[] { typeof(Tool), typeof(Pawn), typeof(Thing) })]
    public static class LWOPWeaponAdjustedCooldownToolThingPatch
    {
        public static void Postfix(Pawn attacker, ref float __result)
        {
            if (LWOPMechUtility.HasLWOPWeaponMasteryApparel(attacker))
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(VerbProperties), "AdjustedCooldown", new Type[] { typeof(Tool), typeof(Pawn), typeof(ThingDef), typeof(ThingDef) })]
    public static class LWOPWeaponAdjustedCooldownToolDefPatch
    {
        public static void Postfix(Pawn attacker, ref float __result)
        {
            if (LWOPMechUtility.HasLWOPWeaponMasteryApparel(attacker))
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(VerbProperties), "AdjustedCooldownTicks")]
    public static class LWOPWeaponAdjustedCooldownTicksPatch
    {
        public static void Postfix(Pawn attacker, ref int __result)
        {
            if (LWOPMechUtility.HasLWOPWeaponMasteryApparel(attacker))
            {
                __result = 1;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_StanceTracker), "SetStance")]
    public static class LWOPWeaponCooldownStancePatch
    {
        private static readonly FieldInfo StanceVerbField = AccessTools.Field(typeof(Stance_Busy), "verb");
        private static readonly FieldInfo StanceTicksLeftField = AccessTools.Field(typeof(Stance_Busy), "ticksLeft");

        public static void Prefix(ref Stance newStance)
        {
            Stance_Busy busyStance = newStance as Stance_Busy;
            if (busyStance == null || StanceVerbField == null || StanceTicksLeftField == null)
            {
                return;
            }

            Verb verb = StanceVerbField.GetValue(busyStance) as Verb;
            if (verb == null || !LWOPMechUtility.HasLWOPWeaponMasteryApparel(verb.CasterPawn))
            {
                return;
            }

            int ticksLeft = (int)StanceTicksLeftField.GetValue(busyStance);
            if (ticksLeft > 1)
            {
                StanceTicksLeftField.SetValue(busyStance, 1);
            }
        }
    }

    [HarmonyPatch(typeof(Ability), "Activate", new Type[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) })]
    public static class LWOPMeleeBlastNoCooldownPatch
    {
        private static readonly FieldInfo InCooldownField = AccessTools.Field(typeof(Ability), "inCooldown");
        private static readonly FieldInfo CooldownDurationField = AccessTools.Field(typeof(Ability), "cooldownDuration");
        private static readonly FieldInfo CooldownEndTickField = AccessTools.Field(typeof(Ability), "cooldownEndTick");

        public static void Postfix(Ability __instance, bool __result)
        {
            if (!__result || __instance == null || __instance.def == null || __instance.def.defName != "LWOPMeleeBlast")
            {
                return;
            }

            InCooldownField.SetValue(__instance, false);
            CooldownDurationField.SetValue(__instance, 0);
            CooldownEndTickField.SetValue(__instance, 0);
        }
    }

    [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
    public static class LWOPMechSpawnAbilityPatch
    {
        public static void Postfix(Pawn __instance)
        {
            if (LWOPPlayerOnlyUtility.TryRemoveUnauthorizedLWOPWorkerMech(__instance))
            {
                return;
            }

            LWOPMechUtility.EnsureLWOPMechAbilities(__instance);
            LWOPMechUtility.EnsureLWOPMechWorkTrackers(__instance);
            LWOPMechUtility.EnsureLWOPMechAnimalInteractionTracker(__instance);
            LWOPMechUtility.ClearLWOPMechAnimalMaster(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn), "TickInterval")]
    public static class LWOPMechAbilityMaintenancePatch
    {
        public static void Postfix(Pawn __instance, int delta)
        {
            if (__instance != null && __instance.IsHashIntervalTick(250))
            {
                if (LWOPPlayerOnlyUtility.TryRemoveUnauthorizedLWOPWorkerMech(__instance))
                {
                    return;
                }

                LWOPMechUtility.EnsureLWOPMechAbilities(__instance);
                LWOPMechUtility.EnsureLWOPMechWorkTrackers(__instance);
                LWOPMechUtility.EnsureLWOPMechAnimalInteractionTracker(__instance);
                LWOPMechUtility.ClearLWOPMechAnimalMaster(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(JobDriver_GatherAnimalBodyResources), "MakeNewToils")]
    public static class LWOPMechGatherAnimalBodyResourcesPatch
    {
        public static void Prefix(JobDriver_GatherAnimalBodyResources __instance)
        {
            LWOPMechUtility.EnsureLWOPMechWorkTrackers(__instance == null ? null : __instance.pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_SkillTracker), "SkillsTickInterval")]
    public static class LWOPMechSkillTrackerTickPatch
    {
        public static bool Prefix(Pawn_SkillTracker __instance)
        {
            return !LWOPMechUtility.IsLWOPWorkerMech(LWOPMechUtility.PawnForSkillTracker(__instance));
        }
    }

    [HarmonyPatch(typeof(SkillRecord), "Interval")]
    public static class LWOPMechSkillRecordIntervalPatch
    {
        public static bool Prefix(Pawn ___pawn)
        {
            return !LWOPMechUtility.IsLWOPWorkerMech(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_MechanitorTracker), "CanCommandTo")]
    public static class LWOPMechanitorFullMapCommandPatch
    {
        public static void Postfix(Pawn_MechanitorTracker __instance, ref bool __result)
        {
            if (!__result && __instance != null && LWOPMechUtility.WearsLWOPMechanitorCommandApparel(__instance.Pawn))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_MechanitorTracker), "DrawCommandRadius")]
    public static class LWOPMechanitorCommandRadiusDrawPatch
    {
        public static bool Prefix(Pawn_MechanitorTracker __instance)
        {
            return __instance == null || !LWOPMechUtility.WearsLWOPMechanitorCommandApparel(__instance.Pawn);
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), "InMechanitorCommandRange")]
    public static class LWOPMechIndependentCommandRangePatch
    {
        public static void Postfix(Pawn mech, ref bool __result)
        {
            if (LWOPMechUtility.IsPlayerLWOPWorkerMech(mech))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), "CanDraftMech")]
    public static class LWOPMechIndependentDraftPatch
    {
        public static bool Prefix(Pawn mech, ref AcceptanceReport __result)
        {
            if (!LWOPMechUtility.IsPlayerLWOPWorkerMech(mech))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), "CanControlMech")]
    public static class LWOPMechIndependentControlPatch
    {
        public static bool Prefix(Pawn mech, ref AcceptanceReport __result)
        {
            if (!LWOPMechUtility.IsPlayerLWOPWorkerMech(mech))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility), "IsPlayerOverseerSubject")]
    public static class LWOPMechIndependentPlayerSubjectPatch
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (LWOPMechUtility.IsPlayerLWOPWorkerMech(pawn))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "get_IsColonyMechPlayerControlled")]
    public static class LWOPMechIndependentColonyControlPatch
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (LWOPMechUtility.IsPlayerLWOPWorkerMech(__instance))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "get_IsPlayerControlled")]
    public static class LWOPMechIndependentPlayerControlPatch
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (LWOPMechUtility.IsPlayerLWOPWorkerMech(__instance))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), "get_CanTakeOrder")]
    public static class LWOPMechIndependentCanTakeOrderPatch
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (LWOPMechUtility.IsPlayerLWOPWorkerMech(__instance))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_DraftController), "get_ShowDraftGizmo")]
    public static class LWOPMechIndependentDraftGizmoPatch
    {
        private static readonly FieldInfo PawnField = AccessTools.Field(typeof(Pawn_DraftController), "pawn");

        public static void Postfix(Pawn_DraftController __instance, ref bool __result)
        {
            Pawn pawn = PawnField == null ? null : PawnField.GetValue(__instance) as Pawn;
            if (LWOPMechUtility.IsPlayerLWOPWorkerMech(pawn))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(ThinkNode_ConditionalPlayerControlledMech), "Satisfied")]
    public static class LWOPMechIndependentThinkNodePatch
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (!LWOPMechUtility.IsPlayerLWOPWorkerMech(pawn))
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompOverseerSubject), "get_State")]
    public static class LWOPMechIndependentOverseerStatePatch
    {
        public static void Postfix(CompOverseerSubject __instance, ref OverseerSubjectState __result)
        {
            if (__instance != null && LWOPMechUtility.IsPlayerLWOPWorkerMech(__instance.Parent))
            {
                __result = OverseerSubjectState.Overseen;
            }
        }
    }

    [HarmonyPatch(typeof(CompOverseerSubject), "CanGoFeral")]
    public static class LWOPMechIndependentFeralCheckPatch
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (!LWOPMechUtility.IsPlayerLWOPWorkerMech(pawn))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompOverseerSubject), "TryMakeFeral")]
    public static class LWOPMechIndependentTryMakeFeralPatch
    {
        public static bool Prefix(CompOverseerSubject __instance, ref bool __result)
        {
            if (__instance == null || !LWOPMechUtility.IsPlayerLWOPWorkerMech(__instance.Parent))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(CompOverseerSubject), "Notify_DisconnectedFromOverseer")]
    public static class LWOPMechIndependentDisconnectPatch
    {
        public static bool Prefix(CompOverseerSubject __instance)
        {
            return __instance == null || !LWOPMechUtility.IsPlayerLWOPWorkerMech(__instance.Parent);
        }
    }

    [HarmonyPatch(typeof(Command_VerbTarget), "get_IconDrawColor")]
    public static class LWOPVerbCommandIconColorPatch
    {
        public static void Postfix(Command_VerbTarget __instance, ref Color __result)
        {
            if (LWOPMechUtility.IsLWOPFixedColorVerbCommand(__instance))
            {
                __result = Color.white;
            }
        }
    }

    [HarmonyPatch]
    public static class LWOPAllowToolPartyHuntMechCompatibilityPatch
    {
        public static bool Prepare()
        {
            return AccessTools.TypeByName("AllowTool.Patches.JobDriverWait_CheckForAutoAttack_Patch") != null;
        }

        public static MethodBase TargetMethod()
        {
            Type patchType = AccessTools.TypeByName("AllowTool.Patches.JobDriverWait_CheckForAutoAttack_Patch");
            return patchType == null ? null : AccessTools.Method(patchType, "DoPartyHunting", new Type[] { typeof(JobDriver_Wait) });
        }

        public static bool Prefix(JobDriver_Wait __instance)
        {
            Pawn pawn = __instance == null ? null : __instance.pawn;
            return !LWOPMechUtility.IsLWOPWorkerMech(pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_WorkSettings), "CacheWorkGiversInOrder")]
    public static class LWOPMechWorkSettingsCachePatch
    {
        public static void Prefix(Pawn_WorkSettings __instance, ref List<WorkGiverDef> __state)
        {
            __state = LWOPMechUtility.AllowAllWorkGiversForLWOPMech(LWOPMechUtility.PawnForWorkSettings(__instance));
        }

        public static void Postfix(List<WorkGiverDef> __state)
        {
            LWOPMechUtility.RestoreMechWorkGivers(__state);
        }
    }

    [HarmonyPatch(typeof(WorkGiver), "ShouldSkip")]
    public static class LWOPMechWorkGiverShouldSkipPatch
    {
        public static void Prefix(Pawn pawn, ref List<WorkGiverDef> __state)
        {
            __state = LWOPMechUtility.AllowAllWorkGiversForLWOPMech(pawn);
        }

        public static void Postfix(List<WorkGiverDef> __state)
        {
            LWOPMechUtility.RestoreMechWorkGivers(__state);
        }
    }

    public static class LWOPAnimalHandlingPatchTargets
    {
        public static MethodBase CanInteractWithAnimalMethod()
        {
            return AccessTools.Method(
                typeof(WorkGiver_InteractAnimal),
                "CanInteractWithAnimal",
                new Type[]
                {
                    typeof(Pawn),
                    typeof(Pawn),
                    typeof(string).MakeByRefType(),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool)
                });
        }
    }

    [HarmonyPatch]
    public static class LWOPMechAnimalInteractPatch
    {
        public static MethodBase TargetMethod()
        {
            return LWOPAnimalHandlingPatchTargets.CanInteractWithAnimalMethod();
        }

        public static bool Prefix(
            Pawn pawn,
            Pawn animal,
            ref string jobFailReason,
            bool forced,
            bool canInteractWhileSleeping,
            bool ignoreSkillRequirements,
            bool canInteractWhileRoaming,
            ref bool __result)
        {
            if (!LWOPMechUtility.IsLWOPWorkerMech(pawn))
            {
                return true;
            }

            __result = LWOPMechUtility.CanInteractWithAnimalAsLWOPMech(
                pawn,
                animal,
                out jobFailReason,
                forced,
                canInteractWhileSleeping,
                ignoreSkillRequirements,
                canInteractWhileRoaming);
            return false;
        }
    }

    [HarmonyPatch(typeof(Toils_Interpersonal), "TryTrain")]
    public static class LWOPMechTryTrainPatch
    {
        public static void Postfix(Toil __result, TargetIndex traineeInd)
        {
            if (__result == null)
            {
                return;
            }

            Action originalInitAction = __result.initAction;
            __result.initAction = delegate
            {
                Pawn actor = __result.actor;
                if (!LWOPMechUtility.IsLWOPWorkerMech(actor))
                {
                    if (originalInitAction != null)
                    {
                        originalInitAction();
                    }
                    return;
                }

                Pawn animal = actor.jobs == null || actor.jobs.curJob == null ? null : actor.jobs.curJob.GetTarget(traineeInd).Thing as Pawn;
                LWOPMechUtility.TryTrainAnimalAsLWOPMech(actor, animal);
            };
        }
    }

    [HarmonyPatch(typeof(Toils_Interpersonal), "TryRecruit")]
    public static class LWOPMechTryRecruitPatch
    {
        public static void Postfix(Toil __result, TargetIndex recruiteeInd)
        {
            if (__result == null)
            {
                return;
            }

            Action originalInitAction = __result.initAction;
            __result.initAction = delegate
            {
                Pawn actor = __result.actor;
                Pawn animal = actor == null || actor.jobs == null || actor.jobs.curJob == null ? null : actor.jobs.curJob.GetTarget(recruiteeInd).Thing as Pawn;
                if (!LWOPMechUtility.IsLWOPWorkerMech(actor) || animal == null || !animal.AnimalOrWildMan())
                {
                    if (originalInitAction != null)
                    {
                        originalInitAction();
                    }
                    return;
                }

                LWOPMechUtility.TryTameAnimalAsLWOPMech(actor, animal);
            };
        }
    }

    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractWith")]
    public static class LWOPMechAnimalInteractionTrackerPatch
    {
        private static readonly FieldInfo PawnField = AccessTools.Field(typeof(Pawn_InteractionsTracker), "pawn");

        public static bool Prefix(Pawn_InteractionsTracker __instance, Pawn recipient, InteractionDef intDef, ref bool __result)
        {
            Pawn pawn = PawnField == null ? null : PawnField.GetValue(__instance) as Pawn;
            if (!LWOPMechUtility.IsLWOPWorkerMech(pawn))
            {
                return true;
            }

            if (LWOPMechUtility.IsLWOPMechAnimalInteraction(pawn, recipient, intDef))
            {
                __result = recipient != null && recipient.Spawned && (intDef == InteractionDefOf.AnimalChat || recipient.Awake());
                return false;
            }

            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Need_MechEnergy), "NeedInterval")]
    public static class LWOPMechEnergySlowDrainPatch
    {
        private static readonly FieldInfo PawnField = AccessTools.Field(typeof(Need), "pawn");

        public static void Postfix(Need_MechEnergy __instance)
        {
            Pawn pawn = PawnField == null ? null : PawnField.GetValue(__instance) as Pawn;
            if (!LWOPMechUtility.IsLWOPWorkerMech(pawn))
            {
                return;
            }

            if (__instance.CurLevelPercentage < 0.95f)
            {
                __instance.CurLevelPercentage = 0.95f;
            }
        }
    }

    [HarmonyPatch(typeof(Building_MechCharger), "GenerateWastePack")]
    public static class LWOPMechChargerWastePatch
    {
        private static readonly FieldInfo CurrentlyChargingMechField = AccessTools.Field(typeof(Building_MechCharger), "currentlyChargingMech");

        public static bool Prefix(Building_MechCharger __instance)
        {
            Pawn pawn = CurrentlyChargingMechField == null ? null : CurrentlyChargingMechField.GetValue(__instance) as Pawn;
            return !LWOPMechUtility.IsLWOPWorkerMech(pawn);
        }
    }
}
