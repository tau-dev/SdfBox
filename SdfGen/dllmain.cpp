
#include "pch.h"
#include "math.h"
#include "obj_reader.h"
#include "ply_reader.h"

using namespace std;
using namespace gsl;



const float HalfSqrt3 = 0.866025404f;
const float padding = 1.1f;
int MaxDepth = 6;



struct OctS
{
	Vector3 Lower;
	float Scale;

	int Parent;
	int Empty;
	int Children;
	int __pad1;

	OctS() { }
	OctS(Vector3 lower, float scale, int parent)
	{
		Lower = lower;
		Scale = scale;
		Parent = parent;
		Empty = true;
		Children = -1;
	}
};
typedef array<float, 8> OctVerts;
OctVerts EmptyVerts()
{
	return OctVerts { INFINITY, INFINITY, INFINITY, INFINITY, INFINITY, INFINITY, INFINITY, INFINITY };
}

struct OctData
{
	int32_t Length;
	owner<OctS *> Structs;
	owner<uint8_t *> Values;
};

float GlobalScale;
Vector3 GlobalOffset = Vector3(0);
vector<OctS> octs;
vector<OctVerts> vals;

/*
vector<Vertex> LoadFrom(string filename)
{
	ifstream file(filename);
	tinyobj::attrib_t attrib;
	vector<tinyobj::shape_t> shapes;
	vector<tinyobj::material_t> materials;
	string warn;
	string err;
	tinyobj::LoadObj(&attrib, &shapes, &materials, &warn, &err, &file);
	shapes[0].mesh.indices[0];

	int facevertcount = shapes[0].mesh.indices.size();
	vector<Vertex> x;
	vector<Vector3> normals;

	int vertcount = attrib.vertices.size();
	for (index i = 0; i < vertcount; i++) {
		x.push_back({ Vector3(attrib.vertices[i * 3], attrib.vertices[i * 3 + 1], attrib.vertices[i * 3 + 2]), 0 });
	}
	int normcount = attrib.normals.size();
	for (index i = 0; i < vertcount; i++) {
		normals.push_back(Vector3(attrib.normals[i * 3], attrib.normals[i * 3 + 1], attrib.normals[i * 3 + 2]));
	}
	for (index i = 0; i < facevertcount; i++) {
		auto facevert = shapes[0].mesh.indices[i];
		x[facevert.vertex_index].Normal = normals[facevert.normal_index];
	}
	return x;
}
*/
void FindDimensions(span<Vertex> vertices)
{
	Vector3 lower = Vector3(numeric_limits<float>::infinity());
	Vector3 higher = Vector3(-numeric_limits<float>::infinity());
	for(auto &vert : vertices)
	{
		lower = Vector3::ElementMin(lower, vert.Position);
		higher = Vector3::ElementMax(higher, vert.Position);
	}
	GlobalOffset = (lower + higher) * .5 + Vector3(0.003f);
	float lowest = min(lower.x, min(lower.y, lower.z));
	float highest = max(higher.x, max(higher.y, higher.z));
	GlobalScale = (highest - lowest) * padding;
}

Vector3 Transform(Vector3 worldPos)
{
	worldPos.y = 1 - worldPos.y;
	Vector3 modelPos = (worldPos - Vector3(.5, .5, .5)) * GlobalScale + GlobalOffset; //(.5f, .625f, .5f)
	return modelPos;
}
bool Inside(Vector3 p, Vertex closest)
{
	return Vector3::Dot(closest.Normal, closest.Position - p) > 0;
}
float Sample(Vector3 p)
{
	p = Transform(p);
	float dist = p.Length() - 0.5;
	return dist / GlobalScale;
}
float DistanceAt(Vector3 p, vector<Vertex *> vertices)
{
	p = Transform(p);
	Vertex* closest = nullptr;
	float minDistance = numeric_limits<float>::infinity();

	for (Vertex *&v : vertices) {
		if (Vector3::DistanceSquared(v->Position, p) < minDistance) {
			minDistance = Vector3::DistanceSquared(v->Position, p);
			closest = v;
		}
		/*if ((v->Position.x - closest->Position.x) * (v->Position.x - closest->Position.x) > minDistance)
			break;//*/
	}

	if (minDistance == numeric_limits<float>::infinity() || closest == nullptr)
		throw range_error("Did not find ");
	if (isinf(minDistance) || isnan(minDistance))
		throw range_error("NaN distance, Oh noes.");

	minDistance = (float) sqrt(minDistance);

	if (Inside(p, *closest))
		minDistance *= -1;

	return minDistance / GlobalScale;
}

