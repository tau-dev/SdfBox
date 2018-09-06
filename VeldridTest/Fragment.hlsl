struct FragmentIn
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
};

struct Oct
{
    float x;
    float3 higher;
    float3 lower;
};

Oct data[9];



float4 FS(FragmentIn input) : SV_Target0
{
    if (data[0].x != 0)
    {
        return input.Color + float4(0, 0, 255, 128);
    }
    return input.Color + float4(255, 0, 0, 128);
}