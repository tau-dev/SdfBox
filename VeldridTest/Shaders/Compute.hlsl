
#define groupSizeX 28
#define groupSizeY 28
#define OctCount 73

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
	float interpol(float3 pos)
	{
		float3 d = (pos - lower) / (higher - lower);
		float cOO = lerp(vertsL.x, vertsL.y, d.x);
		float cOI = lerp(vertsL.z, vertsL.w, d.x);
		float cIO = lerp(vertsH.x, vertsH.y, d.x);
		float cII = lerp(vertsH.z, vertsH.w, d.x);
		return lerp(lerp(cOO, cOI, d.y),
			lerp(cIO, cII, d.y), d.z);
	}
};

struct Info
{
	float3x3 heading;
	float3 position;
	float margin;
	float2 screen_size;
	uint buffer_size;
	float limit;
	float3 light;
};

StructuredBuffer<Oct> data : register(t0);
RWTexture2D<float3> tex : register(t1);
cbuffer B : register(b0)
{
	Info inf;
}
uint2 absoluteCoord(uint3 threadID, uint3 groupID)
{
	return threadID.xy + groupID.xy * uint2(groupSizeX, groupSizeY);
}


Oct find(float3 pos, Oct c)
{
	int index = 0;
	int iterations = 0;
	while (!c.inside(pos) && c.parent >= 0) {
		index = c.parent;
		c = data[index];
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
	float3 incX = float3(pos.x + inf.margin, pos.y, pos.z);
	float3 incY = float3(pos.x, pos.y + inf.margin, pos.z);
	float3 incZ = float3(pos.x, pos.y, pos.z + inf.margin);
	float at = frame.interpol(pos);
	float x = frame.interpol(incX);
	float y = frame.interpol(incY);
	float z = frame.interpol(incZ);
	return float3(x - at, y - at, z - at);
}

float3 ray(uint2 coord)
{
	float2 screendir = (float2)coord / inf.screen_size - float2(.5, .5);
	float3 dir = mul(float3(screendir, .5), inf.heading);
	return normalize(dir);
}
void set(float4 value, uint2 coord)
{
	tex[coord.xy] = value;
}



[numthreads(groupSizeX, groupSizeY, 1)]
void CS(uint3 groupID : SV_GroupID, uint3 threadID : SV_GroupThreadID)
{
    float3 pos = inf.position;
	uint2 coord = absoluteCoord(threadID, groupID);
    float3 dir = ray(coord);
	float prox = 1;
	Oct current = find(pos, data[0]);
	// remove multiplications?
	for (int i = 0; i < 100 && pos.x*pos.x + pos.y*pos.y + pos.z*pos.z < inf.limit && abs(prox) > inf.margin; i++) {
		current = find(pos, current); //data[0];//find(pos);
        prox = current.interpol(pos);
        
        if (prox <= inf.margin)
        {
			float3 grad = float3(gradient(pos, current));
			float3 col = float3(abs(grad.x), abs(grad.y), abs(grad.z));
            set(float4(normalize(col), 0), coord);
			return;
        }
        pos += dir * (prox) * (1 + inf.margin);
    }

    set(float4(0.1, 0.2, 0.4, 1), coord);
    return;
}