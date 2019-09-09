#pragma once
#include "pch.h"
#include "math.h"


class ObjParser
{
	std::vector<Vertex> verts;
	std::vector<Vector3> normals;
public:
	ObjParser() { }
	gsl::span<Vertex> *Parse(std::string filename);

private:
	void ParseFace(std::ifstream &f);
};