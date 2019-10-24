
#pragma warning( disable: 3571  )

#define groupSize 12
#define subdiv 2
#define textureWidth 8192
//2048


Texture2D values : register(t0);
SamplerState samp : register(s0);

static uint index = 0;
static float2 dimensions;

float sam(float2 p)
{
    return values.SampleLevel(samp, (p + float2(.5, .5)) * dimensions, 0).x;
}
float sample_at(float3 d, float scale)
{
    float2 p = int2((index * 4) % textureWidth, index * 4 / textureWidth * 2);
    float2 pl = (d.xy + p);
    float2 ph = (d.xy + p + float2(2, 0));

    float loadL = sam(d.xy + p);
    float loadH = sam(d.xy + p + float2(2, 0));
    float result = (lerp(loadL, loadH, d.z) - .25) * scale * 2;
    return result;// - .2 * result * result;
}

struct Cube
{
    float3 lower;
    float scale;
    
    void scale_up()
    {
        scale *= 2;
        lower = floor(lower / scale) * scale;
    }
    void scale_down(int3 p)
    {
        scale /= 2;
        lower += p * scale;
    }
    float3 higher()
    {
        return lower + scale;
    }
    bool inside(float3 pos)
    {
        return all(lower <= pos) && all(pos <= higher());
    }
    float interpol_world(float3 pos)
    {
        float3 d = saturate((pos - lower) / scale); //smoothstep(lower, higher, pos);//
        return sample_at(d, scale);
    }
};

static Cube box;


struct Oct
{
    int parent;
    int children;
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
	float strength;
    float fov;
};

StructuredBuffer<Oct> data : register(t1);
cbuffer B : register(b0)
{
	Info inf;
}
Oct find(float3 pos)
{
    int iterations = 0;
    Oct c = data[index];
    
    while (!box.inside(pos) && c.parent >= 0) {
        index = c.parent;
        c = data[index];
        box.scale_up();
    }
    while (index < inf.buffer_size && iterations < 12 && c.children >= 0)
    {
        int3 dir = (int3) saturate((pos - box.lower) / box.scale * subdiv);
        int p = dot(dir, int3(1, subdiv, subdiv * subdiv));
        index = c.children + p;
        c = data[index];
        box.scale_down(dir);
        iterations++;
    }
    return c;
}



float3 gradient(float3 pos)
{
    float3 d = saturate((pos - box.lower) / box.scale);
    float2 p = int2((index * 4) % textureWidth, index * 4 / textureWidth * 2);
    float2 ph = p + float2(2, 0);

    float2 xzl = (float2(d.x, 0) + p);

    float xl = lerp(sam(float2(0, d.y) + p), sam(float2(0, d.y) + ph), d.z);
    float xh = lerp(sam(float2(1, d.y) + p), sam(float2(1, d.y) + ph), d.z);

    float yl = lerp(sam(float2(d.x, 0) + p), sam(float2(d.x, 0) + ph), d.z);
    float yh = lerp(sam(float2(d.x, 1) + p), sam(float2(d.x, 1) + ph), d.z);
    
    float zl = sam(d.xy + p);
    float zh = sam(d.xy + ph);

    return float3(xh-xl, yh-yl, zh-zl);
}
/*
float3 gradient(float3 pos)
{
    float2 d = float2(box.scale * 0.5, 0);
    find(pos);
    float c = box.interpol_world(pos);
    find(pos + d.xyy);
    float dx = box.interpol_world(pos + d.xyy);
    find(pos + d.yxy);
    float dy = box.interpol_world(pos + d.yxy);
    find(pos + d.yyx);
    float dz = box.interpol_world(pos + d.yyx);
    return float3(dx - c, dy - c, dz - c);
}
*/

float2 box_intersect(Oct c, float3 pos, float3 dir)
{
    float3 inv_dir = 1 / dir;
    float3 t1 = (box.lower - pos) * inv_dir;
    float3 t2 = (box.higher() - pos) * inv_dir;

    return float2(max(max(
		min(t1.x, t2.x),
		min(t1.y, t2.y)),
		min(t1.z, t2.z)), // three intersections behind pos
	min(min(
		max(t1.x, t2.x),
		max(t1.y, t2.y)),
		max(t1.z, t2.z))); // three intersections after pos
}

float3 ray(uint2 coord)
{
	float2 screendir = (float2)coord / inf.screen_size.y - float2(inf.screen_size.x / inf.screen_size.y * .5, .5);
	float3 dir = mul(float3(screendir * inf.fov, .5), inf.heading);
	return normalize(dir);
}


RWTexture2D<float4> target : register(u0);

void set(float4 value, uint2 coord)
{
	target[coord.xy] = value;//pow(value, 1 / 2.2);
}



[numthreads(groupSize, groupSize, 1)]
void main(uint2 coord : SV_DispatchThreadID)//uint3 groupID : SV_GroupID, uint3 threadID : SV_GroupThreadID)
{
    walk[0] = 0;
    float3 pos = inf.position;
    values.GetDimensions(dimensions.x, dimensions.y);
    dimensions = 1 / dimensions;

    float3 dir = ray(coord.xy);
	float prox = 1;
    //float step = 1;
    box.lower = float3(0, 0, 0);
    box.scale = 1;

	int i;
	for (i = 0; (prox > inf.margin * 2 || prox < 0) && i < 100; i++) {
		if (dot(pos, pos) > inf.limit) {
            float3 c = float3(0.005, 0.01, 0.2);
			set(float4(c, i), coord);
			return;
		}
		find(pos);
        prox = box.interpol_world(pos);
        pos += dir * prox;// * (1 - inf.margin);
    }

	dir = normalize(inf.light - pos);
	pos += dir * inf.margin;
	float angle = dot(dir, normalize(gradient(pos)));
	if (angle < 0) {
		set(float4(0, 0, 0, i), coord);
		return;
	}
	float dist = length(inf.light - pos) / 2;
    int j;
	for (j = 0; j < 40 && prox > -inf.margin; j++) {
		if (prox > dist || any(pos < 0) || any(pos > 1)) {
			float attenuation = angle / (dist*dist) * (exp2(inf.strength) - 1);
			set(float4(attenuation, attenuation, attenuation, i+j), coord);
			return;
		}
        
        if (prox < inf.margin)
            if (dot(gradient(pos), dir) < 0)
                break;
        
		find(pos);
        prox = box.interpol_world(pos);
		pos += dir * (prox + inf.margin);
	}
	set(float4(0, 0, 0, i+j), coord);
	return;
}