
#define groupSize 512

struct Face
{
	float4 a;
	float4 b;
	float4 c;
};

StructuredBuffer<Face> data : register(t0);
RWStructuredBuffer<float> result : register(u0);

cbuffer B : register(b0)
{
	float4 testPos;
}

float dot2(float3 v) { return dot(v, v); }

/*
 * Triangle - exact
 * from
 * http://www.iquilezles.org/www/articles/distfunctions/distfunctions.htm
 */
float udTriangle(float3 p, Face face)
{
	float3 a = face.a.xyz;
	float3 b = face.b.xyz;
	float3 c = face.c.xyz;
	float3 ba = b - a; float3 pa = p - a;
	float3 cb = c - b; float3 pb = p - b;
	float3 ac = a - c; float3 pc = p - c;
	float3 nor = cross(ba, ac);

	int heading = sign(dot(cross(ba, nor), pa)) +
		sign(dot(cross(cb, nor), pb)) +
		sign(dot(cross(ac, nor), pc));
	float sqrval = (abs(heading) < 2)
		?
		min(min(dot2(ba * saturate(dot(ba, pa) / dot2(ba)) - pa),
			dot2(cb * saturate(dot(cb, pb) / dot2(cb)) - pb)),
			dot2(ac * saturate(dot(ac, pc) / dot2(ac)) - pc))
		:
			dot(nor, pa) * dot(nor, pa) / dot2(nor);
	return sqrt(sqrval);
	//d = length( pa - ba*clamp(dot(pa, ba)/sqrlen(ba) , 0, 1) ).
}
float udBTriangle(float3 v1, float3 v2, float3 v3, float3 p)
{
	float3 v21 = v2 - v1; float3 p1 = p - v1;
	float3 v32 = v3 - v2; float3 p2 = p - v2;
	float3 v13 = v1 - v3; float3 p3 = p - v3;
	float3 nor = cross(v21, v13);

	return sqrt((sign(dot(cross(v21, nor), p1)) +
		sign(dot(cross(v32, nor), p2)) +
		sign(dot(cross(v13, nor), p3)) < 2.0)
		?
		min(min(
			dot2(v21 * clamp(dot(v21, p1) / dot2(v21), 0.0, 1.0) - p1),
			dot2(v32 * clamp(dot(v32, p2) / dot2(v32), 0.0, 1.0) - p2)),
			dot2(v13 * clamp(dot(v13, p3) / dot2(v13), 0.0, 1.0) - p3))
		:
		dot(nor, p1) * dot(nor, p1) / dot2(nor));
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
	float dist = udBTriangle(f.a.xyz, f.b.xyz, f.c.xyz, testPos.xyz);//udTriangle(testPos.xyz, f);
	/*
	float2 ab = normalize(f.b.xy - f.a.xy);
	float2 ac = normalize(f.c.xy - f.a.xy);

	float u = dot(testPos.xy - f.a.xy, ab);
	float v = dot(testPos.xy - f.a.xy, ac);
	*/
	if (inside(testPos, f.a, f.b, f.c)) {
		float3 normal = normalize(cross(f.b.xyz - f.a.xyz, f.c.xyz - f.a.xyz));
		float t = dot(normal, f.a.xyz - testPos.xyz) / normal.z; //  testPos.xyz + f.a.xyz

		if (t > 0)
			dist = -dist;
	}

	result[id] = dist;
}