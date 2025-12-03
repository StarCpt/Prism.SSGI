#ifndef SSGI2_DEFINITIONS
#define SSGI2_DEFINITIONS

struct GIConstants
{
    float HalfProjScale;
    float TemporalOffsets;
    float TemporalDirections;
    bool JitterSamples;

    float GIIntensity;
    float AOIntensity;
    int SliceCount;
    uint StepCount; // steps per slice direction. a slice has 2*StepCount steps.
    
    float Radius; // radius in world space units (meters)
    float ExpFactor;
    float Thickness; // meters
    uint _pad1;
};

struct DenoiserConstants
{
    float MaxHistory;
    float BlurRadius;
    uint _pad1;
    uint _pad2;
};

#endif
