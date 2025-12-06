using HarmonyLib;
using Prism.Common;
using Prism.Maths;
using Prism.Render.Pipeline.Old;
using SharpDX.Direct3D11;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VRage.Render11.Common;
using VRage.Render11.Culling;
using VRage.Render11.Render;
using VRage.Render11.Resources;
using VRageMath;
using VRageRender;

namespace Prism.Render.Patches;

[HarmonyPatch(typeof(MyRenderScheduler))]
public static class Patch_MyRenderScheduler
{
    [StructLayout(LayoutKind.Sequential)]
    struct PrevMatricesConstants
    {
        public Matrix PrevViewProj;
        public float Farplane;
        private uint3 _pad1;
    }

    public static IConstantBuffer PrismRenderConstants => _prevMatricesCbv;

    static IConstantBuffer _prevMatricesCbv = null!;

    static MyCommon.MyFrameConstantsLayout _prevFrameConstants;

    public static unsafe void Init()
    {
        _prevMatricesCbv = MyManagers.Buffers.CreateConstantBuffer("Prism.Render.CBPrevMatrices", MathHelper.Align(sizeof(PrevMatricesConstants), 16), usage: ResourceUsage.Dynamic, isGlobal: true);
    }

    [HarmonyPatch(nameof(MyRenderScheduler.Init))]
    [HarmonyPrefix]
    static void Init_Prefix()
    {
        using (BufferMapping mapping = _prevMatricesCbv.MapWriteDiscard())
        {
            var data = new PrevMatricesConstants
            {
                PrevViewProj = _prevFrameConstants.Environment.ViewProjection,
                Farplane = MyRender11.Environment.Matrices.FarClipping,
            };
            mapping.WriteAndPosition(ref data);
        }

        _prevFrameConstants = MyCommon.FrameConstantsData;
    }

    [HarmonyPatch(nameof(MyRenderScheduler.Done))]
    [HarmonyPostfix]
    static void Done_Postfix()
    {
        // update MyRenderableProxy previous matrices
        //foreach (MyCullProxy cullProxy in MyManagers.Cull.GetGBufferCullQuery().Results.CullProxies.AsSpan())
        //{
        //    PrismRenderableProxy[] proxies = Unsafe.As<PrismRenderableProxy[]>(cullProxy.RenderableProxies);
        //    int proxyCount = proxies.Length;
        //    for (int i = 0; i < proxyCount; i++)
        //    {
        //        proxies[i].UpdatePrevMatrix();
        //    }
        //}
    }
}
