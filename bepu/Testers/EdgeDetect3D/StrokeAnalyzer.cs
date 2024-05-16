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
            // Convert the raw points into something more uniform
            points = StrokeCleaner.CleanPath(points);

            double search_radius = GetSearchRadius(points);




            var triangles = GetNearbyTriangles(points, objects.Objects, search_radius);
            Draw_NearbyTriangles(points, triangles);






            // Iterate each line segment, looking for the best nearby edge
            //  some combination of distance to edge and alignment

            // Join the best matches together, looking for long chains of edges

            // Smooth it out with a bezier


        }

        private static double GetSearchRadius(Point3D[] points)
        {
            double[] lengths = new double[points.Length - 1];

            for (int i = 0; i < points.Length - 1; i++)
                lengths[i] = (points[i + 1] - points[i]).Length;

            double avg = Math1D.Avg(lengths);

            return avg * 6;
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

            window.AddHull(centered_triangles, Colors.Snow);

            window.Show();
        }
    }
}