vector<Vertex *> GetPossible(Vector3 pos, float minDistance, const vector<Vertex *> &possible)
{
	pos = Transform(pos);
	minDistance *= GlobalScale;
	minDistance *= minDistance;
	vector<Vertex *> next;
	for(Vertex *v : possible) {
		if (Vector3::DistanceSquared(v->Position, pos) < minDistance)
			next.push_back(v);
	}
	return next;
}
void construct(const vector<Vertex *> &vertices, int depth, Vector3 pos, int parent, int insert)
{
	float scale = powf(0.5, static_cast<float>(depth));
	Vector3 center = pos + Vector3(1) * 0.5f * scale;
	float centerValue = DistanceAt(center, vertices);
	vector<Vertex *> possible = GetPossible(center, abs(centerValue) + HalfSqrt3 * scale, vertices);
	OctS current(pos, scale, parent);

	for (index i = 0; i < 8; i++) {
		if (vals[insert][i] == INFINITY)
			vals[insert][i] = DistanceAt(pos + Vector3::split(i) * scale, possible);
		if (vals[insert][i] < 0)
			current.Empty = false;
	}

	if (abs(centerValue) < scale * 2 && depth < MaxDepth) { // split condition
		current.Children = octs.size();
		for (index i = 0; i < 8; i++) {
			octs.push_back(OctS());
			OctVerts n = EmptyVerts();
			n[i] = vals[insert][i];
			n[7-i] = centerValue;
			vals.push_back(n);
		}
		for (index i = 0; i < 8; i++) {
			construct(possible, depth + 1, pos + Vector3::split(i) * (scale / 2), insert, current.Children + i);
		}
	}
	octs[insert] = current;
}

uint8_t FromFloat(float f, float scale)
{
	float normd = f / 4 / scale;
	return static_cast<uint8_t>(saturate(normd + 0.25f) * 255);
}

extern "C" {
	__declspec(dllexport)
	span<Vertex> * __stdcall LoadObj(char *name)
	{
		auto p = new ObjParser();
		return p->Parse(name);
	}
	
	__declspec(dllexport)
	span<Vertex> * __stdcall LoadPly(char *name)
	{
		return Parse(name);
	}

	__declspec(dllexport)
	OctData __stdcall SdfGen(span<Vertex> *vertices, int32_t depth) // (Vertex *raw_vertices, int32_t vertexcount, int32_t depth)
	{
		MaxDepth = depth;
		FindDimensions(*vertices);

		vector<Vertex *> all;
		for (auto &vert : *vertices) {
			all.push_back(&vert);
		}

		vals.push_back(EmptyVerts());
		octs.push_back(OctS());
		construct(all, 0, 0, -1, 0);
		delete[] vertices->data();
		delete vertices;

		int length = octs.size();
		OctS *resultoct = new OctS[length];
		copy(make_span(octs), make_span(resultoct, length));

		uint8_t *resultvals = new uint8_t[length * 8];
		for (index i = 0; i < length; i++) {
			for (index j = 0; j < 8; j++) {
				resultvals[i * 8 + j] = FromFloat(vals[i][j], octs[i].Scale);
			}
		}

		return OctData { length, resultoct, resultvals };
	}

	__declspec(dllexport)
	void __stdcall Free(OctData x)
	{
		delete[] x.Structs;
		delete[] x.Values;
	}
}



BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
	return TRUE;
}
