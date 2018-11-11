using System;
using System.IO;
using ObjLoader.Loader.Common;
using ObjLoader.Loader.Data.VertexData;
using ObjLoader.Loader.Loaders;
using ObjLoader.Loader.TypeParsers;
using System.Numerics;
using System.Collections.Generic;


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
        LoadResult RawData;
        Vertex[] xSorted;
        Normal[] xSortedNormals;
        const float Scale = 5.7f;
        
        public VertexModel(string fileName)
        {
            FileStream file;
            file = new FileStream(fileName, FileMode.Open);
            RawData = ((new ObjLoaderFactory()).Create()).Load(file);
            Sort();
        }
        public VertexModel(string fileName, IObjLoader loader)
        {
            FileStream file;
            file = new FileStream(fileName, FileMode.Open);
            RawData = loader.Load(file);
            Sort();
        }
        
        public float DistanceAt(Vector3 v, float knownMax)
        {
            v = (v - new Vector3(.5f, .5f, .5f)) * Scale; //(.5f, .625f, .5f)
            v.Y *= -1;
            int closest = 0;
            float minDistance = knownMax * Scale;
            
            int upperLimit = Find(v.X + minDistance, 0, xSorted.Length - 1);
            
            for (int i = Find(v.X - minDistance, 0, upperLimit);  i < upperLimit; i++) {
                if(Math.Abs(xSorted[i].Y - v.Y) < minDistance && Math.Abs(xSorted[i].X - v.X) < minDistance) {
                    if (Distance(xSorted[i], v) < minDistance) {
                        minDistance = Distance(xSorted[i], v);
                        closest = i;

                        upperLimit = Find(v.X + minDistance, i, upperLimit);
                    }
                }
            }
            Vertex test = xSorted[closest];
            if (Dot(xSortedNormals[closest], Difference(xSorted[closest], v)) > 0) {
                minDistance *= -1;
                Dot(xSortedNormals[closest], Difference(xSorted[closest], v));
            }
            return minDistance / Scale;

            /*
            for (int i = 0; i < rawData.Vertices.Count; i++) {
                float d = DistanceSquared(rawData.Vertices[i], v);
                if (d < minDistance) {
                    closest = i;
                    minDistance = d;
                }
            }
            minDistance = (float) Math.Sqrt(minDistance);
            Vertex test = rawData.Vertices[closest];
            if (Dot(rawData.Normals[closest], Difference(rawData.Vertices[closest], v)) > 0) {
                minDistance *= -1;
                Dot(rawData.Normals[closest], Difference(rawData.Vertices[closest], v));
            }
            return minDistance / 4;
            */

            int Find(float border, int lower, int higher)
            {
                while (higher - lower > 1) {
                    int half = (lower + higher) / 2;

                    if (xSorted[half].X > border)
                        higher = half;
                    else
                        lower = half;
                }
                return higher;
            }
        }

        static float Dot(Normal a, Vector3 b)
        {
            return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
        }
        static float Distance(Vertex a, Vector3 b)
        {
            return (float) Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
        }
        static Vector3 Difference(Vertex a, Vector3 b)
        {
            return new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        void Sort()
        {
            xSorted = new Vertex[RawData.Vertices.Count];
            xSortedNormals = new Normal[RawData.Vertices.Count];
            Queue<int> ordered = Quicksort(0, RawData.Vertices.Count);

            for (int i = 0; ordered.Count > 0; i++) {
                xSorted[i] = RawData.Vertices[ordered.Peek()];
                xSortedNormals[i] = RawData.Normals[ordered.Dequeue()];
            }
            
            Queue<int> Quicksort(int start, int end)
            {
                if (end - start <= 1) {
                    Queue<int> leaf = new Queue<int>();
                    leaf.Enqueue(start);
                    return leaf;
                }

                int half = (start + end) / 2;
                Queue<int> a = Quicksort(start, half);
                Queue<int> b = Quicksort(half, end);
                Queue<int> result = new Queue<int>();

                while (a.Count > 0 && b.Count > 0) {
                    if (RawData.Vertices[a.Peek()].X < RawData.Vertices[b.Peek()].X)
                        result.Enqueue(a.Dequeue());
                    else
                        result.Enqueue(b.Dequeue());
                }

                while (a.Count > 0) {
                    result.Enqueue(a.Dequeue());
                }
                while (b.Count > 0) {
                    result.Enqueue(b.Dequeue());
                }
                return result;
            }
        }
    }
}
