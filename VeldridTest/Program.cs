#region TODO
/*
 * Pruning
        Undivide (turn into leaves) all branches with smallest(absolute distance) > 2*sidelength
 * Use Compute shader
        Figure out storing & display of image
        Figure out data transfers
 * 
 */
#endregion

using System;
using System.IO;
using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using System.Runtime.InteropServices;

namespace SDFbox
{
    class Program
    {
        static Sdl2Window window;
        static GraphicsDevice graphicsDevice;
        static CommandList commandList;
        static DeviceBuffer vertexBuffer;
        static DeviceBuffer indexBuffer;

        static DeviceBuffer dataSBuffer;
        static DeviceBuffer infoUBuffer;
        static TextureView resultTBuffer;
        static ResourceSet computeResources;
        static ResourceSet structuredResources;
        static ResourceLayout computeResourceLayout;
        static ResourceLayout resourceLayout;
        static Shader vertexShader;
        static Shader fragmentShader;
        static Shader computeShader;
        static Texture renderTexture;
        static Pipeline computePipeline;
        static Pipeline pipeline;
        static ResourceFactory factory;

        static void Main(string[] args)
        {
            window = Utilities.MakeWindow(720, 720);

            window.KeyDown += Logic.KeyHandler;
            window.MouseMove += (MouseMoveEventArgs mouseEvent) => {
                if (mouseEvent.State.IsButtonDown(0)) {
                    window.SetMousePosition(360, 360);
                    Logic.MouseMove(mouseEvent.MousePosition - new Vector2(360, 360));
                }
            };

            graphicsDevice = VeldridStartup.CreateGraphicsDevice(window);

            CreateResources();

            FPS fpsCounter = new FPS();

            while (window.Exists) {
                window.PumpEvents();
                Draw();
                fpsCounter.Frame();
            }
            DisposeResources();
        }

        static void Draw()
        {
            window.PumpEvents();
            window.PumpEvents((ref SDL_Event ev) => {
                Console.WriteLine(ev.type);
            });

            graphicsDevice.UpdateBuffer(infoUBuffer, 0, Logic.GetInfo);

            commandList.Begin();
            commandList.SetPipeline(computePipeline);
            commandList.SetComputeResourceSet(0, computeResources);
            commandList.Dispatch((uint) window.Width / 25, (uint) window.Height / 25, 1);
            commandList.End();
            graphicsDevice.SubmitCommands(commandList);
            


            commandList.Begin();
            commandList.SetFramebuffer(graphicsDevice.SwapchainFramebuffer);
            commandList.SetFullViewports();

            commandList.ClearColorTarget(0, RgbaFloat.Black);
            
            commandList.SetPipeline(pipeline);
            commandList.SetGraphicsResourceSet(0, structuredResources);
            commandList.SetVertexBuffer(0, vertexBuffer);
            commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
            commandList.DrawIndexed(
                indexStart: 0,
                indexCount: 4,
                instanceStart: 0,
                instanceCount: 1,
                vertexOffset: 0);

            commandList.End();
            graphicsDevice.SubmitCommands(commandList);
            
            graphicsDevice.SwapBuffers();
        }


