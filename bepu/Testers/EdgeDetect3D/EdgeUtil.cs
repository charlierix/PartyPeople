using Game.Core;
using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers.EdgeDetect3D
{
    public static class EdgeUtil
    {
        public static NormalDot GetNormalDot(TriangleIndexedLinked_wpf.NeighborEdgeSingle edge)
        {
            return new NormalDot()
            {
                Edge_Single = edge,
                Dot = 0,        // 0 thru -1 should be reasonable.  -1 may grab too much priority when compared to pair edges
                Direction = TriangleFoldDirection.Single,
                Token = TokenGenerator.NextToken(),
            };
        }
        public static NormalDot GetNormalDot(TriangleIndexedLinked_wpf.NeighborEdgePair edge)
        {
            double dot = Vector3D.DotProduct(edge.Triangle0.NormalUnit, edge.Triangle1.NormalUnit);

            if (dot.IsNearValue(1))
                return new NormalDot()
                {
                    Edge_Pair = edge,
                    Dot = dot,
                    Direction = TriangleFoldDirection.Parallel,
                    Token = TokenGenerator.NextToken(),
                };

            // Pick a triangle, find the other triangle's non edge vertex
            //  If that other vertex is above the first triangle, then it's a pinch

            // Need to do that for both triangles, because the normals might be backward (one up, one down)

            int other0 = edge.Triangle0.GetOppositeIndex(edge.EdgeIndex0, edge.EdgeIndex1);
            int other1 = edge.Triangle1.GetOppositeIndex(edge.EdgeIndex0, edge.EdgeIndex1);

            bool isAbove0 = Math3D.IsAbovePlane(edge.Triangle0, edge.Triangle0.AllPoints[other1]);
            bool isAbove1 = Math3D.IsAbovePlane(edge.Triangle1, edge.Triangle1.AllPoints[other0]);

            return new NormalDot()
            {
                Edge_Pair = edge,
                Dot = dot,
                Direction =
                    isAbove0 && isAbove1 ? TriangleFoldDirection.Valley :
                    !isAbove0 && !isAbove1 ? TriangleFoldDirection.Peak :
                    TriangleFoldDirection.UpsideDown,
                Token = TokenGenerator.NextToken(),
            };
        }
    }

    #region record: NormalDot

    public record NormalDot
    {
        public TriangleIndexedLinked_wpf.NeighborEdgePair Edge_Pair { get; init; }
        public TriangleIndexedLinked_wpf.NeighborEdgeSingle Edge_Single { get; init; }

        public int EdgeIndex0 => Edge_Pair?.EdgeIndex0 ?? Edge_Single?.EdgeIndex0 ?? throw new InvalidOperationException("Both edges are null");
        public int EdgeIndex1 => Edge_Pair?.EdgeIndex1 ?? Edge_Single?.EdgeIndex1 ?? throw new InvalidOperationException("Both edges are null");

        public Point3D EdgePoint0 => Edge_Pair?.EdgePoint0 ?? Edge_Single?.EdgePoint0 ?? throw new InvalidOperationException("Both edges are null");
        public Point3D EdgePoint1 => Edge_Pair?.EdgePoint1 ?? Edge_Single?.EdgePoint1 ?? throw new InvalidOperationException("Both edges are null");

        public TriangleIndexedLinked_wpf[] Triangles =>
            Edge_Pair != null ? [Edge_Pair.Triangle0, Edge_Pair.Triangle1] :
            Edge_Single != null ? [Edge_Single.Triangle0] :
            throw new InvalidOperationException("Both edges are null");

        public double Dot { get; init; }
        public TriangleFoldDirection Direction { get; init; }
        public long Token { get; init; }
    }

    #endregion

    #region enum: TriangleFoldDirection

    /// <summary>
    /// Used when two triangles are connected at an edge, tells how they are angled (normals pointing up)
    /// </summary>
    public enum TriangleFoldDirection
    {
        Parallel,
        /// <summary>
        /// The triangles face each other
        /// </summary>
        Valley,
        /// <summary>
        /// The triangles face away from each other
        /// </summary>
        Peak,
        /// <summary>
        /// One triangle points up, the other points down.  This would be considered a badly formed mesh
        /// </summary>
        UpsideDown,
        /// <summary>
        /// This is an edge of a triangle with no neighbor on the other side
        /// May want to call this boundary
        /// </summary>
        Single,
    }

    #endregion
}
