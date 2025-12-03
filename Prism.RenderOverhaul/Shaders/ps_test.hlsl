Texture2D<float4> velocity : register(t0);
Texture2D<float> depths : register(t1);

float4 ps(float4 position : SV_Position, float2 uv : TEXCOORD) : SV_Target
{
    float3 color = velocity[position.xy].xyz;
    color = isfinite(color) ? color : 0;
    color = abs(color);
    return float4(color.xyz * float3(5, 5, 1000), 0);
}
