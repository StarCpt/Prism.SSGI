using HarmonyLib;
using Prism.Render.SSGI;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using VRage.Render11.GBufferResolve;
using VRage.Render11.LightingStage;

namespace Prism.Render.Patches.SSGI;

[HarmonyPatch]
public static class Patch_MyGBufferResolver
{
    [HarmonyPatch(typeof(MyGBufferResolver), nameof(MyGBufferResolver.DoWork))]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> DoWork_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo anchor = AccessTools.Method(typeof(MyLightsRendering), nameof(MyLightsRendering.Render));

        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Call && (instruction.operand as MethodInfo) == anchor)
            {
                // MyLightsRendering.Render(..)
                yield return instruction;
                // load MyRenderContext rc
                yield return new CodeInstruction(OpCodes.Ldloc, 0);
                // call SSGIPass.Run(rc)
                yield return CodeInstruction.Call(typeof(SSGIPass), nameof(SSGIPass.Run));
            }
            else
            {
                yield return instruction;
            }
        }
    }
}
