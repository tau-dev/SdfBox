
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Converter;
using SDFbox;
using Veldrid;

namespace Generator
{
    class ComputedModel : VertexModel
    {
        // TODO: Stronger encapsultion
        ResourceFactory factory;
        GraphicsDevice device;
        CommandList commandList;
        //static DeviceBuffer vertexSBuffer;
        //static DeviceBuffer resultSBuffer;
        //static DeviceBuffer infoUBuffer;
        public ComputeUnit distanceCompute;
        public DeviceBuffer distanceStaging;
        public ComputeUnit orientationCompute;
        public DeviceBuffer orientationStaging;
        public uint count;
        const float HalfSqrt3 = 0.866025f;

        public ComputedModel(string file, GraphicsDevice d, CommandList cl, ResourceFactory f) : base(file)
        {
            device = d;
            factory = f;
            commandList = cl;
            List<Face> faces = new List<Face>();
            foreach (var face in RawData.Groups[0].Faces) {
                for (int i = 0; i < face.Count - 2; i++) {
                    var a = RawData.Vertices[face[0].VertexIndex-1];
                    var b = RawData.Vertices[face[i+1].VertexIndex-1];
                    var c = RawData.Vertices[face[i+2].VertexIndex-1];
                    faces.Add(new Face(a, b, c));
                }
            }
            count = (uint) RawData.Groups[0].Faces.Count;

            DeviceBuffer vertexSBuffer = Program.MakeBuffer(faces.ToArray(), BufferUsage.StructuredBufferReadOnly);
            DeviceBuffer infoUBuffer = Program.MakeEmptyBuffer<Vector4>(BufferUsage.UniformBuffer, 1);

            var desc = new ComputeUnit.Description(Program.LoadShader("Distancer", ShaderStages.Compute)) {
                xGroupSize = 256,
                yGroupSize = 1,
                zGroupSize = 1
            };
            distanceStaging = Program.MakeEmptyBuffer<float>(BufferUsage.Staging, count);
            DeviceBuffer resultSBuffer = Program.MakeEmptyBuffer<float>(BufferUsage.StructuredBufferReadWrite, count);
            desc.AddResource("vertices", ResourceKind.StructuredBufferReadOnly, vertexSBuffer);
            desc.AddResource("result", ResourceKind.StructuredBufferReadWrite, resultSBuffer);
            desc.AddResource("info", ResourceKind.UniformBuffer, infoUBuffer);
            distanceCompute = new ComputeUnit(factory, desc);

            desc = new ComputeUnit.Description(Program.LoadShader("InsideFinder", ShaderStages.Compute)) {
                xGroupSize = 256,
                yGroupSize = 1,
                zGroupSize = 1
            };
            orientationStaging = Program.MakeEmptyBuffer<bool>(BufferUsage.Staging, count);
            DeviceBuffer orientationSBuffer = Program.MakeEmptyBuffer<int>(BufferUsage.StructuredBufferReadWrite, count);
            desc.AddResource("vertices", ResourceKind.StructuredBufferReadOnly, vertexSBuffer);
            desc.AddResource("result", ResourceKind.StructuredBufferReadWrite, orientationSBuffer);
            desc.AddResource("info", ResourceKind.UniformBuffer, infoUBuffer);
            orientationCompute = new ComputeUnit(factory, desc);
        }

        /*
        public override float DistanceAt(Vector3 pos, List<int> possible)
        {
            pos = Transform(pos);
            //unit.UpdateResources(factory, new BindableResource[] { vertexSBuffer, resultSBuffer, infoUBuffer });
            device.UpdateBuffer(distanceCompute.Buffer("info"), 0, pos);
            distanceCompute.Update(factory);
            commandList.Begin();
            distanceCompute.DispatchSized(commandList, count, 1, 1);
            commandList.CopyBuffer(distanceCompute.Buffer("result"), 0, distanceStaging, 0, count * sizeof(float));
            commandList.End();
            device.SubmitCommands(commandList);
            device.WaitForIdle();

            var results = device.Map<float>(distanceStaging, MapMode.Read);
            float bestDistance = float.PositiveInfinity;
            int flip = 0;

            for (int i = 0; i < count; i++) {
                bestDistance = Math.Min(bestDistance, Math.Abs(results[i]));
                if (results[i] < 0)
                    flip++;
            }
            device.Unmap(distanceStaging);

            if (flip % 2 == 1)
                bestDistance *= -1;
            return bestDistance / Scale;
        }
        private float DistanceAt(Vector3 pos)
        {
            return DistanceAt(pos, new List<int>());
        }
        //*/
        public override bool Inside(Vector3 pos, int closest)
        {
            device.UpdateBuffer(orientationCompute.Buffer("info"), 0, pos);
            orientationCompute.Update(factory);
            commandList.Begin();
            orientationCompute.DispatchSized(commandList, count, 1, 1);
            commandList.CopyBuffer(orientationCompute.Buffer("result"), 0, orientationStaging, 0, count * sizeof(float));
            commandList.End();
            device.SubmitCommands(commandList);
            device.WaitForIdle();

            var results = device.Map<int>(orientationStaging, MapMode.Read);
            int flip = 0;
            for (int i = 0; i < count; i++) {
                if (results[i] != 0)
                    flip++;
            }
            device.Unmap(orientationStaging);
            return flip % 2 == 1;
        }
        /*
        public override List<int> GetPossible(Vector3 pos, float scale, List<int> possible)
        {
            return new List<int>();
        }

        // NOTE: Copied from Model.cs/Model - find bettter solution
        Octree construct(VertexModel vm, int depth, Vector3 pos)
        {
            float scale = (float) Math.Pow(0.5, depth);
            float[] verts = new float[8];
            Vector3 center = pos + Vector3.One * 0.5f * scale;
            float centerValue = DistanceAt(center);

            for (int i = 0; i < 8; i++) {
                verts[i] = DistanceAt(pos + SdfMath.split(i).Vector * scale);
            }
            Octree build = new Octree(verts);


            if (Math.Abs(centerValue) < scale * 2 && depth < Model.MaxDepth) { // error(centerValue, build.Vertices, pos) > precision
                build.Children = new Octree[8];
                for (int i = 0; i < 8; i++) {
                    build.Children[i] = construct(vm, depth + 1, pos + SdfMath.split(i).Vector * scale / 2);
                }
            }

            return build;
        }
        //*/
    }

    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    struct Face
    {
        Vector4 a;
        Vector4 b;
        Vector4 c;
        public Face(ObjLoader.Loader.Data.VertexData.Vertex a,
            ObjLoader.Loader.Data.VertexData.Vertex b,
            ObjLoader.Loader.Data.VertexData.Vertex c)
        {
            this.a = new Vector4(a.X, a.Y, a.Z, 0);
            this.b = new Vector4(b.X, b.Y, b.Z, 0);
            this.c = new Vector4(c.X, c.Y, c.Z, 0);
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
