using HarmonyLib;
using System.Collections.Generic;
using VRage.Library.Collections;
using VRage.Render.Scene;
using VRage.Render11.Culling;
using VRage.Render11.GeometryStage2.Instancing;
using VRage.Render11.GeometryStage2.PrepareGroupPass;
using VRage.Render11.GeometryStage2.PreparePass;
using VRage.Render11.GeometryStage2.Rendering;
using VRage.Render11.GeometryStage2.RenderPass;
using VRage.Render11.GeometryStage2.StaticGroup;
using VRage.Render11.Resources;
using VRageRender;

namespace Prism.Render.Pipeline;

[HarmonyPatch]
public static class MotionVectorGenerationPatches
{
    [HarmonyPatch(typeof(MyCullQueries), nameof(MyCullQueries.AddMainViewPass))]
    [HarmonyPrefix]
    static bool MyCullQueries_AddMainViewPass_Prefix(MyCullQueries __instance, ref MyViewport viewport, MyGBuffer gbuffer)
    {
        MyCullQuery query = __instance.AddView(MyViewType.Main, 0, MyRender11.Environment.Matrices.ViewFrustumClippedD, MyRender11.Environment.Matrices.ViewFrustumClippedFarD, ref MyRender11.Environment.Matrices.ViewProjectionAt0, ref MyRender11.Environment.Matrices.CameraPosition, MyGBuffer.Main.ResolvedDepthStencil.DsvRo, MyGBuffer.Main.LBuffer);
        PrismGBufferPass pass = __instance.AddRenderPass<PrismGBufferPass>(query, ref viewport, ref MyRender11.Environment.Matrices.Projection);
        pass.GBuffer = gbuffer;
        pass.VelocityBuffer = GBufferVelocity.Get(gbuffer);
        return false;
    }

    [HarmonyPatch(typeof(MyGeometryRenderer), nameof(MyGeometryRenderer.InitPasses))]
    [HarmonyPostfix]
    static void MyGeometryRenderer_InitPasses_Postfix(MyGeometryRenderer __instance, MyCullQueries cullQueries, IGBufferSrvStrategy srvStrategy, List<IPrepareWork> outPreparePasses, List<MyRenderPass> outRenderPasses)
    {
        for (int i = 0; i < cullQueries.Size; i++)
        {
            if (cullQueries.RenderingPasses[i] == null)
            {
                continue;
            }

            MyCullQuery cullQuery = cullQueries.CullQueries[i];
            if (cullQueries.RenderingPasses[i] is PrismGBufferPass gbufferPass)
            {
                int passId = MyGeometryRenderer.GBufferPassId;
                MyViewport viewport = MyGeometryRenderer.GBufferViewport;
                MyList<MyInstance> instances = cullQuery.Results.Instances;
                MyList<MyStaticGroup> staticGroups = cullQuery.Results.StaticGroups;

                __instance.m_visibleInstances[passId] = instances;
                __instance.m_visibleStaticGroups[passId] = staticGroups;

                MyRenderData renderData = __instance.m_instanceRenderData[passId];
                renderData.Init(MyGeometryRenderer.GBufferViewProjection, MyRender11.Environment.Matrices.Projection);

                var preparePass = MyObjectPoolManager.Allocate<MyPreparePass<PrismColorPreparePass0, MyColorPreparePass1>>();
                preparePass.Init(passId, instances, renderData, gbufferPass.DebugName, cullQuery.ViewIndex);
                outPreparePasses.Add(preparePass);

                // not sure if static groups even exist, might be a legacy thing
                //if (staticGroups.Count > 0)
                //{
                //    MyPrepareGroupPass myPrepareGroupPass = MyObjectPoolManager.Allocate<MyPrepareGroupPass>();
                //    myPrepareGroupPass.Init(staticGroups, __instance.m_staticGroupsRenderData[passId], gbufferPass.ViewProjection, gbufferPass.Projection, passId);
                //    outPreparePasses.Add(myPrepareGroupPass);
                //}

                PrismGBufferRenderPass renderPass = MyObjectPoolManager.Allocate<PrismGBufferRenderPass>();
                renderPass.Init(true, passId, viewport, gbufferPass.GBuffer, srvStrategy, gbufferPass.DebugName, renderData, __instance.m_staticGroupsRenderData[passId]);
                renderPass.VelocityBuffer = gbufferPass.VelocityBuffer;
                outRenderPasses.Add(renderPass);
            }
        }
    }
}

public class PrismGBufferPass : MyRenderingPass
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public MyGBuffer GBuffer;
    public IRtvTexture VelocityBuffer;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}
