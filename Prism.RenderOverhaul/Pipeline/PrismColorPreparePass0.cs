using System.Collections.Generic;
using System.Runtime.InteropServices;
using VRage.Render11.GeometryStage2.Instancing;
using VRage.Render11.GeometryStage2.Model;
using VRage.Render11.GeometryStage2.PreparePass;
using VRageMath.PackedVector;
using VRageRender;

namespace Prism.Render.Pipeline;

public struct PrismColorPreparePass0 : ICustomPreparePass0
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct InstanceDataElement
    {
        public RowMatrix WorldMatrix;
        public RowMatrix PrevWorldMatrix;
        public HalfVector4 KeyColorDithering;
        public HalfVector4 ColorMultEmissivity;
    }

    public readonly unsafe int ElementSize => sizeof(InstanceDataElement);

    private InstanceDataElement[] _elements;
    private int _elementCount;

    public void InitInstanceElements(int elementsCount)
    {
        _elementCount = elementsCount;
        if (elementsCount > 0 && (_elements is null || _elements.Length < elementsCount))
        {
            _elements = new InstanceDataElement[elementsCount];
        }
    }

    public void AddInstanceIntoInstanceElements(int bufferOffset, MyInstance instance, int instanceMaterialOffsetInData, float stateData)
    {
        _elements[bufferOffset] = new()
        {
            WorldMatrix = instance.TransformStrategy.RowMatrix,
            PrevWorldMatrix = ((PrismInstance)instance).PrevRowMatrix,
            KeyColorDithering = new HalfVector4(instance.KeyColor.PackedValue | (ulong)HalfUtils.Pack(stateData) << 48),
            ColorMultEmissivity = instanceMaterialOffsetInData != -1 ? instance.GetInstanceMaterialPackedColorMultEmissivity(instanceMaterialOffsetInData) : MyInstanceMaterial.Default.PackedColorMultEmissivity,
        };
        // TODO: ensure this is only called once per frame
        // currently there's only 1 gbuffer pass per frame but who knows if it'll change in the future
        ((PrismInstance)instance).UpdatePrevMatrix();
    }

    public readonly List<int> GetInstanceMaterialOffsetsForThePass(MyLod lod) => lod.GetInstanceMaterialOffsets();
    public readonly int GetInstanceMaterialsCount(MyLod lod) => lod.InstanceMaterialsCount;

    public readonly void WriteData(ref MyMapping vb)
    {
        vb.WriteAndPosition(_elements, _elementCount);
    }
}
