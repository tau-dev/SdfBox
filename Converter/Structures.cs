
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Converter
{
    class KdTree
    {
        KdTree Lower, Higher;
        VertexN Value;
        readonly int axis;

        public KdTree(List<VertexN> pointList, int depth = 0)
        {
            axis = depth % 3;

            pointList.Sort((a, b) => Component(a.Position).CompareTo(Component(b.Position)));
            int pivot = pointList.Count / 2;
            Value = pointList.ElementAt(pivot);

            var lowerHalf = pointList.GetRange(0, pivot);
            var higherHalf = pointList.GetRange(pivot + 1, pointList.Count - pivot - 1);
            if (lowerHalf.Count > 0)
                Lower = new KdTree(lowerHalf, depth + 1);
            if (higherHalf.Count > 0)
                Higher = new KdTree(higherHalf, depth + 1);
        }

        public KdTree NodeFor(VertexN vert)
        {
            if (vert.Equals(Value))
                return this;

            if (Component(vert.Position) < Component(Value.Position)) {
                if (Lower != null)
                    return Lower.NodeFor(vert);
                else
                    return this;
            } else {
                if (Higher != null)
                    return Higher.NodeFor(vert);
                else
                    return this;
            }
        }
        public VertexN NearestNeighbor(Vector3 pos, float MinDistance = Single.PositiveInfinity)
        {
            bool below = Component(pos) < Component(Value.Position);
            KdTree containing, rest;

            if (below) {
                containing = Lower;
                rest = Higher;
            } else {
                containing = Higher;
                rest = Lower;
            }

            VertexN closest = (containing == null) ?
                Value :
                containing.NearestNeighbor(pos, MinDistance);
            if (Distance(pos, closest) < MinDistance)
                MinDistance = Distance(pos, closest);

            float sideDist = Component(pos - Value.Position) * Component(pos - Value.Position);

            if (sideDist < MinDistance) {
                if (Distance(pos, Value) < MinDistance) {
                    closest = Value;
                    MinDistance = Distance(pos, closest);
                }
                if (rest != null) {
                    VertexN otherNearest = rest.NearestNeighbor(pos, MinDistance);
                    if (Distance(pos, otherNearest) < MinDistance)
                        closest = otherNearest;
                }
            }
            return closest;
        }
        float Distance(Vector3 p, VertexN other)
        {
            return Vector3.DistanceSquared(p, other.Position);
        }

        float Component(Vector3 v)
        {
            switch (axis) {
                case 0:
                default:
                    return v.X;
                case 1:
                    return v.Y;
                case 2:
                    return v.Z;
            }
        }
    }
    class VertexN
    {
        public Vector3 Position;
        public Vector3 Normal;

        public VertexN(Vector3 pos, Vector3 normal)
        {
            Position = pos;
            Normal = normal;
        }
    }

    public struct NVertex
    {
    }
}
