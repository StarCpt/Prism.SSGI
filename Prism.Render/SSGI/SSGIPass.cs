using Prism.Common;
using Prism.Render.Pipeline;
using Sandbox;
using Sandbox.Engine.Utils;
using Sandbox.ModAPI;
using SharpDX.Direct3D;
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
        private uint _pad1;
        private uint _pad2;
        public RawBool JitterSamples;

        public float GIIntensity;
        public float AOIntensity;
        public int SliceCount;
        public uint StepCount;

        public float Radius;
        public float ExpFactor;
        public float Thickness;
        public float MipLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DenoiserConstants
    {
        public float MaxHistory;
        public float BlurRadius;
        public int AtrousStepSize;
        private uint _pad2;
    }

    static bool _compileError = false;
    static PixelShader? _ps;
    static PixelShader? _psTemporal;
    static PixelShader? _psBlur;
    static PixelShader? _psSvgfTemporal;
    static PixelShader? _psSvgfAtrous;
    static PixelShader? _psSvgfAtrousBlendedOutput;
    static PixelShader? _psCopyBlend;
    static IConstantBuffer _cbv = null!;
    static IRtvTexture _lbufferCopy = null!;
    static IRtvTexture _historyTexture = null!;
    static IRtvTexture _prevMomentsAndHistoryLength = null!;
    static IRtvTexture _prevDepthTex = null!;
    static IRtvTexture _prevGBuffer1 = null!;
    // Rtv0: Replace
    // Rtv1: Additive
    static IBlendState _blendReplace0Additive1 = null!;

    static readonly Random _rand = new();
    static Matrix _prevViewMatrix = Matrix.Identity;

    public static unsafe void Init()
    {
        _cbv = MyManagers.Buffers.CreateConstantBuffer("Prism.SSGI2.CbvConstants", MathHelper.Align(sizeof(Constants), 16), usage: ResourceUsage.Dynamic, isGlobal: true);
        Vector2I res = MyRender11.BackBufferResolution;
        _lbufferCopy    = MyManagers.RwTextures.CreateRtv("Prism.SSGI2.RtvLBufferCopy",  res.X, res.Y, Format.R16G16B16A16_Float, mipLevels: 5, optionFlags: ResourceOptionFlags.GenerateMipMaps);
        _historyTexture = MyManagers.RwTextures.CreateRtv("Prism.SSGI2.RtvHistory",      res.X, res.Y, Format.R16G16B16A16_Float);
        _prevMomentsAndHistoryLength = MyManagers.RwTextures.CreateRtv("Prism.SSGI2.RtvPrevMomentsAndHistoryLength",  res.X, res.Y, Format.R16G16B16A16_Float);
        _prevDepthTex   = MyManagers.RwTextures.CreateRtv("Prism.SSGI2.RtvPrevDepth",    res.X, res.Y, Format.R32_Float);
        _prevGBuffer1   = MyManagers.RwTextures.CreateRtv("Prism.SSGI2.RtvPrevGBuffer1", res.X, res.Y, MyGBuffer.Main.GBuffer1.Format);

        BlendStateDescription blendDesc = new()
        {
            AlphaToCoverageEnable = false,
            IndependentBlendEnable = true,
        };
        blendDesc.RenderTarget[0] = MyBlendStateManager.BlendReplace.Description.RenderTarget[0];
        blendDesc.RenderTarget[1] = MyBlendStateManager.BlendAdditive.Description.RenderTarget[0];
        _blendReplace0Additive1 = MyManagers.BlendStates.CreateResource("Prism.SSGI2", ref blendDesc);

        ReloadShaders();
    }

    public static void ReloadShaders()
    {
        _ps?.Dispose();
        _psTemporal?.Dispose();
        _psBlur?.Dispose();
        _psSvgfTemporal?.Dispose();
        _psSvgfAtrous?.Dispose();
        _psSvgfAtrousBlendedOutput?.Dispose();
        _psCopyBlend?.Dispose();

        var compiler = new FileShaderCompiler(Plugin.ShaderDirectory, MyShaderCompiler.ShadersPath);
        SharpDX.Direct3D11.Device device = MyRender11.RC.DeviceContext.Device;
        try
        {
            _ps                        = compiler.CompilePixel(device, "SSGI/ps.hlsl", "ps");
            _psTemporal                = compiler.CompilePixel(device, "SSGI/Denoiser/temporal.hlsl", "ps");
            _psBlur                    = compiler.CompilePixel(device, "SSGI/Denoiser/blur.hlsl", "ps");
            _psSvgfTemporal            = compiler.CompilePixel(device, "SSGI/Denoiser/temporal.hlsl", "ps", new ShaderMacro("VARIANCE_GUIDED", 1));
            _psSvgfAtrous              = compiler.CompilePixel(device, "SSGI/Denoiser/atrous.hlsl", "ps", new ShaderMacro("VARIANCE_GUIDED", 1));
            _psSvgfAtrousBlendedOutput = compiler.CompilePixel(device, "SSGI/Denoiser/atrous.hlsl", "ps", new ShaderMacro("VARIANCE_GUIDED", 1), new ShaderMacro("ENABLE_BLENDED_OUTPUT", 1));
            _psCopyBlend               = compiler.CompilePixel(device, "SSGI/Denoiser/copyblend.hlsl", "ps", new ShaderMacro("VARIANCE_GUIDED", 1));

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

    private static void UpdateCbv(MyRenderContext rc, int atrousStepSize, bool updatePrevMatrices)
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
                    JitterSamples = true,

                    GIIntensity = MathHelper.Clamp(config.GIIntensity * 2f, 0, 1000),
                    AOIntensity = 1.5f,
                    SliceCount = MathHelper.Clamp(config.SliceCount, 0, 1000),
                    StepCount = (uint)MathHelper.Clamp(config.StepCount, 0, 1000),

                    Radius = MathHelper.Clamp(config.Radius, 0, 1000),
                    ExpFactor = MathHelper.Clamp(config.ExpFactor, 0, 1000),
                    Thickness = MathHelper.Clamp(config.Thickness, 0, 1000),
                    MipLevel = MathHelper.Clamp(config.InputMipLevel, 0, 1000),
                },

                Denoiser = new DenoiserConstants
                {
                    MaxHistory = MathHelper.Clamp(config.DenoiserMaxHistory, 0, 1000),
                    BlurRadius = MathHelper.Clamp(config.DenoiserBlurRadius, 0, 1000),
                    AtrousStepSize = atrousStepSize,
                },
            };
            mapping.Write(in data);
        }

        if (updatePrevMatrices)
        {
            _prevViewMatrix = MyRender11.Environment.Matrices.ViewAt0;
        }
    }

    public static void Run(MyRenderContext rc)
    {
        if (_compileError || !Plugin.SSGIConfig.Enabled)
            return;

        if (Plugin.IsRendererHijacked)
            return;

        UpdateCbv(rc, 0, true);

        ISrvTexture lightBuffer;
        if (Plugin.SSGIConfig.InputMipLevel > 0)
        {
            CopyReplace(rc, MyGBuffer.Main.LBuffer, _lbufferCopy);
            rc.SetRtvNull();
            rc.GenerateMips(_lbufferCopy);
            lightBuffer = _lbufferCopy;
        }
        else
        {
            lightBuffer = MyGBuffer.Main.LBuffer;
        }

        // common bindings
        rc.SetRasterizerState(MyRasterizerStateManager.NocullRasterizerState);
        rc.SetDepthStencilState(MyDepthStencilStateManager.IgnoreDepthStencil);
        rc.PixelShader.SetSamplers(0, MySamplerStateManager.StandardSamplers);
        rc.PixelShader.SetConstantBuffer(0, _cbv);
        rc.PixelShader.SetSrvs(0, MyGBuffer.Main.GBuffer0, MyGBuffer.Main.GBuffer1, MyGBuffer.Main.GBuffer2, lightBuffer, MyGBuffer.Main.DepthStencil.SrvDepth);

        IBorrowedRtvTexture tempRtv = MyManagers.RwTexturesPool.BorrowRtv("Prism.SSGI2.TempRtv1", Format.R16G16B16A16_Float);

        // main pass
        {
            rc.SetBlendState(null);
            rc.PixelShader.Set(_ps);
            rc.SetRtv(tempRtv);
            MyScreenPass.DrawFullscreenQuad(rc);
            rc.SetRtvNull();
        }

        const bool USE_SVGF = true;
        if (!USE_SVGF)
        {
            Denoise(rc, tempRtv, _historyTexture, MyGBuffer.Main.LBuffer);
        }
        else
        {
            DenoiseVarianceGuided(rc, tempRtv, _historyTexture, MyGBuffer.Main.LBuffer);
        }
        tempRtv.Release();

        rc.CopyResource(MyGBuffer.Main.GBuffer1, _prevGBuffer1);

        // note: MyCopyToRT will fuck you over!
        CopyReplace(rc, MyGBuffer.Main.DepthStencil.SrvDepth, _prevDepthTex);
        //rc.SetRtvNull();
    }

    private static void Denoise(MyRenderContext rc, ISrvTexture input, IRtvTexture history, IRtvTexture output)
    {
        IBorrowedRtvTexture tempRtv = MyManagers.RwTexturesPool.BorrowRtv("Prism.SSGI2.TempRtvDenoiser", Format.R16G16B16A16_Float);

        // temporal pass
        {
            rc.SetBlendState(null);
            rc.PixelShader.Set(_psTemporal);
            // 5: history
            // 6: noisy input
            // 7: velocity
            // 8: previous depth
            // 9: previous gbuffer1 (normals + ao)
            rc.PixelShader.SetSrvs(5, history, input, GBufferVelocity.Get(MyGBuffer.Main), _prevDepthTex, _prevGBuffer1);
            rc.SetRtv(tempRtv);
            MyScreenPass.DrawFullscreenQuad(rc);
            rc.SetRtvNull();
        }

        // blur pass
        {
            rc.SetBlendState(_blendReplace0Additive1);
            rc.PixelShader.Set(_psBlur);
            rc.PixelShader.SetSrv(5, tempRtv);
            rc.SetRtvs([history.Rtv, output.Rtv]);
            MyScreenPass.DrawFullscreenQuad(rc);
            //rc.SetRtvNull(); // does not need to be finished immediately
        }

        tempRtv.Release();
    }

    private static void DenoiseVarianceGuided(MyRenderContext rc, IRtvTexture input, IRtvTexture history, IRtvTexture output)
    {
        // color and variance
        IBorrowedRtvTexture tempRtv = MyManagers.RwTexturesPool.BorrowRtv("Prism.SSGI2.TempRtvDenoiser", Format.R16G16B16A16_Float);

        // temporal pass
        {
            IBorrowedRtvTexture tempRtvMomentsAndHistoryLength = MyManagers.RwTexturesPool.BorrowRtv("Prism.SSGI2.TempRtvDenoiserVariance", _prevMomentsAndHistoryLength.Format);

            rc.SetBlendState(null);
            rc.PixelShader.Set(_psSvgfTemporal);
            // 5: history
            // 6: noisy input
            // 7: velocity
            // 8: previous depth
            // 9: previous gbuffer1 (normals + ao)
            // 10: previous moments and history length
            rc.PixelShader.SetSrvs(5, history, input, GBufferVelocity.Get(MyGBuffer.Main), _prevDepthTex, _prevGBuffer1, _prevMomentsAndHistoryLength);
            rc.SetRtvs([tempRtv.Rtv, tempRtvMomentsAndHistoryLength.Rtv]);
            MyScreenPass.DrawFullscreenQuad(rc);
            rc.SetRtvNull();
            rc.PixelShader.SetSrv(10, null); // moments

            rc.CopyResource(tempRtvMomentsAndHistoryLength, _prevMomentsAndHistoryLength);
            tempRtvMomentsAndHistoryLength.Release();
        }

        // atrous passes
        int iterations = Plugin.SSGIConfig.DenoiserBlurIterations;
        if (iterations == 0)
        {
            rc.CopyResource(tempRtv, history);

            rc.SetBlendState(MyBlendStateManager.BlendAdditive);
            rc.PixelShader.Set(_psCopyBlend);
            rc.PixelShader.SetSrvs(5, history);
            rc.SetRtv(output);
            MyScreenPass.DrawFullscreenQuad(rc);
            rc.SetRtvNull();
        }
        else
        {
            IRtvTexture atrousInput = tempRtv;
            IRtvTexture atrousOutput = input;
            IRtvTexture gbufferVelocity = GBufferVelocity.Get(MyGBuffer.Main);

            for (int i = 0; i < iterations; i++)
            {
                UpdateCbv(rc, (1 << (iterations - 1)) >> i, false);

                bool isLastIteration = (i == iterations - 1);
                if (isLastIteration)
                {
                    rc.SetBlendState(_blendReplace0Additive1);
                    rc.PixelShader.Set(_psSvgfAtrousBlendedOutput);
                    rc.PixelShader.SetSrvs(5, atrousInput, gbufferVelocity);
                    rc.SetRtvs([atrousOutput.Rtv, output.Rtv]);
                }
                else
                {
                    rc.SetBlendState(MyBlendStateManager.BlendReplace);
                    rc.PixelShader.Set(_psSvgfAtrous);
                    rc.PixelShader.SetSrvs(5, atrousInput, gbufferVelocity);
                    rc.SetRtv(atrousOutput);
                }

                MyScreenPass.DrawFullscreenQuad(rc);
                rc.SetRtvNull();

                //if (i == 0)
                if (isLastIteration)
                {
                    rc.CopyResource(atrousOutput, history);
                }

                Swap(ref atrousInput, ref atrousOutput);
            }
        }

        tempRtv.Release();
    }

    private static void Swap<T>(ref T a, ref T b)
    {
        (a, b) = (b, a);
    }

    private static void CopyReplace(MyRenderContext rc, ISrvBindable source, IRtvBindable destination, MyViewport? viewport = null, bool shouldStretch = false)
    {
        CopyBlend(rc, null, source, destination, viewport, shouldStretch);
    }

    private static void CopyBlend(MyRenderContext rc, IBlendState? blend, ISrvBindable source, IRtvBindable destination, MyViewport? viewport = null, bool shouldStretch = false)
    {
        rc.SetBlendState(blend);

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
