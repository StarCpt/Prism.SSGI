#include "bindings.hlsli"
#include "common.hlsli"

// From Activision GTAO paper: https://www.activision.com/cdn/research/s2016_pbs_activision_occlusion.pptx
float SpatialOffsets(int2 position)
{
    return 0.25 * float((position.y - position.x) & 3);
}

// From http://byteblacksmith.com/improvements-to-the-canonical-one-liner-glsl-rand-for-opengl-es-2-0/
float rand(float2 uv)
{
    float a = 12.9898;
    float b = 78.233;
    float c = 43758.5453;
    float dt = dot(uv.xy, float2(a, b));
    float sn = fmod(dt, 3.14);
    return frac(sin(sn) * c);
}

float2 GTAOFastAcos(float2 x)
{
    float2 outVal = -0.156583 * abs(x) + HALF_PI;
    outVal *= sqrt(1.0 - abs(x));
    return x >= 0 ? outVal : PI - outVal;
}

float3 ReconstructViewPosition(float hwDepth, float2 uv)
{
    return ComputeWorldDepth(hwDepth) * ComputeScreenRay(uv);
}

static const float2 InvScreenSize = 1.0 / ScreenSize;
static const uint SectorCount = 32;
static const float InvSectorCount = 1.0 / float(SectorCount);

float3 HorizonAngle(float3 viewPosition, float3 viewDir, float3 viewNormal, float3 projNormal, float2 rayOrigin, float2 rayDir, float stepSize, float initialStep, bool directionIsRight, float N, inout uint occludedSectors)
{
    float radius = stepSize * max(1, GI.StepCount - 1);
    float samplingDirection = directionIsRight ? 1 : -1;
    
    rayDir *= samplingDirection;
    
    float3 light = 0;
    for (uint i = 0; i < GI.StepCount; i++)
    {
        float rayOffset = pow(abs(stepSize * (i + initialStep)) / radius, GI.ExpFactor) * radius; // pixels
        float2 rayPosUV = rayOrigin + ((rayDir * InvScreenSize) * max(rayOffset, i + 1));
        
        if (any(saturate(rayPosUV) != rayPosUV)) // sometimes improves performance
            break;
        
        float rayHitDepthRaw = DepthBuffer.SampleLevel(PointSampler, rayPosUV, 0);
        float3 rayHitPos = ReconstructViewPosition(rayHitDepthRaw, rayPosUV);
        float3 rayHitPosBack = rayHitPos + (-viewDir * GI.Thickness);
        
        float3 rayHitDir = normalize(rayHitPos - viewPosition); // viewPos -> rayHitPos
        float3 rayHitDirBack = normalize(rayHitPosBack - viewPosition);
        
        float2 frontBackHorizon = float2(dot(rayHitDir, viewDir), dot(rayHitDirBack, viewDir));
        frontBackHorizon = GTAOFastAcos(clamp(frontBackHorizon, -1, 1));
        frontBackHorizon = saturate(((samplingDirection * -frontBackHorizon) - N + HALF_PI) / PI);
        frontBackHorizon = directionIsRight ? frontBackHorizon.yx : frontBackHorizon.xy;
        
        uint startSector = frontBackHorizon.x * 32; // already saturated so dont't need to clamp
        //uint endSector = round(frontBackHorizon.y * 32);
        uint sectorCount = round((frontBackHorizon.y - frontBackHorizon.x) * 32);
        
        uint newlyOccludedSectors = sectorCount > 0 ? ((0xFFFFFFFFu >> (32 - sectorCount)) << startSector) : 0; // exhaustively tested
        newlyOccludedSectors &= ~occludedSectors;
        occludedSectors |= newlyOccludedSectors;
        
        if (newlyOccludedSectors > 0)
        {
            float3 rayHitColor = LBuffer.SampleLevel(PointSampler, rayPosUV, GI.MipLevel);
            
            // currently just lambertian diffuse, can apply different brdf in the future
            float cosineTerm = saturate(dot(viewNormal, rayHitDir));
            
            if (any(rayHitColor > 0.001) && cosineTerm > 0.001)
            {
                float3 hitViewNormal = LoadViewNormal(rayPosUV * ScreenSize);
                
                float outgoingLightFactor = saturate(dot(hitViewNormal, -rayHitDir));
                
                light += countbits(newlyOccludedSectors) * InvSectorCount * rayHitColor * cosineTerm * (outgoingLightFactor > 0);
            }
        }
    }
    return light;
}

