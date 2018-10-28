
#define groupSizeX 32
#define groupSizeY 32
#define OctCount 73

struct Oct
{
	int Parent;
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

struct Info {
	float3x3 heading;
	float3 position;
	float margin;
	uint2 absoluteDimensions;
};

StructuredBuffer<Oct> data : register(t0);
cbuffer B : register(b1)
{
	Info inf;
}
RWStructuredBuffer<float4> BufferOut : register(u0);
uint2 absoluteCoord(uint3 threadID, uint3 groupID)
{
	return threadID.xy + groupID.xy * uint2(groupSizeX, groupSizeY);
}


Oct find(float3 pos)
{
    int i = 0;
    while (i < OctCount) {
        Oct c = data[i];
        
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
				i = c.childrenL.x; break;
			case 1:
				i = c.childrenL.y; break;
			case 2:
				i = c.childrenL.z; break;
			case 3:
				i = c.childrenL.w; break;
			case 4:
				i = c.childrenH.x; break;
			case 5:
				i = c.childrenH.y; break;
			case 6:
				i = c.childrenH.z; break;
			case 7:
				i = c.childrenH.w; break;
		}
    }
    return data[0];
}
float3 Gradient(float3 pos)
{
	Oct frame = find(pos);
	float3 incX = float3(pos.x + 1, pos.y, pos.z);
	float3 incY = float3(pos.x, pos.y + 1, pos.z);
	float3 incZ = float3(pos.x, pos.y, pos.z + 1);
	float at = frame.interpol(pos);
	float x = frame.interpol(incX);
	float y = frame.interpol(incY);
	float z = frame.interpol(incZ);
	return float3(x - at, y - at, z - at);
}
float3 ray(uint2 coord)
{
	return normalize(mul(float3(inf.absoluteDimensions / (float2)coord - float2(.5, .5), .5), inf.heading));
}
void set(float4 value, uint2 coord)
{
	BufferOut[coord.x + coord.y*inf.absoluteDimensions.x] = value;
}



[numthreads(groupSizeX, groupSizeY, 1)]
void CS(uint3 groupID : SV_GroupID, uint3 threadID : SV_GroupThreadID)
{
    float3 pos = inf.position;
	uint2 coord = absoluteCoord(threadID, groupID);
    float3 dir = ray(coord);
	float prox = 1;

	for (int i = 0; i < 100 && data[0].inside(pos) && abs(prox) > inf.margin; i++)
    {
		Oct current = find(pos); //data[0];//find(pos);
        prox = current.interpol(pos);
        
        if (prox <= inf.margin)
        {
			float3 grad = float3(Gradient(pos));
			float3 col = float3(abs(grad.x), abs(grad.y), abs(grad.z));
            set(float4(normalize(col), 0), threadID, groupID);
			return;
        }
        pos += dir * (prox) * (1 + inf.margin);
    }

    set(float4(0.1, 0.2, 0.4, 1), coord);
    return;
}