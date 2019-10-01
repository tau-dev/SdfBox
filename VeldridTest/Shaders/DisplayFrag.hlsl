//#include "Cast.hlsli"

struct FragmentIn
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
};

Texture2D image : register(t0);
cbuffer B : register(b0)
{
	bool debug;
}
SamplerState imageSampler;

float4 FS(FragmentIn input) : SV_Target0
{
	uint2 dimensions;
	image.GetDimensions(dimensions.x,dimensions.y);
    float4 val = image.Sample(imageSampler, input.Position.xy / dimensions);
	if(debug)
		return float4(1, 1, 1, 0) * val.w / 140;
    else
        return pow(val, 1 / 2.2);
}