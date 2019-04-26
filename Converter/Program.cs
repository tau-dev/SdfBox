
using ObjLoader.Loader.Data.VertexData;
using ObjLoader.Loader.Loaders;
using ObjParser;
using System;
using System.Collections.Generic;
using System.IO;
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
            } catch (FileNotFoundException) {
                return;
            }
        }
    }

    public class VertexModel
    {
        LoadResult RawData;
        public Obj Raw;// { get; private set; }
        public Vector3[] xSorted; // TODO privatify
        public Vector3[] xSortedNormals;
        const float Scale = 3.0f;
        KdTree verts;

        public VertexModel(string fileName) : this(fileName, (new ObjLoaderFactory()).Create())
        {

        }
        public VertexModel(string fileName, IObjLoader loader)
        {
            //Raw = new Obj();
            //Raw.LoadObj(new FileStream(fileName, FileMode.Open));
            #region deprecated

            FileStream file;
            file = new FileStream(fileName, FileMode.Open);
            RawData = loader.Load(file);
            #endregion deprecated
            Sort();
            //verts = new KdTree(Vertices());
        }

        public float DistanceAt(Vector3 v, float knownMax = Single.PositiveInfinity)
        {
            v = Transform(v);
            float minDistance = knownMax * Scale;

            VertexN found = verts.NearestNeighbor(v, minDistance);
            minDistance = Vector3.Distance(v, found.Position);

            if (Vector3.Dot(found.Normal, found.Position - v) > 0) {
                minDistance *= -1;
            }

            return minDistance / Scale;
        }
        public float DistanceAt(Vector3 v, List<int> possible)
        {
            v = Transform(v);
            int closest = 0;
            float minDistance = Single.PositiveInfinity;

            foreach (int i in possible) {
                if (Vector3.DistanceSquared(xSorted[i], v) < minDistance) {
                    minDistance = Vector3.DistanceSquared(xSorted[i], v);
                    closest = i;
                    if ((xSorted[i].X - xSorted[closest].X) * (xSorted[i].X - xSorted[closest].X) > minDistance)
                        break;
                }
            }
            minDistance = (float) Math.Sqrt(minDistance);
            Vector3 test = xSorted[closest];
            if (Vector3.Dot(xSortedNormals[closest], xSorted[closest] - v) > 0) {
                minDistance *= -1;
            }
            return minDistance / Scale;
        }

        public List<int> GetPossible(Vector3 pos, float minDist, List<int> possible)
        {
            pos = Transform(pos);
            minDist *= Scale;

            List<int> next = new List<int>();
            foreach (int i in possible) {
                if (Vector3.Distance(xSorted[i], pos) < minDist)
                    next.Add(i);
            }
            return next;
        }
        public List<int> All()
        {
            List<int> x = new List<int>();
            for (int i = 0; i < xSorted.Length; i++) {
                x.Add(i);
            }
            return x;
        }

        private Vector3 Transform(Vector3 p)
        {
            Vector3 t = (p - new Vector3(.5f, .5f, .5f)) * Scale; //(.5f, .625f, .5f)
            t.Y *= -1;
            return t;
        }

        void Sort()
        {
            xSorted = new Vector3[RawData.Vertices.Count];
            xSortedNormals = new Vector3[RawData.Vertices.Count];
            Queue<int> ordered = Quicksort(0, RawData.Vertices.Count);

            Dictionary<int, int> NormalMapping = new Dictionary<int, int>();
            foreach (var face in RawData.Groups[0].Faces) {
                for (int i = 0; i < face.Count; i++) {
                    NormalMapping[face[i].VertexIndex - 1] = face[i].NormalIndex - 1;
                }
            }

            for (int i = 0; ordered.Count > 0; i++) {
                int vertex = ordered.Dequeue();
                int normal;
                if (NormalMapping.ContainsKey(vertex))
                    normal = NormalMapping[vertex];
                else
                    normal = vertex;
                Vertex v = RawData.Vertices[vertex];
                Normal n = RawData.Normals[normal];
                xSorted[i] = new Vector3(v.X, v.Y, v.Z);
                xSortedNormals[i] = new Vector3(n.X, n.Y, n.Z);
            }
            //*/

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
        List<VertexN> Vertices()
        {
            VertexN[] elems = new VertexN[RawData.Vertices.Count];

            Dictionary<int, int> NormalMapping = new Dictionary<int, int>();
            foreach (var face in RawData.Groups[0].Faces) {
                for (int i = 0; i < face.Count; i++) {
                    NormalMapping[face[i].VertexIndex - 1] = face[i].NormalIndex - 1;
                }
            }

            for (int i = 0; i < xSorted.Length; i++) {
                Vertex v = RawData.Vertices[i];
                Normal n = RawData.Normals[NormalMapping[i]];
                elems[i] = new VertexN(new Vector3(v.X, v.Y, v.Z), new Vector3(n.X, n.Y, n.Z));
            }
            return new List<VertexN>(elems);
        }
    }
}
