struct Oct
{
	int parent;
	float3 lower;
	float3 higher;
	int empty;
	int4 childrenL;
	int4 childrenH;
	float4 vertsL;
	float4 vertsH;

	bool inside(float3 pos)
	{
		return all(lower <= pos) && all(pos <= higher);
	}
	float interpol_world(float3 pos)
	{
		float3 d = saturate((pos - lower) / (higher - lower)); //smoothstep(lower, higher, pos);//
		return interpol_inside(d);
		/*
		float cOO = lerp(vertsL.x, vertsL.y, d.x);
		float cOI = lerp(vertsL.z, vertsL.w, d.x);
		float cIO = lerp(vertsH.x, vertsH.y, d.x);
		float cII = lerp(vertsH.z, vertsH.w, d.x);
		return lerp(lerp(cOO, cOI, d.y),
			lerp(cIO, cII, d.y), d.z);*/
	}
	float interpol_inside(float3 d)
	{
		float cOO = lerp(vertsL.x, vertsL.y, d.x);
		float cOI = lerp(vertsL.z, vertsL.w, d.x);
		float cIO = lerp(vertsH.x, vertsH.y, d.x);
		float cII = lerp(vertsH.z, vertsH.w, d.x);
		return lerp(lerp(cOO, cOI, d.y),
			lerp(cIO, cII, d.y), d.z);

		/*float4 cO = lerp(vertsL, vertsH, d.z);
		float2 cI = lerp(cO.xy, cO.zw, d.y);
		return lerp(cI.x, cI.y, d.x);*/
	}
};

struct Info
{
	float3x3 heading;
	float3 position;
	float margin;
	float2 screen_size;
	int buffer_size;
	float limit;
	float3 light;
	float strength;
};

StructuredBuffer<Oct> data : register(t0);
cbuffer B : register(b0)
{
	Info inf;
}
Oct find(float3 pos, Oct c)
{
	int index = 0;
	int iterations = 0;
	if (c.parent >= 0) {
		do {
			index = c.parent;
			c = data[index];
		} while (!c.inside(pos) && c.parent >= 0);
	}
	while (index < inf.buffer_size && iterations < 12) {
		c = data[index];

		if (c.childrenL.x < 0) {
			return c;
		}

		float3 direction = pos - (c.lower + c.higher) / 2;
		int p = 0;
		if (direction.x > 0) {
			p = 1;
		}
		if (direction.y > 0) {
			p += 2;
		}
		if (direction.z > 0) {
			p += 4;
		}

		switch (p) {
		case 0:
			index = c.childrenL.x; break;
		case 1:
			index = c.childrenL.y; break;
		case 2:
			index = c.childrenL.z; break;
		case 3:
			index = c.childrenL.w; break;
		case 4:
			index = c.childrenH.x; break;
		case 5:
			index = c.childrenH.y; break;
		case 6:
			index = c.childrenH.z; break;
		case 7:
			index = c.childrenH.w; break;
		}
		iterations++;
	}
	return data[0];
}

float3 gradient(float3 pos, Oct frame)
{
	pos = (pos - frame.lower) / (frame.higher - frame.lower);
	float3 incX = float3(pos.x + inf.margin, pos.y, pos.z);
	float3 incY = float3(pos.x, pos.y + inf.margin, pos.z);
	float3 incZ = float3(pos.x, pos.y, pos.z + inf.margin);
	float at = frame.interpol_inside(pos);
	float x = frame.interpol_inside(incX);
	float y = frame.interpol_inside(incY);
	float z = frame.interpol_inside(incZ);
	return float3(x - at, y - at, z - at);
}

float3 ray(uint2 coord)
{
	float2 screendir = (float2)coord / inf.screen_size.y - float2( inf.screen_size.x / inf.screen_size.y * .5, .5);
	float3 dir = mul(float3(screendir, .5), inf.heading);
	return normalize(dir);
}

uint2 absoluteCoord(uint3 threadID, uint3 groupID)
{
	return threadID.xy + groupID.xy * uint2(groupSizeX, groupSizeY);
}

