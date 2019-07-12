
using Converter;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using Veldrid.Sdl2;

namespace SDFbox
{
    class Logic
    {
        public const int xSize = 720;
        public const int ySize = 720;
        public static bool mouseDown = false;
        public const int GroupSize = 25;
        public static bool debugGizmos = false;
        public static bool debugWindow = false;
        public static string loadPath = "";
        public static float[] frames = new float[24];
        public static VertexModel vm;

        const float mSpeed = 0.2f;
        const float tSpeed = 0.1f;
        public static Info State = new Info() {
            position = new Vector3(0.5f, 0.5f, 0.1f),
            light = new Vector3(0f, 0f, 0f),
            strength = .5f,
            margin = 0.001f,
            screen_size = new Vector2(xSize, ySize)
        };
        public static DrawInfo DrawState {
            get {
                return new DrawInfo() {
                        showDebug = debugGizmos
                    };
            }
        }


        /*public static Info[] GetInfo {
            get {
                return new Info[] { new Info(new Float3x3(headingMat), position, dataSize) };
            }
        }*/

        static Vector2 heading = Vector2.Zero;
        public static Vector2 Heading {
            get {
                return heading;
            }
            set {
                heading = value;
                State.heading = new Float3x3(Matrix4x4.CreateFromYawPitchRoll(heading.Y, heading.X, 0));
            }
        }
        public static Vector2 Look {
            get {
                Vector2 degree = heading / (2*(float) Math.PI) * 360;
                return new Vector2(
                    (float) Math.Round(((-degree.X + 90) % 360 + 360) % 360, 2),
                    (float) Math.Round((degree.Y % 360 + 360) % 360, 2));
            }
        }
        public static Vector3 Position {
            get {
                return State.position;
            }
            set {
                State.position = value;
                float furthest = 0;
                for (int i = 0; i < 8; i++) {
                    float distance = (SdfMath.split(i).Vector - State.position).LengthSquared();
                    if (distance > furthest)
                        furthest = distance;
                }
                State.limit = furthest;
            }
        }
        private static Matrix4x4 yawMat {
            get {
                return Matrix4x4.CreateFromYawPitchRoll(heading.Y, 0, 0);
            }
        }

        private static Dictionary<Key, bool> pressed;

        public static OctS[] MakeData(string filename)
        {
            OctS[] data = new OctS[0];
            filename = AutocompleteFile(filename);
            string basename = Path.ChangeExtension(filename, null);
            

            if (Path.GetExtension(filename) == ".asdf") {
                Debug.WriteLine("Loading from " + filename);
                using (BinaryReader reader = new BinaryReader(File.Open(filename, FileMode.Open)))
                    data = OctS.Deserialize(reader);

            } else if (Path.GetExtension(filename) == ".obj") {
                Debug.WriteLine("Generating from " + filename);
                Debug.WriteLine("Preprocessing...");
                vm = new VertexModel(filename);//, Program.device, Program.commandList, Program.factory);
                Debug.WriteLine("Building ASDF...");

                Model m = new Model(vm);
                //Model m = new Model(Generator.GpuGenerator.Generate(vm, Program.device, Program.commandList, Program.factory));

                data = m.Cast();

                Debug.WriteLine("Saving to " + basename + ".asdf");
                using (BinaryWriter writer = new BinaryWriter(File.Create(basename + ".asdf")))
                    OctS.Serialize(data, writer);
            } else
                throw new ArgumentException("Invalid " + Path.GetExtension(filename) + "; can only read .obj or .asdf files.");

            State.buffer_size = data.Length;
            return data;
        }
        public static string AutocompleteFile(string filename)
        {
            if (File.Exists(filename))
                return filename;
            else if (File.Exists(filename + ".asdf"))
                return filename + ".asdf";
            else if (File.Exists(filename + ".obj"))
                return filename + ".obj";
            else
                return null;
        }

