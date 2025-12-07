using Prism.Render.Patches;
using SharpDX.Direct3D11;
using System;
using System.Drawing;
using VRage;
using VRage.Render11.Resources;
using VRageRender;

namespace Prism.Render.Pipeline.Old;

public class PrismGBufferPass : MyRenderingPass
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public MyGBuffer GBuffer;
    public IRtvTexture VelocityBuffer;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private readonly RenderTargetView[] _rtvs = new RenderTargetView[4];

    public override void Begin()
    {
        base.Begin();
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

    public override void End()
    {
        base.End();
    }

    // the original RecordCommandsInternal methods have stereo rendering code
    // but I removed it here since other parts of the SE pipeline doesn't support it anyways
    // for ex afait the MyInstance renderer (new geometry pipeline) fully ditched stereo rendering support

    public override void RecordCommandsInternal(MyRenderableProxy proxy)
    {
        SetProxyConstants(proxy);

        if (!Locals.BindConstantBuffersBatched)
        {
            RC.VertexShader.SetConstantBuffer(7, proxy.GetObjectBuffer(RC));
        }
        else
        {
            throw new NotImplementedException();
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

    public override void RecordCommandsInternal(ref MyRenderableProxy_2 proxy, int instance, int section)
    {
        // shaders not implemented
        throw new NotImplementedException();
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

    public override void Cleanup()
    {
        base.Cleanup();
        GBuffer = null!;
        VelocityBuffer = null!;
    }

    public override MyRenderingPass Fork()
    {
        PrismGBufferPass clone = (PrismGBufferPass)base.Fork();
        clone.GBuffer = GBuffer;
        clone.VelocityBuffer = VelocityBuffer;
        return clone;
    }
}
