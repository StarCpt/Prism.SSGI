#ifndef SSGI2_COMMON
#define SSGI2_COMMON

static const float PI = 3.141592653589793;
static const float HALF_PI = 1.5707963267948966;

bool IsForeground(float hwDepth)
{
    return hwDepth != 0;
}

// Interleaved gradient function from Jimenez 2014 http://goo.gl/eomGso
float GradientNoise(int2 position)
{
    return frac(52.9829189 * frac(dot(position, float2(0.06711056, 0.00583715))));
}

float sq(float val)
{
    return val * val;
}

float luminance(float3 color)
{
    return dot(color, float3(0.2126f, 0.7152f, 0.0722f));
}

#endif
