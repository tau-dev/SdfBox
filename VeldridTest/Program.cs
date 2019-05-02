#region TODO
/*
 * Modeling
 * Moar lights
 * Moar speed
 */
#endregion

using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace SDFbox
{
    class Program
    {
        //TODO: Encapsulate most of this
        public static Sdl2Window window;
        public static GraphicsDevice device;
        public static ResourceFactory factory;
        public static ImGuiRenderer imGuiRenderer;
        static CommandList commandList;
        static DeviceBuffer quadVertexBuffer;
        static DeviceBuffer debugVertexBuffer;

        static DeviceBuffer dataSBuffer;
        static DeviceBuffer infoUBuffer;
        static DeviceBuffer drawUBuffer;
        static TextureView resultTBuffer;
        static Texture renderTexture;
        static ComputeUnit compUnit;
        static VertFragUnit renderUnit;
        static VertFragUnit debugUnit;

        static void Main(string[] args)
        {
            DateTime start = DateTime.Now;
            OctS[] model = Logic.MakeData(args[0]);
            Debug.WriteLine("Load time " + (DateTime.Now - start));

            window = Utilities.MakeWindow(720, 720);
            Logic.Init(window);
            window.Resized += Resize;

            FPS fpsCounter = new FPS();
            device = VeldridStartup.CreateGraphicsDevice(window);
            CreateResources(model);
            DateTime time;
            TimeSpan delta = TimeSpan.FromSeconds(0);
            Logic.Heading = Logic.Heading;
            Logic.Position = Logic.Position;

            while (window.Exists) {
                delta = DateTime.Now - start;
                start = DateTime.Now;

                var input = window.PumpEvents();
                Logic.Update(delta, input);
                Logic.MakeGUI(fpsCounter.Frames);
                Draw();
                fpsCounter.Frame();
            }
            ImGui.DestroyContext();
            DisposeResources();
        }

        static void Draw()
        {
            device.UpdateBuffer(infoUBuffer, 0, Logic.State);
            device.UpdateBuffer(drawUBuffer, 0, Logic.DrawState);

            var cl = commandList;
            cl.Begin();

            compUnit.DispatchSized(cl, (uint) window.Width, (uint) window.Height, 1);

            cl.SetFramebuffer(device.SwapchainFramebuffer);
            cl.SetFullViewports();
            cl.ClearColorTarget(0, RgbaFloat.Black);
            renderUnit.Draw(cl, indexCount: 4, instanceCount: 1);
            if (Logic.debugGizmos) {
                device.UpdateBuffer(debugVertexBuffer, 0, Logic.DebugUtils);
                debugUnit.Draw(cl, indexCount: 6, instanceCount: 1);
            }

            imGuiRenderer.Render(device, commandList);

            cl.End();
            device.SubmitCommands(commandList);
            device.SwapBuffers(device.MainSwapchain);
        }


        static void CreateResources(OctS[] octData)
        {
            factory = device.ResourceFactory;
            Vertex[] quadVertices = Logic.ScreenQuads;
            ushort[] quadIndices = { 0, 1, 2, 3 };
            Debug.WriteLine("Sizeof oct struct" + Marshal.SizeOf(octData[0]));

            imGuiRenderer = new ImGuiRenderer(device,
                device.SwapchainFramebuffer.OutputDescription, window.Width, window.Height);

            quadVertexBuffer = MakeBuffer(quadVertices, BufferUsage.VertexBuffer);
            renderTexture = MakeTexture(Round(window.Width, Logic.GroupSize), Round(window.Height, Logic.GroupSize));
            resultTBuffer = factory.CreateTextureView(new TextureViewDescription(renderTexture));
            drawUBuffer = MakeBuffer(new Info[] { Logic.State }, BufferUsage.UniformBuffer);

            var renderUdesc = new VertFragUnit.Description() {
                Vertex = LoadShader(ShaderStages.Vertex),
                Fragment = LoadShader("DisplayFrag", ShaderStages.Fragment),
                VertexBuffer = quadVertexBuffer,
                IndexBuffer = MakeBuffer(quadIndices, BufferUsage.IndexBuffer),
                Topology = PrimitiveTopology.TriangleStrip,
                Output = device.SwapchainFramebuffer.OutputDescription
            };
            renderUdesc.AddResource("TB0", ResourceKind.TextureReadOnly, resultTBuffer);
            renderUdesc.AddResource("UB0", ResourceKind.UniformBuffer, drawUBuffer);
            renderUnit = new VertFragUnit(factory, renderUdesc);


            dataSBuffer = MakeBuffer(octData, BufferUsage.StructuredBufferReadOnly);
            infoUBuffer = MakeBuffer(new Info[] { Logic.State }, BufferUsage.UniformBuffer);

            var compUDesc = new ComputeUnit.Description() { Shader = LoadShader(ShaderStages.Compute) };
            compUDesc.AddResource("SB0", ResourceKind.StructuredBufferReadOnly, dataSBuffer);
            compUDesc.AddResource("TB0", ResourceKind.TextureReadWrite, resultTBuffer);
            compUDesc.AddResource("UB0", ResourceKind.UniformBuffer, infoUBuffer);
            compUnit = new ComputeUnit(factory, compUDesc);


            ushort[] debugIndices = { 0, 1, 2, 3, 4, 5 };
            debugVertexBuffer = MakeBuffer(Logic.DebugUtils, BufferUsage.VertexBuffer);

            var debugUDesc = new VertFragUnit.Description() {
                Vertex = LoadShader(ShaderStages.Vertex),
                Fragment = LoadShader("Plain", ShaderStages.Fragment),
                VertexBuffer = debugVertexBuffer,
                IndexBuffer = MakeBuffer(debugIndices, BufferUsage.IndexBuffer),
                Topology = PrimitiveTopology.LineList,
                Output = device.SwapchainFramebuffer.OutputDescription
            };//*/
            debugUnit = new VertFragUnit(factory, debugUDesc);

            commandList = factory.CreateCommandList();
        }
        public static DeviceBuffer MakeBuffer<T>(T[] data, BufferUsage usage, uint size = 0) where T : struct
        {
            BufferDescription description;
            uint structuredStride = 0;
            uint singleSize = (uint) Marshal.SizeOf(data[0]);

            if (usage == BufferUsage.StructuredBufferReadOnly || usage == BufferUsage.StructuredBufferReadWrite)
                structuredStride = singleSize;
            if (size == 0)
                size = (uint) data.Length * singleSize;

            description = new BufferDescription(size, usage, structuredStride);
            DeviceBuffer newBuffer = factory.CreateBuffer(description);

            device.UpdateBuffer(newBuffer, 0, data);
            return newBuffer;
        }
        static DeviceBuffer MakeEmptyBuffer<T>(BufferUsage usage, uint count) where T : struct
        {
            BufferDescription description;
            uint structuredStride = 0;
            uint singleSize = (uint) Marshal.SizeOf(new T());
            uint size = count * singleSize;

            if (usage == BufferUsage.StructuredBufferReadOnly || usage == BufferUsage.StructuredBufferReadWrite)
                structuredStride = singleSize;

            description = new BufferDescription(size, usage, structuredStride);
            DeviceBuffer newBuffer = factory.CreateBuffer(description);

            return newBuffer;
        }
        static Texture MakeTexture(uint x, uint y)
        {
            TextureDescription tDesc = new TextureDescription {
                Type = TextureType.Texture2D,
                ArrayLayers = 0,
                Format = PixelFormat.R32_G32_B32_A32_Float,
                Width = x,
                Height = y,
                Depth = 1,
                Usage = TextureUsage.Storage | TextureUsage.Sampled,
                MipLevels = 1
            };
            tDesc.ArrayLayers = 1;
            tDesc.SampleCount = TextureSampleCount.Count1;
            return factory.CreateTexture(tDesc);
        }
        public static Shader LoadShader(string name, ShaderStages stage)
        {
            string extension = GraphicsExtension();
            string entryPoint = "";
            switch (stage) {
                case ShaderStages.Vertex:
                    entryPoint = "VS";
                    break;
                case ShaderStages.Fragment:
                    entryPoint = "FS";
                    break;
                case ShaderStages.Compute:
                    entryPoint = "main";
                    break;
            }
            string path = Path.Combine(AppContext.BaseDirectory, "Shaders", $"{name}.{extension}");
            Debug.WriteLine("Loading shader" + path);
            byte[] shaderBytes = File.ReadAllBytes(path);
            var desc = new ShaderDescription(stage, shaderBytes, entryPoint);
            return factory.CreateShader(desc);

            string GraphicsExtension()
            {
                switch (device.BackendType) {
                    case GraphicsBackend.Direct3D11:
                        return "hlsl";        // RUNTIME-COMPILE
                    case GraphicsBackend.Vulkan:
                        return "spv";
                    case GraphicsBackend.OpenGL:
                        return "glsl";
                    case GraphicsBackend.Metal:
                        return "metallib";
                    default: throw new InvalidOperationException();
                }
            }
        }
        static Shader LoadShader(ShaderStages stage)
        {
            return LoadShader(stage.ToString(), stage);
        }

        public static void ToggleFullscreen()
        {
            if (window.WindowState == WindowState.BorderlessFullScreen)
                window.WindowState = WindowState.Normal;
            else
                window.WindowState = WindowState.BorderlessFullScreen;
        }
        static void Resize()
        {
            imGuiRenderer.WindowResized(window.Width, window.Height);
            device.MainSwapchain.Resize((uint) window.Width, (uint) window.Height);
            renderTexture = MakeTexture(Round(window.Width, Logic.GroupSize), Round(window.Height, Logic.GroupSize));
            resultTBuffer = factory.CreateTextureView(new TextureViewDescription(renderTexture));
            Logic.State.screen_size = ScreenSize;
            renderUnit.UpdateResources(factory, new BindableResource[] { resultTBuffer, drawUBuffer });
            compUnit.UpdateResources(factory, new BindableResource[] { dataSBuffer, resultTBuffer, infoUBuffer });
        }
        static void DisposeResources()
        {
            renderUnit.Dispose();
            compUnit.Dispose();

            commandList.Dispose();
            device.Dispose();
        }

        public static Vector2 ScreenSize {
            get {
                return new Vector2(window.Width, window.Height);
            }
            set {
                window.Width = (int) value.X;
                window.Height = (int) value.Y;
            }
        }
        public static uint Round(float val, int blocksize)
        {
            return (uint) (val + blocksize - val % blocksize);
        }
    }



    struct Vertex
    {
        public Vector3 Position; // This is the position, in normalized device coordinates.
        public RgbaFloat Color; // This is the color of the vertex.
        public Vertex(RgbaFloat color, float x, float y, float z = 0)
        {
            Position = new Vector3(x, y, z);
            Color = color;
        }
        public Vertex(RgbaFloat color, double x, double y, double z)
        {
            Position = new Vector3((float) x, (float) y, (float) z);
            Color = color;
        }
    }

    ///*
    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    [Serializable]
    struct OctS
    {
        public int Parent;
        public Vector3 lower;
        public Vector3 higher;
        public int empty;

        public Int8 children;
        public Vector8 verts;
        //*/
        /*
        public OctS(int Parent, Int8 children, Vector8 verts, Vector3 lower, Vector3 higher) {
            this.Parent = Parent;
            this.lower = lower;
            this.higher = higher;

            this.children = children;

            this.verts = verts;

            empty = 1;
            if (vertsL.X <= 0 || vertsL.X <= 0 || vertsL.W <= 0 || vertsL.Z <= 0)
                empty = 0;
            if (vertsH.X <= 0 || vertsH.X <= 0 || vertsH.W <= 0 || vertsH.Z <= 0)
                empty = 0;
        }//*/
        public OctS(int Parent, int[] children, float[] verts, Vector3 lower, Vector3 higher)
        {
            this.Parent = Parent;
            this.lower = lower;
            this.higher = higher;

            this.children = new Int8(children);
            this.verts = new Vector8(verts);

            empty = 1;
            foreach (float x in verts) {
                if (x <= 0)
                    empty = 0;
            }
        }

        public static void Serialize(OctS[] octs, BinaryWriter writer)
        {
            foreach (OctS current in octs) {
                writer.Write(current.Parent);
                writer.Write(current.empty);
                current.children.Serialize(writer);
                current.verts.Serialize(writer);
            }
        }

        public static OctS[] Deserialize(BinaryReader reader)
        {
            List<OctS> read = new List<OctS>();
            while (reader.BaseStream.Position != reader.BaseStream.Length) {
                read.Add(new OctS() {
                    Parent = reader.ReadInt32(),
                    empty = reader.ReadInt32(),
                    children = Int8.Deserialize(reader),
                    verts = Vector8.Deserialize(reader)
                });
            }
            Rectify(0, Vector3.Zero, 1);

            return read.ToArray();

            void Rectify(int p, Vector3 lower, float scale)
            {
                OctS current = read[p];
                current.lower = lower;
                current.higher = lower + Vector3.One * scale;
                read[p] = current;
                if (current.children.S == -1)
                    return;

                scale /= 2;
                for (int i = 0; i < 8; i++) {
                    Rectify(current.children.Array[i], lower + SdfMath.split(i).Vector * scale, scale);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 16)]
    struct Int4
    {
        public Int32 X;
        public Int32 Y;
        public Int32 Z;
        public Int32 W;

        public Int4(int x, int y, int z, int w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 32)]
    struct Int8
    {
        public Int32 S;
        public Int32 T;
        public Int32 U;
        public Int32 V;
        public Int32 W;
        public Int32 X;
        public Int32 Y;
        public Int32 Z;

        public Int8(int s, int t, int u, int v, int w, int x, int y, int z)
        {
            S = s;
            T = t;
            U = u;
            V = v;
            W = w;
            X = x;
            Y = y;
            Z = z;
        }
        public Int8(int[] d)
        {
            S = d[0];
            T = d[1];
            U = d[2];
            V = d[3];
            W = d[4];
            X = d[5];
            Y = d[6];
            Z = d[7];
        }


        public int[] Array {
            get {
                return new int[] { S, T, U, V, W, X, Y, Z };
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(S);
            writer.Write(T);
            writer.Write(U);
            writer.Write(V);
            writer.Write(W);
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Z);
        }
        public static Int8 Deserialize(BinaryReader reader)
        {
            return new Int8(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32());
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 32)]
    struct Vector8
    {
        public Single S;
        public Single T;
        public Single U;
        public Single V;
        public Single W;
        public Single X;
        public Single Y;
        public Single Z;

        public Vector8(float s, float t, float u, float v, float w, float x, float y, float z)
        {
            S = s;
            T = t;
            U = u;
            V = v;
            W = w;
            X = x;
            Y = y;
            Z = z;
        }
        public Vector8(float[] d)
        {
            S = d[0];
            T = d[1];
            U = d[2];
            V = d[3];
            W = d[4];
            X = d[5];
            Y = d[6];
            Z = d[7];
        }

        public float[] Array {
            get {
                return new float[] { S, T, U, V, W, X, Y, Z };
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(S);
            writer.Write(T);
            writer.Write(U);
            writer.Write(V);
            writer.Write(W);
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Z);
        }
        public static Vector8 Deserialize(BinaryReader reader)
        {
            return new Vector8(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }
    }
}