struct FragmentIn
{
    float4 Position : SV_Position;
    float4 Color : COLOR0;
};

struct Oct
{
    int Parent;
    int children[8];
    float verts[8];
    float3 lower;
    float3 higher;
    bool empty;

    bool inside(float3 pos)
    {
        return all(lower <= pos <= higher);
    }
    float interpol(float3 pos)
    {
        float3 d = (pos - lower) / (higher - lower);
        float cOO = lerp(verts[0], verts[1], d.x);
        float cOI = lerp(verts[2], verts[3], d.x);
        float cIO = lerp(verts[4], verts[5], d.x);
        float cII = lerp(verts[6], verts[7], d.x);
        return lerp(lerp(cOO, cIO, d.y),
                    lerp(cOI, cII, d.y), d.z);
    }
};

Oct data[9];
//static const float margin = 0.001;
//static const float3 Camera = float3(0.4, 0.4, 0.1);
//static const float heading = 0;

float margin()
{
    return 0.001;
}
float3 Camera()
{
    return float3(0.4, 0.4, 0.1);
}
float heading()
{
    return 0;
}


Oct find(float3 pos)
{
    int i = 0;
    while (i < 9)
    {
        Oct c = data[i];
        
        if (c.children[0] == 0)
        {
            return c;
        }

        float3 direction = pos - (c.lower + c.higher) * .5;
        int p = 0;
        if (direction.x > 0)
        {
            p = 1;
        }
        if (direction.y > 0)
        {
            p += 2;
        }
        if (direction.z > 0)
        {
            p += 4;
        }
        i = c.children[p];
    }
    return data[0];
}
float3 Gradient(float3 pos)
{
    float3 incX = float3(pos.x + margin(), pos.y, pos.z);
    float3 incY = float3(pos.x, pos.y + margin(), pos.z);
    float3 incZ = float3(pos.x, pos.y, pos.z + margin());
    float at = find(pos).interpol(pos);
    float x = find(incX).interpol(incX);
    float y = find(incY).interpol(incY);
    float z = find(incZ).interpol(incZ);
    return float3(x - at, y - at, z - at);
}
float3 turn(float3 p)
{
    return float3(p.x * cos(heading()) - p.z * sin(heading()), p.y, p.z * cos(heading()) + p.x * sin(heading()));
}
float3 ray(float4 screen)
{
    return normalize(turn(float3(screen.x - .5, screen.y - .5, .5)));
}


float4 FS(FragmentIn input) : SV_Target0
{
    if (data[0].verts[0] != 0)
    {
        return input.Color + float4(20, 40, 80, 0);
    }
    float3 pos = Camera();
    float3 dir = ray(input.Position);

    while (data[0].inside(pos))
    {
        Oct current = find(pos);
        /*
        if(!current.empty) {
          Point collision = current.cast(pos, dir);
          if(collision != null) {
            if(collision.dist > 0) {
              pos = collision.pos;
            } else {
              return color(getCam().sub(collision.pos).mag()*200);
            }
          }
        }
        //*/
        float prox = current.interpol(pos);
        //println(prox);
        if (prox <= 0)
        {
            //return color(getCam().sub(pos).mag()*200);
            float3 normal = normalize(Gradient(pos)) * -256;
            return input.Color + float4(normal, 0);
        }
        pos += dir * (prox + margin());
        //println(pos);
    }
  
    return input.Color + float4(20, 40, 80, 0);
}