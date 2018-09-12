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
	int4 childrenL;
	int4 childrenH;
	float4 vertsL;
	float4 vertsH;
	int empty;

	bool inside(float3 pos)
	{
		return all(lower <= pos) && all(pos <= higher);
	}
	float interpol(float3 pos)
	{
		float3 d = (pos - lower) / (higher - lower);
		float cOO = lerp(vertsL.x, vertsL.y, d.x);
		float cOI = lerp(vertsL.z, vertsL.w, d.x);
		float cIO = lerp(vertsH.x, vertsH.y, d.x);
		float cII = lerp(vertsH.z, vertsH.w, d.x);
		return lerp(lerp(cOO, cIO, d.y),
			lerp(cOI, cII, d.y), d.z);
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
	Oct data[9];
}
cbuffer B : register(b1)
{
	Info inf;
}

Oct find(float3 pos)
{
    int i = 0;
    while (i < 9) {
        Oct c = data[i];
        
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
				i = c.childrenL.x; break;
			case 1:
				i = c.childrenL.y; break;
			case 2:
				i = c.childrenL.z; break;
			case 3:
				i = c.childrenL.w; break;
			case 4:
				i = c.childrenH.x; break;
			case 5:
				i = c.childrenH.y; break;
			case 6:
				i = c.childrenH.z; break;
			case 7:
				i = c.childrenH.w; break;
		}
    }
    return data[0];
}
float3 Gradient(float3 pos)
{
	float3 incX = float3(pos.x + inf.margin, pos.y, pos.z);
	float3 incY = float3(pos.x, pos.y + inf.margin, pos.z);
	float3 incZ = float3(pos.x, pos.y, pos.z + inf.margin);
	float at = data[0].interpol(pos);
	float x = data[0].interpol(incX);
	float y = data[0].interpol(incY);
	float z = data[0].interpol(incZ);
	return float3(x - at, y - at, z - at);
}

float3 ray(float4 screen)
{
	return normalize(mul(float3(screen.x / info.screen_size.x - .5, screen.y / info.screen_size.y - .5, .5), info.heading));
}


float4 FS(FragmentIn input) : SV_Target0
{
    float3 pos = inf.position;
    float3 dir = ray(input.Position);

	for (int i = 0; i < 100 && data[0].inside(pos); i++)
    {
		Oct current = find(pos);//data[0];//find(pos);

        float prox = current.interpol(pos);
        
        if (prox <= inf.margin)
        {
			float3 grad = float3(Gradient(pos));
			float3 col = float3(abs(grad.x), abs(grad.y), abs(grad.z));
			return input.Color + float4(normalize(col), 0);
			//return input.Color + float4(1, 0, 0, 1);
        }
        pos += dir * (prox);
        //println(pos);
    }
  
    return input.Color + float4(0.1, 0.2, 0.4, 1);
}