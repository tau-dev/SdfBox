
using Converter;
using SDFbox;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace Generator
{
    class GpuGenerator
    {
        static GraphicsDevice graphicsDevice;
        static ResourceFactory factory;
        static DeviceBuffer vertexSBuffer;
        static DeviceBuffer dataSBuffer;
        static DeviceBuffer infoUBuffer;
        static DeviceBuffer readBuffer;
        static Shader computeShader;
        static Pipeline computePipeline;
        static ResourceLayout computeResourceLayout;
        static ResourceSet computeResources;
        static CommandList commandList;


        public static Model Generate(VertexModel v, GraphicsDevice device, ResourceFactory factory)
        {
            graphicsDevice = device;
            GpuGenerator.factory = factory;
            var Vertices = v.Raw.VertexList;
            Vector4[] data = new Vector4[Vertices.Count];
            for (int i = 0; i < data.Length; i++) {
                data[i] = new Vector4((float) Vertices[i].X, (float) Vertices[i].Y, (float) Vertices[i].Z, 0);
            }
            ComputePrepare(data);

            Octree o = new Octree(new float[] { 2, 2, 2, 2, 2, 2, 2, 2 });

            Queue<OctPrimitive> tasks = FirstEight();

            while (tasks.Count > 0) {
                Console.WriteLine("Scanning " + tasks.Count + " elements");
                MappedResourceView<OctPrimitive> MappedView = ComputeLayer(tasks.ToArray());
                tasks = new Queue<OctPrimitive>();
                Queue<OctPrimitive> results = new Queue<OctPrimitive>();
                for (int i = 0; i < MappedView.Count; i++) {
                    results.Enqueue(MappedView[i]);
                    TrySubdivide(MappedView[i], tasks);
                }
                graphicsDevice.Unmap(readBuffer);
                RecursiveMount(o, results);
            }

            computeShader.Dispose();
            vertexSBuffer.Dispose();
            dataSBuffer.Dispose();
            infoUBuffer.Dispose();
            return new Model(o);
        }

        static void RecursiveMount(Octree o, Queue<OctPrimitive> results)
        {
            if (o.Children == null) {
                Octree[] childs = new Octree[8];
                for (int i = 0; i < 8; i++) {
                    OctPrimitive child = results.Dequeue();
                    childs[i] = new Octree(child.verts.Array);
                }
                o.Children = childs;
            } else {
                foreach (var child in o.Children) {
                    RecursiveMount(child, results);
                }
            }
        }

        static void ComputePrepare(Vector4[] vertices)
        {
            vertexSBuffer = Program.MakeBuffer(vertices, BufferUsage.StructuredBufferReadOnly);
            computeResourceLayout = Program.factory.CreateResourceLayout(new ResourceLayoutDescription(new ResourceLayoutElementDescription[] {
                new ResourceLayoutElementDescription("SB0", ResourceKind.StructuredBufferReadOnly, ShaderStages.Compute),
                new ResourceLayoutElementDescription("SB1", ResourceKind.StructuredBufferReadWrite, ShaderStages.Compute),
                new ResourceLayoutElementDescription("UB0", ResourceKind.UniformBuffer, ShaderStages.Compute)
            }));

            computeShader = Program.LoadShader("DistFind", ShaderStages.Compute);
            ComputePipelineDescription pipelineDescription = new ComputePipelineDescription {
                ResourceLayouts = new ResourceLayout[] { computeResourceLayout },
                ComputeShader = computeShader,
                ThreadGroupSizeX = 1,
                ThreadGroupSizeY = 1,
                ThreadGroupSizeZ = 1
            };
            computePipeline = factory.CreateComputePipeline(pipelineDescription);
            commandList = factory.CreateCommandList();
        }

        static MappedResourceView<OctPrimitive> ComputeLayer(OctPrimitive[] data)
        {

            infoUBuffer = Program.MakeBuffer(Info.MakeInfo(data.Length), BufferUsage.UniformBuffer);
            dataSBuffer = Program.MakeBuffer(data, BufferUsage.StructuredBufferReadWrite);
            readBuffer = factory.CreateBuffer(new BufferDescription(dataSBuffer.SizeInBytes, BufferUsage.Staging));

            computeResources = factory.CreateResourceSet(new ResourceSetDescription(computeResourceLayout, vertexSBuffer, dataSBuffer, infoUBuffer));
            commandList.Begin();
            commandList.SetPipeline(computePipeline);
            commandList.SetComputeResourceSet(0, computeResources);
            commandList.Dispatch((uint) data.Length / 8, 1, 1);
            commandList.CopyBuffer(dataSBuffer, 0, readBuffer, 0, dataSBuffer.SizeInBytes);
            commandList.End();
            graphicsDevice.SubmitCommands(commandList);
            graphicsDevice.WaitForIdle();

            return graphicsDevice.Map<OctPrimitive>(readBuffer, MapMode.Read);
        }

        static void TrySubdivide(OctPrimitive o, Queue<OctPrimitive> tasks)
        {
            if (o.to_be_split == 1) {
                float nextScale = o.scale / 2;
                for (int i = 0; i < 8; i++) {
                    tasks.Enqueue(new OctPrimitive() {
                        scale = nextScale,
                        lowerCorner = o.lowerCorner + SdfMath.split(i).Vector * nextScale,
                        to_be_split = 0,
                    });

                }
            }
        }

        static Queue<OctPrimitive> FirstEight()
        {
            var d = new Queue<OctPrimitive>();
            for (int i = 0; i < 8; i++) {
                d.Enqueue(new OctPrimitive(SdfMath.split(i).Vector * .5f, .5f));
            }
            return d;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    struct OctPrimitive
    {
        public float scale;
        public Vector3 lowerCorner;
        public Vector8 verts;
        public int to_be_split;
        public int next_element;

        public OctPrimitive(Vector3 lowerCorner, float scale)
        {
            this.scale = scale;
            this.lowerCorner = lowerCorner;
            verts = new Vector8();
            to_be_split = 0;
            next_element = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16, Size = 16)]
    struct Info
    {
        int vertex_amount;
        float precision;
        float min_scale;
        float pad;

        Info(int vM, float p, float sc)
        {
            vertex_amount = vM;
            precision = p;
            min_scale = sc;
            pad = 0;
        }

        public static Info[] MakeInfo(int vertex_amount)
        {
            return new Info[] { new Info(vertex_amount, 0.01f, 0.02f) };
        }
    }
}
