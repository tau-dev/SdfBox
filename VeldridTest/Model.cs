
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
        const float HalfSqrt3 = 0.866025f;
        public static float precision = 0.002f;
        public static int MaxDepth = 5;
        //public static float maxResolution = 0.004f;

        public Model(Octree c)
        {
            Tree = c;
        }
        public Model(OctS[] c)
        {
            Tree = Octree.Load(c, 0);
        }
        /*public Model(SaveModel model)
        {
            save = model;
            Tree = construct(new Queue<bool>(save.Hierachy));
            Tree.Init(save, new Int3(0, 0, 0), 0);

            Octree construct(Queue<bool> hierachy)
            {
                if (hierachy.Dequeue() == false) {
                    return new Octree();
                } else {
                    Octree[] children = new Octree[8];
                    for (int i = 0; i < 8; i++) {
                        children[i] = construct(hierachy);
                    }
                    return new Octree(children);
                }
            }
        }*/
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
            float error(float center, float[] values, Vector3 pos)
            {
                Vector3 test = Vector3.One / 2;
                return Math.Abs(1 - SdfMath.Lerp3(new Vector8(values), test) / center);
            }
        }


        public static float Sample(OctS[] sdf, Vector3 pos)
        {
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
                p = sdf[p].children + delta;
            }
            OctS found = sdf[p];
            return SdfMath.Lerp3(found.verts, (pos - found.lower) / (found.lower + found.higher));
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
        public OctS[] Cast()
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

        /*internal void Init(SaveModel data, Int3 pos, int level)
        {
            Vertices = new float[8];

            for (int i = 0; i < 8; i++) {
                Vertices[i] = data.Values[(pos + SdfMath.split(i))*data.ScaleLevel(level)];
            }

            if (Children != null) {
                for (int i = 0; i < 8; i++) {
                    Children[i].Init(data, pos * 2 + SdfMath.split(i), level + 1);
                }
            }
        }//*/

        public static Octree Load(OctS[] raw, int position)
        {
            OctS point = raw[position];
            Octree current = new Octree(point.verts.Array);
            if (point.children > 0) {
                Octree[] children = new Octree[8];
                for (int i = 0; i < 8; i++) {
                    children[i] = Load(raw, point.children + i);
                }
                current.Children = children;
            }
            return current;
        }


        public OctS[] Cast()
        {
            List<OctS> res = new List<OctS>();
            res.Add(new OctS());
            Cast(res, 1, Vector3.Zero, -1, 0);
            return res.ToArray();
        }
        private void Cast(List<OctS> octs, float scale, Vector3 pos, int parent, int at)
        {
            if (Children == null)
                octs[at] = new OctS(parent, -1, Vertices, pos, scale);
            else {
                int start = octs.Count;
                octs[at] = new OctS(parent, start, Vertices, pos, scale);
                for (int i = 0; i < 8; i++) {
                    octs.Add(new OctS());
                }
                for (int i = 0; i < 8; i++) {
                    Children[i].Cast(octs, scale / 2, pos + SdfMath.split(i).Vector * scale / 2, at, start + i);
                }
            }
        }
        public static void Reduce(ref Int3 pos, ref int level)
        {
            while (pos % 2 == 0 && level > 0) {
                pos /= 2;
                level--;
            }
        }
    }
    // TODO  deprecate this
    /*
    [Serializable]
    class SaveModel
    {
        public bool[] Hierachy { get; }
        public int Depth { get; } // 0-indexed depth of the Hierachy
        public Dictionary<Int3, float> Values { get; }

        public SaveModel(bool[] hierachy, int depth)
        {
            Hierachy = hierachy;
            Depth = depth;
            Values = new Dictionary<Int3, float>();
        }

        public void Add(Int3 pos, int level, float value)
        {
            pos *= ScaleLevel(level);

            if (Values.ContainsKey(pos)) {
                Values.Remove(pos);
            }
            Values.Add(pos, value);
        }
        public void Add(int x, int y, int z, int level, float v)
        {
            Int3 pos = new Int3(x, y, z);
            pos *= ScaleLevel(level);

            if (Values.ContainsKey(pos)) {
                Values.Remove(pos);
            }
            Values.Add(pos, v);
        }

        public int ScaleLevel(int level)
        {
            int s = 1;
            for (int i = 0; i < Depth - level; i++) {
                s *= 2;
            }
            return s;
        }
    }*/

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
