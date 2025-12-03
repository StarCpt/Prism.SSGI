#ifndef SSGI2_BINDINGS
#define SSGI2_BINDINGS

#include "definitions.hlsli"

SamplerState DefaultSampler	: register(s0);
SamplerState PointSampler   : register(s1);
SamplerState LinearSampler  : register(s2);

cbuffer Constants : register(b0)
{
    float4x4 ViewMatrix;
    float4x4 ProjMatrix;
    float4x4 InvProjMatrix;
    
    float3 SunDirection;
    float Farplane;
    
    float2 ScreenSize;
    uint FrameIndex;
    uint RandomSeed;
    
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

float3 LoadViewNormal(uint2 pixel)
{
    float2 fenc = mad(GBuffer1[pixel].xy, 4, -2);
    float f = dot(fenc, fenc);
    float g = sqrt(1 - f / 4);
    return float3(fenc * g, 1 - f / 2);
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

#endif
