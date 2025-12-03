#include "material_defines.hlsli"

#if defined(MATERIAL_STANDARD)
#include <Geometry/Materials/Standard/Pixel.hlsl>
#elif defined(MATERIAL_ALPHAMASKED)
#include <Geometry/Materials/AlphaMasked/Pixel.hlsl>
#elif defined(MATERIAL_ALPHAMASKEDARRAY)
#include <Geometry/Materials/AlphaMaskedArray/Pixel.hlsl>
#elif defined(MATERIAL_GLASS)
#include <Geometry/Materials/Glass/Pixel.hlsl>
#elif defined(MATERIAL_HOLO)
#include <Geometry/Materials/Holo/Pixel.hlsl>
#elif defined(MATERIAL_SHIELD)
#include <Geometry/Materials/Shield/Pixel.hlsl>
#elif defined(MATERIAL_SHIELDLIT)
#include <Geometry/Materials/ShieldLit/Pixel.hlsl>
#elif defined(MATERIAL_TEST)
#include <Geometry/Materials/Test/Pixel.hlsl>
#elif defined(MATERIAL_TRIPLANARDEBRIS)
#include <Geometry/Materials/TriplanarDebris/Pixel.hlsl>
#elif defined(MATERIAL_TRIPLANARMULTI)
#include <Geometry/Materials/TriplanarMulti/Pixel.hlsl>
#elif defined(MATERIAL_TRIPLANARSINGLE)
#include <Geometry/Materials/TriplanarSingle/Pixel.hlsl>
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

float4 ComputeVelocity(PrismVertexStageOutput input)
{
    float4 vel;
    vel.xy = (input.CurrClipPos.xy / input.CurrClipPos.w) - (input.PrevClipPos.xy / input.PrevClipPos.w);
    vel.z = (input.PrevClipPos.w - input.CurrClipPos.w) / Farplane;
    vel.w = 0;
    return vel;
}

void ps(uint coverage : SV_Coverage, PixelStageInput input, PrismVertexStageOutput input2, out GbufferOutput output, out float4 velocity : SV_Target3)
{
    __pixel_shader(input, output, coverage);
    
    velocity = ComputeVelocity(input2);
}
