#include "common.hlsli"

Texture2D<float4> History : register(t5);
Texture2D<float3> Source : register(t6);
Texture2D<float3> velocityTex : register(t7);
Texture2D<float3> prevDepthTex : register(t8);

void LoadHistory(float2 uv, out float3 color, out float weight)
{
    // TODO: bilinear with rejection
    float4 history = History.SampleLevel(PointSampler, uv, 0);
    color = history.xyz;
    weight = history.w;
    weight = clamp(weight, 0, Denoiser.MaxHistory);
    weight += 1.0;
}

float4 ps(const float4 position : SV_Position, const float2 uv : TEXCOORD) : SV_Target
{
    //return float4(Source[position.xy].xyz, 1);
#if VISUALIZE_MOTION
    return float4(abs(velocityTex[position.xy].xy) * 1000, 0, 1);
#endif
    
    const uint2 pixelPos = position.xy;
    const float3 currentColor = Source[pixelPos].xyz;
    
    const float rawDepth = DepthBuffer[pixelPos];
    if (rawDepth == 0) // not foreground
    {
        return float4(currentColor, 0);
    }

    const float2 prevUV = uv - velocityTex[pixelPos].xy;
    
    float prevRawDepth = prevDepthTex.SampleLevel(PointSampler, prevUV, 0);
    float reprojectedLinearDepth = ComputeWorldDepth(prevRawDepth) - (velocityTex[pixelPos].z * Farplane);
    
    float depthDiff = abs(ComputeWorldDepth(rawDepth) - reprojectedLinearDepth);
    if (any(saturate(prevUV) != prevUV) || depthDiff > 0.5)
    {
        return float4(currentColor, 1);
    }
    
    float3 historyColor;
    float historyLength;
    LoadHistory(prevUV, historyColor, historyLength);
    
    float3 result = lerp(historyColor, currentColor, 1.0 / historyLength);
    return float4(result, historyLength);
}