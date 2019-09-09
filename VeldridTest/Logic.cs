
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
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
        public static bool debugWindow = true;
        public static string loadPath = "";
        public static ImGuiConsole console = new ImGuiConsole(80, 64);
        public static float[] frames = new float[96];
        public static int desiredFrameCount = 96;

        const float mSpeed = 0.2f;
        const float tSpeed = 0.1f;
        public static Info State = new Info() {
            position = new Vector3(0.5f, 0.5f, 0.1f),
            light = new Vector3(0f, 0f, 0f),
            strength = .5f,
            margin = .001f,
            screen_size = new Vector2(xSize, ySize),
            fov = 1
        };
        public static DrawInfo DrawState {
            get {
                return new DrawInfo() { showDebug = debugGizmos };
            }
        }


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

        public static OctData MakeData(string filename)
        {
            OctData data;

            filename = AutocompleteFile(filename);
            string basename = Path.ChangeExtension(filename, null);
            FileFormat format = FormatOf(filename);

            if (format == FileFormat.ASDF) {
                Debug.WriteLine("Loading from " + filename);
                throw new NotImplementedException();

            } else if (MustImport(format)) {
                var raw = OctData.NativeOctData.Generate(filename, format);
                data = new OctData(raw);
                OctData.NativeOctData.Free(raw);
            } else
                throw new ArgumentException("Invalid " + Path.GetExtension(filename) + "; can only read .obj or .asdf files.");

            State.buffer_size = (uint) data.Length;
            return data;

            OctData Sample()
            {
                var children = new Octree[8];
                for (int i = 0; i < 8; i++) {
                    var vals = new float[8];
                    for (int j = 0; j < 8; j++) {
                        vals[j] = dist(SdfMath.split(j) + SdfMath.split(i));
                    }
                    children[i] = new Octree(vals);
                }
                var mainvals = new float[8];
                for (int i = 0; i < 8; i++) {
                    mainvals[i] = dist(SdfMath.split(i)*2);
                }
                var t = new Octree(children, mainvals);

                return new Model(t).Cast();
            }
            float dist(Int3 p) => ((p.Vector - Vector3.One).Length() - 0.6f)*0.5f;
        }
        public static FileFormat FormatOf(string filename)
        {
            switch (Path.GetExtension(filename)) {
                case ".asdf":
                    return FileFormat.ASDF;
                case ".ply":
                    return FileFormat.Stanford;
                case ".obj":
                    return FileFormat.Wavefront;
                default:
                    return (FileFormat) (-1);
            }
        }
        public static string AutocompleteFile(string filename)
        {
            if (File.Exists(filename))
                return filename;
            else if (File.Exists(filename + ".asdf"))
                return filename + ".asdf";
            else if (File.Exists(filename + ".ply"))
                return filename + ".ply";
            else if (File.Exists(filename + ".obj"))
                return filename + ".obj";
            else
                return null;
        }
        public static bool MustImport(FileFormat f)
        {
            return f == FileFormat.Wavefront || f == FileFormat.Stanford;
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
                    /*
                    new Vertex(RgbaFloat.Red, .96, 1, 1),
                    new Vertex(RgbaFloat.Red, 1, 1, 1),
                    new Vertex(RgbaFloat.Green, 1, .96, 1),
                    new Vertex(RgbaFloat.Green, 1, 1, 1),
                    new Vertex(RgbaFloat.Blue, 1, 1, .96),
                    new Vertex(RgbaFloat.Blue, 1, 1, 1)*/
                };
                for (int i = 0; i < 6; i++) {
                    Vector3 p = d[i].Position - State.position - Vector3.UnitY;
                    p *= new Vector3(1, -1, 1);
                    Matrix4x4 m;
                    Matrix4x4.Invert(Matrix4x4.CreateFromYawPitchRoll(heading.Y, heading.X, 0), out m);
                    p = Vector3.Transform(p, m);
                    d[i].Position = new Vector3(p.X / p.Z, p.Y / p.Z, Math.Sign(d[i].Position.Z)) + new Vector3(0.5f);
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
                else if (keyEvent.Key == Key.Space)
                    debugWindow = !debugWindow;
                else if (keyEvent.Key == Key.X)
                    debugGizmos = !debugGizmos;
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
            if (desiredFrameCount != frames.Length)
                frames = new float[desiredFrameCount];
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
                Key.X, Key.Space

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
            if (!debugWindow)
                return;
            double milliPerFrame = Math.Round(1000.0/time);
            double area = Program.ScreenSize.X * Program.ScreenSize.Y;

            string nanoPerPixel = (1000000000.0 * SdfMath.Average(frames) / area).ToString("F2");
            ImGui.StyleColorsClassic();

            if (ImGui.Begin("Debug window ([space] to hide/show)", ImGuiWindowFlags.AlwaysAutoResize)) {
                

                ImGui.Text($"{Program.ScreenSize.X} x {Program.ScreenSize.Y} px");
                ImGui.SameLine();
                if (Program.ScreenSize != new Vector2(720, 720)) {
                    if (ImGui.Button("Reset")) {
                        Program.window.WindowState = WindowState.Normal;
                        Program.ScreenSize = new Vector2(720, 720);
                        Program.window.WindowState = WindowState.Normal;
                    }
                    if (Program.window.WindowState != WindowState.BorderlessFullScreen)
                        ImGui.SameLine();
                }
                if (Program.window.WindowState != WindowState.BorderlessFullScreen)
                    if (ImGui.Button("Fullscreen [F11]"))
                        Program.window.WindowState = WindowState.BorderlessFullScreen;


                ImGui.Text($"Y-Rotation {Look.Y.ToString().PadLeft(6)}, X-Rotation {Look.X.ToString()}");
                ImGui.SliderFloat("Light X", ref State.light.X, -1, 2);
                ImGui.SliderFloat("Light Y", ref State.light.Y, -1, 2);
                ImGui.SliderFloat("Light Z", ref State.light.Z, -1, 2);
                ImGui.SliderFloat("Light Intensity", ref State.strength, 0, 4);
                ImGui.SliderFloat("FOV", ref State.fov, 0.05f, 1.5f);

                bool submitted = ImGui.InputText("Load File", ref loadPath, 128, ImGuiInputTextFlags.EnterReturnsTrue);
                string complete = AutocompleteFile(loadPath.Replace('/', '\\'));
                if (complete != null) {
                    string buttonText = "Load";
                    if (MustImport(FormatOf(complete))) {
                        ImGui.InputInt("Resolution Level", ref Model.MaxDepth);
                        buttonText = "Import";
                    }
                    if (submitted || ImGui.Button(buttonText))
                        Program.Load(loadPath);
                }
                
                ImGui.Checkbox("Show Debug Gizmos", ref debugGizmos);
                if (debugGizmos) {
                    ImGui.PlotLines("", ref frames[0], frames.Length, 0, $"{time} FPS - {nanoPerPixel} nspp", 0, 0.1f, new Vector2(200, 40));
                    ImGui.DragInt("averaging time", ref desiredFrameCount, 1, 1, 96);
                    console.Render();
                } else
                    ImGui.Text($"{time} FPS - {milliPerFrame} mspf - {nanoPerPixel} nspp");
            }
        }
    }

    class ImGuiConsole : TextWriter
    {
        public int BufferLength { get; }
        public int BufferWidth { get; }
        List<string> lines = new List<string>();
        StringBuilder builder = new StringBuilder();
        public override Encoding Encoding => Encoding.UTF8;
        public ImGuiConsole(int bufferlength, int width)
        {
            BufferLength = bufferlength;
            BufferWidth = width;
            NewLine = "\n";
        }

        public void Render()
        {
            StringBuilder result = new StringBuilder();
            foreach (var line in lines) {
                result.AppendLine(line);
                //ImGui.Text(line);
            }
            result.Append(builder.ToString());
            string output = result.ToString();
            ImGui.InputTextMultiline("", ref output, 1024, new Vector2(7*BufferWidth, 128), ImGuiInputTextFlags.ReadOnly);
        }
        public override void Write(string value)
        {
            foreach (char c in value) {
                if (c == '\n' || builder.Length >= BufferWidth) {
                    lines.Add(builder.ToString());
                    builder.Clear();
                } else
                    builder.Append(c);
            }
            while (lines.Count > BufferLength)
                lines.RemoveAt(0);
        }
        public override void WriteLine(string value)
        {
            Write(value + '\n');
        }
    }

    enum FileFormat
    {
        ASDF,
        Stanford,
        Wavefront
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 112)]
    struct Info
    {
        public Float3x3 heading;
        public Vector3 position;
        public float margin;
        public Vector2 screen_size;
        public uint buffer_size;
        public float limit;
        public Vector3 light;
        public float strength;
        public float fov;
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
