#if defined(TECHNIQUE_MESH) || defined(TECHNIQUE_DECAL) || defined(TECHNIQUE_DECAL_NOPREMULT) || defined(TECHNIQUE_DECAL_CUTOUT)
#include <Geometry/Materials/Standard/Pixel.hlsl>
#elif defined(TECHNIQUE_SHIELD_LIT)
#include <Geometry/Materials/ShieldLit/Pixel.hlsl>
#elif defined(TECHNIQUE_SHIELD)
#include <Geometry/Materials/Shield/Pixel.hlsl>
#elif defined(TECHNIQUE_GLASS)
#include <Geometry/Materials/Glass/Pixel.hlsl>
#elif defined(TECHNIQUE_HOLO)
#include <Geometry/Materials/Holo/Pixel.hlsl>
#elif defined(TECHNIQUE_ALPHA_MASKED) || defined(TECHNIQUE_ALPHA_MASKED_SINGLE_SIDED)
#include <Geometry/Materials/AlphaMasked/Pixel.hlsl>
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