using HarmonyLib;
using Prism.Common;
using SharpDX.Direct3D11;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using VRage.Generics;
using VRage.Render11.GeometryStage2.Instancing;
using VRage.Render11.RenderContext;
using VRage.Render11.Resources;
using VRage.Render11.Scene.Components;
using VRageMath;
using VRageRender;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace Prism.Render;

public static class Extensions
{
    public static BufferMapping MapWriteDiscard(this IBuffer buffer)
    {
        return new BufferMapping(buffer.Buffer, MapMode.WriteDiscard);
    }

    public static BufferMapping MapWriteDiscard(this IBuffer buffer, MyRenderContext rc)
    {
        return new BufferMapping(rc.DeviceContext, buffer.Buffer, MapMode.WriteDiscard);
    }

    public static void SetConstantBuffer(this MyAllShaderStages stage, int slot, IConstantBuffer constantBuffer, int offsetInBytes, int num16ByteConstants)
    {
        if (offsetInBytes % 256 != 0)
            throw new Exception("Constant buffer offset must be a multiple of 256 bytes.");

        Buffer[] cbvs = [constantBuffer.Buffer];
        int[] offsets = [offsetInBytes / 16];
        int[] nums = [num16ByteConstants];

        stage.m_vertexStage.m_constantBuffers[slot] = null;
        stage.m_pixelStage.m_constantBuffers[slot] = null;
        stage.m_computeStage.m_constantBuffers[slot] = null;

        ((DeviceContext1)stage.m_vertexStage.m_deviceContext).VSSetConstantBuffers1(slot, 1, cbvs, offsets, nums);
        ((DeviceContext1)stage.m_pixelStage.m_deviceContext).PSSetConstantBuffers1(slot, 1, cbvs, offsets, nums);
        ((DeviceContext1)stage.m_computeStage.m_deviceContext).CSSetConstantBuffers1(slot, 1, cbvs, offsets, nums);

        stage.m_vertexStage.m_statistics.SetConstantBuffers++;
        stage.m_pixelStage.m_statistics.SetConstantBuffers++;
        stage.m_computeStage.m_statistics.SetConstantBuffers++;
    }

    private static TBase CreateInstance<TBase, TChild>() where TChild : TBase, new()
    {
        return new TChild();
    }

    public static void ChangeObjectType<T, TNew>(this MyObjectsPool<T> pool)
        where T : class, new()
        where TNew : T, new()
    {
        using (pool.m_activeLock.Acquire())
        {
            if (pool.m_active.Count > 0)
            {
                throw new Exception("Cannot change the object type of a pool with allocations.");
            }

            pool.m_activator = CreateInstance<T, TNew>;

            int unusedCount = pool.m_unused.Count;
            pool.m_unused.Clear();
            for (int i = 0; i < unusedCount; i++)
            {
                pool.m_unused.Enqueue(pool.m_activator());
            }
        }
    }

    public static void ChangeObjectType<TOriginal, TNew>(this MyGenericObjectPool pool)
        where TOriginal : class, IPooledObject
        where TNew : TOriginal, new()
    {
        lock (pool) // this is how MyObjectPoolManager locks the pool
        {
            if (pool.m_active.Count > 0 || pool.m_marked.Count > 0)
            {
                throw new Exception("Cannot change the object type of a pool with allocations.");
            }

            // set the readonly field with reflection
            FieldInfo field_m_storedType = AccessTools.Field(typeof(MyGenericObjectPool), nameof(MyGenericObjectPool.m_storedType));
            field_m_storedType.SetValue(pool, typeof(TNew));

            pool.m_unused.Clear();
            for (int i = 0; i < pool.m_baseCapacity; i++)
            {
                pool.m_unused.Enqueue(new TNew());
            }
        }
    }

    public static uint NextUInt(this Random rng)
    {
        return (uint)rng.Next(1 << 16) << 16 | (uint)rng.Next(1 << 16);
    }

    public static ref RowMatrix GetRowMatrixRef(this ref MyObjectDataCommon data)
    {
        return ref Unsafe.As<Vector4, RowMatrix>(ref data.m_row0);
    }
}
