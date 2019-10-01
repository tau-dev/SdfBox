#pragma once

struct Vector3
{
	float x;
	float y;
	float z;

	Vector3()
	{
		x = 0;
		y = 0;
		z = 0;
	}
	Vector3(float X)
	{
		x = X;
		y = X;
		z = X;
	}

	Vector3(float X, float Y, float Z)
	{
		x = X;
		y = Y;
		z = Z;
	}

	inline float LengthSquared()
	{
		return  x*x + y*y + z*z;
	}
	inline float Length()
	{
		return sqrt(LengthSquared());
	}
	static float Dot(Vector3 a, Vector3 b);
	static Vector3 ElementMin(Vector3 a, Vector3 b);
	static Vector3 ElementMax(Vector3 a, Vector3 b);
	static Vector3 split(int i)
	{
		return Vector3(i % 2, (i / 2) % 2, (i / 2 / 2) % 2);
	}
};
Vector3 operator+(Vector3 a, Vector3 b);
Vector3 operator-(Vector3 a, Vector3 b);
Vector3 operator*(Vector3 a, float b);
Vector3 operator/(Vector3 a, float b);

float saturate(float x);


struct Vertex
{
	Vector3 Position;
	Vector3 Normal;
};

struct Face
{
	Vector3 *a, *b, *c;
};