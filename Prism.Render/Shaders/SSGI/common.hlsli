#ifndef SSGI2_COMMON
#define SSGI2_COMMON

static const float PI = 3.141592653589793;
static const float HALF_PI = 1.5707963267948966;

bool IsForeground(float hwDepth)
{
    return hwDepth != 0;
}

// From Activision GTAO paper: https://www.activision.com/cdn/research/s2016_pbs_activision_occlusion.pptx
float SpatialOffsets(int2 position)
{
    return 0.25 * float((position.y - position.x) & 3);
}

// Interleaved gradient function from Jimenez 2014 http://goo.gl/eomGso
float GradientNoise(float2 position)
{
    return frac(52.9829189 * frac(dot(position, float2(0.06711056, 0.00583715))));
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

#endif
