using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LWOP.Buildings
{
    [StaticConstructorOnStartup]
    public static class LWOPHydroponicsPatches
    {
        static LWOPHydroponicsPatches()
        {
            new Harmony("com.colonyseries.lwop.Hydroponics").PatchAll();
        }
    }

    public class Building_LWOPHydroponicsBasin : Building_PlantGrower, IThingHolder
    {
        private const float DefaultGrowthTemperature = 21f;
        private const int HarvestYieldMultiplier = 2;
        private const float EmptyGlowRadius = 6f;
        private const float StoredGlowRadius = 3f;
        private static readonly ColorInt EmptyGlowColor = new ColorInt(217, 217, 180, 0);
        private static readonly ColorInt StoredGlowColor = new ColorInt(95, 210, 120, 0);

        private CompForbiddable forbiddableComp;
        private ThingOwner<Thing> innerContainer;
        private bool autoStoreHarvests = true;
        private List<IntVec3> cachedOccupiedCells;
        private readonly List<Thing> tmpStoredThings = new List<Thing>();
        private static readonly StringBuilder InspectBuilder = new StringBuilder();

        public Building_LWOPHydroponicsBasin()
        {
            EnsureInnerContainer();
        }

        public override void PostMake()
        {
            base.PostMake();
            EnsureInnerContainer();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            cachedOccupiedCells = null;
            EnsureInnerContainer();
            base.SpawnSetup(map, respawningAfterLoad);
            forbiddableComp = GetComp<CompForbiddable>();
            RefreshStorageGlow();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
            cachedOccupiedCells = null;
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (innerContainer != null && innerContainer.Any)
            {
                if (Spawned && Map != null && mode != DestroyMode.Vanish)
                {
                    TryEjectAllStored();
                }
                else
                {
                    innerContainer.ClearAndDestroyContentsOrPassToWorld(mode);
                }
            }
            base.Destroy(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_Values.Look(ref autoStoreHarvests, "autoStoreHarvests", true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                EnsureInnerContainer();
            }
        }

        public override void TickRare()
        {
            base.TickRare();
            if (!Spawned ||
                Map == null ||
                forbiddableComp != null && forbiddableComp.Forbidden)
            {
                return;
            }

            CutBlightedPlants();
            HarvestMaturePlants();
            SowMissingPlants();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                if (gizmo is Command_SetPlantToGrow)
                {
                    continue;
                }
                yield return gizmo;
            }

            yield return new Command_Action
            {
                defaultLabel = "LWOPHydroponicsSelectPlant".Translate(),
                defaultDesc = "LWOPHydroponicsSelectPlantDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LWOPHydroponicsSelectPlant", false) ?? BaseContent.BadTex,
                action = delegate
                {
                    Find.WindowStack.Add(new Dialog_LWOPHydroponicsPlantSelection(this));
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "LWOPHydroponicsOpenInventory".Translate(),
                defaultDesc = "LWOPHydroponicsOpenInventoryDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LWOPHydroponicsInventory", false) ?? BaseContent.BadTex,
                action = delegate
                {
                    Find.WindowStack.Add(new Dialog_LWOPHydroponicsInventory(this));
                }
            };

            yield return new Command_Toggle
            {
                defaultLabel = "LWOPHydroponicsAutoStore".Translate(),
                defaultDesc = "LWOPHydroponicsAutoStoreDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LWOPHydroponicsAutoStore", false) ?? BaseContent.BadTex,
                isActive = () => autoStoreHarvests,
                toggleAction = delegate
                {
                    autoStoreHarvests = !autoStoreHarvests;
                    Messages.Message((autoStoreHarvests ? "LWOPHydroponicsAutoStoreEnabled" : "LWOPHydroponicsAutoStoreDisabled").Translate(), this, MessageTypeDefOf.NeutralEvent, false);
                }
            };

            yield return new Command_Action
            {
                defaultLabel = "LWOPHydroponicsEjectAll".Translate(),
                defaultDesc = "LWOPHydroponicsEjectAllDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LWOPHydroponicsEjectAll", false) ?? BaseContent.BadTex,
                action = delegate
                {
                    if (!TryEjectAllStored())
                    {
                        Messages.Message("LWOPHydroponicsInventoryEmpty".Translate(), this, MessageTypeDefOf.RejectInput, false);
                    }
                }
            };
        }

        public override string GetInspectString()
        {
            InspectBuilder.Clear();
            InspectBuilder.Append(base.GetInspectString());
            GetStoredThings(tmpStoredThings);
            InspectBuilder.AppendLineIfNotEmpty();
            InspectBuilder.Append("LWOPHydroponicsAutoStoreInspect".Translate((autoStoreHarvests ? "LWOPHydroponicsAutoStoreOn" : "LWOPHydroponicsAutoStoreOff").Translate()));
            if (tmpStoredThings.Count > 0)
            {
                InspectBuilder.AppendLineIfNotEmpty();
                InspectBuilder.Append("LWOPHydroponicsInventoryInspect".Translate(tmpStoredThings.Count.ToString(), GetStoredTotalCount().ToString()));
            }
            return InspectBuilder.ToString();
        }

        public new IThingHolder ParentHolder
        {
            get { return null; }
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            EnsureInnerContainer();
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            EnsureInnerContainer();
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, innerContainer);
        }

        public IEnumerable<IntVec3> AllSlotCells()
        {
            if (!Spawned)
            {
                yield break;
            }

            foreach (IntVec3 cell in GenAdj.CellsOccupiedBy(this))
            {
                yield return cell;
            }
        }

        public List<IntVec3> AllSlotCellsList()
        {
            if (cachedOccupiedCells == null)
            {
                cachedOccupiedCells = AllSlotCells().ToList();
            }
            return cachedOccupiedCells;
        }

        public bool HasStoredContents()
        {
            EnsureInnerContainer();
            return innerContainer.Any;
        }

        public void GetStoredThings(List<Thing> outThings)
        {
            outThings.Clear();
            EnsureInnerContainer();
            foreach (Thing thing in innerContainer)
            {
                outThings.Add(thing);
            }
        }

        public int GetStoredTotalCount()
        {
            EnsureInnerContainer();
            return innerContainer.TotalStackCount;
        }

        public bool TryEjectThing(Thing thing)
        {
            EnsureInnerContainer();
            if (thing == null || !innerContainer.Contains(thing) || !Spawned || Map == null)
            {
                return false;
            }

            Thing resultingThing;
            bool result = innerContainer.TryDrop(thing, Position, Map, ThingPlaceMode.Near, thing.stackCount, out resultingThing, EjectedThingPlaced, cell => CanDropHarvestAroundBasin(cell, Map));
            NotifyStoredContentsChanged();
            return result;
        }

        public bool TryEjectAllStored()
        {
            EnsureInnerContainer();
            if (!innerContainer.Any || !Spawned || Map == null)
            {
                return false;
            }

            bool result = innerContainer.TryDropAll(Position, Map, ThingPlaceMode.Near, EjectedThingPlaced, cell => CanDropHarvestAroundBasin(cell, Map), true);
            NotifyStoredContentsChanged();
            return result;
        }

        public void NotifyPlantDefToGrowChanged(ThingDef oldPlantDef, ThingDef newPlantDef)
        {
            if (!Spawned || Map == null || oldPlantDef == newPlantDef)
            {
                return;
            }

            ReplaceExistingPlantsForNewSelection(newPlantDef);
            SowMissingPlants();
            NotifyVisualChanged();
        }

        public void GetSelectablePlants(List<ThingDef> outPlants)
        {
            outPlants.Clear();
            foreach (ThingDef plantDef in DefDatabase<ThingDef>.AllDefs)
            {
                if (CanSelectPlantDef(plantDef))
                {
                    outPlants.Add(plantDef);
                }
            }
            outPlants.SortBy(plantDef => plantDef.LabelCap.ToString());
        }

        public static bool IsPlantOnLWOPHydroponics(Plant plant)
        {
            if (plant == null || !plant.Spawned || plant.Map == null)
            {
                return false;
            }
            return plant.Position.GetEdifice(plant.Map) is Building_LWOPHydroponicsBasin;
        }

        public bool HasContentsForVisuals()
        {
            return HasStoredContents();
        }

        private void EnsureInnerContainer()
        {
            if (innerContainer == null)
            {
                innerContainer = new ThingOwner<Thing>(this, false, LookMode.Deep);
            }
            innerContainer.dontTickContents = true;
        }

        public static float TemperatureFactorAtDefaultGrowthTemperature(Plant plant)
        {
            if (plant == null || plant.def == null)
            {
                return 1f;
            }
            return PlantUtility.GrowthRateFactorFor_Temperature(plant.def, DefaultGrowthTemperature);
        }

        private void CutBlightedPlants()
        {
            foreach (Plant plant in PlantsOnMe.ToList())
            {
                if (plant == null || plant.Destroyed || !plant.Blighted)
                {
                    continue;
                }

                Blight blight = plant.Blight;
                if (blight != null && !blight.Destroyed)
                {
                    blight.Destroy();
                }

                IntVec3 harvestCell = plant.Position;
                Map harvestMap = plant.Map;
                List<Thing> harvests = MakeHarvestProducts(plant, false);
                if (!plant.Destroyed)
                {
                    plant.Destroy(DestroyMode.Vanish);
                }
                StoreHarvests(harvests, harvestCell, harvestMap);
                NotifyStoredContentsChanged();
            }
        }

        private void HarvestMaturePlants()
        {
            foreach (Plant plant in PlantsOnMe.ToList())
            {
                if (!CanAutoHarvestPlant(plant, true))
                {
                    continue;
                }

                IntVec3 harvestCell = plant.Position;
                Map harvestMap = plant.Map;
                List<Thing> harvests = MakeHarvestProducts(plant, true);
                if (harvests.Count == 0)
                {
                    continue;
                }

                FinishAutomaticHarvest(plant);
                StoreHarvests(harvests, harvestCell, harvestMap);
            }
        }

        private bool CanAutoHarvestPlant(Plant plant, bool requireFullyGrown)
        {
            if (plant == null || plant.Destroyed || plant.def == null || plant.def.plant == null)
            {
                return false;
            }
            if (!plant.HarvestableNow || requireFullyGrown && plant.Growth < 1f)
            {
                return false;
            }

            return true;
        }

        private List<Thing> MakeHarvestProducts(Plant plant, bool requireCleanYield)
        {
            List<Thing> harvests = new List<Thing>();
            if (!CanAutoHarvestPlant(plant, false))
            {
                return harvests;
            }

            int yield = ApplyHarvestYieldMultiplier(requireCleanYield ? plant.YieldNow() : YieldNowAllowingRecentBlightCut(plant));
            ThingDef harvestedThingDef = plant.def.plant.harvestedThingDef;
            if (yield > 0 && harvestedThingDef != null)
            {
                Thing harvest = ThingMaker.MakeThing(harvestedThingDef);
                harvest.stackCount = yield;
                harvests.Add(harvest);
            }

            if (requireCleanYield && plant.HarvestableNow)
            {
                foreach (ThingComp comp in plant.AllComps)
                {
                    foreach (ThingDefCountClass extraYield in comp.GetAdditionalHarvestYield())
                    {
                        if (extraYield == null || extraYield.thingDef == null || extraYield.count <= 0)
                        {
                            continue;
                        }

                        Thing extraHarvest = ThingMaker.MakeThing(extraYield.thingDef);
                        extraHarvest.stackCount = ApplyHarvestYieldMultiplier(extraYield.count);
                        harvests.Add(extraHarvest);
                    }
                }
            }

            return harvests;
        }

        private static int ApplyHarvestYieldMultiplier(int yield)
        {
            return yield <= 0 ? 0 : yield * HarvestYieldMultiplier;
        }

        private static int YieldNowAllowingRecentBlightCut(Plant plant)
        {
            if (plant == null ||
                plant.def == null ||
                plant.def.plant == null ||
                !plant.HarvestableNow ||
                plant.def.plant.harvestYield <= 0f)
            {
                return 0;
            }

            float harvestYield = plant.def.plant.harvestYield;
            float growthFactor = Mathf.InverseLerp(plant.def.plant.harvestMinGrowth, 1f, plant.Growth);
            harvestYield *= 0.5f + growthFactor * 0.5f;
            harvestYield *= Mathf.Lerp(0.5f, 1f, (float)plant.HitPoints / (float)plant.MaxHitPoints);
            if (plant.def.plant.harvestYieldAffectedByDifficulty)
            {
                harvestYield *= Find.Storyteller.difficulty.cropYieldFactor;
            }
            return GenMath.RoundRandom(harvestYield);
        }

        private void FinishAutomaticHarvest(Plant plant)
        {
            if (plant == null || plant.Destroyed)
            {
                return;
            }

            if (plant.def.plant.HarvestDestroys)
            {
                plant.Destroy(DestroyMode.Vanish);
                return;
            }

            plant.Growth = plant.def.plant.harvestAfterGrowth;
            if (plant.Spawned && plant.Map != null)
            {
                plant.Map.mapDrawer.MapMeshDirty(plant.Position, MapMeshFlagDefOf.Things);
            }
            NotifyVisualChanged();
        }

        private void DropHarvestsNearBasin(List<Thing> harvests, IntVec3 preferredCell, Map harvestMap)
        {
            if (harvests == null)
            {
                return;
            }

            for (int i = 0; i < harvests.Count; i++)
            {
                DropHarvestNearBasin(harvests[i], preferredCell, harvestMap);
            }
        }

        private void DropHarvestNearBasin(Thing harvest, IntVec3 preferredCell, Map harvestMap)
        {
            if (harvest == null || harvestMap == null)
            {
                return;
            }

            Thing placedThing;
            GenPlace.TryPlaceThing(harvest, preferredCell, harvestMap, ThingPlaceMode.Near, out placedThing, null, cell => CanDropHarvestAroundBasin(cell, harvestMap));

            if (placedThing != null && !placedThing.Destroyed)
            {
                EjectedThingPlaced(placedThing, placedThing.stackCount);
            }
        }

        private bool CanDropHarvestAroundBasin(IntVec3 cell, Map harvestMap)
        {
            if (harvestMap == null || !cell.InBounds(harvestMap))
            {
                return false;
            }
            if (AllSlotCellsList().Contains(cell))
            {
                return false;
            }
            return cell.GetEdifice(harvestMap) == null || !(cell.GetEdifice(harvestMap) is Building_LWOPHydroponicsBasin);
        }

        private void StoreHarvests(List<Thing> harvests, IntVec3 fallbackCell, Map fallbackMap)
        {
            if (harvests == null)
            {
                return;
            }

            if (!autoStoreHarvests)
            {
                DropHarvestsNearBasin(harvests, fallbackCell, fallbackMap);
                return;
            }

            EnsureInnerContainer();
            for (int i = 0; i < harvests.Count; i++)
            {
                Thing harvest = harvests[i];
                if (harvest == null)
                {
                    continue;
                }

                if (!innerContainer.TryAdd(harvest, true))
                {
                    DropHarvestNearBasin(harvest, fallbackCell, fallbackMap);
                }
            }
            NotifyStoredContentsChanged();
        }

        private void EjectedThingPlaced(Thing thing, int count)
        {
            if (thing != null && !thing.Destroyed)
            {
                thing.SetForbidden(false, false);
            }
        }

        private void SowMissingPlants()
        {
            ThingDef plantDef = GetPlantDefToGrow();
            if (plantDef == null || plantDef.plant == null || !CanAcceptSowNow())
            {
                return;
            }

            foreach (IntVec3 cell in AllSlotCellsList())
            {
                if (cell.GetPlant(Map) != null)
                {
                    continue;
                }
                if (!CanAutoSow(plantDef, cell))
                {
                    continue;
                }

                Plant plant = GenSpawn.Spawn(plantDef, cell, Map) as Plant;
                if (plant == null)
                {
                    continue;
                }

                plant.Growth = 0.0001f;
                plant.sown = true;
                Map.mapDrawer.MapMeshDirty(cell, MapMeshFlagDefOf.Things);
                NotifyVisualChanged();
            }
        }

        private bool CanAutoSow(ThingDef plantDef, IntVec3 cell)
        {
            if (!CanUsePlantDef(plantDef) || Map == null || !cell.InBounds(Map))
            {
                return false;
            }
            return true;
        }

        private bool CanSelectPlantDef(ThingDef plantDef)
        {
            return CanUsePlantDef(plantDef);
        }

        private static bool CanUsePlantDef(ThingDef plantDef)
        {
            if (plantDef == null ||
                plantDef.plant == null ||
                plantDef.category != ThingCategory.Plant ||
                !plantDef.plant.Sowable)
            {
                return false;
            }

            List<ResearchProjectDef> sowResearchPrerequisites = plantDef.plant.sowResearchPrerequisites;
            if (sowResearchPrerequisites == null)
            {
                return true;
            }

            for (int i = 0; i < sowResearchPrerequisites.Count; i++)
            {
                ResearchProjectDef researchProject = sowResearchPrerequisites[i];
                if (researchProject != null && !researchProject.IsFinished)
                {
                    return false;
                }
            }
            return true;
        }

        private void ReplaceExistingPlantsForNewSelection(ThingDef newPlantDef)
        {
            foreach (Plant plant in PlantsOnMe.ToList())
            {
                if (plant == null || plant.Destroyed || plant.def == newPlantDef)
                {
                    continue;
                }

                Blight blight = plant.Blight;
                if (blight != null && !blight.Destroyed)
                {
                    blight.Destroy();
                }

                IntVec3 harvestCell = plant.Position;
                Map harvestMap = plant.Map;
                List<Thing> harvests = MakeHarvestProducts(plant, false);
                if (!plant.Destroyed)
                {
                    plant.Destroy(DestroyMode.Vanish);
                }
                StoreHarvests(harvests, harvestCell, harvestMap);
            }
        }

        private void NotifyStoredContentsChanged()
        {
            RefreshStorageGlow();
            NotifyVisualChanged();
        }

        private void RefreshStorageGlow()
        {
            CompGlower glower = GetComp<CompGlower>();
            if (glower == null)
            {
                return;
            }

            bool hasStoredContents = HasStoredContents();
            ColorInt desiredColor = hasStoredContents ? StoredGlowColor : EmptyGlowColor;
            float desiredRadius = hasStoredContents ? StoredGlowRadius : EmptyGlowRadius;
            if (!glower.GlowColor.Equals(desiredColor))
            {
                glower.GlowColor = desiredColor;
            }
            if (!Mathf.Approximately(glower.GlowRadius, desiredRadius))
            {
                glower.GlowRadius = desiredRadius;
            }
            if (Spawned && Map != null)
            {
                glower.UpdateLit(Map);
            }
        }

        private void NotifyVisualChanged()
        {
            if (!Spawned || Map == null)
            {
                return;
            }

            List<IntVec3> cells = AllSlotCellsList();
            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                Map.mapDrawer.MapMeshDirty(cells[cellIndex], MapMeshFlagDefOf.Things);
            }
        }
    }

    public class Dialog_LWOPHydroponicsInventory : Window
    {
        private readonly Building_LWOPHydroponicsBasin basin;
        private readonly List<Thing> things = new List<Thing>();
        private Vector2 scrollPosition;

        public Dialog_LWOPHydroponicsInventory(Building_LWOPHydroponicsBasin basin)
        {
            this.basin = basin;
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
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "LWOPHydroponicsInventoryTitle".Translate());
            Text.Font = GameFont.Small;

            basin.GetStoredThings(things);
            if (things.Count == 0)
            {
                Widgets.Label(new Rect(0f, 44f, inRect.width, 32f), "LWOPHydroponicsInventoryEmpty".Translate());
                return;
            }

            Rect ejectAllRect = new Rect(inRect.width - 128f, 0f, 128f, 32f);
            if (Widgets.ButtonText(ejectAllRect, "LWOPHydroponicsEjectAllShort".Translate()))
            {
                if (basin.TryEjectAllStored())
                {
                    Messages.Message("LWOPHydroponicsEjectedAll".Translate(), basin, MessageTypeDefOf.PositiveEvent, false);
                }
                Close();
                return;
            }

            Rect scrollRect = new Rect(0f, 42f, inRect.width, inRect.height - 42f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, Mathf.Max(scrollRect.height, things.Count * 36f));
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing == null)
                {
                    continue;
                }

                Rect rowRect = new Rect(0f, i * 36f, viewRect.width, 32f);
                Rect iconRect = new Rect(rowRect.x, rowRect.y + 2f, 28f, 28f);
                Widgets.ThingIcon(iconRect, thing, 1f, null, true, 1f, false);

                Rect buttonRect = new Rect(rowRect.xMax - 118f, rowRect.y, 118f, 30f);
                Rect countRect = new Rect(buttonRect.x - 86f, rowRect.y + 4f, 76f, 28f);
                Rect labelRect = new Rect(iconRect.xMax + 8f, rowRect.y + 4f, countRect.x - iconRect.xMax - 16f, 28f);
                Widgets.Label(labelRect, thing.LabelCapNoCount);

                TextAnchor oldAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(countRect, "x" + thing.stackCount);
                Text.Anchor = oldAnchor;

                if (Widgets.ButtonText(buttonRect, "LWOPHydroponicsRetrieve".Translate()))
                {
                    if (basin.TryEjectThing(thing))
                    {
                        Messages.Message("LWOPHydroponicsRetrieved".Translate(thing.LabelCap), basin, MessageTypeDefOf.PositiveEvent, false);
                    }
                    else
                    {
                        Messages.Message("LWOPHydroponicsRetrieveFailed".Translate(), basin, MessageTypeDefOf.RejectInput, false);
                    }
                    break;
                }
            }

            Widgets.EndScrollView();
        }
    }

    public class Dialog_LWOPHydroponicsPlantSelection : Window
    {
        private readonly Building_LWOPHydroponicsBasin basin;
        private readonly List<Building_LWOPHydroponicsBasin> targetBasins = new List<Building_LWOPHydroponicsBasin>();
        private readonly List<ThingDef> plants = new List<ThingDef>();
        private Vector2 scrollPosition;
        private string filter = string.Empty;

        public Dialog_LWOPHydroponicsPlantSelection(Building_LWOPHydroponicsBasin basin)
        {
            this.basin = basin;
            AddTargetBasinsFromSelection(basin);
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            forcePause = false;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(620f, 640f); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "LWOPHydroponicsPlantSelectionTitle".Translate());
            Text.Font = GameFont.Small;

            ThingDef currentPlant = basin.GetPlantDefToGrow();
            string currentPlantLabel = currentPlant == null ? "None".Translate().ToString() : currentPlant.LabelCap.ToString();
            Widgets.Label(new Rect(0f, 38f, inRect.width, 24f), "LWOPHydroponicsCurrentPlant".Translate(currentPlantLabel));
            filter = Widgets.TextField(new Rect(0f, 68f, inRect.width, 32f), filter);

            basin.GetSelectablePlants(plants);
            string normalizedFilter = filter.NullOrEmpty() ? string.Empty : filter.ToLowerInvariant();
            if (!normalizedFilter.NullOrEmpty())
            {
                plants.RemoveAll(plantDef => !plantDef.LabelCap.ToString().ToLowerInvariant().Contains(normalizedFilter) && !plantDef.defName.ToLowerInvariant().Contains(normalizedFilter));
            }

            if (plants.Count == 0)
            {
                Widgets.Label(new Rect(0f, 112f, inRect.width, 32f), "LWOPHydroponicsNoSelectablePlants".Translate());
                return;
            }

            Rect scrollRect = new Rect(0f, 110f, inRect.width, inRect.height - 110f);
            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, Mathf.Max(scrollRect.height, plants.Count * 36f));
            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            for (int i = 0; i < plants.Count; i++)
            {
                ThingDef plantDef = plants[i];
                Rect rowRect = new Rect(0f, i * 36f, viewRect.width, 32f);
                Widgets.Label(new Rect(rowRect.x, rowRect.y + 4f, rowRect.width - 126f, 28f), plantDef.LabelCap);

                Rect buttonRect = new Rect(rowRect.xMax - 118f, rowRect.y, 118f, 30f);
                if (plantDef == currentPlant)
                {
                    Widgets.Label(buttonRect, "LWOPHydroponicsCurrentPlantShort".Translate());
                }
                else if (Widgets.ButtonText(buttonRect, "LWOPHydroponicsSelectPlantShort".Translate()))
                {
                    SetPlantDefToGrowForTargets(plantDef);
                    Close();
                    break;
                }
            }

            Widgets.EndScrollView();
        }

        private void AddTargetBasinsFromSelection(Building_LWOPHydroponicsBasin fallbackBasin)
        {
            targetBasins.Clear();
            if (Find.Selector != null)
            {
                List<object> selectedObjects = Find.Selector.SelectedObjectsListForReading;
                for (int i = 0; i < selectedObjects.Count; i++)
                {
                    Building_LWOPHydroponicsBasin selectedBasin = selectedObjects[i] as Building_LWOPHydroponicsBasin;
                    if (selectedBasin != null && !targetBasins.Contains(selectedBasin))
                    {
                        targetBasins.Add(selectedBasin);
                    }
                }
            }

            if (targetBasins.Count == 0 && fallbackBasin != null)
            {
                targetBasins.Add(fallbackBasin);
            }
        }

        private void SetPlantDefToGrowForTargets(ThingDef plantDef)
        {
            for (int i = 0; i < targetBasins.Count; i++)
            {
                Building_LWOPHydroponicsBasin targetBasin = targetBasins[i];
                if (targetBasin == null || targetBasin.Destroyed)
                {
                    continue;
                }

                targetBasin.SetPlantDefToGrow(plantDef);
            }
        }
    }

    public static class LWOPHydroponicsVisualUtility
    {
        public static bool CountsAsContents(Thing thing)
        {
            if (thing == null || thing is Building_LWOPHydroponicsBasin)
            {
                return false;
            }
            if (thing is Plant || IsStoredThingToHide(thing))
            {
                return true;
            }
            return false;
        }

        public static bool ShouldHideThingGraphic(Thing thing)
        {
            if (thing == null ||
                thing.def == null ||
                thing is Building_LWOPHydroponicsBasin ||
                !thing.Spawned ||
                thing.Map == null)
            {
                return false;
            }

            if (!(thing.Position.GetEdifice(thing.Map) is Building_LWOPHydroponicsBasin))
            {
                return false;
            }

            return IsStoredThingToHide(thing) || thing is Blight;
        }

        private static bool IsStoredThingToHide(Thing thing)
        {
            if (thing == null || thing is Plant)
            {
                return false;
            }
            if (thing is Corpse || thing is MinifiedThing)
            {
                return true;
            }
            return thing.def != null && thing.def.category == ThingCategory.Item;
        }
    }

    [HarmonyPatch(typeof(Plant), "get_GrowthRateFactor_Light")]
    public static class LWOPHydroponicsPlantLightPatch
    {
        public static void Postfix(Plant __instance, ref float __result)
        {
            if (Building_LWOPHydroponicsBasin.IsPlantOnLWOPHydroponics(__instance))
            {
                __result = 1f;
            }
        }
    }

    [HarmonyPatch(typeof(Plant), "get_GrowthRateFactor_Temperature")]
    public static class LWOPHydroponicsPlantTemperaturePatch
    {
        public static void Postfix(Plant __instance, ref float __result)
        {
            if (Building_LWOPHydroponicsBasin.IsPlantOnLWOPHydroponics(__instance))
            {
                __result = Building_LWOPHydroponicsBasin.TemperatureFactorAtDefaultGrowthTemperature(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Plant), "get_Resting")]
    public static class LWOPHydroponicsPlantRestingPatch
    {
        public static void Postfix(Plant __instance, ref bool __result)
        {
            if (Building_LWOPHydroponicsBasin.IsPlantOnLWOPHydroponics(__instance))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Plant), "get_GrowthRate")]
    public static class LWOPHydroponicsPlantGrowthRatePatch
    {
        public static bool Prefix(Plant __instance, ref float __result)
        {
            if (!Building_LWOPHydroponicsBasin.IsPlantOnLWOPHydroponics(__instance))
            {
                return true;
            }

            if (__instance.Blighted)
            {
                __result = 0f;
                return false;
            }

            __result =
                __instance.GrowthRateFactor_Fertility *
                Building_LWOPHydroponicsBasin.TemperatureFactorAtDefaultGrowthTemperature(__instance) *
                __instance.GrowthRateFactor_Light *
                __instance.GrowthRateFactor_NoxiousHaze *
                __instance.GrowthRateFactor_Drought;
            return false;
        }
    }

    [HarmonyPatch(typeof(Plant), "get_DyingBecauseExposedToLight")]
    public static class LWOPHydroponicsPlantLightDamagePatch
    {
        public static void Postfix(Plant __instance, ref bool __result)
        {
            if (Building_LWOPHydroponicsBasin.IsPlantOnLWOPHydroponics(__instance))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Building_PlantGrower), "SetPlantDefToGrow")]
    public static class LWOPHydroponicsSetPlantDefToGrowPatch
    {
        public static void Prefix(Building_PlantGrower __instance, ref ThingDef __state)
        {
            if (__instance is Building_LWOPHydroponicsBasin)
            {
                __state = __instance.GetPlantDefToGrow();
            }
        }

        public static void Postfix(Building_PlantGrower __instance, ThingDef plantDef, ThingDef __state)
        {
            Building_LWOPHydroponicsBasin basin = __instance as Building_LWOPHydroponicsBasin;
            if (basin != null)
            {
                basin.NotifyPlantDefToGrowChanged(__state, plantDef);
            }
        }
    }

}
