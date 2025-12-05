using Prism.Common;
using Prism.Render.Pipeline;
using Sandbox;
using Sandbox.ModAPI;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using System;
using System.Runtime.InteropServices;
using VRage.Render.Scene;
using VRage.Render11.Common;
using VRage.Render11.RenderContext;
using VRage.Render11.Resources;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace Prism.Render.SSGI;

public static class SSGIPass
{
    [StructLayout(LayoutKind.Sequential)]
    struct Constants
    {
        public Matrix ViewMatrix;
        public Matrix ProjMatrix;
        public Matrix InvProjMatrix;
        public Matrix PrevViewMatrix;

        public Vector3 SunDirection;
        public float Farplane;

        public Vector2 ScreenSize;
        public uint FrameIndex;
        public uint RandomSeed;

        public Vector3 CameraDelta;
        private uint _pad1;

        public GIConstants GI;
        public DenoiserConstants Denoiser;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct GIConstants
    {
        public float HalfProjScale;
        public float TemporalOffsets;
        public float TemporalDirections;
        public RawBool JitterSamples;

        public float GIIntensity;
        public float AOIntensity;
        public int SliceCount;
        public uint StepCount;

        public float Radius;
        public float ExpFactor;
        public float Thickness;
        private uint _pad1;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DenoiserConstants
    {
        public float MaxHistory;
        public float BlurRadius;
        private uint _pad1;
        private uint _pad2;
    }

    static bool _compileError = false;
    static PixelShader? _ps;
    static PixelShader? _psTemporal;
    static PixelShader? _psBlur;
    static IConstantBuffer _cbv = null!;
    static IRtvTexture _historyTexture = null!;
    static IRtvTexture _prevDepthTex = null!;
    static IRtvTexture _prevGBuffer1 = null!;
    // Rtv0: Replace
    // Rtv1: Additive
    static IBlendState _blendReplaceNoAlpha0Additive1 = null!;

    // From Activision GTAO paper: https://www.activision.com/cdn/research/s2016_pbs_activision_occlusion.pptx
    static readonly float[] _spatialOffsets = { 0, 0.5f, 0.25f, 0.75f };
    static readonly float[] _temporalRotations = { 60, 300, 180, 240, 120, 0 };

    static readonly Random _rand = new();
    static Matrix _prevViewMatrix = Matrix.Identity;

    public static unsafe void Init()
    {
        _cbv = MyManagers.Buffers.CreateConstantBuffer("Prism.SSGI2.CbvConstants", MathHelper.Align(sizeof(Constants), 16), usage: ResourceUsage.Dynamic, isGlobal: true);
        _historyTexture = MyManagers.RwTextures.CreateRtv("Prism.SSGI2.RtvHistory", MyRender11.BackBufferResolution.X, MyRender11.BackBufferResolution.Y, Format.R16G16B16A16_Float);
        _prevDepthTex = MyManagers.RwTextures.CreateRtv("Prism.SSGI2.RtvPrevDepth", MyRender11.BackBufferResolution.X, MyRender11.BackBufferResolution.Y, Format.R32_Float);
        _prevGBuffer1 = MyManagers.RwTextures.CreateRtv("Prism.SSGI2.RtvPrevGBuffer1", MyRender11.BackBufferResolution.X, MyRender11.BackBufferResolution.Y, MyGBuffer.Main.GBuffer1.Format);

        BlendStateDescription blendDesc = new()
        {
            AlphaToCoverageEnable = false,
            IndependentBlendEnable = true,
        };
        blendDesc.RenderTarget[0] = MyBlendStateManager.BlendReplace.Description.RenderTarget[0];
        blendDesc.RenderTarget[1] = MyBlendStateManager.BlendAdditive.Description.RenderTarget[0];
        _blendReplaceNoAlpha0Additive1 = MyManagers.BlendStates.CreateResource("Prism.SSGI2", ref blendDesc);

        ReloadShaders();
    }

    public static void ReloadShaders()
    {
        _ps?.Dispose();
        _psTemporal?.Dispose();
        _psBlur?.Dispose();

        var compiler = new FileShaderCompiler(Plugin.ShaderDirectory, MyShaderCompiler.ShadersPath);
        try
        {
            _ps         = compiler.CompilePixel(MyRender11.RC.DeviceContext.Device, "SSGI/ps.hlsl", "ps");
            _psTemporal = compiler.CompilePixel(MyRender11.RC.DeviceContext.Device, "SSGI/Denoiser/temporal.hlsl", "ps");
            _psBlur     = compiler.CompilePixel(MyRender11.RC.DeviceContext.Device, "SSGI/Denoiser/blur.hlsl", "ps");

            _compileError = false;
        }
        catch (Exception e)
        {
#if DEV
            _compileError = true;
            MyLog.Default.WriteLine(e);
            MySandboxGame.Static.Invoke(() =>
            {
                try
                {
                    MyAPIGateway.Utilities.ShowMessage("Prism.SSGI2", e.ToString());
                }
                catch { }
            }, "Prism.SSGI2");
#else
            throw;
#endif
        }
    }

    private static void UpdateCbv(MyRenderContext rc)
    {
        using (var mapping = _cbv.MapWriteDiscard(rc))
        {
            var env = MyRender11.Environment;
            long frame = MyScene.FrameCounter;
            var config = Plugin.SSGIConfig;
            var data = new Constants
            {
                ViewMatrix = env.Matrices.ViewAt0,
                ProjMatrix = env.Matrices.Projection,
                InvProjMatrix = env.Matrices.InvProjection,
                PrevViewMatrix = _prevViewMatrix,

                SunDirection = -env.Data.EnvironmentLight.SunLightDirection,
                Farplane = env.Matrices.FarClipping,

                ScreenSize = MyRender11.ResolutionF,
                FrameIndex = (uint)frame,
                RandomSeed = _rand.NextUInt(),

                CameraDelta = MyCommon.FrameConstantsData.Environment.CameraPositionDelta,

                GI = new GIConstants
                {
                    HalfProjScale = (float)(MyRender11.ResolutionF.Y / (Math.Tan(env.Matrices.FovH * 0.5) * 2) * 0.5),
                    //TemporalOffsets = _spatialOffsets[(frame / 6) % 4],
                    TemporalOffsets = _spatialOffsets[frame % 4],
                    //TemporalDirections = _temporalRotations[frame % 6] / 360f, // can help with low sample count scenarios but introduces unwanted flickering
                    JitterSamples = true,

                    GIIntensity = MathHelper.Clamp(config.GIIntensity * 2.5f, 0, 1000),
                    AOIntensity = 1.5f,
                    SliceCount = MathHelper.Clamp(config.SliceCount, 0, 1000),
                    StepCount = (uint)MathHelper.Clamp(config.StepCount, 0, 1000),

                    Radius = MathHelper.Clamp(config.Radius, 0, 1000),
                    ExpFactor = MathHelper.Clamp(config.ExpFactor, 0, 1000),
                    Thickness = MathHelper.Clamp(config.Thickness, 0, 1000),
                },

                Denoiser = new DenoiserConstants
                {
                    MaxHistory = MathHelper.Clamp(config.DenoiserMaxHistory, 0, 1000),
                    BlurRadius = MathHelper.Clamp(config.DenoiserBlurRadius, 0, 1000),
                },
            };
            mapping.Write(in data);
        }

        _prevViewMatrix = MyRender11.Environment.Matrices.ViewAt0;
    }

    public static void Run(MyRenderContext rc)
    {
        if (_compileError || !Plugin.SSGIConfig.Enabled)
            return;

        UpdateCbv(rc);

        // common bindings
        rc.SetRasterizerState(MyRasterizerStateManager.NocullRasterizerState);
        rc.SetDepthStencilState(MyDepthStencilStateManager.IgnoreDepthStencil);
        rc.PixelShader.SetSamplers(0, MySamplerStateManager.StandardSamplers);
        rc.PixelShader.SetConstantBuffer(0, _cbv);
        rc.PixelShader.SetSrvs(0, MyGBuffer.Main.GBuffer0, MyGBuffer.Main.GBuffer1, MyGBuffer.Main.GBuffer2, MyGBuffer.Main.LBuffer, MyGBuffer.Main.DepthStencil.SrvDepth);

        IBorrowedRtvTexture tempRtv = MyManagers.RwTexturesPool.BorrowRtv("Prism.SSGI2.TempRtv1", Format.R16G16B16A16_Float);

        // main pass
        {
            rc.SetBlendState(null);
            rc.PixelShader.Set(_ps);
            rc.SetRtv(tempRtv);
            MyScreenPass.DrawFullscreenQuad(rc);
            rc.SetRtvNull();
        }

        IBorrowedRtvTexture tempRtv2 = MyManagers.RwTexturesPool.BorrowRtv("Prism.SSGI2.TempRtv2", Format.R16G16B16A16_Float);

        // temporal pass
        {
            rc.SetBlendState(null);
            rc.PixelShader.Set(_psTemporal);
            // 5: history
            // 6: noisy input
            // 7: velocity
            // 8: previous depth
            // 9: previous gbuffer1 (normals + ao)
            rc.PixelShader.SetSrvs(5, _historyTexture, tempRtv, GBufferVelocity.Get(MyGBuffer.Main), _prevDepthTex, _prevGBuffer1);
            rc.SetRtv(tempRtv2);
            MyScreenPass.DrawFullscreenQuad(rc);
            rc.SetRtvNull();
        }

        // blur pass
        {
            rc.SetBlendState(_blendReplaceNoAlpha0Additive1);
            rc.PixelShader.Set(_psBlur);
            rc.PixelShader.SetSrv(5, tempRtv2);
            rc.SetRtvs([_historyTexture.Rtv, MyGBuffer.Main.LBuffer.Rtv]);
            MyScreenPass.DrawFullscreenQuad(rc);
            //rc.SetRtvNull(); // does not need to be finished immediately
        }

        tempRtv.Release();
        tempRtv2.Release();

        rc.CopyResource(MyGBuffer.Main.GBuffer1, _prevGBuffer1);

        // note: MyCopyToRT will fuck you over!
        CopyReplace(rc, MyGBuffer.Main.DepthStencil.SrvDepth, _prevDepthTex);
        rc.SetRtvNull();
    }

    private static void CopyReplace(MyRenderContext rc, ISrvBindable source, IRtvBindable destination, MyViewport? viewport = null, bool shouldStretch = false)
    {
        rc.SetBlendState(null);

        rc.SetInputLayout(null);
        if (source.Size != destination.Size || shouldStretch)
        {
            if (shouldStretch)
            {
                rc.PixelShader.Set(MyCopyToRT.m_stretchPs);
            }
            else
            {
                rc.PixelShader.Set(MyCopyToRT.m_copyFilterPs);
                rc.PixelShader.SetSampler(2, MySamplerStateManager.Linear);
            }
        }
        else
        {
            rc.PixelShader.Set(MyCopyToRT.m_copyPs);
        }

        rc.SetRtv(destination);
        rc.SetDepthStencilState(MyDepthStencilStateManager.IgnoreDepthStencil);
        rc.PixelShader.SetSrv(0, source);
        MyScreenPass.DrawFullscreenQuad(rc, viewport ?? new MyViewport(destination.Size.X, destination.Size.Y));
    }
}
