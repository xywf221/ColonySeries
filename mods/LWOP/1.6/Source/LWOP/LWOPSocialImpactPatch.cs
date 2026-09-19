using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LWOP.Buildings
{
    [StaticConstructorOnStartup]
    public static class LWOPSocialImpactPatch
    {
        static LWOPSocialImpactPatch()
        {
            new Harmony("com.colonyseries.lwop.SocialImpactPositiveOnly").PatchAll();
        }
    }

    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "AddInteractionThought")]
    public static class PawnInteractionsTrackerAddInteractionThoughtPatch
    {
        private static readonly FieldInfo MoodPowerFactorField = AccessTools.Field(typeof(Thought_Memory), "moodPowerFactor");
        private static readonly FieldInfo OpinionOffsetField = AccessTools.Field(typeof(Thought_MemorySocial), "opinionOffset");
        private static readonly MethodInfo AdjustMoodFactorMethod = AccessTools.Method(typeof(PawnInteractionsTrackerAddInteractionThoughtPatch), "AdjustMoodFactor");
        private static readonly MethodInfo AdjustOpinionFactorMethod = AccessTools.Method(typeof(PawnInteractionsTrackerAddInteractionThoughtPatch), "AdjustOpinionFactor");

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (LoadsLocal0(codes[i]) && i + 1 < codes.Count && StoresField(codes[i + 1], MoodPowerFactorField))
                {
                    codes[i] = new CodeInstruction(OpCodes.Ldloc_1);
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc_0));
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Call, AdjustMoodFactorMethod));
                    i += 2;
                    continue;
                }

                if (LoadsLocal0(codes[i]) &&
                    i > 0 &&
                    LoadsField(codes[i - 1], OpinionOffsetField) &&
                    i + 1 < codes.Count &&
                    codes[i + 1].opcode == OpCodes.Mul)
                {
                    codes[i] = new CodeInstruction(OpCodes.Ldloc_2);
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc_0));
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Call, AdjustOpinionFactorMethod));
                    i += 2;
                }
            }

            return codes;
        }

        public static float AdjustMoodFactor(Thought_Memory thought, float socialImpactFactor)
        {
            if (socialImpactFactor > 1f && ThoughtHasNegativeMood(thought))
            {
                return 1f;
            }

            return socialImpactFactor;
        }

        public static float AdjustOpinionFactor(Thought_MemorySocial thought, float socialImpactFactor)
        {
            if (socialImpactFactor > 1f && thought != null && thought.opinionOffset < 0f)
            {
                return 1f;
            }

            return socialImpactFactor;
        }

        private static bool ThoughtHasNegativeMood(Thought_Memory thought)
        {
            if (thought == null)
            {
                return false;
            }

            float moodOffset = thought.moodOffset;
            ThoughtDef def = thought.def;
            if (def != null && def.stages != null && def.stages.Count > 0)
            {
                int stageIndex = thought.CurStageIndex;
                if (stageIndex >= 0 && stageIndex < def.stages.Count)
                {
                    moodOffset += def.stages[stageIndex].baseMoodEffect;
                }
            }

            return moodOffset < 0f;
        }

        private static bool LoadsLocal0(CodeInstruction instruction)
        {
            if (instruction.opcode == OpCodes.Ldloc_0)
            {
                return true;
            }

            LocalBuilder local = instruction.operand as LocalBuilder;
            return instruction.opcode == OpCodes.Ldloc_S && local != null && local.LocalIndex == 0;
        }

        private static bool LoadsField(CodeInstruction instruction, FieldInfo field)
        {
            return instruction.opcode == OpCodes.Ldfld && (instruction.operand as FieldInfo) == field;
        }

        private static bool StoresField(CodeInstruction instruction, FieldInfo field)
        {
            return instruction.opcode == OpCodes.Stfld && (instruction.operand as FieldInfo) == field;
        }
    }
}
