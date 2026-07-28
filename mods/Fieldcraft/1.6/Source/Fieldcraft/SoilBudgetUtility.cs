using RimWorld;
using UnityEngine;
using Verse;

namespace Fieldcraft
{
    public static class SoilBudgetUtility
    {
        public const float MaxBudget = 100f;

        public static bool IsGreenManure(ThingDef plantDef)
        {
            if (plantDef == null)
            {
                return false;
            }
            if (plantDef == FieldcraftDefOf.Plant_FC_GreenManure)
            {
                return true;
            }
            return plantDef.defName == "Plant_FC_GreenManure";
        }

        public static float GreenManureRestoreFor(ThingDef plantDef)
        {
            float raw = 28f;
            float mult = FieldcraftMod.Settings != null ? FieldcraftMod.Settings.greenManureMultiplier : 1f;
            return Mathf.Max(5f, raw * mult);
        }

        public static float PlowInRestoreFor(ThingDef plantDef, float growth)
        {
            growth = Mathf.Clamp01(growth);
            float baseRestore;
            if (IsGreenManure(plantDef))
            {
                baseRestore = 36f;
            }
            else if (plantDef != null && (plantDef.defName ?? "").Contains("Hay"))
            {
                baseRestore = 22f;
            }
            else
            {
                // General plow-in: modest return scaled by nutrition potential.
                baseRestore = 12f;
            }
            float mult = FieldcraftMod.Settings != null ? FieldcraftMod.Settings.greenManureMultiplier : 1f;
            return Mathf.Max(3f, baseRestore * growth * mult);
        }

        public static float HarvestDrainFor(ThingDef plantDef)
        {
            if (plantDef == null)
            {
                return 8f;
            }

            if (IsGreenManure(plantDef))
            {
                return 0f;
            }

            float raw;
            string name = plantDef.defName ?? "";

            if (name.Contains("Smokeleaf") || name.Contains("Psychoid") || name.Contains("Cotton")
                || name.Contains("Devilstrand") || name.Contains("Hop"))
            {
                raw = 18f;
            }
            else if (name.Contains("Corn") || name.Contains("Rice") || name.Contains("Potato")
                     || name.Contains("Nutrifungus"))
            {
                raw = 14f;
            }
            else if (name.Contains("Hay") || name.Contains("Dandelion") || name.Contains("Clover")
                     || name.Contains("Grass") || name.Contains("GreenManure"))
            {
                raw = 4f;
            }
            else if (plantDef.plant != null)
            {
                float yield = plantDef.plant.harvestYield;
                float days = Mathf.Max(1f, plantDef.plant.growDays);
                raw = Mathf.Clamp(6f + yield * 0.12f + days * 0.15f, 3f, 22f);
            }
            else
            {
                raw = 8f;
            }

            float mult = FieldcraftMod.Settings != null ? FieldcraftMod.Settings.harvestDrainMultiplier : 1f;
            mult *= FieldcraftMod.Settings != null ? FieldcraftMod.Settings.EasyMult : 1f;
            return Mathf.Max(0.5f, raw * mult);
        }

        public static float GrowthFactorFromBudget(float budget)
        {
            if (budget >= 80f)
            {
                return 1.05f;
            }
            if (budget <= 0f)
            {
                return 0.35f;
            }
            return Mathf.Lerp(0.35f, 1.0f, budget / 80f);
        }

        /// <summary>Extra growth multiplier from paddy state for rice-like crops.</summary>
        public static float PaddyGrowthFactor(ThingDef plantDef, TerrainDef terrain)
        {
            if (plantDef == null || terrain == null)
            {
                return 1f;
            }
            bool riceLike = (plantDef.defName ?? "").Contains("Rice");
            if (!riceLike)
            {
                // Non-rice slightly dislike flooded paddy.
                if (terrain == FieldcraftDefOf.FC_PaddyFlooded)
                {
                    return 0.85f;
                }
                return 1f;
            }
            if (terrain == FieldcraftDefOf.FC_PaddyFlooded)
            {
                return 1.25f;
            }
            if (terrain == FieldcraftDefOf.FC_PaddyDry)
            {
                return 1.05f;
            }
            // Rice off-paddy mild penalty (still plantable).
            return 0.9f;
        }

        public static string BudgetLabel(float budget)
        {
            if (budget >= 75f)
            {
                return "FC_Budget_Rich".Translate();
            }
            if (budget >= 45f)
            {
                return "FC_Budget_Fair".Translate();
            }
            if (budget >= 20f)
            {
                return "FC_Budget_Poor".Translate();
            }
            return "FC_Budget_Exhausted".Translate();
        }

        public static Color BudgetColor(float budget)
        {
            if (budget >= 75f)
            {
                return new Color(0.45f, 0.85f, 0.45f);
            }
            if (budget >= 45f)
            {
                return new Color(0.85f, 0.85f, 0.4f);
            }
            if (budget >= 20f)
            {
                return new Color(0.9f, 0.55f, 0.25f);
            }
            return new Color(0.9f, 0.3f, 0.25f);
        }
    }
}
