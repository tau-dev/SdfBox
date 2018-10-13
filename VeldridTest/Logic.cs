using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using Veldrid.Sdl2;

namespace SDFbox
{
    using ModelValues = Dictionary<Int3, float>;
    class Logic
    {
        public const int xSize = 720;
        public const int ySize = 720;

        const float mSpeed = 0.04f;
        const float tSpeed = 0.02f;

        static Vector2 heading = new Vector2(0, 0);
        static Vector3 position = new Vector3(0.5f, 0.5f, 0.1f);
        private static Matrix4x4 headingMat {
            get {
                Matrix4x4 yaw = Matrix4x4.CreateRotationY(heading.Y);
                Vector3 camXaxis = Vector3.Transform(new Vector3(1, 0, 0), yaw);
                Matrix4x4 pitch = Matrix4x4.CreateFromAxisAngle(camXaxis, heading.X);
                return Matrix4x4.CreateFromYawPitchRoll(heading.Y, heading.X, 0);
            }
        }
        private static Matrix4x4 yawMat {
            get {
                return Matrix4x4.CreateFromYawPitchRoll(heading.Y, 0, 0);
            }
        }

        public static Info[] GetInfo
        {
            get {
                return new Info[] { new Info(new Float3x3(headingMat), position) };
            }
        }

        public static OctS[] MakeData()
        {
            Model model = new Model(Model.Build());//(Model.Build());//(tempMake());
            OctS[] data = new OctS[9];
            data = model.Cast();
            return data;

            SaveModel tempMake()
            {
                SaveModel m = new SaveModel(new bool[] { false }, 1);
                ModelValues layera = new ModelValues();
                m.Add(Octree.split(0), 0, 0.8f);
                m.Add(Octree.split(1), 0, 0.7f);
                m.Add(Octree.split(2), 0, 0.8f);
                m.Add(Octree.split(3), 0, 0.8f);
                m.Add(Octree.split(4), 0, -0.2f);
                m.Add(Octree.split(5), 0, -0.2f);
                m.Add(Octree.split(6), 0, -0.2f);
                m.Add(Octree.split(7), 0, 0.8f);
                /*
                for (int i = 0; i < 8; i++) {
                    m.Add(Octree.split(i), 0, 1 - (float)i / 4);
                }*/
                return m;
            }
        }
        
        public static Vertex[] ScreenQuads
        {
            get {
                return new Vertex[] {
                    new Vertex(-1f, 1f, RgbaFloat.Black),
                    new Vertex(1f, 1f, RgbaFloat.Black),
                    new Vertex(-1f, -1f, RgbaFloat.Black),
                    new Vertex(1f, -1f, RgbaFloat.Black)
                };
            }
        }
        
        public static void KeyHandler(KeyEvent keyEvent)
        {
            switch (keyEvent.Key)
            {
                case Key.Right:
                    heading.Y += tSpeed;
                    break;
                case Key.Left:
                    heading.Y -= tSpeed; ;
                    break;
                case Key.Up:
                    heading.X += tSpeed;
                    break;
                case Key.Down:
                    heading.X -= tSpeed;
                    break;

                case Key.W:
                    position += Vector3.Transform(new Vector3(0, 0, 1), yawMat) * mSpeed;
                    Console.WriteLine(Vector3.Transform(new Vector3(0, 0, 1), headingMat));
                    break;
                case Key.S:
                    position += Vector3.Transform(new Vector3(0, 0, -1), yawMat) * mSpeed;
                    break;
                case Key.D:
                    position += Vector3.Transform(new Vector3(1, 0, 0), yawMat) * mSpeed;
                    break;
                case Key.A:
                    position += Vector3.Transform(new Vector3(-1, 0, 0), yawMat) * mSpeed;
                    break;
                case Key.LShift:
                    position += new Vector3(0, -1, 0) * mSpeed;
                    break;
                case Key.LControl:
                    position += new Vector3(0, 1, 0) * mSpeed;
                    break;
            }
        }
        public static void MouseMove(Vector2 diff)
        {
            heading += new Vector2(-diff.Y, diff.X) / 512 * 4;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 80)]
    struct Info
    {
        Float3x3 heading;
        Vector3 position;
        float margin;
        Vector2 screen;

        public Info(Float3x3 heading, Vector3 position)
        {
            this.heading = heading;
            this.position = position;
            margin = 0.001f;
            screen = Program.ScreenSize;
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
        }

    }
}
