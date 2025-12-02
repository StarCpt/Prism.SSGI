Texture2D<float4> velocity : register(t0);
Texture2D<float> depths : register(t1);

float4 ps(float4 position : SV_Position, float2 uv : TEXCOORD) : SV_Target
{
    return float4(abs(velocity[position.xy].xy) * 5, 0, depths[position.xy] != 0);
}
