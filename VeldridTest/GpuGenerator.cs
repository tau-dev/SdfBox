
using Converter;
using SDFbox;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace Generator
{
    class GpuGenerator
    {
        // TODO: Stronger encapsultion
        static ResourceFactory factory;
        static GraphicsDevice device;
        static CommandList commandList;
        //static DeviceBuffer vertexSBuffer;
        //static DeviceBuffer resultSBuffer;
        //static DeviceBuffer infoUBuffer;
        public static ComputeUnit distanceCompute;
        public static DeviceBuffer distanceStaging;
        public static uint count;
        const float HalfSqrt3 = 0.866025f;

        public static Octree Generate(VertexModel v, GraphicsDevice d, CommandList cl, ResourceFactory f)
        {
            device = d;
            factory = f;
            commandList = cl;
            List<Face> faces = new List<Face>();
            foreach (var face in v.RawData.Groups[0].Faces) {
                for (int i = 0; i < face.Count - 2; i++) {
                    var a = v.RawData.Vertices[face[0].VertexIndex-1];
                    var b = v.RawData.Vertices[face[i+1].VertexIndex-1];
                    var c = v.RawData.Vertices[face[i+2].VertexIndex-1];
                    faces.Add(new Face(a, b, c));
                }
            }
            count = (uint) faces.Count;

            var desc = new ComputeUnit.Description(Program.LoadShader("Distancer", ShaderStages.Compute)) {
                xGroupSize = 256,
                yGroupSize = 1,
                zGroupSize = 1
            };
            DeviceBuffer vertexSBuffer = Program.MakeBuffer(faces.ToArray(), BufferUsage.StructuredBufferReadOnly);
            DeviceBuffer resultSBuffer = Program.MakeEmptyBuffer<float>(BufferUsage.StructuredBufferReadWrite, count);
            distanceStaging = Program.MakeEmptyBuffer<float>(BufferUsage.Staging, count);
            DeviceBuffer infoUBuffer = Program.MakeEmptyBuffer<Vector4>(BufferUsage.UniformBuffer, 1);
            desc.AddResource("SB0", ResourceKind.StructuredBufferReadOnly, vertexSBuffer);
            desc.AddResource("TB0", ResourceKind.StructuredBufferReadWrite, resultSBuffer);
            desc.AddResource("UB0", ResourceKind.UniformBuffer, infoUBuffer);

            distanceCompute = new ComputeUnit(factory, desc);

            var tree = construct(v, 0, Vector3.Zero);

            //vertexSBuffer.Dispose();
            //resultSBuffer.Dispose();
            //infoUBuffer.Dispose();

            return tree;
        }

        public static float DistanceAt(Vector3 pos)
        {
            pos = Transform(pos);
            //unit.UpdateResources(factory, new BindableResource[] { vertexSBuffer, resultSBuffer, infoUBuffer });
            device.UpdateBuffer(distanceCompute.Buffer("UB0"), 0, pos);
            distanceCompute.Update(factory);
            commandList.Begin();
            distanceCompute.DispatchSized(commandList, count, 1, 1);
            commandList.CopyBuffer(distanceCompute.Buffer("TB0"), 0, distanceStaging, 0, count * sizeof(float));
            commandList.End();
            device.SubmitCommands(commandList);
            device.WaitForIdle();

            var results = device.Map<float>(distanceStaging, MapMode.Read);
            float bestDistance = float.PositiveInfinity;
            int flip = 0;

            for (int i = 0; i < count; i++) {
                if (Math.Abs(results[i]) < bestDistance)
                    bestDistance = results[i];
                if (results[i] < 0)
                    flip++;
            }
            device.Unmap(distanceStaging);

            if (flip % 2 == 1)
                bestDistance *= -1;
            return bestDistance / VertexModel.Scale;
        }

        // NOTE: Copied from Model.cs/Model - find bettter solution
        static Octree construct(VertexModel vm, int depth, Vector3 pos)
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
        private static Vector3 Transform(Vector3 p)
        {
            Vector3 t = (p - new Vector3(.5f, .5f, .5f)) * VertexModel.Scale; //(.5f, .625f, .5f)
            t.Y *= -1;
            return t;
        }
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
