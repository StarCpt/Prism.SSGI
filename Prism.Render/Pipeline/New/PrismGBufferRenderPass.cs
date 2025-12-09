using Prism.Render.Patches;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using VRage.Render11.GeometryStage2.Common;
using VRage.Render11.GeometryStage2.Materials;
using VRage.Render11.GeometryStage2.Model;
using VRage.Render11.GeometryStage2.Model.Preprocess;
using VRage.Render11.GeometryStage2.Rendering;
using VRage.Render11.GeometryStage2.RenderPass;
using VRage.Render11.RenderContext;
using VRage.Render11.Resources;
using VRageRender;
using VRageRender.Import;

namespace Prism.Render.Pipeline.New;

public class PrismGBufferRenderPass : MyGBufferRenderPass
{
    private readonly RenderTargetView[] _rtvs = new RenderTargetView[4];
    public IRtvTexture VelocityBuffer = null!;

#if DEV
    public override void BeginDraw(MyRenderContext RC)
#else
    protected override void BeginDraw(MyRenderContext RC)
#endif
    {
        base.BeginDraw(RC);

        RC.VertexShader.SetConstantBuffer(4, Patch_MyRenderScheduler.PrismRenderConstants);
        RC.PixelShader.SetConstantBuffer(4, Patch_MyRenderScheduler.PrismRenderConstants);

        _rtvs[0] = m_gbuffer.GbufferRtvs[0];
        _rtvs[1] = m_gbuffer.GbufferRtvs[1];
        _rtvs[2] = m_gbuffer.GbufferRtvs[2];
        _rtvs[3] = VelocityBuffer.Rtv;

        RC.SetRtvs(m_gbuffer.DepthStencil.Dsv, _rtvs);
        Array.Clear(_rtvs, 0, _rtvs.Length);
    }

    /// <summary>
    /// Same as the base method but uses <see cref="PrismPreprocessedParts.PrismGBufferParts"/>
    /// </summary>
    /// <param name="RC"></param>
    /// <param name="itGroup"></param>
#if DEV
    public override void DrawInstanceLodGroup(MyRenderContext RC, MyInstanceLodGroup itGroup)
#else
    protected override void DrawInstanceLodGroup(MyRenderContext RC, MyInstanceLodGroup itGroup)
#endif
    {
        if (itGroup.Lod.PreprocessedParts is not PrismPreprocessedParts)
        {
            // MyManagers.ModelFactory.m_dummyModel is initialized before plugin init
            // TODO: use preloader to patch MyLod ctor

            base.DrawInstanceLodGroup(RC, itGroup);
            return;
        }

        MyLod lod = itGroup.Lod;
        RC.SetVertexBuffer(0, lod.VB0);
        RC.SetVertexBuffer(1, lod.VB1);
        RC.SetIndexBuffer(lod.IB);
        if (MyRender11.Settings.Wireframe)
        {
            RC.SetDepthStencilState(MyDepthStencilStateManager.DepthTestWrite);
            RC.SetBlendState(null);
            RC.SetRasterizerState(MyRasterizerStateManager.NocullWireframeRasterizerState);
        }

        MyPreprocessedPart[] parts = ((PrismPreprocessedParts)lod.PreprocessedParts).PrismGBufferParts;
        MyRenderMaterialBindings[] materials = itGroup.LodInstance.GBufferParts;
        for (int i = 0; i < parts.Length; i++)
        {
            if (!MyRender11.Settings.Wireframe)
            {
                RC.SetDepthStencilState(parts[i].DepthStencilState);
                RC.SetRasterizerState(parts[i].RasterizerState);
                RC.SetBlendState(parts[i].BlendState);
            }

            MyShaderBundle shaderBundle = parts[i].GetShaderBundle(itGroup.State, itGroup.MetalnessColorable);
            RC.SetInputLayout(shaderBundle.InputLayout);
            RC.VertexShader.Set(shaderBundle.VertexShader);
            RC.PixelShader.Set(shaderBundle.PixelShader);
            RC.PixelShader.SetSrvs(0, m_srvStrategy.GetSrvs(RC, materials[i], lod.LodNum));
            int instancesCount = itGroup.InstancesCount;
            int startInstanceLocation = itGroup.OffsetInInstanceBuffer + (parts[i].InstanceMaterialOffsetWithinLod + 1) * instancesCount;
            RC.DrawIndexedInstanced(parts[i].IndicesCount, instancesCount, parts[i].IndexStart, 0, startInstanceLocation);
            m_stats.Triangles += parts[i].IndicesCount / 3 * instancesCount;
            m_stats.Draws++;
        }
    }

    public static new void PreprocessData(out List<MyPreprocessedPart> parts, MyMwmData mwmData, MyLod lod)
    {
        parts = [];
        foreach (MyMwmDataPart part in mwmData.Parts)
        {
            if (!part.Technique.IsTransparent())
            {
                MyPreprocessedPart preprocessedPart = default;
                preprocessedPart.Init(part.MaterialName, lod, part.IndexOffset, part.IndicesCount, MyModelMaterials.GetMaterial(part), (MyRenderPassType)PrismRenderPassType.GBufferVelocity);
                string colorMetalFilepath = part.ColorMetalFilepath;
                string normalGlossFilepath = part.NormalGlossFilepath;
                string extensionFilepath = part.ExtensionFilepath;
                switch (part.Technique)
                {
                    case MyMeshDrawTechnique.MESH:
                        preprocessedPart.DepthStencilState = MyDepthStencilStateManager.DefaultDepthState;
                        preprocessedPart.BlendState = null;
                        preprocessedPart.RasterizerState = null;
                        break;
                    case MyMeshDrawTechnique.DECAL:
                        InitPartForDecal(ref preprocessedPart, colorMetalFilepath, normalGlossFilepath, extensionFilepath, isPremultipliedAlpha: true, isCutout: false);
                        break;
                    case MyMeshDrawTechnique.DECAL_NOPREMULT:
                        InitPartForDecal(ref preprocessedPart, colorMetalFilepath, normalGlossFilepath, extensionFilepath, isPremultipliedAlpha: false, isCutout: false);
                        break;
                    case MyMeshDrawTechnique.DECAL_CUTOUT:
                        InitPartForDecal(ref preprocessedPart, colorMetalFilepath, normalGlossFilepath, extensionFilepath, isPremultipliedAlpha: true, isCutout: true);
                        break;
                    case MyMeshDrawTechnique.ALPHA_MASKED:
                        preprocessedPart.DepthStencilState = MyDepthStencilStateManager.DefaultDepthState;
                        preprocessedPart.BlendState = null;
                        preprocessedPart.RasterizerState = MyRasterizerStateManager.NocullRasterizerState;
                        break;
                    case MyMeshDrawTechnique.ALPHA_MASKED_SINGLE_SIDED:
                        preprocessedPart.DepthStencilState = MyDepthStencilStateManager.DefaultDepthState;
                        preprocessedPart.BlendState = null;
                        preprocessedPart.RasterizerState = null;
                        break;
                    default:
                        MyRenderProxy.Error("Material is not resolved");
                        break;
                }

                parts.Add(preprocessedPart);
            }
        }
    }
}
