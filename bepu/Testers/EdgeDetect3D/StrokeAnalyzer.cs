using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF.Viewers;
using NetOctree.Octree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers.EdgeDetect3D
{
    public static class StrokeAnalyzer
    {
        const bool SHOULD_DRAW = true;

        public static void Stroke(Point3D[] points, EdgeBackgroundWorker.WorkerResponse objects)
        {
            double? avg_segment_len = GetAverageSegmentLength(points, objects);

            points = avg_segment_len == null ?
                StrokeCleaner.CleanPath(points, objects.Average_Segment_Length * 0.25) :        // there are no triangles in the path's box.  Use the global average triangle size
                StrokeCleaner.CleanPath(points, avg_segment_len.Value * 0.25);

            double search_radius = GetSearchRadius(points);


            // This is just a visual for debugging
            var triangles = GetNearbyTriangles(points, objects.Objects, search_radius);
            Draw_NearbyTriangles(points, triangles);




            StrokeEdgeLinker.GetBestEdges(points, objects.Objects, search_radius);


        }

        /// <summary>
        /// Returns the average length of edges within the points aabb
        /// </summary>
        /// <remarks>
        /// objects.Average_Segment_Length only works if all the triangles are roughly the same size.  If there are high density
        /// patches, that would make the path return too course of a path, and the triangle search will return way too many triangles
        /// 
        /// so instead of getting average for the entire object, this gets average of the current volume
        /// </remarks>
        private static double GetAverageSegmentLength_ATTEMPT1(Point3D[] points, EdgeBackgroundWorker.WorkerResponse objects)
        {
            var aabb = Math3D.GetAABB(points);

            NormalDot[] edges = GetEdgesInBox(aabb.min, aabb.max, objects);

            double[] lengths = edges.
                Select(o => (o.EdgePoint1 - o.EdgePoint0).Length).
                ToArray();

            return Math1D.Avg(lengths);
        }
        private static NormalDot[] GetEdgesInBox(Point3D min, Point3D max, EdgeBackgroundWorker.WorkerResponse objects)
        {
            Vector3 center = Math3D.GetCenter(min, max).ToVector3();
            Vector3 size = (max - min).ToVector3();

            BoundingBox box = new BoundingBox(center, size);

            var retVal = new List<NormalDot>();

            foreach (var obj in objects.Objects)
                retVal.AddRange(obj.Tree_Edges.GetColliding(box));

            return retVal.ToArray();
        }

        private static double? GetAverageSegmentLength(Point3D[] points, EdgeBackgroundWorker.WorkerResponse objects)
        {
            var aabb = Math3D.GetAABB(points);

            var triangles_per_obj = GetTrianglesInBox(aabb.min, aabb.max, objects);     // NOTE: this is a jagged array, because each object probably has different AllPoints
            if (triangles_per_obj.Length == 0 || triangles_per_obj.All(o => o.Length == 0))
                return null;

            var edges_len = triangles_per_obj.
                Select(o =>
                {
                    var lines = TriangleIndexed_wpf.GetUniqueLines(o);
                    return lines.
                        Select(p => (o[0].AllPoints[p.Item1], o[0].AllPoints[p.Item2])).
                        ToArray();
                }).
                SelectMany(o => o).
                //Select(o => (o.Item2 - o.Item1).LengthSquared).       // this doesn't work
                Select(o => (o.Item2 - o.Item1).Length).
                ToArray();

            return edges_len.Sum() / edges_len.Length;
        }

        private static TriangleIndexedLinked_wpf[][] GetTrianglesInBox(Point3D min, Point3D max, EdgeBackgroundWorker.WorkerResponse objects)
        {
            Vector3 center = Math3D.GetCenter(min, max).ToVector3();
            Vector3 size = (max - min).ToVector3();

            BoundingBox box = new BoundingBox(center, size);

            var retVal = new List<TriangleIndexedLinked_wpf[]>();

            foreach (var obj in objects.Objects)
                retVal.Add(obj.Tree_Triangles.GetColliding(box));

            return retVal.ToArray();
        }

        private static double GetSearchRadius(Point3D[] points)
        {
            double[] lengths = new double[points.Length - 1];

            for (int i = 0; i < points.Length - 1; i++)
                lengths[i] = (points[i + 1] - points[i]).Length;

            double avg = Math1D.Avg(lengths);

            return avg * 4;
        }

        private static TriangleIndexedLinked_wpf[] GetNearbyTriangles(Point3D[] points, EdgeBackgroundWorker.WorkerResponse_Object[] objects, double search_radius)
        {
            var retVal = new List<TriangleIndexedLinked_wpf>();

            foreach (var obj in objects)
                retVal.AddRange(GetNearbyTriangles_Obj(points, obj.Tree_Triangles, search_radius));

            return retVal.ToArray();
        }
        private static TriangleIndexedLinked_wpf[] GetNearbyTriangles_Obj(Point3D[] points, BoundsOctree<TriangleIndexedLinked_wpf> tree, double search_radius)
        {
            var retVal = new List<TriangleIndexedLinked_wpf>();

            Vector3 box_size = new Vector3((float)search_radius, (float)search_radius, (float)search_radius);

            for (int i = 0; i < points.Length - 1; i++)
            {
                Point3D center = points[i] + ((points[i + 1] - points[i]) / 2);
                var search_box = new BoundingBox(center.ToVector3(), box_size);

                retVal.AddRange(tree.GetColliding(search_box));
            }

            return retVal.
                Distinct(o => o.Token).
                ToArray();
        }

        private static void Draw_NearbyTriangles(Point3D[] points, TriangleIndexed_wpf[] triangles)
        {
            if (!SHOULD_DRAW)
                return;

            Point3D center = Math3D.GetCenter(points);

            Point3D[] centered_points = points.
                Select(o => (o - center).ToPoint()).
                ToArray();

            Point3D[] allpoints_shifted = triangles[0].AllPoints.
                Select(o => (o - center).ToPoint()).
                ToArray();

            var centered_triangles = triangles.
                Select(o => new TriangleIndexed_wpf(o.Index0, o.Index1, o.Index2, allpoints_shifted)).
                ToArray();


            var window = new Debug3DWindow()
            {
                Title = "Nearby Triangles",
            };

            var sizes = Debug3DWindow.GetDrawSizes(centered_points);

            window.AddDots(centered_points, sizes.dot, Colors.DarkOliveGreen);
            window.AddLines(centered_points, sizes.line, Colors.DarkSeaGreen);

            window.AddHull(centered_triangles, Colors.Gainsboro);

            window.Show();
        }
    }
}
