using HarmonyLib;
using Prism.Maths;
using System.Runtime.CompilerServices;
using VRage.Render11.Culling;
using VRage.Render11.GeometryStage2.Instancing;
using VRage.Render11.Resources;
using VRage.Render11.Scene.Components;
using VRage.Utils;
using VRageMath;
using VRageRender;
using VRageRender.Import;

namespace Prism.Render.Pipeline.Old;

/// <summary>
/// Extends <see cref="MyRenderableProxy"/>
/// </summary>
public class PrismRenderableProxy : MyRenderableProxy
{
    [HarmonyPatch]
    static class Patches
    {
        [HarmonyPatch(typeof(MyRenderableComponent), nameof(MyRenderableComponent.AssignShadersToProxy))]
        [HarmonyPostfix]
        static void MyRenderableComponent_AssignShadersToProxy_Postfix(MyRenderableProxy renderableProxy, MyStringId shaderMaterial, VertexLayoutId vertexLayoutId, MyShaderUnifiedFlags shaderFlags, MyStringId forwardMaterial)
        {
            MyFileTextureEnum textureTypes = MyFileTextureEnum.UNSPECIFIED;
            MyMeshDrawTechnique technique = MyMeshDrawTechnique.MESH;
            if (renderableProxy.Material != MyMeshMaterialId.NULL)
            {
                textureTypes = renderableProxy.Material.Info.TextureTypes;
                technique = renderableProxy.Material.Info.Technique;
            }

            MyStringId passId = MyMaterialShaders.MapTechniqueToDefaultPass(technique);
            if (passId == MyMaterialShaders.GBUFFER_PASS_ID)
            {
                passId = PrismMaterialShaders.PRISM_GBUFFER_PASS_ID;
            }

            ((PrismRenderableProxy)renderableProxy).PrismGBufferShaders = MyMaterialShaders.Get(shaderMaterial, passId, vertexLayoutId, shaderFlags, textureTypes);
        }

        [HarmonyPatch(typeof(MyRenderableComponent), nameof(MyRenderableComponent.GetConstantBufferSize))]
        [HarmonyPostfix]
        static unsafe void MyRenderableComponent_GetConstantBufferSize_Postfix(ref int __result)
        {
            __result += sizeof(float3x4);
        }

        [HarmonyPatch(typeof(MyRenderableProxy), nameof(MyRenderableProxy.UpdateObjectBuffer), [ typeof(MyMapping) ], [ ArgumentType.Ref ])]
        [HarmonyPostfix]
        static unsafe void MyRenderableProxy_UpdateObjectBuffer_Postfix(MyRenderableProxy __instance, ref MyMapping mapping)
        {
            var renderableProxy = (PrismRenderableProxy)__instance;
            mapping.Position(__instance.ObjectBufferSize - sizeof(RowMatrix));
            mapping.WriteAndPosition(ref renderableProxy.PrevMatrix);
        }

        [HarmonyPatch(typeof(MyRenderableProxy), nameof(MyRenderableProxy.Clear))]
        [HarmonyPostfix]
        static unsafe void MyRenderableProxy_Clear_Postfix(MyRenderableProxy __instance)
        {
            ((PrismRenderableProxy)__instance).PrevMatrix = default;
        }

        [HarmonyPatch(typeof(MyCullProxy), nameof(MyCullProxy.UpdateWorldMatrix))]
        [HarmonyPrefix]
        static void MyCullProxy_UpdateWorldMatrix_Prefix(MyCullProxy __instance, MyRenderableProxy[] ___RenderableProxies)
        {
            bool matrixValid = __instance.m_worldMatrixIndex != -1; // current matrix is valid
            if (matrixValid)
            {
                PrismRenderableProxy[] proxies = Unsafe.As<PrismRenderableProxy[]>(___RenderableProxies);
                for (int i = 0; i < proxies.Length; i++)
                {
                    proxies[i].UpdatePrevMatrix();
                }
            }
        }


        struct RenderableData
        {
            public bool Valid;
            public RowMatrix PrevMatrix;
        }

        [HarmonyPatch(typeof(MyRenderableComponent), nameof(MyRenderableComponent.RebuildRenderProxies))]
        [HarmonyPrefix]
        static void MyRenderableComponent_RebuildRenderProxies_Prefix(MyRenderableComponent __instance, out RenderableData __state)
        {
            if (__instance.Lods != null)
            {
                __state = new()
                {
                    Valid = true,
                    PrevMatrix = __instance.Lods![__instance.CurrentLod].RenderableProxies[0].CommonObjectData.GetRowMatrixRef(),
                };
            }
            else
            {
                __state = default;
            }
        }

        [HarmonyPatch(typeof(MyRenderableComponent), nameof(MyRenderableComponent.RebuildRenderProxies))]
        [HarmonyPostfix]
        static void MyRenderableComponent_RebuildRenderProxies_Postfix(MyRenderableComponent __instance, RenderableData __state)
        {
            if (__state.Valid)
            {
                MyRenderLod[] lods = __instance.Lods;
                for (var lod = 0; lod < lods.Length; lod++)
                {
                    PrismRenderableProxy[] lodProxies = Unsafe.As<PrismRenderableProxy[]>(lods[lod].RenderableProxies);
                    for (int i = 0; i < lodProxies.Length; i++)
                    {
                        lodProxies[i].PrevMatrix = __state.PrevMatrix;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MyRenderableComponent), nameof(MyRenderableComponent.SetProxiesForCurrentLod))]
        [HarmonyPrefix]
        static void MyRenderableComponent_SetProxiesForCurrentLod_Prefix(MyRenderableComponent __instance, out RenderableData __state)
        {
            if (__instance.CullProxy.RenderableProxies != null)
            {
                __state = new()
                {
                    Valid = true,
                    PrevMatrix = __instance.CullProxy.RenderableProxies[0].CommonObjectData.GetRowMatrixRef(),
                };
            }
            else
            {
                __state = default;
            }
        }

        [HarmonyPatch(typeof(MyRenderableComponent), nameof(MyRenderableComponent.SetProxiesForCurrentLod))]
        [HarmonyPostfix]
        static void MyRenderableComponent_SetProxiesForCurrentLod_Postfix(MyRenderableComponent __instance, RenderableData __state)
        {
            if (__state.Valid)
            {
                PrismRenderableProxy[] proxies = Unsafe.As<PrismRenderableProxy[]>(__instance.CullProxy.RenderableProxies);
                for (int i = 0; i < proxies.Length; i++)
                {
                    proxies[i].PrevMatrix = __state.PrevMatrix;
                }
            }
        }
    }

    public MyMaterialShadersBundleId PrismGBufferShaders;
    public RowMatrix PrevMatrix; // float4x3

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdatePrevMatrix()
    {
        PrevMatrix = CommonObjectData.GetRowMatrixRef();
    }
}
