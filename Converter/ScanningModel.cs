using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Converter
{
    class ScanningModel : VertexModel
    {
        Cell[][] Subcells;
        List<Face> AllFaces;
        float Cellsize;
        public ScanningModel(string filename, int resolution) : base(filename)
        {
            Cellsize = Scale / resolution;
            Subcells = new Cell[resolution][];

            AllFaces = new List<Face>();
            foreach (var face in RawData.Groups[0].Faces) {
                for (int i = 0; i < face.Count - 2; i++) {
                    var a = RawData.Vertices[face[0].VertexIndex-1];
                    var b = RawData.Vertices[face[i+1].VertexIndex-1];
                    var c = RawData.Vertices[face[i+2].VertexIndex-1];
                    AllFaces.Add(new Face(a, b, c));
                }
            }
            for (int i = 0; i < resolution; i++) {
                Subcells[i] = new Cell[resolution];
                for (int j = 0; j < resolution; j++) {
                    Subcells[i][j] = MakeCell(new Vector2(i, j) * Cellsize);
                }
            }
        }

        Cell MakeCell(Vector2 position)
        {
            var l = new List<Face>();
            foreach (var face in AllFaces) {
                if (face.IntersectsAABB(position, Cellsize))
                    l.Add(face);
            }
            return new Cell() { Faces = l };
        }
        public override bool Inside(Vector3 pos, int closest)
        {
            //return flip % 2 == 1;
            throw new NotImplementedException();
        }

        class Cell
        {
            public List<Face> Faces;
        }
    }
    struct Face
    {
        Vector2 a;
        Vector2 b;
        Vector2 c;
        public Face(ObjLoader.Loader.Data.VertexData.Vertex a,
            ObjLoader.Loader.Data.VertexData.Vertex b,
            ObjLoader.Loader.Data.VertexData.Vertex c)
        {
            this.a = new Vector2(a.X, a.Y);
            this.b = new Vector2(b.X, b.Y);
            this.c = new Vector2(c.X, c.Y);
        }

        public bool IntersectsAABB(Vector2 pos, float size)
        {
            throw new NotImplementedException();
        }
    }
}
