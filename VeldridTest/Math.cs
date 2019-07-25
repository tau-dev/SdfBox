
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
        public static Int3 split(int index)
        {
            return new Int3() {
                X = index % 2,
                Y = index / 2 % 2,
                Z = index / 4 % 2
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
    }
}
