#region TODO
/*
 * Modeling
 * GUI layer
 * 
 */
#endregion

using ImGuiNET;
using System;
using System.Collections.Generic;
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
        static Sdl2Window window;
        public static GraphicsDevice graphicsDevice;
        public static ResourceFactory factory;
        static CommandList commandList;
        static DeviceBuffer vertexBuffer;
        static DeviceBuffer indexBuffer;

        static DeviceBuffer dataSBuffer;
        static DeviceBuffer infoUBuffer;
        static TextureView resultTBuffer;
        static ResourceSet structuredResources;
        static ResourceLayout resourceLayout;
        static Shader vertexShader;
        static Shader fragmentShader;
        static Texture renderTexture;
        static Pipeline pipeline;
        static ComputeUnit compUnit;

        static void Main(string[] args)
        {
            DateTime start = DateTime.Now;
            OctS[] model = Logic.MakeData(args[0]);
            TimeSpan duration = DateTime.Now - start;
            Console.WriteLine(duration);
            window = Utilities.MakeWindow(720, 720);

            Logic.ResetKeys();
            window.KeyDown += Logic.KeyDown;
            window.KeyUp += Logic.KeyUp;
            window.MouseMove += (MouseMoveEventArgs mouseEvent) => {
                if (mouseEvent.State.IsButtonDown(0)) {
                    window.SetMousePosition(360, 360);
                    if (Logic.mouseDown)
                        Logic.MouseMove(mouseEvent.MousePosition - new Vector2(360, 360));
                }
                Logic.mouseDown = mouseEvent.State.IsButtonDown(0);
            };

            graphicsDevice = VeldridStartup.CreateGraphicsDevice(window);

            CreateResources(model);

            ImGui.CreateContext();

            FPS fpsCounter = new FPS();
            DateTime time = DateTime.Now;

            while (window.Exists) {
                window.PumpEvents();
                Logic.Update(DateTime.Now - start);
                start = DateTime.Now;
                Draw();
                fpsCounter.Frame();
            }
            ImGui.DestroyContext();
            DisposeResources();
        }

        static void Draw()
        {
            window.PumpEvents();
            window.PumpEvents((ref SDL_Event ev) => {
                Console.WriteLine(ev.type);
            });

            graphicsDevice.UpdateBuffer(infoUBuffer, 0, Logic.GetInfo);
            var cl = commandList;
            cl.Begin();
            compUnit.DispatchSized(cl, (uint) window.Width, (uint) window.Height, 1);
            cl.End();
            graphicsDevice.SubmitCommands(commandList);



            cl.Begin();
            cl.SetFramebuffer(graphicsDevice.SwapchainFramebuffer);
            cl.SetFullViewports();

            cl.ClearColorTarget(0, RgbaFloat.Black);

            cl.SetPipeline(pipeline);
            cl.SetGraphicsResourceSet(0, structuredResources);
            cl.SetVertexBuffer(0, vertexBuffer);
            cl.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
            cl.DrawIndexed(
                indexStart: 0,
                indexCount: 4,
                instanceStart: 0,
                instanceCount: 1,
                vertexOffset: 0);
            cl.End();
            graphicsDevice.SubmitCommands(commandList);
            /*
            cl.Begin();
            cl.SetFramebuffer(graphicsDevice.MainSwapchain.Framebuffer);
            _controller.Render(graphicsDevice, cl);
            cl.End();
            graphicsDevice.SubmitCommands(cl);//*/

            graphicsDevice.SwapBuffers(graphicsDevice.MainSwapchain);
            graphicsDevice.SwapBuffers();
        }


        static void CreateResources(OctS[] octData)
        {
            factory = graphicsDevice.ResourceFactory;
            Vertex[] quadVertices = Logic.ScreenQuads;
            ushort[] quadIndices = { 0, 1, 2, 3 };


            vertexBuffer = MakeBuffer(quadVertices, BufferUsage.VertexBuffer);
            indexBuffer = MakeBuffer(quadIndices, BufferUsage.IndexBuffer);

            Console.WriteLine(Marshal.SizeOf(octData[0]));

            renderTexture = MakeTexture(Round(window.Width, Logic.GroupSize), Round(window.Height, Logic.GroupSize));
            dataSBuffer = MakeBuffer(octData, BufferUsage.StructuredBufferReadOnly);
            resultTBuffer = factory.CreateTextureView(new TextureViewDescription(renderTexture));
            infoUBuffer = MakeBuffer(Logic.GetInfo, BufferUsage.UniformBuffer);

            resourceLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(new ResourceLayoutElementDescription[] {
                new ResourceLayoutElementDescription("TB0", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
            }));
            structuredResources = factory.CreateResourceSet(new ResourceSetDescription(resourceLayout, resultTBuffer));


            vertexShader = LoadShader(ShaderStages.Vertex);
            fragmentShader = LoadShader("DisplayFrag", ShaderStages.Fragment);

            MakePipeline();

            var compUDesc = new ComputeUnit.Description(LoadShader(ShaderStages.Compute));
            compUDesc.AddResource("SB0", ResourceKind.StructuredBufferReadOnly, dataSBuffer);
            compUDesc.AddResource("TB0", ResourceKind.TextureReadWrite, resultTBuffer);
            compUDesc.AddResource("UB0", ResourceKind.UniformBuffer, infoUBuffer);

            compUnit = new ComputeUnit(factory, compUDesc);

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

            graphicsDevice.UpdateBuffer(newBuffer, 0, data);
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
            Console.WriteLine(extension);
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
            byte[] shaderBytes = File.ReadAllBytes(path);
            return graphicsDevice.ResourceFactory.CreateShader(new ShaderDescription(stage, shaderBytes, entryPoint));

            string GraphicsExtension()
            {
                switch (graphicsDevice.BackendType) {
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

        static void MakePipeline()
        {
            GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription {
                BlendState = BlendStateDescription.SingleOverrideBlend
            };

            Utilities.SetStencilState(pipelineDescription);
            Utilities.SetRasterizerState(pipelineDescription);
            // TODO: inline this v ^
            pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            pipelineDescription.ResourceLayouts = new ResourceLayout[] { resourceLayout };
            pipelineDescription.ShaderSet = Utilities.MakeShaderSet(vertexShader, fragmentShader);
            pipelineDescription.Outputs = graphicsDevice.SwapchainFramebuffer.OutputDescription;

            pipeline = factory.CreateGraphicsPipeline(pipelineDescription);
        }

        static void DisposeResources()
        {
            pipeline.Dispose();
            vertexShader.Dispose();
            fragmentShader.Dispose();
            commandList.Dispose();
            vertexBuffer.Dispose();
            indexBuffer.Dispose();
            graphicsDevice.Dispose();
        }

        public static Vector2 ScreenSize {
            get {
                return new Vector2(window.Width, window.Height);
            }
        }
        public static uint Round(float val, int blocksize)
        {
            return (uint) (val + blocksize - val % blocksize);
        }
    }



    struct Vertex
    {
        public Vector2 Position; // This is the position, in normalized device coordinates.
        public RgbaFloat Color; // This is the color of the vertex.
        public Vertex(Vector2 position, RgbaFloat color)
        {
            Position = position;
            Color = color;
        }
        public Vertex(float x, float y, RgbaFloat color)
        {
            Position = new Vector2(x, y);
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