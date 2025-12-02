using HarmonyLib;
using Prism.Common;
using SharpDX.Direct3D;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using VRage.Render11.GeometryStage2.Common;
using VRage.Render11.GeometryStage2.Rendering;
using VRageRender;
using VRageRender.Import;

namespace Prism.Render.Pipeline;

[HarmonyPatch(typeof(MyShaderBundleManager))]
public static class PrismShaderBundleManager
{
    [HarmonyPatch(nameof(MyShaderBundleManager.GetVertexInputComponents))]
    [HarmonyPrefix]
    static bool GetVertexInputComponents_Prefix(ref MyVertexInputComponent[] __result, MyRenderPassType pass)
    {
        if ((PrismRenderPassType)pass is PrismRenderPassType.GBufferVelocity)
        {
            __result = [
                new MyVertexInputComponent(MyVertexInputComponentType.POSITION_PACKED),
                new MyVertexInputComponent(MyVertexInputComponentType.NORMAL, 1),
                new MyVertexInputComponent(MyVertexInputComponentType.TANGENT_SIGN_OF_BITANGENT, 1),
                new MyVertexInputComponent(MyVertexInputComponentType.TEXCOORD0_H),
                new MyVertexInputComponent(MyVertexInputComponentType.SIMPLE_INSTANCE, 2, MyVertexInputComponentFreq.PER_INSTANCE),
                new MyVertexInputComponent((MyVertexInputComponentType)PrismVertexInputComponentType.SIMPLE_INSTANCE_PREVMATRIX, 2, MyVertexInputComponentFreq.PER_INSTANCE),
                new MyVertexInputComponent(MyVertexInputComponentType.SIMPLE_INSTANCE_COLORING, 2, MyVertexInputComponentFreq.PER_INSTANCE)
            ];
            return false;
        }
        return true;
    }

    [HarmonyPatch(nameof(MyShaderBundleManager.AddMacrosForRenderingPass))]
    [HarmonyPrefix]
    static void AddMacrosForRenderingPass_Prefix(ref MyRenderPassType pass)
    {
        if ((PrismRenderPassType)pass is PrismRenderPassType.GBufferVelocity)
        {
            pass = MyRenderPassType.GBuffer;
        }
    }

    [HarmonyPatch(typeof(MyShaderBundleManager), nameof(MyShaderBundleManager.GetShaderBundle))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> GetShaderBundle_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo target1 = AccessTools.Method(typeof(MyShaderBundleManager), nameof(MyShaderBundleManager.GetShaderFilepath));
        MethodInfo patch1 = AccessTools.Method(typeof(PrismShaderBundleManager), nameof(GetShaderFilepath_Patch));

        MethodInfo target2 = AccessTools.Method(typeof(MyShaderBundleManager), nameof(MyShaderBundleManager.AddMacrosForTechnique));
        MethodInfo patch2 = AccessTools.Method(typeof(PrismShaderBundleManager), nameof(AddMacrosForTechnique_Patch));

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Call && (instruction.operand as MethodInfo) == target1)
            {
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return instruction.Clone(patch1);
            }
            else if (instruction.opcode == OpCodes.Call && (instruction.operand as MethodInfo) == target2)
            {
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return instruction.Clone(patch2);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    static string GetShaderFilepath_Patch(MyShaderBundleManager instance, MyMeshDrawTechnique technique, MyShaderBundleManager.MyShaderType type, MyRenderPassType pass)
    {
        if ((PrismRenderPassType)pass is PrismRenderPassType.GBufferVelocity)
        {
            if (type is MyShaderBundleManager.MyShaderType.SHADER_TYPE_VERTEX)
            {
                return Path.Combine(Plugin.ShaderDirectory, @"Pipeline\vs.hlsl");
            }
            else
            {
                return Path.Combine(Plugin.ShaderDirectory, @"Pipeline\ps.hlsl");
            }
        }
        else
        {
            return instance.GetShaderFilepath(technique, type);
        }
    }

    static void AddMacrosForTechnique_Patch(MyShaderBundleManager instance, MyMeshDrawTechnique technique, bool isCm, bool isNg, bool isExt, ref List<ShaderMacro> macros, MyRenderPassType pass)
    {
        instance.AddMacrosForTechnique(technique, isCm, isNg, isExt, ref macros);
        if ((PrismRenderPassType)pass is PrismRenderPassType.GBufferVelocity)
        {
            macros.Add(new ShaderMacro($"TECHNIQUE_{technique}", null));
        }
    }

    [HarmonyPatch(typeof(MyVertexShaders), nameof(MyVertexShaders.Init))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> MyVertexShaders_Init_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo target = AccessTools.Method(typeof(MyShaderCompiler), nameof(MyShaderCompiler.Compile), [ typeof(MyShaderCompilationInfo).MakeByRefType(), typeof(bool) ]);
        MethodInfo patch = AccessTools.Method(typeof(PrismShaderBundleManager), nameof(CompileVertex));
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Call && (instruction.operand as MethodInfo) == target)
            {
                yield return instruction.Clone(patch);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(MyPixelShaders), nameof(MyPixelShaders.Init))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> MyPixelShaders_Init_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo target = AccessTools.Method(typeof(MyShaderCompiler), nameof(MyShaderCompiler.Compile), [typeof(MyShaderCompilationInfo).MakeByRefType(), typeof(bool)]);
        MethodInfo patch = AccessTools.Method(typeof(PrismShaderBundleManager), nameof(CompilePixel));
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Call && (instruction.operand as MethodInfo) == target)
            {
                yield return instruction.Clone(patch);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    static readonly FileShaderCompiler _compiler = new("", MyShaderCompiler.ShadersPath);

    static byte[] CompileVertex(ref MyShaderCompilationInfo info, bool invalidateCache = false)
    {
        if (info.File.String.StartsWith(Plugin.ShaderDirectory))
        {
            return _compiler.CompileVertexBytecode(info.File.String, "vs", info.Macros);
        }
        return MyShaderCompiler.Compile(ref info, invalidateCache);
    }

    static byte[] CompilePixel(ref MyShaderCompilationInfo info, bool invalidateCache = false)
    {
        if (info.File.String.StartsWith(Plugin.ShaderDirectory))
        {
            return _compiler.CompilePixelBytecode(info.File.String, "ps", info.Macros);
        }
        return MyShaderCompiler.Compile(ref info, invalidateCache);
    }
}
