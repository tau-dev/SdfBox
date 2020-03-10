
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
	int32_t Parent;
	int32_t Children;

	OctS() { }
	OctS(int parent)
	{
		Parent = parent;
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
	uint32_t Length;
	owner<OctS *> Structs;
	owner<uint8_t *> Values;
};
OctData make(uint32_t length)
{
	return OctData { length, new OctS[length], new uint8_t[length * 8] };
}
struct OctTexture
{
	uint32_t StructsLength;
	uint32_t TextureWidth;
	uint32_t TextureHeight;
	owner<OctS *> Structs;
	owner<uint8_t *> Texture;
};
OctTexture make_tex(uint32_t s_length, uint32_t tex_width, uint32_t tex_height)
{
	return OctTexture { s_length, tex_width, tex_height, new OctS[s_length], new uint8_t[tex_width * tex_height] };
}

float GlobalScale;
Vector3 GlobalOffset = Vector3(0);
int maxTexDim = 8192;
vector<OctS> octs;
vector<OctVerts> vals;



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
	float dist = p.Length() - 0.5f;
	return dist / GlobalScale;
}

float TrueDistanceAt(Vector3 p, vector<Vertex *> vertices)
{
	p = Transform(p);
	Vertex *closest = nullptr;
	float minDistance = numeric_limits<float>::infinity();

	for (Vertex *&v : vertices) {
		if ((v->Position - p).LengthSquared() < minDistance) {
			minDistance = (v->Position - p).LengthSquared();
			closest = v;
		}
	}

	if (minDistance == numeric_limits<float>::infinity() || closest == nullptr)
		throw range_error("Did not find ");
	if (isinf(minDistance) || isnan(minDistance))
		throw range_error("NaN distance, Oh noes.");

	return sqrt(minDistance) / GlobalScale;
}
float DistanceAt(Vector3 p, vector<Vertex *> vertices)
{
	p = Transform(p);
	Vertex* closest = nullptr;
	float minDistance = numeric_limits<float>::infinity();

	for (Vertex *&v : vertices) {
		if ((v->Position - p).LengthSquared() < minDistance) {
			minDistance = (v->Position - p).LengthSquared();
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
	if (minDistance < 0.015)
		minDistance = Vector3::Dot(closest->Normal / closest->Normal.Length(), p - closest->Position);
	else if (Inside(p, *closest))
		minDistance *= -1;

	//if (Inside(p, *closest))
		//minDistance *= -1;

	return minDistance / GlobalScale;
}

vector<Vertex *> GetPossible(Vector3 pos, float minDistance, const vector<Vertex *> &possible)
{
	pos = Transform(pos);
	vector<Vertex *> next;
	minDistance *= GlobalScale;
	minDistance *= minDistance;
	for(Vertex *v : possible) {
		if ((v->Position - pos).LengthSquared() < minDistance)
			next.push_back(v);
	}
	return next;
}
void construct(const vector<Vertex *> &vertices, int depth, Vector3 pos, int parent, int insert)
{
	float scale = powf(0.5, static_cast<float>(depth));
	Vector3 center = pos + Vector3(1) * 0.5f * scale;
	float centerValue = TrueDistanceAt(center, vertices);
	vector<Vertex *> possible = GetPossible(center, centerValue + HalfSqrt3 * scale, vertices);
	OctS current(parent);

	for (index i = 0; i < 8; i++) {
		if (vals[insert][i] == INFINITY)
			vals[insert][i] = DistanceAt(pos + Vector3::split(i) * scale, possible);
	}

	if (centerValue < scale * 2 && depth < MaxDepth) { // split condition
		current.Children = octs.size();
		for (index i = 0; i < 8; i++) {
			octs.push_back(OctS());
			OctVerts n = EmptyVerts();
			n[i] = vals[insert][i];
			//n[7-i] = centerValue;
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
	float normd = f / 2 / scale;
	return static_cast<uint8_t>(floor(saturate(normd + 0.25f) * 255));
}
void WriteBytes(uint8_t *dest, int p = 0, float scale = 1)
{
	for (index j = 0; j < 8; j++) {
		dest[p * 8 + j] = FromFloat(vals[p][j], scale);
	}
	if (octs[p].Children != -1) {
		for (index i = 0; i < 8; i++) {
			WriteBytes(dest, octs[p].Children + i, scale / 2);
		}
	}
}
void WriteTexture(uint8_t *dest, int i = 0, float s = 1)
{
	OctVerts v = vals[i];
	int p = ((i * 4) % maxTexDim) + 2 * maxTexDim * ((i * 4) / maxTexDim);

	dest[p] = FromFloat(v[0], s);
	dest[p + 1] = FromFloat(v[1], s);
	dest[p + 2] = FromFloat(v[4], s);
	dest[p + 3] = FromFloat(v[5], s);

	p += maxTexDim;
	dest[p] = FromFloat(v[2], s);
	dest[p + 1] = FromFloat(v[3], s);
	dest[p + 2] = FromFloat(v[6], s);
	dest[p + 3] = FromFloat(v[7], s);

	if (octs[i].Children != -1) {
		for (index j = 0; j < 8; j++) {
			WriteBytes(dest, octs[i].Children + j, s / 2);
		}
	}
}
int RoundRow(int x)
{
	return (x + maxTexDim - 1) / maxTexDim;
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
	OctData __stdcall LoadAsdf(char *name)
	{
		ifstream file(name, ios::binary);
		CheckError(!file.is_open(), "Could not open file.");

		uint8_t m1, m2, m3, m4;
		uint16_t v;
		file.seekg(0);
		file >> m1; file >> m2; file >> m3; file >> m4;// file >> v;
		CheckError(
			!(m1 == 'a' && m2 == 's' && m3 == 'd' && m4 == 'f'), 
			"Not a supported ASDF file.");

		uint32_t length = 42;
		CheckError(file.bad(), "BAD FILE");
		file.read(reinterpret_cast<char *>(&(length)), 4);
		CheckError(file.bad(), "BAD FILE 2");
		CheckError(!file.is_open(), "BAD FILE 3");

		OctData result = make(length);
		
		file.read(reinterpret_cast<char *>(result.Structs), result.Length * sizeof(OctS));
		file.read(reinterpret_cast<char *>(result.Values), result.Length * 8);
		file.close();
		return result;
	}

	__declspec(dllexport)
	void __stdcall Save(OctData data, char *name)
	{
		ofstream file(name, ios::binary | ios::out);
		CheckError(!file.is_open(), "Could not open file for saving.");

		file << "asdf";
		uint16_t version = 0;
		//file.write(reinterpret_cast<char *>(&version), 2);

		file.write(reinterpret_cast<char *>(&data.Length), 4);
		file.write(reinterpret_cast<char *>(data.Structs), data.Length * sizeof(OctS));
		file.write(reinterpret_cast<char *>(data.Values), data.Length * 8);
		file.close();
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

		OctData result = make(octs.size());
		copy(make_span(octs), make_span(result.Structs, result.Length));

		WriteBytes(result.Values);
		octs.clear();
		vals.clear();

		return result;
	}
	__declspec(dllexport)
	OctTexture __stdcall SdfTex(span<Vertex> *vertices, int32_t depth) // (Vertex *raw_vertices, int32_t vertexcount, int32_t depth)
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

		OctTexture result = make_tex(octs.size(), maxTexDim, RoundRow(vals.size() * 4) * 2);

		copy(make_span(octs), make_span(result.Structs, result.StructsLength));

		WriteBytes(result.Texture);
		octs.clear();
		vals.clear();

		return result;
	}
	__declspec(dllexport)
	void __stdcall Free(OctData x)
	{
		delete[] x.Structs;
		delete[] x.Values;
	}
	__declspec(dllexport)
		void __stdcall FreeTex(OctTexture x)
	{
		delete[] x.Structs;
		delete[] x.Texture;
	}
}



BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
	return TRUE;
}
