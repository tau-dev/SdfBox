
#include "pch.h"
#include "obj_reader.h"

using namespace std;
using namespace gsl;

void InvalidFile()
{
	cout << "File format is unsupported or invalid." << endl;
	throw runtime_error("unsupported");
}

Vector3 Parse3(ifstream &f)
{
	float x, y, z;
	f >> x >> y >> z;
	return Vector3(x, y, z);
}

void ObjParser::ParseFace(ifstream &f)
{
	int p = 0;
	while (f.peek() != '\n') {
		index vindex, tindex, nindex;

		f >> vindex;

		if (f.get() != '/')
			InvalidFile();

		if (f.peek() != '/')
			f >> tindex;

		if (f.get() != '/')
			InvalidFile();

		f >> nindex;

		verts[vindex - 1].Normal = normals[nindex - 1];
	}
}


span<Vertex> *ObjParser::Parse(string filename)
{
	ifstream file(filename);
	if (!file.is_open()) {
		cout << "Could not open file." << endl;
		throw runtime_error("file");

	}

	int line = 1;
	while (!file.eof()) {
		while (isspace(file.peek()))
			file.get();
		if (file.eof())
			break;
		switch (file.get()) {
		case '#':
		case 'o':
		case 's':
			while (file.peek() != '\n' && !file.eof())
				file.get();
			break;
		case 'f':
			ParseFace(file);
			break;
		case 'v':
			switch (file.get()) {
				case ' ': 
					verts.push_back({ Parse3(file), 0 });	break;
				case 'n': 
					normals.push_back(Parse3(file));		break;
				case 't':
					while (file.peek() != '\n' && !file.eof())
						file.get();
					break;
				default: 
					break;
			}
			break;
		default:
			InvalidFile();
			break;
		}
		if (file.fail())
			InvalidFile();

		line++;
	}
	int count = verts.size();
	span<Vertex> *x = new span<Vertex>(new Vertex[count], count);
	copy(verts.begin(), verts.end(), x->data());
	return x;
}

