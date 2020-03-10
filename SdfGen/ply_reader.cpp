
#include "pch.h"
#include "ply_reader.h"

using namespace std;
using namespace gsl;

const string invalid_message = "File format is unsupported or invalid.";

bool SystemBigEndian(void)
{
	union { uint32_t i; char c[4]; } test = { 0x01020304 };
	return test.c[0] == 1;
}
void SwapEndianness(Vertex &x)
{
	CheckError(true, "Not implemented");
}
void SwapEndianness(span<Vertex> buffer)
{
	for (Vertex &v : buffer) {
		SwapEndianness(v);
	}
}

void Search(ifstream &file, string word)
{
	string last = "";
	while (last != word) {
		file >> last;
		CheckError(!file, invalid_message);
	}
}

span<Vertex> *Parse(string filename)
{
	ifstream file(filename, ios::binary);
	CheckError (!file.is_open(), "Could not open file.");

	auto verts = new vector<Vertex>();
	bool big_endian = false;

	string word;
	file >> word;
	CheckError(word != "ply", invalid_message);
	file >> word;
	CheckError(word != "format", invalid_message);
	file >> word;
	if (word == "ascii")
		CheckError(true, "ASCII");
	else if (word == "binary_big_endian")
		big_endian = true;
	else if (word == "binary_little_endian")
		big_endian = false;
	else
		CheckError(true, invalid_message);

	Search(file, "vertex");
	int vertcount;
	file >> vertcount;
	Search(file, "end_header");
	file.get();

	span<Vertex> *dest = new span<Vertex>(new Vertex[vertcount], vertcount);
	file.read((char *) dest->data(), vertcount * sizeof(Vertex));

	if (big_endian != SystemBigEndian())
		SwapEndianness(*dest);

	return dest;
}
