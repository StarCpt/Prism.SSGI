#if defined(TECHNIQUE_MESH) || defined(TECHNIQUE_DECAL) || defined(TECHNIQUE_DECAL_NOPREMULT) || defined(TECHNIQUE_DECAL_CUTOUT)
#include <Geometry/Materials/Standard/Vertex.hlsl>
#elif defined(TECHNIQUE_SHIELD_LIT)
#include <Geometry/Materials/ShieldLit/Vertex.hlsl>
#elif defined(TECHNIQUE_SHIELD)
#include <Geometry/Materials/Shield/Vertex.hlsl>
#elif defined(TECHNIQUE_GLASS)
#include <Geometry/Materials/Glass/Vertex.hlsl>
#elif defined(TECHNIQUE_HOLO)
#include <Geometry/Materials/Holo/Vertex.hlsl>
#elif defined(TECHNIQUE_ALPHA_MASKED) || defined(TECHNIQUE_ALPHA_MASKED_SINGLE_SIDED)
#include <Geometry/Materials/AlphaMasked/Vertex.hlsl>
#endif

struct PrismVertexStageOutput
{
    float4 CurrClipPos : PRISM_CURR_CLIP_POS;
    float4 PrevClipPos : PRISM_PREV_CLIP_POS;
};

cbuffer PrevViewProjConstants : register(b6)
{
    float4x4 PrevViewProj;
    float Farplane;
    uint3 _pad1;
}

void vs(uint vertexId : SV_VertexID, __VertexInput input, out VertexStageOutput output, out PrismVertexStageOutput output2)
{
    __vertex_shader(input, output, vertexId);
    
    output2.CurrClipPos = output.position;
    
    //VertexShaderInterface vertex = __prepare_interface(input, vertexId);
    float4x4 prevInstanceMatrix = construct_matrix_43(input.prev_matrix_row0, input.prev_matrix_row1, input.prev_matrix_row2);
    float4 objPos = unpack_position_and_scale(input.position);
    float4 prevWorldPos = mul(objPos, prevInstanceMatrix);
    output2.PrevClipPos = mul(prevWorldPos, PrevViewProj);
}