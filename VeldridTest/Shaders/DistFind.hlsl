
#define MININIZE(element, distance) element = min(sqrlen(distance), element)

struct OctPrimitive
{
	float scale;
	float3 lowerCorner;
	float4 lower_verts;
	float4 higher_verts;
	int to_be_split;
	int next_element;
};

struct Info
{
	int vertex_amount;
	float precision;
	float min_scale;
	float pad;
};

StructuredBuffer<float3> vertices : register(t0);
RWStructuredBuffer<OctPrimitive> data : register(t1);
cbuffer B : register(b0)
{
	Info inf;
}

float sqrlen(float3 a)
{
	return dot(a, a);
}

[numthreads(8, 1, 1)]
void CS(uint3 groupID : SV_GroupID, uint3 threadID : SV_GroupThreadID)
{
	OctPrimitive p = data[groupID.x * 8 + threadID.x];
	float scale = p.scale;
	float3 corner1OO = p.lowerCorner + float3(scale, 0, 0);
	float3 cornerO1O = p.lowerCorner + float3(0, scale, 0);
	float3 corner11O = p.lowerCorner + float3(scale, scale, 0);
	float3 cornerOO1 = p.lowerCorner + float3(0, 0, scale);
	float3 corner1O1 = p.lowerCorner + float3(scale, 0, scale);
	float3 cornerO11 = p.lowerCorner + float3(0, scale, scale);
	float3 corner111 = p.lowerCorner + float3(scale, scale, scale);
	float3 center = p.lowerCorner + float3(scale, scale, scale) / 2;
	float center_dist = -100;
	float corner_dist = .8660254*scale;
	for (int i = 0; i < inf.vertex_amount; i++) {
		if (distance(center, vertices[i]) + corner_dist < center_dist) {
			p.lower_verts.x = min(sqrlen(p.lowerCorner - vertices[i]), p.lower_verts.x);
			p.lower_verts.y = min(sqrlen(corner1OO - vertices[i]), p.lower_verts.y);
			p.lower_verts.z = min(sqrlen(cornerO1O - vertices[i]), p.lower_verts.z);
			p.lower_verts.w = min(sqrlen(corner11O - vertices[i]), p.lower_verts.w);
			p.higher_verts.x = min(sqrlen(cornerOO1 - vertices[i]), p.higher_verts.x);
			p.higher_verts.y = min(sqrlen(corner1O1 - vertices[i]), p.higher_verts.y);
			p.higher_verts.z = min(sqrlen(cornerO11 - vertices[i]), p.higher_verts.z);
			p.higher_verts.w = min(sqrlen(corner111 - vertices[i]), p.higher_verts.w);
			center_dist = min(distance(cornerOO1, vertices[i]), center_dist);
		}
	}
	p.lower_verts = sqrt(p.lower_verts);
	p.higher_verts = sqrt(p.higher_verts);
	float interpolation = p.lower_verts.x + p.lower_verts.y + p.lower_verts.z + p.lower_verts.w + p.higher_verts.x + p.higher_verts.y + p.higher_verts.z + p.higher_verts.w;
	if (abs(interpolation / 8 - center_dist) > inf.precision && p.scale > inf.min_scale) {
		p.to_be_split = 1;
	}

	data[groupID.x * 8 + threadID.x] = p;
}