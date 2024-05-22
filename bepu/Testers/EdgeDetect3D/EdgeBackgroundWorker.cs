using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF.FileHandlers3D;
using NetOctree.Octree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Windows.Media.Media3D;
using static Game.Math_WPF.Mathematics.TriangleIndexedLinked_wpf;

namespace Game.Bepu.Testers.EdgeDetect3D
{
    /// <summary>
    /// After a file is loaded, this gets called to do some post process analysis
    /// </summary>
    /// <remarks>
    /// Links triangles by edge, takes dot products of each edge's triangle's normals
    /// </remarks>
    public class EdgeBackgroundWorker
    {
        #region record: WorkerRequest

        public record WorkerRequest
        {
            public string Filename { get; init; }
            public Obj_File ParsedFile { get; init; }
        }

        #endregion
        #region record: WorkerResponse

        public record WorkerResponse
        {
            public WorkerResponse_Object[] Objects { get; init; }

            public Point3D AABB_Min { get; init; }
            public Point3D AABB_Max { get; init; }
            public double AABB_DiagLen { get; init; }

            public double Average_Segment_Length { get; init; }
        }

        #endregion
        #region record: WorkerResponse_Object

        public record WorkerResponse_Object
        {
            public Obj_Object Obj { get; init; }

            public TriangleIndexedLinked_wpf[] Triangles { get; init; }

            public Point3D[] AllPoints { get; init; }

            public Point3D AABB_Min { get; init; }
            public Point3D AABB_Max { get; init; }
            public double AABB_DiagLen { get; init; }

            public NeighborEdgeSingle[] EdgeSingles { get; init; }
            public NeighborEdgePair[] EdgePairs { get; init; }

            public BoundsOctree<TriangleIndexedLinked_wpf> Tree_Triangles { get; init; }
            public BoundsOctree<NormalDot> Tree_Edges { get; init; }
        }

        #endregion

        const float TREE_LOOSENESS = 1.25f;

        public static WorkerResponse DoWork(WorkerRequest args, CancellationToken cancel)
        {
            var objects = new List<WorkerResponse_Object>();

            var edge_lengths = new List<double>();

            foreach (var obj in args.ParsedFile.Objects)
            {
                var triangles_fromobj = Obj_Util.ToTrianglesIndexed(obj);

                if (triangles_fromobj.Length == 0)
                    continue;

                var (triangles, by_edge) = TriangleIndexedLinked_wpf.ConvertToLinked(triangles_fromobj, true, false);

                // May also want to throw out tiny triangles

                var edge_dots = by_edge.EdgePairs.
                    Select(o => EdgeUtil.GetNormalDot(o)).
                    Where(o => o.Dot < 0.97).       // throw out the mostly parallel joins
                    ToArray();

                var bounds = GetTreeBounds(by_edge.AllPoints);

                // octree of triangles
                var tree_triangles = CreateOctree_Triangles(triangles, bounds.world_size, bounds.center, bounds.min_size);

                // octree of dot_diffs
                var tree_edgedots = CreateOctree_EdgeDots(edge_dots, bounds.world_size, bounds.center, bounds.min_size);

                edge_lengths.AddRange(collection: GetEdgeLengths(by_edge));

                objects.Add(new WorkerResponse_Object()
                {
                    Obj = obj,
                    Triangles = triangles,
                    AllPoints = by_edge.AllPoints,

                    AABB_Min = bounds.aabb_min,
                    AABB_Max = bounds.aabb_max,
                    AABB_DiagLen = bounds.aabb_diaglen,

                    EdgeSingles = by_edge.EdgeSingles,
                    EdgePairs = by_edge.EdgePairs,
                    Tree_Triangles = tree_triangles,
                    Tree_Edges = tree_edgedots,
                });
            }

            if (objects.Count == 0)
                return new WorkerResponse()
                {
                    Objects = [],
                    AABB_Min = new Point3D(),
                    AABB_Max = new Point3D(),
                    AABB_DiagLen = 0,
                };

            var aabb = Math3D.GetAABB(objects.SelectMany(o => new Point3D[] { o.AABB_Min, o.AABB_Max }));       // compiler can't infer []

            return new WorkerResponse()
            {
                Objects = objects.ToArray(),

                AABB_Min = aabb.min,
                AABB_Max = aabb.max,
                AABB_DiagLen = (aabb.max - aabb.min).Length,
                Average_Segment_Length = Math1D.Avg(edge_lengths.ToArray()),
            };
        }

        private static (float world_size, Vector3 center, float min_size, Point3D aabb_min, Point3D aabb_max, double aabb_diaglen) GetTreeBounds(Point3D[] allPoints, int size_divider = 150)
        {
            var aabb = Math3D.GetAABB(allPoints);

            //Point3D center = Math3D.GetCenter(triangles[0].AllPoints);        // can't use this way because it's weighted
            Point3D center = aabb.min + ((aabb.max - aabb.min) / 2);

            double aabb_diaglen = (aabb.max - aabb.min).Length;

            //TODO: may want max of width,height,depth
            double diag_len = aabb_diaglen;

            double min_size = diag_len / size_divider;

            return ((float)diag_len, center.ToVector3(), (float)min_size, aabb.min, aabb.max, aabb_diaglen);
        }

        // Populates an octree with triangles.  The larger the divider, the smaller the node sizes can be
        // TODO: don't ask for divider size.  Calculate based on triangle count
        private static BoundsOctree<T> CreateOctree_Triangles<T>(T[] triangles, float world_size, Vector3 center, float min_size) where T : ITriangleIndexed_wpf
        {
            var tree = new BoundsOctree<T>(world_size, center, min_size, TREE_LOOSENESS);

            foreach (var triangle in triangles)
            {
                var aabb = Math3D.GetAABB(triangle);
                Vector3 size = new Vector3((float)(aabb.max.X - aabb.min.X), (float)(aabb.max.Y - aabb.min.Y), (float)(aabb.max.Z - aabb.min.Z));

                tree.Add(triangle, new BoundingBox(triangle.GetCenterPoint().ToVector3(), size));
            }

            return tree;
        }

        private static BoundsOctree<NormalDot> CreateOctree_EdgeDots(NormalDot[] edge_dots, float world_size, Vector3 center, float min_size)
        {
            var tree = new BoundsOctree<NormalDot>(world_size, center, min_size, TREE_LOOSENESS);

            foreach (var edge in edge_dots)
            {
                var aabb = Math3D.GetAABB([edge.Edge.Triangle0, edge.Edge.Triangle1]);
                Vector3 size = new Vector3((float)(aabb.max.X - aabb.min.X), (float)(aabb.max.Y - aabb.min.Y), (float)(aabb.max.Z - aabb.min.Z));

                Vector3 edge_center = new Vector3((float)aabb.min.X + size.X / 2, (float)aabb.min.Y + size.Y / 2, (float)aabb.min.Z + size.Z / 2);

                tree.Add(edge, new BoundingBox(edge_center, size));
            }

            return tree;
        }

        private static IEnumerable<double> GetEdgeLengths(NeighborResults edges)
        {
            var singles = edges.EdgeSingles.
                Select(o => (o.EdgePoint0, o.EdgePoint1));

            var pairs = edges.EdgePairs.
                Select(o => (o.EdgePoint0, o.EdgePoint1));

            return singles.
                Concat(pairs).
                Select(o => (o.Item2 - o.Item1).Length);
        }
    }
}
