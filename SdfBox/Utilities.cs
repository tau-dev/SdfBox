
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
            Sdl2Window window = VeldridStartup.CreateWindow(
                new WindowCreateInfo(512, 512, width, height, WindowState.Minimized, "SDFbox"));
            //window.CursorVisible = false;
            return window;
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
            new VertexElementDescription("Position", VertexElementSemantic.Position, VertexElementFormat.Float3),
            new VertexElementDescription("Color", VertexElementSemantic.Color, VertexElementFormat.Float4)));
        }
    }

    abstract internal class RenderUnit : IDisposable
    {
        protected readonly ResourceLayout Layout;
        protected ResourceSet ResourceSet;
        protected BindableResource[] Resources;
        protected Pipeline Pipeline;
        protected Dictionary<string, int> ResourceNames;

        public RenderUnit(ResourceFactory factory, Description d)
        {
            Layout = factory.CreateResourceLayout(d.LayoutDescription);
            Resources = d.ResourceElements.ToArray();
            ResourceNames = d.ResourceNames;
            Update(factory);
        }
        public BindableResource this[string s] {
            get {
                int position;
                if (!ResourceNames.TryGetValue(s, out position))
                    throw new ArgumentException("No resource named " + s);
                return Resources[position];
            }
            set {
                int position;
                if (!ResourceNames.TryGetValue(s, out position))
                    throw new ArgumentException("No resource named " + s);
                Resources[position] = value;
            }
        }
        public DeviceBuffer Buffer(string s)
        {
            return (DeviceBuffer) Resources[ResourceNames[s]];
        }
        public void UpdateResources(ResourceFactory f, BindableResource[] newResources)
        {
            ResourceSet = f.CreateResourceSet(new ResourceSetDescription(Layout, newResources));
        }
        public void Update(ResourceFactory f)
        {
            ResourceSet = f.CreateResourceSet(new ResourceSetDescription(Layout, Resources));
        }

        public virtual void Dispose()
        {
            Layout.Dispose();
            ResourceSet.Dispose();
            Pipeline.Dispose();
        }

        public class Description
        {
            List<ResourceLayoutElementDescription> LayoutElements = new List<ResourceLayoutElementDescription>();
            public List<BindableResource> ResourceElements = new List<BindableResource>();
            public Dictionary<string, int> ResourceNames = new Dictionary<string, int>();
            public ResourceLayoutDescription LayoutDescription => new ResourceLayoutDescription(LayoutElements.ToArray());

            protected void AddResource(string name, ResourceKind kind, BindableResource resource, ShaderStages stage)
            {
                LayoutElements.Add(new ResourceLayoutElementDescription(name, kind, stage));
                ResourceElements.Add(resource);
                ResourceNames[name] = ResourceElements.Count - 1;
            }
        }
    }

    class ComputeUnit : RenderUnit
    {
        public Shader Shader { get; set; }
        public uint xGroupSize { get; }
        public uint yGroupSize { get; }
        public uint zGroupSize { get; }

        public ComputeUnit(ResourceFactory factory, Description d) : base(factory, d)
        {
            Shader = d.Shader;
            xGroupSize = d.xGroupSize;
            yGroupSize = d.yGroupSize;
            zGroupSize = d.zGroupSize;

            ComputePipelineDescription pipelineDescription = 
                new ComputePipelineDescription(Shader, Layout, xGroupSize, yGroupSize, zGroupSize);

            Pipeline = factory.CreateComputePipeline(pipelineDescription);
        }
        public void Dispatch(CommandList cl, uint x, uint y, uint z)
        {
            cl.SetPipeline(Pipeline);
            cl.SetComputeResourceSet(0, ResourceSet);
            cl.Dispatch(x, y, z);
        }
        public void DispatchSized(CommandList cl, uint x, uint y, uint z)
        {

            Dispatch(cl,
                (x + xGroupSize - 1) / xGroupSize,
                (y + yGroupSize - 1) / yGroupSize,
                (z + zGroupSize - 1) / zGroupSize);
        }

        public new void Dispose()
        {
            Shader.Dispose();
            base.Dispose();
        }

        public new class Description : RenderUnit.Description
        {
            public Shader Shader;
            public uint xGroupSize = 1;
            public uint yGroupSize = 1;
            public uint zGroupSize = 1;

            public Description(Shader s)
            {
                Shader = s;
            }

            public void AddResource(string name, ResourceKind kind, BindableResource resource)
            {
                AddResource(name, kind, resource, ShaderStages.Compute);
            }
        }
    }

    class VertFragUnit : RenderUnit
    {
        Shader Vertex;
        Shader Fragment;
        public DeviceBuffer VertexBuffer { get; private set; }
        public DeviceBuffer IndexBuffer { get; private set; }

        public VertFragUnit(ResourceFactory factory, Description d) : base(factory, d)
        {
            Vertex = d.Vertex;
            Fragment = d.Fragment;
            VertexBuffer = d.VertexBuffer;
            IndexBuffer = d.IndexBuffer;

            GraphicsPipelineDescription pipelineDescription = new GraphicsPipelineDescription {
                BlendState = d.BlendState,
                PrimitiveTopology = d.Topology,
                ResourceLayouts = new ResourceLayout[] { Layout },
                ShaderSet = Utilities.MakeShaderSet(Vertex, Fragment),
                Outputs = d.Output,
                DepthStencilState = d.DepthStencil,
                RasterizerState = d.RasterizerState
            };

            Pipeline = factory.CreateGraphicsPipeline(pipelineDescription);
        }
        public void Draw(CommandList cl, uint indexCount, uint instanceCount, int vertexOffset = 0)
        {
            cl.SetPipeline(Pipeline);
            cl.SetGraphicsResourceSet(0, ResourceSet);
            cl.SetVertexBuffer(0, VertexBuffer);
            cl.SetIndexBuffer(IndexBuffer, IndexFormat.UInt16);
            cl.DrawIndexed(
                indexStart: 0,
                indexCount: indexCount,
                instanceStart: 0,
                instanceCount: instanceCount,
                vertexOffset: vertexOffset);
        }

        public new void Dispose()
        {
            Vertex.Dispose();
            Fragment.Dispose();
            VertexBuffer.Dispose();
            IndexBuffer.Dispose();
            base.Dispose();
        }

        public new class Description : RenderUnit.Description
        {
            public Shader Vertex;
            public Shader Fragment;
            public DeviceBuffer VertexBuffer;
            public DeviceBuffer IndexBuffer;
            public OutputDescription Output;
            public PrimitiveTopology Topology;
            public BlendStateDescription BlendState = BlendStateDescription.SingleOverrideBlend;
            public DepthStencilStateDescription DepthStencil = new DepthStencilStateDescription(
                depthTestEnabled: true,
                depthWriteEnabled: true,
                comparisonKind: ComparisonKind.LessEqual);
            public RasterizerStateDescription RasterizerState = new RasterizerStateDescription(
                cullMode: FaceCullMode.Back,
                fillMode: PolygonFillMode.Solid,
                frontFace: FrontFace.Clockwise,
                depthClipEnabled: true,
                scissorTestEnabled: false);
            public new void AddResource(string name, ResourceKind kind, BindableResource resource, ShaderStages stage = ShaderStages.Fragment)
            {
                base.AddResource(name, kind, resource, stage);
            }
        }
    }

    class FPS
    {
        Timer secondTimer;
        int frame = 0;
        int lastframe = 0;
        public int Frames {
            get {
                return lastframe;
            }
        }
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
            lastframe = frame;
            frame = 0;
        }
    }
}
