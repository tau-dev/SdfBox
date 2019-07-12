
#pragma warning( disable: 3571  )

#define groupSizeX 24
#define groupSizeY 24
#define OctCount 73

struct Oct
{
    float3 lower;
    float scale;

    float4 vertsL;
    float4 vertsH;

    int parent;
	int empty;
    int children;

    float3 higher()
    {
        return lower + scale;
    }

	bool inside(float3 pos)
	{
        return all(lower <= pos) && all(pos <= higher());
    }
    float interpol_inside(float3 d)
    {
        float cOO = lerp(vertsL.x, vertsL.y, d.x);
        float cOI = lerp(vertsL.z, vertsL.w, d.x);
        float cIO = lerp(vertsH.x, vertsH.y, d.x);
        float cII = lerp(vertsH.z, vertsH.w, d.x);
        return lerp(lerp(cOO, cOI, d.y),
			lerp(cIO, cII, d.y), d.z);
    }
	float interpol_world(float3 pos)
	{
		float3 d = saturate((pos - lower) / scale); //smoothstep(lower, higher, pos);//
		return interpol_inside(d);
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

		if (c.children < 0) {
			return c;
		}

        float3 direction = pos - (c.lower + c.scale / 2);
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
        index = c.children + p;
		iterations++;
	}
	return data[0];
}

float3 gradient(float3 pos, Oct frame)
{
	pos = (pos - frame.lower) / frame.scale;
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


RWTexture2D<float4> tex : register(u0);

uint2 absoluteCoord(uint3 threadID, uint3 groupID)
{
	return threadID.xy + groupID.xy * uint2(groupSizeX, groupSizeY);
}

void set(float4 value, uint2 coord)
{
	tex[coord.xy] = value;//pow(value, 1 / 2.2);
}

float2 box_intersect(Oct c, float3 pos, float3 dir)
{
	float3 inv_dir = 1 / dir;
	float3 t1 = (c.lower - pos) * inv_dir;
    float3 t2 = (c.higher() - pos) * inv_dir;

	return float2(max(max(
		min(t1.x, t2.x), 
		min(t1.y, t2.y)), 
		min(t1.z, t2.z)), // three intersections behind pos
	min(min(
		max(t1.x, t2.x), 
		max(t1.y, t2.y)), 
		max(t1.z, t2.z))); // three intersections after pos
}


[numthreads(groupSizeX, groupSizeY, 1)]
void main(uint3 groupID : SV_GroupID, uint3 threadID : SV_GroupThreadID)
{
    float3 pos = inf.position;
	uint2 coord = absoluteCoord(threadID, groupID);
    float3 dir = ray(coord);
	float prox = 1;
	Oct current = find(pos, data[0]);
	// remove multiplications?
	int i;
	for (i = 0; prox > inf.margin; i++) {
		if (!(i < 100 && dot(pos, pos) < inf.limit && abs(prox) > inf.margin)) {
			set(float4(0.05, 0.1, 0.4, i), coord);
			return;
		}
		current = find(pos, current); //data[0];//find(pos);
		if (current.empty == 0) {
			float2 res = box_intersect(current, pos, dir);
			if (res.x < res.y) {
				float3 a = pos + dir * res.x;
				float3 b = pos + dir * res.y;
				float vala = current.interpol_world(a);
				float valb = current.interpol_world(b);
				if (vala <= 0 || valb <= 0) {
					pos = lerp(a, b, vala / (vala - valb));
					break;
				}
			}
		}
		
        prox = current.interpol_world(pos);
		pos += dir * (prox) * (1 + inf.margin);
    }

	prox = current.interpol_world(pos);
	pos += dir * (prox - inf.margin);

	dir = normalize(inf.light - pos);
	pos += dir * inf.margin;
	float angle = dot(dir, normalize(gradient(pos, current)));
	if (angle < 0) {
		set(float4(0, 0, 0, i), coord);
		return;
	}
	float dist = length(inf.light - pos);

	for (int j = 0; j < 40 && prox > -inf.margin; j++) {
		if (prox > dist / 2) {
			float attenuation = angle / (dist*dist) * (exp2(inf.strength)- 1);
			set(float4(attenuation, attenuation, attenuation, i+j), coord);
			return;
		}
		current = find(pos, current);
		if (current.empty == 0) {
			pos = pos + dir * box_intersect(current, pos, dir).y;
		}
		prox = current.interpol_world(pos);
		pos += dir * (prox + inf.margin);
	}
	set(float4(0, 0, 0, 140), coord);
	return;
}