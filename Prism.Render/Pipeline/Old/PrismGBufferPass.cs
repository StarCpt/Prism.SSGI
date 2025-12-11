using Prism.Render.Patches;
using Prism.Render.Utils;
using SharpDX.Direct3D11;
using System;
using VRage.Render11.Resources;
using VRageRender;

namespace Prism.Render.Pipeline.Old;

// don't tag this class with [HarmonyPatch], if you do it thinks there's a patch for the [virtual System.Void VRageRender.MyRenderingPass::Cleanup()] method,
// even if there's no patches in this class
public class PrismGBufferPass : MyRenderingPass
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public MyGBuffer GBuffer;
    public IRtvTexture VelocityBuffer;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private readonly RenderTargetView[] _rtvs = new RenderTargetView[4];

    [OverrideAfter]
    public new void Begin()
    {
        //base.Begin();
        Locals.BindConstantBuffersBatched = true;

        RC.VertexShader.SetConstantBuffer(4, Patch_MyRenderScheduler.PrismRenderConstants);
        RC.PixelShader.SetConstantBuffer(4, Patch_MyRenderScheduler.PrismRenderConstants);

        _rtvs[0] = GBuffer.GbufferRtvs[0];
        _rtvs[1] = GBuffer.GbufferRtvs[1];
        _rtvs[2] = GBuffer.GbufferRtvs[2];
        _rtvs[3] = VelocityBuffer.Rtv;

        RC.SetRtvs(GBuffer.DepthStencil.Dsv, _rtvs);
        Array.Clear(_rtvs, 0, _rtvs.Length);
    }

    //public override void End()
    //{
    //    base.End();
    //}

    [OverrideAfter]
    public new void SetProxyConstantsBatched(IConstantBuffer cb, int offset, int size)
    {
        RC.DeviceContext.VSSetConstantBuffer(7, cb.Buffer, offset / 16, size / 16);
    }

    // the original RecordCommandsInternal methods have stereo rendering code
    // but I removed it here since other parts of the SE pipeline don't support it anyway
    // for ex afaict the MyInstance renderer (new geometry pipeline) fully ditched stereo rendering support

#if DEV
    public override void RecordCommandsInternal(MyRenderableProxy proxy)
#else
    protected override void RecordCommandsInternal(MyRenderableProxy proxy)
#endif
    {
        SetProxyConstants(proxy);

        if (!Locals.BindConstantBuffersBatched)
        {
            RC.VertexShader.SetConstantBuffer(7, proxy.GetObjectBuffer(RC));
        }

        BindProxyGeometry(proxy);
        MyRenderUtils.BindShaderBundle(RC, ((PrismRenderableProxy)proxy).PrismGBufferShaders);
        if (MyRender11.Settings.Wireframe)
        {
            RC.SetDepthStencilState(MyDepthStencilStateManager.DepthTestWrite);
            RC.SetBlendState(null);
            if ((proxy.Flags & MyRenderableProxyFlags.DisableFaceCulling) > MyRenderableProxyFlags.None)
            {
                RC.SetRasterizerState(MyRasterizerStateManager.NocullWireframeRasterizerState);
            }
            else
            {
                RC.SetRasterizerState(MyRasterizerStateManager.WireframeRasterizerState);
            }
        }
        else
        {
            RC.SetDepthStencilState(proxy.GbufferDepthState);
            RC.SetBlendState(proxy.GbufferBlendState);
            RC.SetRasterizerState(proxy.GbufferRasterizerState);
        }

        MyDrawSubmesh drawSubmesh = proxy.DrawSubmesh;
        if (drawSubmesh.MaterialId != Locals.MatTexturesID)
        {
            Locals.MatTexturesID = drawSubmesh.MaterialId;
            if (drawSubmesh.MaterialId != MyMaterialProxyId.NULL)
            {
                MyMaterialProxy_2 myMaterialProxy_ = MyMaterials1.ProxyPool.Data[drawSubmesh.MaterialId.Index];
                MyRenderUtils.SetConstants(RC, ref myMaterialProxy_.MaterialConstants, 3);
                MyRenderUtils.SetSrvs(RC, ref myMaterialProxy_.MaterialSrvs);
            }
        }

        if (proxy.InstanceCount == 0)
        {
            RC.DrawIndexed(drawSubmesh.IndexCount, drawSubmesh.StartIndex, drawSubmesh.BaseVertex);
            Stats.Triangles += drawSubmesh.IndexCount / 3;
        }
        else
        {
            RC.DrawIndexedInstanced(drawSubmesh.IndexCount, proxy.InstanceCount, drawSubmesh.StartIndex, drawSubmesh.BaseVertex, proxy.StartInstance);
            Stats.Triangles += proxy.InstanceCount * drawSubmesh.IndexCount / 3;
        }

        Stats.Draws++;
    }

#if DEV
    public override void RecordCommandsInternal(ref MyRenderableProxy_2 proxy, int instance, int section)
#else
    protected override void RecordCommandsInternal(ref MyRenderableProxy_2 proxy, int instance, int section)
#endif
    {
        // TODO: setup velocity gbuffer shaders
        MyRenderUtils.SetSrvs(RC, ref proxy.ObjectSrvs);
        MyRenderUtils.BindShaderBundle(RC, proxy.Shaders.MultiInstance);
        RC.SetDepthStencilState(MyDepthStencilStateManager.DepthTestWrite);
        SetProxyConstants(ref proxy);
        for (int i = 0; i < proxy.Submeshes.Length; i++)
        {
            MyDrawSubmesh_2 myDrawSubmesh_ = proxy.Submeshes[i];
            MyMaterialProxy_2 myMaterialProxy_ = MyMaterials1.ProxyPool.Data[myDrawSubmesh_.MaterialId.Index];
            MyRenderUtils.SetConstants(RC, ref myMaterialProxy_.MaterialConstants, 3);
            MyRenderUtils.SetSrvs(RC, ref myMaterialProxy_.MaterialSrvs);
            if (proxy.InstanceCount == 0)
            {
                switch (myDrawSubmesh_.DrawCommand)
                {
                    case MyDrawCommandEnum.Draw:
                        RC.Draw(myDrawSubmesh_.Count, myDrawSubmesh_.Start);
                        break;
                    case MyDrawCommandEnum.DrawIndexed:
                        RC.DrawIndexed(myDrawSubmesh_.Count, myDrawSubmesh_.Start, myDrawSubmesh_.BaseVertex);
                        break;
                }
            }
            else
            {
                switch (myDrawSubmesh_.DrawCommand)
                {
                    case MyDrawCommandEnum.Draw:
                        RC.DrawInstanced(myDrawSubmesh_.Count, proxy.InstanceCount, myDrawSubmesh_.Start, proxy.StartInstance);
                        break;
                    case MyDrawCommandEnum.DrawIndexed:
                        RC.DrawIndexedInstanced(myDrawSubmesh_.Count, proxy.InstanceCount, myDrawSubmesh_.Start, myDrawSubmesh_.BaseVertex, proxy.StartInstance);
                        break;
                }
            }
        }
    }

    [OverrideAfter]
    public new void Cleanup()
    {
        //base.Cleanup();
        GBuffer = null!;
        VelocityBuffer = null!;
    }

    [OverrideAfter]
    public MyRenderingPass Fork(MyRenderingPass __baseResult)
    {
        PrismGBufferPass clone = (PrismGBufferPass)__baseResult; //PrismGBufferPass clone = (PrismGBufferPass)base.Fork();
        clone.GBuffer = GBuffer;
        clone.VelocityBuffer = VelocityBuffer;
        return clone;
    }
}
