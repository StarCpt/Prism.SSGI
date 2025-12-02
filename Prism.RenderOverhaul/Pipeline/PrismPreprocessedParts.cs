using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using VRage.Render11.GeometryStage2.Model;
using VRage.Render11.GeometryStage2.Model.Preprocess;
using VRage.Utils;

namespace Prism.Render.Pipeline;

public class PrismPreprocessedParts : MyPreprocessedParts
{
    [HarmonyPatch]
    static class Patches
    {
#pragma warning disable IDE0051, IDE0002 // don't gray out "unused" patch methods
        [HarmonyPatch(typeof(MyLod), MethodType.Constructor)]
        [HarmonyPostfix]
        static void MyLod_ctor_Postfix(ref MyPreprocessedParts ___m_preprocessedParts)
        {
            ___m_preprocessedParts = new PrismPreprocessedParts();
        }

        [HarmonyPatch(typeof(MyLod), nameof(MyLod.Create))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> MyLod_Create_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo target = AccessTools.Method(typeof(MyPreprocessedParts), nameof(MyPreprocessedParts.Init));
            MethodInfo patch = AccessTools.Method(typeof(PrismPreprocessedParts), nameof(Init));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Callvirt && (instruction.operand as MethodInfo) == target)
                {
                    yield return instruction.Clone(patch);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        [HarmonyPatch(typeof(MyLod), nameof(MyLod.AddInstanceMaterial))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> MyLod_AddInstanceMaterial_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo target = AccessTools.Method(typeof(MyPreprocessedParts), nameof(MyPreprocessedParts.AddInstanceMaterial));
            MethodInfo patch = AccessTools.Method(typeof(PrismPreprocessedParts), nameof(AddInstanceMaterial));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Callvirt && (instruction.operand as MethodInfo) == target)
                {
                    yield return instruction.Clone(patch);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
#pragma warning restore IDE0051, IDE0002
    }

    public MyPreprocessedPart[] PrismGBufferParts;

    public new void Init(MyMwmData mwmData, MyLod parentLod)
    {
        base.Init(mwmData, parentLod);
        PrismGBufferRenderPass.PreprocessData(out var parts, mwmData, parentLod);
        PrismGBufferParts = parts.ToArray();
    }

    public new void AddInstanceMaterial(MyStringId materialName, int instanceMaterialOffsetWithinLod)
    {
        base.AddInstanceMaterial(materialName, instanceMaterialOffsetWithinLod);

        for (int i = 0; i < PrismGBufferParts.Length; i++)
        {
            if (PrismGBufferParts[i].Name == materialName)
            {
                MyPreprocessedPart value = PrismGBufferParts[i];
                value.InstanceMaterialOffsetWithinLod = instanceMaterialOffsetWithinLod;
                PrismGBufferParts[i] = value;
            }
        }
    }
}
