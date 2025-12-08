#ifndef SSGI2_BINDINGS
#define SSGI2_BINDINGS

SamplerState DefaultSampler	: register(s0);
SamplerState PointSampler   : register(s1);
SamplerState LinearSampler  : register(s2);

struct GIConstants
{
    float HalfProjScale;
    uint _pad1;
    uint _pad2;
    bool JitterSamples;

    float GIIntensity;
    float AOIntensity;
    int SliceCount;
    uint StepCount; // steps per slice direction. a slice has 2*StepCount steps.
    
    float Radius; // radius in world space units (meters)
    float ExpFactor;
    float Thickness; // meters
    float MipLevel;
};

struct DenoiserConstants
{
    float MaxHistory;
    float BlurRadius;
    int AtrousStepSize;
    uint _pad2;
};

cbuffer Constants : register(b0)
{
    float4x4 ViewMatrix;
    float4x4 ProjMatrix;
    float4x4 InvProjMatrix;
    float4x4 PrevViewMatrix;
    
    float3 SunDirection;
    float Farplane;
    
    float2 ScreenSize;
    uint FrameIndex;
    uint RandomSeed;
    
    float3 CameraDelta;
    uint _pad1;
    
    GIConstants GI;
    DenoiserConstants Denoiser;
};

// xyz: color
// w: lod / 255.0
Texture2D<float4> GBuffer0 : register(t0); // RGBA8 unorm srgb
// xy: packed view space normals
// z: AO
// w: unused
Texture2D<float4> GBuffer1 : register(t1); // RGB10A2 unorm
// x: metalness
// y: gloss
// z: emissivity
// w: coverage / 255.0 (whats this?)
Texture2D<float4> GBuffer2 : register(t2); // RGBA8 unorm
// HDR render output
Texture2D<float4> LBuffer : register(t3); // R11G11B10 float (by default)
// nonlinear depth 1 -> 0
Texture2D<float> DepthBuffer : register(t4); // if HQ D32 float, else D24 unorm

float3 UnpackNormal(float2 packed)
{
    float2 fenc = mad(packed, 4, -2);
    float f = dot(fenc, fenc);
    float g = sqrt(1 - f / 4);
    return float3(fenc * g, 1 - f / 2);
}

float3 LoadViewNormal(uint2 pixel)
{
    return UnpackNormal(GBuffer1[pixel].xy);
}

 // nonlinear depth -> positive depth in meters
float ComputeWorldDepth(float rawDepth)
{
    return ProjMatrix._34 / (max(rawDepth, 1e-36) + ProjMatrix._33);
}

float LoadWorldDepth(uint2 pixel)
{
    return ComputeWorldDepth(DepthBuffer[pixel]);
}

// result is not normalized!
float3 ComputeScreenRay(float2 uv)
{
    const float ray_x = 1. / ProjMatrix._11;
    const float ray_y = 1. / ProjMatrix._22;
    float3 projOffset = float3(ProjMatrix._31 / ProjMatrix._11, ProjMatrix._32 / ProjMatrix._22, 0);
    return projOffset + float3(lerp(-ray_x, ray_x, uv.x), -lerp(-ray_y, ray_y, uv.y), -1.0);
}

#endif
