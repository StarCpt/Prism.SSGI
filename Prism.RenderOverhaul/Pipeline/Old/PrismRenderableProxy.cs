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
            mapping.WriteAndPosition(ref renderableProxy.PrevMatrix);
        }

        // this seems a bit inefficient.
        // TODO: use transpiler in MyGeometryRendererOld.UpdateCullProxies
        // and call the postfix directly if cullProxy.m_worldMatrixIndex == -1
        [HarmonyPatch(typeof(MyCullProxy), nameof(MyCullProxy.UpdateWorldMatrix))]
        [HarmonyPrefix]
        static void MyCullProxy_UpdateWorldMatrix_Prefix(MyCullProxy __instance, MyRenderableProxy[] ___RenderableProxies, out bool __state)
        {
            bool matrixValid = __instance.m_worldMatrixIndex != -1; // current matrix is valid
            __state = matrixValid;
            //if (matrixValid)
            //{
            //    for (int i = 0; i < ___RenderableProxies.Length; i++)
            //    {
            //        ((PrismRenderableProxy)___RenderableProxies[i]).UpdatePrevMatrix();
            //    }
            //}
        }

        [HarmonyPatch(typeof(MyCullProxy), nameof(MyCullProxy.UpdateWorldMatrix))]
        [HarmonyPostfix]
        static void MyCullProxy_UpdateWorldMatrix_Postfix(MyRenderableProxy[] ___RenderableProxies, bool __state)
        {
            bool matrixValid = __state; // current matrix was valid
            if (!matrixValid)
            {
                for (int i = 0; i < ___RenderableProxies.Length; i++)
                {
                    ((PrismRenderableProxy)___RenderableProxies[i])._lastPrevMatrixUpdateFrame = -1;
                    ((PrismRenderableProxy)___RenderableProxies[i]).UpdatePrevMatrix(0);
                }
            }
        }
    }

    public MyMaterialShadersBundleId PrismGBufferShaders;
    public RowMatrix PrevMatrix; // float4x3

    private long _lastPrevMatrixUpdateFrame = -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdatePrevMatrix(long frame)
    {
        if (frame != _lastPrevMatrixUpdateFrame)
        {
            _lastPrevMatrixUpdateFrame = frame;
            PrevMatrix = Unsafe.As<Vector4, RowMatrix>(ref CommonObjectData.m_row0);
        }
    }
}
