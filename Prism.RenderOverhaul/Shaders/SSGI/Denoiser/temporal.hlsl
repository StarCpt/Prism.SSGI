#include "common.hlsli"

Texture2D<float4> History      : register(t5);
Texture2D<float3> Source       : register(t6);
Texture2D<float3> velocityTex  : register(t7);
Texture2D<float> prevDepthTex  : register(t8);
Texture2D<float4> prevGBuffer1 : register(t9);

float3 ReprojectPrevViewNormal(float3 prevViewNormal)
{
    float3 prevWorldNormal = mul((float3x3) PrevViewMatrix, prevViewNormal);
    float3x3 invView = transpose((float3x3) ViewMatrix);
    return mul(invView, prevWorldNormal);
}

// result is not normalized!
float3 compute_screen_ray(float2 uv)
{
    const float ray_x = 1. / ProjMatrix._11;
    const float ray_y = 1. / ProjMatrix._22;
    float3 projOffset = float3(ProjMatrix._31 / ProjMatrix._11, ProjMatrix._32 / ProjMatrix._22, 0);
    return projOffset + float3(lerp(-ray_x, ray_x, uv.x), -lerp(-ray_y, ray_y, uv.y), -1.0);
}

#define ENABLE_TEMPORAL 1

float4 ps(const float4 position : SV_Position, const float2 uv : TEXCOORD) : SV_Target
{
#if VISUALIZE_MOTION
    return float4(abs(velocityTex[position.xy].xy) * 1000, 0, 1);
#endif
    
#if !ENABLE_TEMPORAL
    return float4(Source[position.xy].xyz, 1);
#endif
    
    const uint2 pixelPos = position.xy;
    const float rawDepth = DepthBuffer[pixelPos];
    if (!IsForeground(rawDepth))
    {
        return float4(Source[pixelPos].xyz, 0);
    }
    
    const float3 motion = velocityTex[pixelPos];
    const float2 prevPosF = pixelPos - motion.xy * ScreenSize;
    
    float2 xyf = frac(prevPosF);
    
    // bilinear offsets and weights
    const int2 offsets[4] = { int2(0, 0), int2(1, 0), int2(0, 1), int2(1, 1) };
    const float weights[4] = { (1 - xyf.x) * (1 - xyf.y), xyf.x * (1 - xyf.y), (1 - xyf.x) * xyf.y, xyf.x * xyf.y };
    
    float depthZ = ComputeWorldDepth(rawDepth);
    float3 viewDir = -normalize(compute_screen_ray(uv));
    float3 viewNormal = LoadViewNormal(pixelPos);
    
    float weightSum = 0;
    float4 historySum = 0;
    for (uint i = 0; i < 4; i++)
    {
        int2 offsetPos = prevPosF + offsets[i];
        if (any(offsetPos < 0 || offsetPos >= ScreenSize))
            continue;
        
        float prevRawDepth = prevDepthTex[offsetPos];
        if (!IsForeground(prevRawDepth))
            continue;
        
        float reprojectedZ = ComputeWorldDepth(prevRawDepth) + (motion.z * Farplane);
        float depthDiff = abs(depthZ - reprojectedZ);
        
        float dot_ray_surface = clamp(dot(viewDir, LoadViewNormal(pixelPos)), 0.01, 1);
        
        // regarding comparing normals between frames:
        // reprojection only takes camera rotation into account, but ignores the surface (block) rotation
        // this is theoretically a problem but not in practice since blocks don't usually rotate 45 degrees in one tick
        // something to keep in mind when adjusting the normal diff threshold.
        float3 prevViewNormalReproj = ReprojectPrevViewNormal(UnpackNormal(prevGBuffer1[offsetPos].xy));
        
        static const float DEPTH_DIFF_THRESHOLD = 0.05; // meters
        static const float NORMAL_DOT_DIFF_THRESHOLD = 0.5;
        
        bool depthRejected = depthDiff > (DEPTH_DIFF_THRESHOLD / dot_ray_surface);
        bool normalRejected = dot(prevViewNormalReproj, viewNormal) < NORMAL_DOT_DIFF_THRESHOLD;
        if (depthRejected || normalRejected)
            continue;
        
        weightSum += weights[i];
        historySum += weights[i] * History[offsetPos];
    }
    
    // xyz = color, w = history
    float4 history = weightSum > 0 ? (historySum / weightSum) : 0;
    history.w = max(history.w, 0);
    history.w = min(history.w + 1.0, Denoiser.MaxHistory); // cap accumulation factor
    
    float3 currentColor = Source[pixelPos].xyz;
    
    float3 finalColor = lerp(history.xyz, currentColor, 1.0 / history.w);
    return float4(finalColor, history.w);
}