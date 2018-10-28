
#define OctCount 73

struct FragmentIn
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
};

struct Oct
{
	int Parent;
	float3 lower;
	float3 higher;
	int empty;
	int4 childrenL;
	int4 childrenH;
	float4 vertsL;
	float4 vertsH;

	bool inside(float3 pos)
	{
		return all(lower <= pos) && all(pos <= higher);
	}
	float interpol(float3 pos)
	{
		float3 d = (pos - lower) * (higher - lower);
		float cOO = lerp(vertsL.x, vertsL.y, d.x);
		float cOI = lerp(vertsL.z, vertsL.w, d.x);
		float cIO = lerp(vertsH.x, vertsH.y, d.x);
		float cII = lerp(vertsH.z, vertsH.w, d.x);
		float cO = lerp(cOO, cOI, d.y);
		float cI = lerp(cIO, cII, d.y);
		return lerp(cO, cI, d.z);
	}
};

struct Info {
	float3x3 heading;
	float3 position;
	float margin;
	float2 screen_size;
};

cbuffer A : register(b0)
{
	Oct data[OctCount];
}
cbuffer B : register(b1)
{
	Info inf;
}

Oct find(float3 pos)
{
    int index = 0;
	int iterations = 0;
    while (index < OctCount && iterations < 12) {
        Oct c = data[index];
        
        if (c.childrenL.x < 0) {
            return c;
        }

        float3 direction = pos - (c.lower + c.higher) / 2;
        int p = 0;
        if (direction.x > 0) {
            p = 1;
        }
        if (direction.y > 0) {
            p += 2;
        }
        if (direction.z > 0) {
            p += 4;
		}

		switch (p) {
			case 0:
				index = c.childrenL.x; break;
			case 1:
				index = c.childrenL.y; break;
			case 2:
				index = c.childrenL.z; break;
			case 3:
				index = c.childrenL.w; break;
			case 4:
				index = c.childrenH.x; break;
			case 5:
				index = c.childrenH.y; break;
			case 6:
				index = c.childrenH.z; break;
			case 7:
				index = c.childrenH.w; break;
		}
		iterations++;
    }
    return data[0];
}
float3 Gradient(float3 pos)
{
	Oct frame = find(pos);
	float3 incX = float3(pos.x + inf.margin, pos.y, pos.z);
	float3 incY = float3(pos.x, pos.y + inf.margin, pos.z);
	float3 incZ = float3(pos.x, pos.y, pos.z + inf.margin);
	float at = frame.interpol(pos);
	float x = frame.interpol(incX);
	float y = frame.interpol(incY);
	float z = frame.interpol(incZ);
	return float3(x - at, y - at, z - at);
}

float3 ray(float4 screen)
{
	return normalize(mul(float3(screen.x / inf.screen_size.x - .5, screen.y / inf.screen_size.y - .5, .5), inf.heading));
}


float4 FS(FragmentIn input) : SV_Target0
{
    float3 pos = inf.position;
    float3 dir = ray(input.Position);
	float prox = 1;

	for (int i = 0; i < 100 && data[0].inside(pos) && abs(prox) > inf.margin; i++)
    {
		Oct current = find(pos);//data[0];//find(pos);
        prox = current.interpol(pos);
		//return float4(0, prox, 0, .5);
        
        if (prox <= inf.margin)
        {
			//return float4(1, 0, 0, .5);
			float3 grad = float3(Gradient(pos));
			float3 col = float3(abs(grad.x), abs(grad.y), abs(grad.z));
			return input.Color + float4(normalize(col), 0);
        }
		//return float4(0, 0, 1, .5);
        pos += dir * (prox) * (1 + inf.margin);
    }
  
    return input.Color + float4(0.1, 0.2, 0.4, 1);
}