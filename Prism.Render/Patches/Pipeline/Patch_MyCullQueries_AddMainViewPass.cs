using HarmonyLib;
using Prism.Render.Pipeline;
using Prism.Render.Pipeline.Old;
using VRage.Render.Scene;
using VRage.Render11.Culling;
using VRage.Render11.Resources;
using VRageRender;

namespace Prism.Render.Patches.Pipeline;

[HarmonyPatch]
public static class Patch_MyCullQueries_AddMainViewPass
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
}
