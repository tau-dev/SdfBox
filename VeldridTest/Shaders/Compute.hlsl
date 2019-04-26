
#pragma warning( disable: 3571  )

#define groupSizeX 25
#define groupSizeY 25
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
		///*
		float cOO = lerp(vertsL.x, vertsL.y, d.x);
		float cOI = lerp(vertsL.z, vertsL.w, d.x);
		float cIO = lerp(vertsH.x, vertsH.y, d.x);
		float cII = lerp(vertsH.z, vertsH.w, d.x);
		return lerp(lerp(cOO, cOI, d.y),
			lerp(cIO, cII, d.y), d.z);
		//*/
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
};

StructuredBuffer<Oct> data : register(t0);
RWTexture2D<float3> tex : register(u0);
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
	float2 screendir = (float2)coord / inf.screen_size - float2(.5, .5);
	float3 dir = mul(float3(screendir, .5), inf.heading);
	return normalize(dir);
}
void set(float3 value, uint2 coord)
{
	tex[coord.xy] = value;//pow(value, 1 / 2.2);
}

float2 box_intersect(Oct c, float3 pos, float3 dir)
{
	float3 inv_dir = 1 / dir;
	float3 t1 = (c.lower - pos)*inv_dir;
	float3 t2 = (c.higher - pos)*inv_dir;

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

	for (int i = 0; prox > inf.margin; i++) {
		if (!(i < 100 && dot(pos, pos) < inf.limit && abs(prox) > inf.margin)) {
			set(float3(0.05, 0.1, 0.4), coord);
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
		/*
			float3 grad = float3(gradient(pos, current));
			float3 col = float3(abs(grad.x), abs(grad.y), abs(grad.z));
			set(float4(normalize(col), 0), coord);
			return;
		*/
    }

	prox = current.interpol_world(pos);
	pos += dir * prox * (1 + inf.margin);

	dir = normalize(inf.light - pos);
	pos += dir * inf.margin;
	float angle = dot(dir, normalize(gradient(pos, current)));
	if (angle < 0) {
		set(float3(0, 0, 0), coord);
		return;
	}
	float dist = length(inf.light - pos);
	float lighting_dist = dist;

	for (int j = 0; j < 40 && prox > -inf.margin; j++) {
		if (dist < prox) {
			float attenuation = angle / (lighting_dist*lighting_dist) * 5;
			set(float3(attenuation, attenuation, attenuation), coord);
			return;
		}
		current = find(pos, current); //data[0];//find(pos);
		if (current.empty == 0) {
			pos = pos + dir * box_intersect(current, pos, dir).y;
		}
		prox = current.interpol_world(pos);
		pos += dir * prox * (1 + inf.margin);
		dist = length(inf.light - pos);
	}
	set(float3(0, 0, 0), coord);
	return;
}