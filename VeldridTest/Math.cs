
using System.Numerics;

namespace SDFbox
{
    class SdfMath
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
        /*
        public static float Lerp3(Half8 a, Vector3 d)
        {
            float cOO = Lerp(a.S, a.T, d.X);
            float cOI = Lerp(a.U, a.V, d.X);
            float cIO = Lerp(a.W, a.X, d.X);
            float cII = Lerp(a.Y, a.Z, d.X);
            return Lerp(Lerp(cOO, cIO, d.Y),
                Lerp(cOI, cII, d.Y), d.Z);
        }*/
        public static Int3 split(int index, int scale = 2)
        {
            return new Int3() {
                X = index % scale,
                Y = index / scale % scale,
                Z = index / (scale*scale) % scale
            };
        }
        public static float Lerp(float a, float b, float p)
        {
            return a * (1 - p) + b * p;
        }
        public static double Average(float[] values)
        {
            double total = 0;
            foreach (float v in values) {
                total += v;
            }
            return total / values.Length;
        }
        public static float Saturate(float f)
        {
            return System.Math.Min(1, System.Math.Max(0, f));
        }
    }
}
