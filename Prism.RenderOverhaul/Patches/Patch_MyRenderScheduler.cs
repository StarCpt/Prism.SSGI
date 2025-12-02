using HarmonyLib;
using Prism.Common;
using Prism.Maths;
using Prism.Render.Pipeline;
using Sandbox;
using Sandbox.ModAPI;
using SharpDX.Direct3D11;
using System;
using System.IO;
using System.Runtime.InteropServices;
using VRage.FileSystem;
using VRage.Render11.Common;
using VRage.Render11.Render;
using VRage.Render11.RenderContext;
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
    static MyRenderContext RC => MyRender11.RC;

    static bool _compileError = false;
    static PixelShader? _psTest;
    static IConstantBuffer _prevMatricesCbv = null!;

    static MyCommon.MyFrameConstantsLayout _prevFrameConstants;

    public static unsafe void Init()
    {
        _prevMatricesCbv = MyManagers.Buffers.CreateConstantBuffer("Prism.Render.CBPrevMatrices", MathHelper.Align(sizeof(PrevMatricesConstants), 16), usage: ResourceUsage.Dynamic, isGlobal: true);

        ReloadShaders();
    }

    public static void ReloadShaders()
    {
        _psTest?.Dispose();

        var compiler = new FileShaderCompiler(Plugin.ShaderDirectory, Path.Combine(MyFileSystem.ShadersBasePath, "Shaders"));
        try
        {
            _psTest = compiler.CompilePixel(RC.DeviceContext.Device, "ps_test.hlsl", "ps");
            _compileError = false;
        }
        catch (Exception e)
        {
            _compileError = true;
            MySandboxGame.Static.Invoke(() => MyAPIGateway.Utilities.ShowMessage("Prism.Render", e.ToString()), "Prism.Render");
        }
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
        if (_compileError)
            return;

        { // testing stuff
            RC.SetRasterizerState(MyRasterizerStateManager.NocullRasterizerState);
            RC.SetDepthStencilState(MyDepthStencilStateManager.IgnoreDepthStencil);
            RC.SetBlendState(MyBlendStateManager.BlendAdditive);
            //RC.SetBlendState(MyBlendStateManager.BlendTransparent);

            RC.PixelShader.Set(_psTest);
            RC.PixelShader.SetSrv(0, GBufferVelocity.Get(MyGBuffer.Main));
            RC.PixelShader.SetSrv(1, MyGBuffer.Main.DepthStencil.SrvDepth);
            RC.SetRtv(MyGBuffer.Main.LBuffer);
            MyScreenPass.DrawFullscreenQuad(RC);
            RC.SetRtvNull();
        }
    }
}
