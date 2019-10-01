
#include "pch.h"
#include "math.h"

Vector3 operator+(Vector3 a, Vector3 b)
{
	return { a.x+b.x, a.y+b.y, a.z+b.z };
}

Vector3 operator-(Vector3 a, Vector3 b)
{
	return { a.x-b.x, a.y-b.y, a.z-b.z };
}
Vector3 operator*(Vector3 a, float b)
{
	return { a.x * b, a.y * b, a.z * b };
}

Vector3 operator/(Vector3 a, float b)
{
	return { a.x / b, a.y / b, a.z / b };
}


float Vector3::Dot(Vector3 a, Vector3 b)
{
	return a.x * b.x + a.y * b.y + a.z * b.z;
}

Vector3 Vector3::ElementMin(Vector3 a, Vector3 b)
{
	return Vector3(fmin(a.x, b.x), fmin(a.y, b.y), fmin(a.z, b.z));
}

Vector3 Vector3::ElementMax(Vector3 a, Vector3 b)
{
	return Vector3(fmax(a.x, b.x), fmax(a.y, b.y), fmax(a.z, b.z));
}



float saturate(float x)
{
	return x > 1 ? 1 : (x < 0 ? 0 : x);
}