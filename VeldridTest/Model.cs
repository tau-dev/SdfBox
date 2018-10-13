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
            Tree.Init(save, new Int3(0, 0, 0), 0);
            
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

        public static SaveModel Build()
        {
            //bool[] structure = new bool[] { true, false, false, false, false, false, false, false, false };
            bool[] structure = new bool[] { true, true, false, false, false, false, false, false, false, false, true, false, false, false, false, false, false, false, false, true, false, false, false, false, false, false, false, false, true, false, false, false, false, false, false, false, false, true, false, false, false, false, false, false, false, false, true, false, false, false, false, false, false, false, false, true, false, false, false, false, false, false, false, false, true, false, false, false, false, false, false, false, false };
            SaveModel b = new SaveModel(structure, 1);

            for (int i = 0; i < 125; i++) {
                int X = i % 5;
                int Y = i / 5 % 5;
                int Z = i / 25 % 5;
                b.Add(X, Y, Z, 1, SDFunction(new Vector3(X/4f, Y/4f, Z/4f)));
            }
            return b;

            float SDFunction(Vector3 pos)
            {
                Vector3 centre = new Vector3(.5f, .5f, 1f);
                return (centre - pos).Length() - .32f;
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
        
        internal void Init(SaveModel data, Int3 pos, int level)
        {
            Vertices = new float[8];

            for (int i = 0; i < 8; i++) {
                Vertices[i] = data.Values[(pos + split(i))*data.ScaleLevel(level)];
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
