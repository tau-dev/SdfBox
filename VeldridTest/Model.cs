using System;
using System.Collections.Generic;
using System.Numerics;

namespace SDFbox
{
    class Model
    {
        Octree Tree;
        public Vector3 Position;
        Matrix4x4 Rotation;
        SaveModel save;

        public Model(SaveModel model)
        {
            save = model;
            Tree = construct(new Queue<bool>(save.Hierachy));
            Tree.Init(save.Values, new Int3(0, 0, 0), 0);

            Octree construct(Queue<bool> hierachy)
            {
                if(hierachy.Dequeue() == false) {
                    return new Octree();
                } else {
                    Octree[] children = new Octree[8];
                    for (int i = 0; i < 8; i++) {
                        children[i] = construct(hierachy);
                    }
                    return new Octree(children);
                }
            }
        }

        public OctS[] Cast()
        {
            return Tree.Cast();
        }
    }

    class Octree
    {
        Octree parent;
        public Octree[] Children { get; }
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
            if(children.Length != 8)
                    throw new ArgumentException();
            if (vertices.Length != 8)
                    throw new ArgumentException();
            Vertices = vertices;
            Children = children;
            foreach (Octree child in children) {
                child.parent = this;
            }
        }
        
        internal void Init(Dictionary<Int3, float>[] data, Int3 pos, int level)
        {
            Vertices = new float[8];
            int lookup = level;
            Octree.reduce(ref pos, ref level);

            for (int i = 0; i < 8; i++) {
                Vertices[i] = data[level][pos + split(i)];
            }

            if (Children != null) {
                for (int i = 0; i < 8; i++) {
                    Children[i].Init(data, pos * 2 + split(i), level + 1);
                }
            }
        }

        public OctS[] Cast()
        {
            List<OctS> res = new List<OctS>();
            Cast(res, 1, Vector3.Zero, -1);
            return res.ToArray();
        }
        private void Cast(List<OctS> octs, float scale, Vector3 pos, int parent)
        {
            if(Children == null) {
                octs.Add(new OctS(parent, new int[] { -1, -1, -1, -1, -1, -1, -1, -1 }, Vertices, pos, pos + Vector3.One * scale));
            } else {
                int mypos = octs.Count;
                octs.Add(new OctS());
                int[] childindices = new int[8];
                for (int i = 0; i < 8; i++) {
                    childindices[i] = octs.Count;
                    Children[i].Cast(octs, scale / 2, 
                        pos + split(i).Vector * scale / 2,
                        mypos);
                }
                octs[mypos] = new OctS(parent, childindices, Vertices, pos, pos + Vector3.One * scale);
            }
        }

        public static void reduce(ref Int3 pos, ref int level)
        {
            while (pos % 2 == 0 && level > 0) {
                pos /= 2;
                level--;
            }
        }
        public static Int3 split(int index)
        {
            return new Int3() {
                X = index % 2,
                Y = index / 2 % 2,
                Z = index / 4 % 2
            };
        }
    }

    class SaveModel
    {
        public bool[] Hierachy { get; }
        public Dictionary<Int3, float>[] Values { get; }

        public SaveModel(bool[] hierachy, int depth)
        {
            Hierachy = hierachy;
            Values = new Dictionary<Int3, float>[depth];
            for (int i = 0; i < depth; i++) {
                Values[i] = new Dictionary<Int3, float>();
            }
        }

        public void Add(Int3 pos, int level, float value)
        {
            Octree.reduce(ref pos, ref level);
            if(Values[level].ContainsKey(pos)) {
                Values[level].Remove(pos);
            }
            Values[level].Add(pos, value);
        }
        public void Add(int x, int y, int z, int level, float v)
        {
            Int3 pos = new Int3(x, y, z);
            Octree.reduce(ref pos, ref level);
            if (Values[level].ContainsKey(pos)) {
                Values[level].Remove(pos);
            }
            Values[level].Add(pos, v);
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

        public static Int3 operator+ (Int3 a, Int3 b)
        {
            return new Int3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }
        public static Int3 operator* (Int3 a, int b)
        {
            return new Int3(a.X * b, a.Y * b, a.Z * b);
        }
        public static Int3 operator/ (Int3 a, int b)
        {
            return new Int3(a.X / b, a.Y / b, a.Z / b);
        }
        public static int operator% (Int3 a, int b)
        {
            return a.X % b + a.Y % b + a.Z % b;
        }

        public override int GetHashCode()
        {
            return (X + Y + Z) / 4;
        }
        public override bool Equals(object obj)
        {
            if (obj is Int3) {
                Int3 other = (Int3) obj;
                return X == other.X && Y == other.Y && Z == other.Z;
            }
            return false;
        }
    }
}
