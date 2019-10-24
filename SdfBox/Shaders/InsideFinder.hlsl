
#define groupSize 256

struct Face
{
	float4 a;
	float4 b;
	float4 c;
};

StructuredBuffer<Face> data : register(t0);
RWStructuredBuffer<int> result : register(u0);

cbuffer B : register(b0)
{
	float4 testPos;
}

bool inside(float4 pos, float4 a, float4 b, float4 c)
{
	float2 p = pos.xy - a.xy;
	float2 u = b.xy - a.xy;
	float2 v = c.xy - a.xy;
	float uyx = u.y / u.x;
	float vxy = v.x / v.y;
	float r = (p.x - vxy * p.y) / (u.x - vxy * u.y);
	float s = (p.y - uyx * p.x) / (v.y - uyx * v.x);
	return r > 0 && s > 0 && r + s <= 1;
}

[numthreads(groupSize, 1, 1)]
void main(uint3 groupID : SV_GroupID, uint3 threadID : SV_GroupThreadID)
{
	uint id = threadID.x + groupID.x * groupSize;
	Face f = data[id];

	if (inside(testPos, f.a, f.b, f.c)) {
		float3 normal = normalize(cross(f.b.xyz - f.a.xyz, f.c.xyz - f.a.xyz));
		float t = dot(normal, f.a.xyz - testPos.xyz) / normal.z;

        if (t > 0) {
            result[id] = 1;
            return;
        }
    }
    result[id] = 0;
}