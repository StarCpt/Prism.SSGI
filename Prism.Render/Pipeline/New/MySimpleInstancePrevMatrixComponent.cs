using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using VRageRender;

namespace Prism.Render.Pipeline.New;

public class MySimpleInstancePrevMatrixComponent : PrismVertexComponent //: MyComponent
{
    public override void AddComponent(MyVertexInputComponent component, List<InputElement> list, Dictionary<string, int> dict, StringBuilder declaration, StringBuilder code)
    {
        MyComponent.AddSingle("TEXCOORD", "float4 prev_matrix_row0", Format.R32G32B32A32_Float, component, list, dict, declaration);
        MyComponent.AddSingle("TEXCOORD", "float4 prev_matrix_row1", Format.R32G32B32A32_Float, component, list, dict, declaration);
        MyComponent.AddSingle("TEXCOORD", "float4 prev_matrix_row2", Format.R32G32B32A32_Float, component, list, dict, declaration);
        code.Append("matrix __prev_instance_matrix = construct_matrix_43(input.prev_matrix_row0, input.prev_matrix_row1, input.prev_matrix_row2);\\\n");
    }
}

// we unsafe cast this class as MyComponent
// wildly dangerous move but it works
public abstract class PrismVertexComponent
{
    public abstract void AddComponent(MyVertexInputComponent component, List<InputElement> list, Dictionary<string, int> dict, StringBuilder declaration, StringBuilder code);

    public static implicit operator MyComponent(PrismVertexComponent comp)
    {
        return Unsafe.As<MyComponent>(comp);
    }
}
