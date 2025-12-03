using HarmonyLib;
using Prism.Render.Pipeline.New;
using SharpDX.Direct3D;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using VRage.Utils;
using VRageRender;

namespace Prism.Render.Pipeline.Old;

/// <summary>
/// Extends <see cref="MyMaterialShaders"/>
/// </summary>
[HarmonyPatch]
public static class PrismMaterialShaders
{
    public const string PRISM_GBUFFER_PASS = "Prism_GBuffer";
    public static readonly MyStringId PRISM_GBUFFER_PASS_ID = MyStringId.GetOrCompute(PRISM_GBUFFER_PASS);

    [HarmonyPatch(typeof(MyMaterialShaders), nameof(MyMaterialShaders.GetRenderingPassMacro))]
    [HarmonyPrefix]
    static bool GetRenderingPassMacro_Prefix(ref ShaderMacro __result, string pass)
    {
        if (pass == PRISM_GBUFFER_PASS)
        {
            __result = new ShaderMacro("RENDERING_PASS", 0); // same index as gbuffer pass
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(MyMaterialShaders), nameof(MyMaterialShaders.InitBundle))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> InitBundle_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo target1 = AccessTools.Method(typeof(MyMaterialShaders), nameof(MyMaterialShaders.AddMaterialShaderFlagMacrosTo));

        MethodInfo target2 = AccessTools.Method(typeof(MyMaterialShaders), nameof(MyMaterialShaders.GetMaterialSources));
        MethodInfo patch2 = AccessTools.Method(typeof(PrismMaterialShaders), nameof(GetMaterialSources_Patch));

        MethodInfo target3 = AccessTools.Method(typeof(MyShaderCompiler), nameof(MyShaderCompiler.Compile), [ typeof(string), typeof(ShaderMacro[]), typeof(MyShaderProfile), typeof(string), typeof(bool) ]);
        MethodInfo patch3 = AccessTools.Method(typeof(PrismMaterialShaders), nameof(Compile_Patch));

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Call && (instruction.operand as MethodInfo) == target1)
            {
                yield return instruction;

                // dup List<ShaderMacro> obj
                yield return new CodeInstruction(OpCodes.Dup);

            }
            else if (instruction.opcode == OpCodes.Call && (instruction.operand as MethodInfo) == target2)
            {
                // load MyMaterialShadersInfo myMaterialShadersInfo
                yield return new CodeInstruction(OpCodes.Ldloc_0);
                // get MyMaterialShadersInfo.Pass field value
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MyMaterialShadersInfo), nameof(MyMaterialShadersInfo.Pass)));

                yield return instruction.Clone(patch2);
            }
            else if (instruction.opcode == OpCodes.Call && (instruction.operand as MethodInfo) == target3)
            {
                // replace MyShaderCompiler.Compile call with patch

                // load MyMaterialShadersInfo myMaterialShadersInfo
                yield return new CodeInstruction(OpCodes.Ldloc_0);
                // get MyMaterialShadersInfo.Pass field value
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(MyMaterialShadersInfo), nameof(MyMaterialShadersInfo.Pass)));

                yield return instruction.Clone(patch3);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    private static readonly Dictionary<MyStringId, MyMaterialShaderInfo> _prismMaterialSources = new(MyStringId.Comparer);

    private static void GetMaterialSources_Patch(List<ShaderMacro> macros, MyStringId id, out MyMaterialShaderInfo info, MyStringId pass)
    {
        if (pass == PRISM_GBUFFER_PASS_ID)
        {
            macros.Add(new ShaderMacro($"MATERIAL_{id.String.ToUpper()}", null));

            if (!_prismMaterialSources.TryGetValue(id, out info))
            {
                string vsPath = Path.Combine(Plugin.ShaderDirectory, "Pipeline", "vs.hlsl");
                string psPath = Path.Combine(Plugin.ShaderDirectory, "Pipeline", "ps.hlsl");
                info = new MyMaterialShaderInfo
                {
                    VertexShaderFilename = vsPath,
                    VertexShaderFilepath = vsPath,
                    PixelShaderFilename = psPath,
                    PixelShaderFilepath = psPath,
                };
                _prismMaterialSources[id] = info;
            }
        }
        else
        {
            MyMaterialShaders.GetMaterialSources(id, out info);
        }
    }

    private static byte[] Compile_Patch(string filePath, ShaderMacro[] macros, MyShaderProfile profile, string sourceDescriptor, bool invalidateCache, MyStringId pass)
    {
        if (pass == PRISM_GBUFFER_PASS_ID)
        {
            if (profile is MyShaderProfile.vs_5_0)
            {
                return PrismShaderBundleManager._compiler.CompileVertexBytecode(filePath, "vs", macros);
            }
            else // if (profile is MyShaderProfile.ps_5_0
            {
                return PrismShaderBundleManager._compiler.CompilePixelBytecode(filePath, "ps", macros);
            }
        }
        else
        {
            return MyShaderCompiler.Compile(filePath, macros, profile, sourceDescriptor, invalidateCache);
        }
    }
}
