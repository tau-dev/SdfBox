
using Converter;
using System;
using System.Collections.Generic;
using System.Numerics;


namespace SDFbox
{
    class Model
    {
        public Octree Tree { get; }
        public Vector3 Position;
        Matrix4x4 Rotation;
        //SaveModel save;
        const float HalfSqrt3 = 0.866025404f;
        public static float precision = 0.002f;
        public static int MaxDepth = 5;
        //public static float maxResolution = 0.004f;

        public Model(Octree c)
        {
            Tree = c;
        }

        public Model(VertexModel vmodel)// : this(Utilities.Convert(vmodel))
        {
            Tree = construct(vmodel, 0, Vector3.Zero, vmodel.All());

            Octree construct(VertexModel vm, int depth, Vector3 pos, List<int> possible)
            {
                float scale = (float) Math.Pow(0.5, depth);
                float[] verts = new float[8];
                Vector3 center = pos + Vector3.One * 0.5f * scale;
                float centerValue = vm.DistanceAt(center, possible);
                //possible = vm.GetPossible(center, HalfSqrt3 * scale, possible);
                possible = vm.GetPossiblePrecomp(center, Math.Abs(centerValue) + HalfSqrt3 * scale, possible);
                for (int i = 0; i < 8; i++) {
                    verts[i] = vm.DistanceAt(pos + SdfMath.split(i).Vector * scale, possible);
                }
                Octree build = new Octree(verts);


                if (Math.Abs(centerValue) < scale * 2 && depth < MaxDepth) { // error(centerValue, build.Vertices, pos) > precision
                    Octree[] children = new Octree[8];
                    for (int i = 0; i < 8; i++) {
                        children[i] = construct(vm, depth + 1, pos + SdfMath.split(i).Vector * scale / 2, possible);
                    }
                    build.Children = children;
                }

                return build;
            }
        }

        public static List<int> PathTo(OctS[] sdf, Vector3 pos)
        {
            List<int> path = new List<int>();
            int p = 0;
            while (sdf[p].children != -1) {
                Vector3 direction = pos - (sdf[p].lower + sdf[p].higher) / 2;
                int delta = 0;
                if (direction.X > 0)
                    delta = 1;
                if (direction.Y > 0)
                    delta += 2;
                if (direction.Z > 0)
                    delta += 4;
                path.Add(delta);
                p = sdf[p].children + delta;
            }
            return path;
        }
        public OctData Cast()
        {
            return Tree.Cast();
        }
    }

    [Serializable]
    class Octree
    {
        [NonSerialized] Octree parent;
        public Octree[] Children;
        public float[] Vertices;

        public Octree() { }
        public Octree(Octree[] children)
        {
            if (children.Length != 8)
                throw new ArgumentException();
            Children = children;
            foreach (Octree child in children) {
                child.parent = this;
            }
        }
        public Octree(float[] vertices)
        {
            if (vertices.Length != 8)
                throw new ArgumentException();
            Vertices = vertices;
        }
        public Octree(Octree[] children, float[] vertices)
        {
            if (children.Length != 8)
                throw new ArgumentException();
            if (vertices.Length != 8)
                throw new ArgumentException();
            Vertices = vertices;
            Children = children;
            foreach (Octree child in children) {
                child.parent = this;
            }
        }

        public OctData Cast()
        {
            List<OctS> res = new List<OctS>();
            List<Byte8> vals = new List<Byte8>();
            res.Add(new OctS());
            vals.Add(new Byte8());
            Cast(res, vals, 1, Vector3.Zero, -1, 0);
            return new OctData(res.ToArray(), vals.ToArray());
        }
        private void Cast(List<OctS> octs, List<Byte8> values, float scale, Vector3 pos, int parent, int at)
        {
            int childstart = -1;
            if (Children != null) {
                childstart = octs.Count;

                for (int i = 0; i < 8; i++) {
                    octs.Add(new OctS());
                    values.Add(new Byte8());
                }
                for (int i = 0; i < 8; i++) {
                    Children[i].Cast(octs, values, scale / 2, pos + SdfMath.split(i).Vector * scale / 2, at, childstart + i);
                }
            }
            octs[at] = new OctS(parent, childstart, Vertices, pos, scale);
            values[at] = new Byte8(Vertices, scale);
        }
        public static void Reduce(ref Int3 pos, ref int level)
        {
            while (pos % 2 == 0 && level > 0) {
                pos /= 2;
                level--;
            }
        }
    }
    
    struct Int3
    {
        public int X;
        public int Y;
        public int Z;

        public Int3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        public Vector3 Vector {
            get {
                return new Vector3(X, Y, Z);
            }
        }

        public static Int3 operator +(Int3 a, Int3 b)
        {
            return new Int3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }
        public static Int3 operator *(Int3 a, int b)
        {
            return new Int3(a.X * b, a.Y * b, a.Z * b);
        }
        public static Int3 operator /(Int3 a, int b)
        {
            return new Int3(a.X / b, a.Y / b, a.Z / b);
        }
        public static int operator %(Int3 a, int b)
        {
            return a.X % b + a.Y % b + a.Z % b;
        }

        public override int GetHashCode()
        {
            return (X + Y + Z) / 4;
        }
        public override bool Equals(object obj)
        {
            if (obj is Int3 other) {
                return X == other.X && Y == other.Y && Z == other.Z;
            }
            return false;
        }
    }
}
