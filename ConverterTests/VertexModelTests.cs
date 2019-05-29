using Microsoft.VisualStudio.TestTools.UnitTesting;
using Converter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ObjLoader.Loader.Data.VertexData;
using System.Numerics;
using System.Diagnostics;

namespace Converter.Tests
{
    [TestClass()]
    public class Tests
    {
        [TestMethod]
        public void TrifyTest()
        {
            var d = new DetailVertex(new Vector3(0, 0, 0));
            d.Add(new Vertex(1, 1, 1), new Vertex(5, 1, 1), new Vertex(1, 5, 1));
            d.Add(new Vertex(3, 3, 0), new Vertex(0, 3, 3), new Vertex(3, 0, 3));

            Console.WriteLine(d.Faces[0]);
            TestPoint(new Vector3(3, 3, 0), d.Faces[0]);
            TestPoint(new Vector3(0, 0, 0), d.Faces[0]);
            TestPoint(new Vector3(0, 0, 0), d.Faces[1]);
        }
        void TestPoint(Vector3 p, Matrix4x4 f)
        {
            Console.WriteLine(Vector3.Transform(p, f));
            Console.WriteLine(DetailVertex.FaceDist(p, f));
        }
    }
}