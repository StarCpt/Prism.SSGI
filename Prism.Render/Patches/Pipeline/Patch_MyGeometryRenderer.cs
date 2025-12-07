using HarmonyLib;
using Prism.Render.Pipeline.New;
using Prism.Render.Pipeline.Old;
using System.Collections.Generic;
using VRage.Library.Collections;
using VRage.Render11.Culling;
using VRage.Render11.GeometryStage2.Instancing;
using VRage.Render11.GeometryStage2.PrepareGroupPass;
using VRage.Render11.GeometryStage2.PreparePass;
using VRage.Render11.GeometryStage2.Rendering;
using VRage.Render11.GeometryStage2.RenderPass;
using VRage.Render11.GeometryStage2.StaticGroup;
using VRageRender;

namespace Prism.Render.Patches.Pipeline;

[HarmonyPatch]
public static class Patch_MyGeometryRenderer
{
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
