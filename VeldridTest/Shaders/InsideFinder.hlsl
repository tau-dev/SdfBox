
#define groupSize 256

struct Face
{
	float4 a;
	float4 b;
	float4 c;
};

StructuredBuffer<Face> data : register(t0);
RWStructuredBuffer<bool> result : register(u0);

cbuffer B : register(b0)
{
	float4 testPos;
}

bool inside(float4 pos, float4 a, float4 b, float4 c)
{
	float2 p = pos.xy - a.xy;
	float2 t = b.xy - a.xy;
	float2 s = c.xy - a.xy;
	float txy = t.x / t.y;
	float syx = s.y / s.x;
	float u = (p.x - txy * p.y) / (s.x - txy * s.y);
	float v = (p.y - syx * p.x) / (t.y - syx * t.x);
	return u >= 0 && v >= 0 && u + v <= 1;
}

[numthreads(groupSize, 1, 1)]
void main(uint3 groupID : SV_GroupID, uint3 threadID : SV_GroupThreadID)
{
	uint id = threadID.x + groupID.x * groupSize;
	Face f = data[id];

	result[id] = false;

	if (inside(testPos, f.a, f.b, f.c)) {
		float3 normal = normalize(cross(f.b.xyz - f.a.xyz, f.c.xyz - f.a.xyz));
		float t = dot(normal, testPos.xyz + f.a.xyz) / normal.z;

		if (t > 0)
			result[id] = true;
	}
}