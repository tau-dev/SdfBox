
using Converter;
using System;
using System.Collections.Generic;
using System.Timers;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace SDFbox
{
    class Utilities
    {
        public static Sdl2Window MakeWindow(int width, int height)
        {
            WindowCreateInfo windowCI = new WindowCreateInfo() {
                X = 512,
                Y = 32,
                WindowWidth = width,
                WindowHeight = height,
                WindowTitle = "SDFbox",
            };
            Sdl2Window window = VeldridStartup.CreateWindow(ref windowCI);
            window.CursorVisible = false;

            return window;
        }

        public static void SetStencilState(GraphicsPipelineDescription pipelineDescription,
            bool depthTest = true, bool depthWrite = true,
            ComparisonKind comparison = ComparisonKind.LessEqual)
        {
            pipelineDescription.DepthStencilState = new DepthStencilStateDescription(
                depthTestEnabled: depthTest,
                depthWriteEnabled: depthWrite,
                comparisonKind: comparison);
        }

        public static void SetRasterizerState(GraphicsPipelineDescription pipelineDescription,
            FaceCullMode faceCull = FaceCullMode.Back,
            PolygonFillMode polygonFill = PolygonFillMode.Solid,
            FrontFace front = FrontFace.Clockwise,
            bool depthClip = true, bool scissorTest = false)
        {
            pipelineDescription.RasterizerState = new RasterizerStateDescription(
                cullMode: faceCull,
                fillMode: polygonFill,
                frontFace: front,
                depthClipEnabled: depthClip,
                scissorTestEnabled: scissorTest);
        }

        internal static Octree Convert(VertexModel vmodel)
        {
            throw new NotImplementedException();
        }

        public static ShaderSetDescription MakeShaderSet(Shader vertexShader, Shader fragmentShader)
        {
            return (new ShaderSetDescription(
                new VertexLayoutDescription[] { LayoutDescription() },
                new Shader[] { vertexShader, fragmentShader }));
        }

        static VertexLayoutDescription LayoutDescription()
        {
            return (new VertexLayoutDescription(
            new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float2),
            new VertexElementDescription("Color", VertexElementSemantic.Color, VertexElementFormat.Float4)));
        }
    }

    class ComputeUnit
    {
        public Shader Shader { get; }
        public Pipeline Pipeline { get; }
        ResourceLayout Layout;
        ResourceSet Resources;
        uint xGroupSize = 25;
        uint yGroupSize = 25;
        uint zGroupSize = 1;

        public ComputeUnit(ResourceFactory factory, Description d)
        {
            this.Shader = d.Shader;

            Layout = factory.CreateResourceLayout(d.LayoutDescription);
            Resources = factory.CreateResourceSet(d.ResourceDescription(Layout));
            ComputePipelineDescription pipelineDescription = new ComputePipelineDescription {
                ResourceLayouts = new ResourceLayout[] { Layout },
                ComputeShader = Shader,
                ThreadGroupSizeX = 32,
                ThreadGroupSizeY = 32
            };
            pipelineDescription.ThreadGroupSizeX = 1;

            Pipeline = factory.CreateComputePipeline(pipelineDescription);
        }
        public void Dispatch(CommandList cl, uint x, uint y, uint z)
        {
            cl.SetPipeline(Pipeline);
            cl.SetComputeResourceSet(0, Resources);
            cl.Dispatch(x, y, z);
        }
        public void DispatchSized(CommandList cl, uint x, uint y, uint z)
        {
            Dispatch(cl,
                (x + xGroupSize - 1) / xGroupSize,
                (y + yGroupSize - 1) / yGroupSize,
                (z + zGroupSize - 1) / zGroupSize);
        }

        public class Description
        {
            public Shader Shader;
            List<ResourceLayoutElementDescription> LayoutElements = new List<ResourceLayoutElementDescription>();
            List<BindableResource> ResourceElements = new List<BindableResource>();

            public ResourceLayoutDescription LayoutDescription => new ResourceLayoutDescription(LayoutElements.ToArray());

            public ResourceSetDescription ResourceDescription(ResourceLayout layout)
            {
                return new ResourceSetDescription(layout, ResourceElements.ToArray());
            }

            public Description(Shader s)
            {
                Shader = s;
            }
            public void AddResource(string name, ResourceKind kind, BindableResource resource)
            {
                LayoutElements.Add(new ResourceLayoutElementDescription(name, kind, ShaderStages.Compute));
                ResourceElements.Add(resource);
            }
        }
    }

    class FPS
    {
        Timer secondTimer;
        int frame = 0;
        public FPS()
        {
            secondTimer = new Timer(1000);
            secondTimer.Elapsed += Sec;
            secondTimer.AutoReset = true;
            secondTimer.Enabled = true;
        }
        ~FPS()
        {
            secondTimer.Stop();
            secondTimer.Dispose();
        }
        public void Frame()
        {
            frame++;
        }

        void Sec(Object source, ElapsedEventArgs e)
        {
            Console.Clear();
            if (frame == 0)
                Console.WriteLine("No frames in this second");
            else {
                Console.WriteLine(1000 / frame + " mspf - " + frame + "fps");
                Console.WriteLine(Math.Round(Logic.position.X, 2));
                Console.WriteLine(Math.Round(Logic.position.Y, 2));
                Console.WriteLine(Math.Round(Logic.position.Z, 2));
            }
            frame = 0;
        }
    }
}