        static void CreateResources()
        {
            factory = graphicsDevice.ResourceFactory;
            Vertex[] quadVertices = Logic.ScreenQuads;
            ushort[] quadIndices = { 0, 1, 2, 3 };


            OctS[] octData = Logic.MakeData("model");

            vertexBuffer = MakeBuffer(quadVertices, BufferUsage.VertexBuffer);
            indexBuffer = MakeBuffer(quadIndices, BufferUsage.IndexBuffer);

            Console.WriteLine(Marshal.SizeOf(octData[0]));

            renderTexture = MakeTexture((uint) window.Width, (uint) window.Height);
            dataSBuffer = MakeBuffer(octData, BufferUsage.StructuredBufferReadOnly);
            resultTBuffer = factory.CreateTextureView(new TextureViewDescription(renderTexture));
            infoUBuffer = MakeBuffer(Logic.GetInfo, BufferUsage.UniformBuffer);

            computeResourceLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(new ResourceLayoutElementDescription[] {
                new ResourceLayoutElementDescription("SB0", ResourceKind.StructuredBufferReadOnly, ShaderStages.Compute),
                new ResourceLayoutElementDescription("TB0", ResourceKind.TextureReadWrite, ShaderStages.Compute),
                new ResourceLayoutElementDescription("UB0", ResourceKind.UniformBuffer, ShaderStages.Compute)
            }));
            resourceLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(new ResourceLayoutElementDescription[] {
                new ResourceLayoutElementDescription("TB0", ResourceKind.TextureReadOnly, ShaderStages.Fragment)
            }));
            computeResources = factory.CreateResourceSet(new ResourceSetDescription(computeResourceLayout, dataSBuffer, resultTBuffer, infoUBuffer));
            structuredResources = factory.CreateResourceSet(new ResourceSetDescription(resourceLayout, resultTBuffer));
            

            vertexShader = LoadShader(ShaderStages.Vertex);
            fragmentShader = LoadShader("DisplayFrag", ShaderStages.Fragment);
            computeShader = LoadShader(ShaderStages.Compute);

            MakePipeline();
            MakeComputePipeline();

            commandList = factory.CreateCommandList();
        }
        static DeviceBuffer MakeBuffer<T>(T[] data, BufferUsage usage, uint size = 0) where T : struct
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
            TextureDescription tDesc = new TextureDescription();
            tDesc.Type = TextureType.Texture2D;
            tDesc.ArrayLayers = 0;
            tDesc.Format = PixelFormat.R32_G32_B32_A32_Float;
            tDesc.Width = x;
            tDesc.Height = y;
            tDesc.Depth = 1;
            tDesc.Usage = TextureUsage.Storage | TextureUsage.Sampled;
            tDesc.MipLevels = 1;
            tDesc.ArrayLayers = 1;
            tDesc.SampleCount = TextureSampleCount.Count1;
            return factory.CreateTexture(tDesc);
        }
        static Shader LoadShader(string name, ShaderStages stage)
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
                    entryPoint = "CS";
                    break;
            }
            string path = Path.Combine(AppContext.BaseDirectory, "Shaders", $"{name}.{extension}");
            byte[] shaderBytes = File.ReadAllBytes(path);
            return graphicsDevice.ResourceFactory.CreateShader(new ShaderDescription(stage, shaderBytes, entryPoint));

            string GraphicsExtension()
            {
                switch (graphicsDevice.BackendType) {
                    case GraphicsBackend.Direct3D11:
                        return ("hlsl.bytes");
                    case GraphicsBackend.Vulkan:
                        return ("spv");
                    case GraphicsBackend.OpenGL:
                        return ("glsl");
                    case GraphicsBackend.Metal:
                        return ("metallib");
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

            pipelineDescription.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
            pipelineDescription.ResourceLayouts = new ResourceLayout[] { resourceLayout };
            pipelineDescription.ShaderSet = Utilities.MakeShaderSet(vertexShader, fragmentShader);
            pipelineDescription.Outputs = graphicsDevice.SwapchainFramebuffer.OutputDescription;

            pipeline = factory.CreateGraphicsPipeline(pipelineDescription);
        }
        static void MakeComputePipeline()
        {
            ComputePipelineDescription pipelineDescription = new ComputePipelineDescription();
            
            pipelineDescription.ResourceLayouts = new ResourceLayout[] { computeResourceLayout };
            pipelineDescription.ComputeShader = computeShader;
            pipelineDescription.ThreadGroupSizeX = 32;
            pipelineDescription.ThreadGroupSizeY = 32;
            pipelineDescription.ThreadGroupSizeX = 1;

            computePipeline = factory.CreateComputePipeline(pipelineDescription);
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
    struct OctS
    {
        public Int32 Parent;
        public Vector3 lower;
        public Vector3 higher;
        public Int32 empty;

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
            foreach(float x in verts) {
                if (x <= 0)
                    empty = 0;
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
    }
}