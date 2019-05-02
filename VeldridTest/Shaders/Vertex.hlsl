struct VertexIn
{
    float3 Position : POSITION0;
    float4 Color : COLOR0;
};

struct FragmentIn
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
};

FragmentIn VS(VertexIn input)
{
    FragmentIn output;
    output.Position = float4(input.Position, 1);
    output.Color = input.Color;
    return output;
}