        public static Vertex[] ScreenQuads {
            get {
                return new Vertex[] {
                    new Vertex(RgbaFloat.Pink, -1, 1),
                    new Vertex(RgbaFloat.Pink, 1, 1),
                    new Vertex(RgbaFloat.Pink, -1, -1),
                    new Vertex(RgbaFloat.Pink, 1, -1)
                };
            }
        }
        public static Vertex[] DebugUtils {
            get {
                var d = new Vertex[] {
                    new Vertex(RgbaFloat.Red, 0, 0, 0),
                    new Vertex(RgbaFloat.Red, .04, 0, 0),
                    new Vertex(RgbaFloat.Green, 0, 0, 0),
                    new Vertex(RgbaFloat.Green, 0, .04, 0),
                    new Vertex(RgbaFloat.Blue, 0, 0, 0),
                    new Vertex(RgbaFloat.Blue, 0, 0, .04),

                    new Vertex(RgbaFloat.Red, .96, 1, 1),
                    new Vertex(RgbaFloat.Red, 1, 1, 1),
                    new Vertex(RgbaFloat.Green, 1, .96, 1),
                    new Vertex(RgbaFloat.Green, 1, 1, 1),
                    new Vertex(RgbaFloat.Blue, 1, 1, .96),
                    new Vertex(RgbaFloat.Blue, 1, 1, 1)
                };
                for (int i = 0; i < 6; i++) {
                    d[i].Position.Y *= -1;
                    d[i].Position -= new Vector3(State.position.X, -State.position.Y, State.position.Z);
                    d[i].Position = Vector3.Transform(d[i].Position, Matrix4x4.CreateFromYawPitchRoll(-heading.Y, heading.X, 0));
                    //d[i].Position = Vector3.Transform(d[i].Position, Matrix4x4.CreateFromYawPitchRoll(0, heading.X, 0));
                    d[i].Position = new Vector3(d[i].Position.X / d[i].Position.Z, d[i].Position.Y / d[i].Position.Z, Math.Sign(d[i].Position.Z));
                }
                return d;
            }
        }

        public static void Init(Sdl2Window window)
        {
            ResetKeys();
            window.KeyDown += KeyDown;
            window.KeyUp += KeyUp;
            window.DragDrop += DragDrop;

            window.MouseMove += (MouseMoveEventArgs mouseEvent) => {
                if (mouseEvent.State.IsButtonDown(0) && !ImGui.GetIO().WantCaptureMouse) {
                    window.SetMousePosition(Program.window.Width / 2, Program.window.Height / 2);
                    window.CursorVisible = false;
                    if (mouseDown)
                        MouseMove(mouseEvent.MousePosition - new Vector2(Program.window.Width / 2, Program.window.Height / 2));
                } else
                    window.CursorVisible = true;
                mouseDown = mouseEvent.State.IsButtonDown(0);
            };//*/
        }
        public static void KeyDown(KeyEvent keyEvent)
        {
            if (!ImGui.GetIO().WantCaptureKeyboard) {
                pressed[keyEvent.Key] = true;
                if (keyEvent.Key == Key.F11)
                    Program.ToggleFullscreen();
            }
        }
        public static void KeyUp(KeyEvent keyEvent)
        {
            pressed[keyEvent.Key] = false;
        }
        public static void DragDrop(DragDropEvent dropEvent)
        {
            loadPath = dropEvent.File;
        }

        public static void Update(TimeSpan t, InputSnapshot input)
        {
            float sec = (float) t.TotalSeconds;
            for (int i = frames.Length - 1; i > 0; i--) {
                frames[i] = frames[i-1];
            }
            frames[0] = sec;
            float transform = mSpeed * sec;
            float rotate = tSpeed * sec;
            if (pressed[Key.Right])
                Heading += new Vector2(0, rotate);
            if (pressed[Key.Left])
                Heading -= new Vector2(0, rotate);
            if (pressed[Key.Up])
                Heading += new Vector2(rotate, 0);
            if (pressed[Key.Down])
                Heading -= new Vector2(rotate, 0);
            if (pressed[Key.W] || pressed[Key.Number8])
                Position += Vector3.Transform(new Vector3(0, 0, 1), yawMat) * transform;
            if (pressed[Key.S] || pressed[Key.Number2])
                Position += Vector3.Transform(new Vector3(0, 0, -1), yawMat) * transform;
            if (pressed[Key.D] || pressed[Key.Number6])
                Position += Vector3.Transform(new Vector3(1, 0, 0), yawMat) * transform;
            if (pressed[Key.A] || pressed[Key.Number4])
                Position += Vector3.Transform(new Vector3(-1, 0, 0), yawMat) * transform;
            if (pressed[Key.LShift] || pressed[Key.Number9])
                Position += new Vector3(0, -1, 0) * transform;
            if (pressed[Key.LControl] || pressed[Key.Number3])
                Position += new Vector3(0, 1, 0) * transform;

            Program.imGuiRenderer.Update(sec, input);
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
            Heading += new Vector2(-diff.Y, diff.X) / 512 * 4;
        }

