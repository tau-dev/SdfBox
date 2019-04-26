
using Converter;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace SDFbox
{
    using ModelValues = Dictionary<Int3, float>;
    class Logic
    {
        public const int xSize = 720;
        public const int ySize = 720;
        public static bool mouseDown = false;
        public const int GroupSize = 25;

        const float mSpeed = 0.2f;
        const float tSpeed = 0.1f;
        static int dataSize = 0;

        static Vector2 heading = new Vector2(0, 0);
        public static Vector3 position { get; private set; } = new Vector3(0.5f, 0.5f, 0.1f);
        private static Matrix4x4 headingMat {
            get {
                Matrix4x4 yaw = Matrix4x4.CreateRotationY(heading.Y);
                Vector3 camXaxis = Vector3.Transform(new Vector3(1, 0, 0), yaw);
                //Matrix4x4 pitch = Matrix4x4.CreateFromAxisAngle(camXaxis, heading.X);
                return Matrix4x4.CreateFromYawPitchRoll(heading.Y, heading.X, 0);
            }
        }
        private static Matrix4x4 yawMat {
            get {
                return Matrix4x4.CreateFromYawPitchRoll(heading.Y, 0, 0);
            }
        }

        private static Dictionary<Key, bool> pressed;

        public static Info[] GetInfo {
            get {
                return new Info[] { new Info(new Float3x3(headingMat), position, dataSize) };
            }
        }

        public static OctS[] MakeData(string filename)
        {
            OctS[] data = new OctS[0];
            //BinaryFormatter readerWriter = new BinaryFormatter();

            if (Path.GetExtension(filename) == "") {
                if (File.Exists(filename + ".asdf"))
                    filename += ".asdf";
                else if (File.Exists(filename + ".obj"))
                    filename += ".obj";
                else
                    throw new FileNotFoundException();
            }

            if (Path.GetExtension(filename) == ".asdf") {
                Console.WriteLine("Loading from " + filename);
                using (BinaryReader reader = new BinaryReader(File.Open(filename, FileMode.Open)))
                    data = OctS.Deserialize(reader);

            } else if (Path.GetExtension(filename) == ".obj") {
                Console.WriteLine("Generating from " + filename);
                #region GPU_Gen
                /*model = Generator.GpuGenerator.Generate(
                    new Converter.VertexModel(filename + ".obj"), 
                    Program.graphicsDevice, Program.factory);//*/
                #endregion GPU_Gen
                var vm = new VertexModel(filename);
                Console.WriteLine("Building trees...");
                data = new Model(vm).Cast();

                Console.WriteLine("Saving to " + filename);
                using (BinaryWriter writer = new BinaryWriter(File.Create(filename)))
                    OctS.Serialize(data, writer);
            } else
                throw new ArgumentException("Invalid " + Path.GetExtension(filename) + "; can only read .obj or .asdf files.");

            dataSize = data.Length;
            return data;

            SaveModel tempMake()
            {
                SaveModel m = new SaveModel(new bool[] { false }, 1);
                ModelValues layera = new ModelValues();
                m.Add(SdfMath.split(0), 0, 0.8f);
                m.Add(SdfMath.split(1), 0, 0.7f);
                m.Add(SdfMath.split(2), 0, 0.8f);
                m.Add(SdfMath.split(3), 0, 0.8f);
                m.Add(SdfMath.split(4), 0, -0.2f);
                m.Add(SdfMath.split(5), 0, -0.2f);
                m.Add(SdfMath.split(6), 0, -0.2f);
                m.Add(SdfMath.split(7), 0, 0.8f);
                /*
                for (int i = 0; i < 8; i++) {
                    m.Add(Octree.split(i), 0, 1 - (float)i / 4);
                }*/
                return m;
            }
        }

        public static Vertex[] ScreenQuads {
            get {
                return new Vertex[] {
                    new Vertex(-1f, 1f, RgbaFloat.Black),
                    new Vertex(1f, 1f, RgbaFloat.Black),
                    new Vertex(-1f, -1f, RgbaFloat.Black),
                    new Vertex(1f, -1f, RgbaFloat.Black)
                };
            }
        }

        public static void KeyDown(KeyEvent keyEvent)
        {
            pressed[keyEvent.Key] = true;
        }
        public static void KeyUp(KeyEvent keyEvent)
        {
            pressed[keyEvent.Key] = false;
        }
        public static void Update(TimeSpan t)
        {
            float transform = mSpeed * (float) t.TotalSeconds;
            float rotate = tSpeed * (float) t.TotalSeconds;
            if (pressed[Key.Right])
                heading.Y += rotate;
            if (pressed[Key.Left])
                heading.Y -= rotate;
            if (pressed[Key.Up])
                heading.X += rotate;
            if (pressed[Key.Down])
                heading.X -= rotate;
            if (pressed[Key.W] || pressed[Key.Number8])
                position += Vector3.Transform(new Vector3(0, 0, 1), yawMat) * transform;
            if (pressed[Key.S] || pressed[Key.Number2])
                position += Vector3.Transform(new Vector3(0, 0, -1), yawMat) * transform;
            if (pressed[Key.D] || pressed[Key.Number6])
                position += Vector3.Transform(new Vector3(1, 0, 0), yawMat) * transform;
            if (pressed[Key.A] || pressed[Key.Number4])
                position += Vector3.Transform(new Vector3(-1, 0, 0), yawMat) * transform;
            if (pressed[Key.LShift] || pressed[Key.Number9])
                position += new Vector3(0, -1, 0) * transform;
            if (pressed[Key.LControl] || pressed[Key.Number3])
                position += new Vector3(0, 1, 0) * transform;
        }
        public static void ResetKeys()
        {
            pressed = new Dictionary<Key, bool>();
            Key[] toCheck = new Key[] {
                Key.Right, Key.Left, Key.Up, Key.Down,
                Key.W, Key.S, Key.A, Key.D, Key.LShift, Key.LControl,
                Key.Number8, Key.Number2, Key.Number6, Key.Number4, Key.Number9, Key.Number3,

            };
            foreach (Key k in toCheck) {
                pressed[k] = false;
            }
        }

        public static void MouseMove(Vector2 diff)
        {
            heading += new Vector2(-diff.Y, diff.X) / 512 * 4;
        }

        public static void MakeGUI()
        {
            ImGui.NewFrame();
            ImGui.Begin("Debug", ImGuiWindowFlags.NoBackground);
            ImGui.Button("Hello Mouse!");
            ImGui.End();
            ImGui.Render();
            var data = ImGui.GetDrawData();
            //data.CmdListsRange[0].CmdBuffer[0].
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 96)]
    struct Info
    {
        Float3x3 heading;
        Vector3 position;
        float margin;
        Vector2 screen_size;
        int buffer_size;
        float limit;
        Vector3 light;

        public Info(Float3x3 heading, Vector3 position, int dataSize)
        {
            this.heading = heading;
            this.position = position;
            margin = 0.001f;
            screen_size = Program.ScreenSize;
            buffer_size = dataSize;

            float furthest = 0;
            for (int i = 0; i < 8; i++) {
                float distance = (SdfMath.split(i).Vector - position).LengthSquared();
                if (distance > furthest)
                    furthest = distance;
            }
            limit = furthest + furthest;
            light = new Vector3(0.5f, -2f, 2f);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 48)]
    struct Float3x3
    {
        public float m11;
        public float m12;
        public float m13;
        public float x1;

        public float m21;
        public float m22;
        public float m23;
        public float x2;

        public float m31;
        public float m32;
        public float m33;
        public float x3;

        public Float3x3(Matrix4x4 x)
        {
            m11 = x.M11;
            m12 = x.M21;
            m13 = x.M31;

            m21 = x.M12;
            m22 = x.M22;
            m23 = x.M32;

            m31 = x.M13;
            m32 = x.M23;
            m33 = x.M33;

            x1 = 0;
            x2 = 0;
            x3 = 0;
        }

    }
}
