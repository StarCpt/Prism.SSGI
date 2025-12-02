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
    uint3 _pad1;
}

void vs(uint vertexId : SV_VertexID, __VertexInput input, out VertexStageOutput output, out PrismVertexStageOutput output2)
{
    __vertex_shader(input, output, vertexId);
    
    output2.CurrClipPos = output.position;
    
#if defined(USE_SIMPLE_INSTANCING) // new pipeline
    float4x4 prevInstanceMatrix = construct_matrix_43(input.prev_matrix_row0, input.prev_matrix_row1, input.prev_matrix_row2);
    float4 objPos = unpack_position_and_scale(input.position);
    float4 prevWorldPos = mul(objPos, prevInstanceMatrix);
    output2.PrevClipPos = mul(prevWorldPos, PrevViewProj);
#else // old pipeline (TODO)
    output2.PrevClipPos = output2.CurrClipPos;
#endif
}