        public static void MakeGUI(int time)
        {
            double milliPerFrame = Math.Round(1000.0/time);
            double area = Program.ScreenSize.X * Program.ScreenSize.Y;
            double nanoPerPixel = Math.Round(1000000000.0 / time / area);
            ImGui.StyleColorsClassic();

            if (ImGui.Begin("Debug", ImGuiWindowFlags.AlwaysAutoResize)) {
                if (debugGizmos)
                    ImGui.PlotLines("", ref frames[0], frames.Length, 0, $"{time} FPS - {nanoPerPixel} nspp", 0, 0.1f, new Vector2(200, 40));
                else
                    ImGui.Text($"{time} FPS - {milliPerFrame} mspf - {nanoPerPixel} nspp");

                ImGui.Text($"{Program.ScreenSize.X} x {Program.ScreenSize.Y} px");
                ImGui.SameLine();
                if (Program.ScreenSize != new Vector2(720, 720)) {
                    if (ImGui.Button("Reset")) {
                        Program.window.WindowState = WindowState.Normal;
                        Program.ScreenSize = new Vector2(720, 720);
                    }
                    if (Program.window.WindowState != WindowState.BorderlessFullScreen)
                        ImGui.SameLine();
                }
                if (Program.window.WindowState != WindowState.BorderlessFullScreen) {
                    if (ImGui.Button("Fullscreen [F11]"))
                        Program.window.WindowState = WindowState.BorderlessFullScreen;
                }

                double value = Math.Round(Model.Sample(Program.model, State.position), 3);
                int[] path = Model.PathTo(Program.model, State.position).ToArray();
                ImGui.Text($"Y-Rotation {Look.Y.ToString().PadLeft(6)}, X-Rotation {Look.X.ToString()}");
                ImGui.Text($"SDF Value at {State.position}: {value.ToString().PadLeft(5)}");
                ImGui.Text("In Quadrant " + string.Join(", ", path));

                ImGui.SliderFloat("Light X", ref State.light.X, -1, 2);
                ImGui.SliderFloat("Light Y", ref State.light.Y, -1, 2);
                ImGui.SliderFloat("Light Z", ref State.light.Z, -1, 2);
                ImGui.SliderFloat("Light Intensity", ref State.strength, 0, 4);
                
                bool submitted = ImGui.InputText($"Load File", ref loadPath, 128, ImGuiInputTextFlags.EnterReturnsTrue);
                string complete = AutocompleteFile(loadPath.Replace('/', '\\'));
                if (complete != null) {
                    string buttonText = "Load";
                    if (Path.GetExtension(complete) == ".obj") {
                        ImGui.InputInt("Resolution Level", ref Model.MaxDepth);
                        buttonText = "Import";
                    }

                    if (submitted ||  ImGui.Button(buttonText))
                        Program.Load(loadPath);
                }
                
                ImGui.Checkbox("Show Debug Gizmos", ref debugGizmos);
            }

            //data.CmdListsRange[0].CmdBuffer[0].
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 112)]
    struct Info
    {
        public Float3x3 heading;
        public Vector3 position;
        public float margin;
        public Vector2 screen_size;
        public int buffer_size;
        public float limit;
        public Vector3 light;
        public float strength;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 16)]
    struct DrawInfo
    {
        public bool showDebug;
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