static const float spatialOffsets[4] = { 0, 0.5f, 0.25f, 0.75f };
static const float temporalRotations[6] = { 60 / 360.0, 300 / 360.0, 180 / 360.0, 240 / 360.0, 120 / 360.0, 0 / 360.0 };

#define USE_TEMPORAL_DIRECTIONS 0 // can help with low sample count scenarios but introduces unwanted flickering

float4 ps(const float4 position : SV_Position, const float2 uv : TEXCOORD) : SV_Target
{
    uint2 pixelPos = position.xy;
    
    if (!IsForeground(DepthBuffer[pixelPos]))
    {
        return LBuffer[pixelPos];
    }
    
    float3 viewPosition = ReconstructViewPosition(DepthBuffer[pixelPos], uv) * 0.999;
    float3 viewNormal = LoadViewNormal(pixelPos);
    float3 viewDir = normalize(-viewPosition);
    
    float noiseOffset = SpatialOffsets(pixelPos); // doesn't seem to do anything
    float noiseDirection = GradientNoise(pixelPos);
#if USE_TEMPORAL_DIRECTIONS
    float initialStep = spatialOffsets[(FrameIndex / 6) % 4] + rand(uv) * GI.JitterSamples;
#else
    float initialStep = spatialOffsets[FrameIndex % 4] + rand(uv) * GI.JitterSamples;
#endif
    float stepSize = max(GI.Radius * GI.HalfProjScale / -viewPosition.z, GI.StepCount) / float(GI.StepCount + 1); // in pixels
    
    float ambientOcclusion = 0;
    float3 light = 0;
    for (int slice = 0; slice < GI.SliceCount; slice++)
    {
        // 0 to 180 degrees, each slice covers the 180* 'opposite' direction
#if USE_TEMPORAL_DIRECTIONS
        float angleInRadians = PI * (float(slice + noiseDirection + temporalRotations[FrameIndex % 6]) / float(GI.SliceCount));
#else
        float angleInRadians = PI * (float(slice + noiseDirection) / float(GI.SliceCount));
#endif
        
        float2 rayDir; // screenspace slice tangent
        sincos(angleInRadians, rayDir.y, rayDir.x);

        // normal of the slice plane
        float3 sliceNormal = normalize(cross(float3(rayDir.x, -rayDir.y, 0), viewDir)); // super duper important to flip y
        
        // normal projected onto the slice plane
        float3 projNormal = normalize(viewNormal - sliceNormal * dot(viewNormal, sliceNormal));
        
        // slice tangent (perpendicular to viewDir and sliceNormal)
        float3 T = cross(viewDir, sliceNormal);
        
        // angle beween viewDir and projNormal in radians (may be negative)
        float N = -sign(dot(projNormal, T)) * acos(dot(projNormal, viewDir));
        
        uint bitmaskLeft = 0, bitmaskRight = 0;
        light += HorizonAngle(viewPosition, viewDir, viewNormal, projNormal, uv, rayDir, stepSize, initialStep, true, N, bitmaskLeft);
        light += HorizonAngle(viewPosition, viewDir, viewNormal, projNormal, uv, rayDir, stepSize, initialStep, false, N, bitmaskRight);
        
        ambientOcclusion += countbits(bitmaskLeft | bitmaskRight);
    }
    
    // get average value
    ambientOcclusion = ambientOcclusion * InvSectorCount / float(GI.SliceCount);
    // convert to [0,1] where 0 is fully occluded and 1 is fully unoccluded
    ambientOcclusion = 1 - saturate(ambientOcclusion);
    // apply intensity
    ambientOcclusion = saturate(pow(ambientOcclusion, GI.AOIntensity));
    
    // compute average
    light /= float(GI.SliceCount);
    light *= GI.GIIntensity;
    
    return float4(light, ambientOcclusion);
}