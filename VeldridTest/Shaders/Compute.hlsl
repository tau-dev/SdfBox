
#pragma warning( disable: 3571  )

#define groupSizeX 24
#define groupSizeY 24
#define subdiv 2
#define textureWidth 2048
//16384

struct Cube
{
    float3 lower;
    float scale;
    
    float3 higher()
    {
        return lower + scale;
    }
    bool inside(float3 pos)
    {
        return all(lower <= pos) && all(pos <= higher());
    }
};

SamplerState samp : register(s0);

Texture2D values : register(t0);

static int index = 0;
float sample_at(float3 d, float scale)
{
    /*loat4 loadL = values.Load4(index * 8);
    float4 loadH = values.Load4((index + 1) * 8);
    float4 vertsL = (loadL / 255 - 0.5) * scale * 4;
    float4 vertsH = (loadH / 255 - 0.5) * scale * 4;
    float2 cO = lerp(vertsL.xz, vertsL.yw, d.x);
    float2 cI = lerp(vertsH.xz, vertsH.yw, d.x);
    return lerp(lerp(cO.x, cO.y, d.y),
			lerp(cI.x, cI.y, d.y), d.z);*/
    float2 dimensions;
    values.GetDimensions(dimensions.x, dimensions.y);
    float2 p = int2((index * 4) % textureWidth, index * 4 / textureWidth);
    float loadL = values.SampleLevel(samp, (d.xy + p) / dimensions, 0);
    float loadH = values.SampleLevel(samp, (d.xy + p + float2(2, 0)) / dimensions, 0);
    return (lerp(loadL, loadH, d.z) - .25) * scale * 4;
}

struct Oct
{
    Cube box;

    int parent;
	int empty;
    int children;
    /*
    float interpol_inside(float3 d)
    {
        float2 cO = lerp(vertsL.xz, vertsL.yw, d.x);
        float2 cI = lerp(vertsH.xz, vertsH.yw, d.x);
        return lerp(lerp(cO.x, cO.y, d.y),
			lerp(cI.x, cI.y, d.y), d.z);
    }*/
	float interpol_world(float3 pos)
	{
		float3 d = saturate((pos - box.lower) / box.scale); //smoothstep(lower, higher, pos);//
		return sample_at(d, box.scale);
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

StructuredBuffer<Oct> data : register(t1);
cbuffer B : register(b0)
{
	Info inf;
}
Oct find(float3 pos)
{
	//int index = 0;
	int iterations = 0;
    Oct c = data[index];
    
    //if (c.parent >= 0) {
        while (!c.box.inside(pos) && c.parent >= 0) {
            index = c.parent;
            c = data[index];
        }
    //}*/
	while (index < inf.buffer_size && iterations < 12) {

		if (c.children < 0)
			return c;
        
        int p = dot((int3) (saturate((pos - c.box.lower) / c.box.scale) * subdiv), int3(1, subdiv, subdiv * subdiv));
        index = c.children + p;
        c = data[index];
        iterations++;
    }
	return data[0];
}

float3 gradient(float3 pos, Oct frame)
{
	pos = (pos - frame.box.lower) / frame.box.scale;
	float3 incX = float3(pos.x + inf.margin, pos.y, pos.z);
	float3 incY = float3(pos.x, pos.y + inf.margin, pos.z);
	float3 incZ = float3(pos.x, pos.y, pos.z + inf.margin);
	float at = sample_at(pos, frame.box.scale);
    float x = sample_at(pos, frame.box.scale);
    float y = sample_at(pos, frame.box.scale);
    float z = sample_at(pos, frame.box.scale);
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
	float3 t1 = (c.box.lower - pos) * inv_dir;
    float3 t2 = (c.box.higher() - pos) * inv_dir;

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
    int pointer;
	Oct current = find(pos);
	// remove multiplications?
	int i;
	for (i = 0; prox > inf.margin; i++) {
		if (i >= 100 || dot(pos, pos) > inf.limit) { // || abs(prox) < inf.margin
			set(float4(0.05, 0.1, 0.4, i), coord);
			return;
		}
		current = find(pos); //data[0];//find(pos);
        /*
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
		}*/
		
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
	float dist = length(inf.light - pos) / 2;

	for (int j = 0; j < 40 && prox > -inf.margin; j++) {
		if (prox > dist || any(pos < 0) || any(pos > 1)) {
			float attenuation = angle / (dist*dist*4) * (exp2(inf.strength)- 1);
			set(float4(attenuation, attenuation, attenuation, i+j), coord);
			return;
		}
		current = find(pos);
		if (current.empty == 0) {
			pos = pos + dir * box_intersect(current, pos, dir).y;
		}
		prox = current.interpol_world(pos);
		pos += dir * (prox + inf.margin);
	}
	set(float4(0, 0, 0, 140), coord);
	return;
}