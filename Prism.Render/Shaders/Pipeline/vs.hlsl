#include "material_defines.hlsli"

#if defined(MATERIAL_STANDARD)
#include <Geometry/Materials/Standard/Vertex.hlsl>
#elif defined(MATERIAL_ALPHAMASKED)
#include <Geometry/Materials/AlphaMasked/Vertex.hlsl>
#elif defined(MATERIAL_ALPHAMASKEDARRAY)
#include <Geometry/Materials/AlphaMaskedArray/Vertex.hlsl>
#elif defined(MATERIAL_GLASS)
#include <Geometry/Materials/Glass/Vertex.hlsl>
#elif defined(MATERIAL_HOLO)
#include <Geometry/Materials/Holo/Vertex.hlsl>
#elif defined(MATERIAL_SHIELD)
#include <Geometry/Materials/Shield/Vertex.hlsl>
#elif defined(MATERIAL_SHIELDLIT)
#include <Geometry/Materials/ShieldLit/Vertex.hlsl>
#elif defined(MATERIAL_TEST)
#include <Geometry/Materials/Test/Vertex.hlsl>
#elif defined(MATERIAL_TRIPLANARDEBRIS)
#include <Geometry/Materials/TriplanarDebris/Vertex.hlsl>
#elif defined(MATERIAL_TRIPLANARMULTI)
#include <Geometry/Materials/TriplanarMulti/Vertex.hlsl>
#elif defined(MATERIAL_TRIPLANARSINGLE)
#include <Geometry/Materials/TriplanarSingle/Vertex.hlsl>
#endif

struct PrismVertexStageOutput
{
    float4 CurrClipPos : PRISM_CURR_CLIP_POS;
    float4 PrevClipPos : PRISM_PREV_CLIP_POS;
};

cbuffer PrevViewProjConstants : register(b4)
{
    float4x4 PrevViewProj;
    float Farplane;
    uint _pad1;
    uint _pad2;
    uint _pad3;
};

cbuffer Object2 : register(b7)
{
    ObjectConstants object_2; // unused, added to get proper field offsets for the following values
    float4 obj_prev_matrix_row0;
    float4 obj_prev_matrix_row1;
    float4 obj_prev_matrix_row2;
};

void vs(uint vertexId : SV_VertexID, __VertexInput input, out VertexStageOutput output, out PrismVertexStageOutput output2)
{
    __vertex_shader(input, output, vertexId);
    
    output2.CurrClipPos = output.position;
    
    const float4x4 invViewProj = frame_.Environment.inv_view_proj_matrix;
    
    float4 prevWorldPos;
    
#if defined(USE_SIMPLE_INSTANCING) // new pipeline
    float4x4 prevInstanceMatrix = construct_matrix_43(input.prev_matrix_row0, input.prev_matrix_row1, input.prev_matrix_row2);
    float4 objPos = unpack_position_and_scale(input.position);
    prevWorldPos = mul(objPos, prevInstanceMatrix);
#else
    float4 objPos;
    
#if defined(USE_SKINNING)
    float4x4 skinningMatrix = 0;
	[unroll]
    for (int i = 0; i < 4; i++)
    {
        skinningMatrix += object_.bone_matrix[input.blend_indices[i]] * input.blend_weights[i];
    }
    objPos = unpack_position_and_scale(input.position);
    objPos = mul(objPos, skinningMatrix);
    objPos /= objPos.w;
#else
    VertexShaderInterface vertex = __prepare_interface(input, vertexId); // pretty inefficient, unsure if the compiler optimizes out unused code
    objPos = vertex.position_scaled_untranslated;
#endif
    
    float4x4 prevObjMatrix = construct_matrix_43(obj_prev_matrix_row0, obj_prev_matrix_row1, obj_prev_matrix_row2);
    prevWorldPos = mul(objPos, prevObjMatrix);
#endif
    
    output2.PrevClipPos = mul(prevWorldPos, PrevViewProj);
}
