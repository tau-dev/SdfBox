//#include "Cast.hlsli"

struct FragmentIn
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
};

Texture2D image : register(t0);

SamplerState imageSampler;

float4 FS(FragmentIn input) : SV_Target0
{
	uint2 dimensions;
	image.GetDimensions(dimensions.x,dimensions.y);
	return image.Sample(imageSampler, input.Position.xy/dimensions);
}