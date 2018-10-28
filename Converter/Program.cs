using System;
using System.IO;
using ObjLoader.Loader.Common;
using ObjLoader.Loader.Data.VertexData;
using ObjLoader.Loader.Loaders;
using ObjLoader.Loader.TypeParsers;
using System.Numerics;


namespace Converter
{
    class Program
    {
        static VertexModel v;
        static void Main(string[] args)
        {
            IObjLoader objLoader = (new ObjLoaderFactory()).Create();
            try {
                v = new VertexModel(args[0], objLoader);
            }
            catch (FileNotFoundException e) {
                return;
            }
        }

    }

    public class VertexModel
    {
        LoadResult rawData;
        
        public VertexModel(string fileName)
        {
            FileStream file;
            file = new FileStream(fileName, FileMode.Open);
            rawData = ((new ObjLoaderFactory()).Create()).Load(file);
        }
        public VertexModel(string fileName, IObjLoader loader)
        {
            FileStream file;
            file = new FileStream(fileName, FileMode.Open);
            rawData = loader.Load(file);
        }
        
        public float DistanceAt(Vector3 v)
        {
            v = v * 4 - Vector3.One * 2; // Scale bodge
            int closest = 0;
            float minDistance = float.PositiveInfinity;
            for (int i = 0; i < rawData.Vertices.Count; i++) {
                float d = DistanceSquared(rawData.Vertices[i], v);
                if (d < minDistance) {
                    closest = i;
                    minDistance = d;
                }
            }
            minDistance = (float) Math.Sqrt(minDistance);
            Vertex test = rawData.Vertices[closest];
            if (Dot(rawData.Normals[closest], Difference(rawData.Vertices[closest], v)) > 0)
                minDistance *= -1;
            return minDistance / 4;
        }

        static float Dot(Normal a, Vector3 b)
        {
            return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
        }
        static float DistanceSquared(Vertex a, Vector3 b)
        {
            return (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z);
        }
        static Vector3 Difference(Vertex a, Vector3 b)
        {
            return new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }
    }
}
