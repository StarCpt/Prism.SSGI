using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Collections.Generic;
using System.Text;
using VRageRender;

namespace Prism.Render.Pipeline;

public class MySimpleInstancePrevMatrixComponent : MyComponent
{
    public override void AddComponent(MyVertexInputComponent component, List<InputElement> list, Dictionary<string, int> dict, StringBuilder declaration, StringBuilder code)
    {
        AddSingle("TEXCOORD", "float4 prev_matrix_row0", Format.R32G32B32A32_Float, component, list, dict, declaration);
        AddSingle("TEXCOORD", "float4 prev_matrix_row1", Format.R32G32B32A32_Float, component, list, dict, declaration);
        AddSingle("TEXCOORD", "float4 prev_matrix_row2", Format.R32G32B32A32_Float, component, list, dict, declaration);
        code.Append("matrix __prev_instance_matrix = construct_matrix_43(input.prev_matrix_row0, input.prev_matrix_row1, input.prev_matrix_row2);\\\n");
    }
}
