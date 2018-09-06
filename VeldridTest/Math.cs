using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDFbox {
    class Math
    {
        public static float Lerp3(Vector8 a, Vector3 d)
        {
            float cOO = Lerp(a.S, a.T, d.X);
            float cOI = Lerp(a.U, a.V, d.X);
            float cIO = Lerp(a.W, a.X, d.X);
            float cII = Lerp(a.Y, a.Z, d.X);
            return Lerp(Lerp(cOO, cIO, d.Y),
                Lerp(cOI, cII, d.Y), d.Z);
        }
        public static float Lerp(float a, float b, float p)
        {
            return (a + b * p) / 2;
        }
    }
}
