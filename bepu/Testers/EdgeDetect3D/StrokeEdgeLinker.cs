using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF.Viewers;
using NetOctree.Octree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers.EdgeDetect3D
{
    public static class StrokeEdgeLinker
    {
        const bool SHOULD_DRAW = true;

        #region record: EdgeMatch

        private record EdgeMatch
        {
            public int SegmentIndex { get; init; }
            public Point3D Segment_From { get; init; }
            public Point3D Segment_To { get; init; }

            public NormalDot Edge { get; init; }
            public double Edge_Dist { get; init; }

            // These are abs value of dot product

            // How parallel the edge and path segment are
            public double Edge_Dot_Along { get; init; }

            // How perpendicular to the segment the edge's position is
            //
            // Each segment of a path will match multiple edges, some of the matches will be a better match for neighboring
            // path segments.  A good way to tell which is best for which is to also find the edges that are in the path
            // segment's "cylinder"
            public double Edge_Dot_Orth { get; init; }
        }

        #endregion

        // Probably return a custom object type
        public static void GetBestEdges(Point3D[] points, EdgeBackgroundWorker.WorkerResponse_Object[] objects, double search_radius)
        {
            // Iterate each line segment, looking for the best nearby edge
            //  some combination of distance to edge and alignment

            var matches_per_segment = new EdgeMatch[points.Length - 1][];

            for (int i = 0; i < points.Length - 1; i++)
                matches_per_segment[i] = FindNearbyEdges(i, points[i], points[i + 1], objects, search_radius);


            // Draw: path segments, deduped edge segments
            //  checkbox: show normal dot
            Draw_AllEdgeMatches(points, matches_per_segment);

            // Draw: Interactive: path segments, edge segments for selected path segment
            //  segment index: slider
            //
            //  importance sliders
            //      normal dot
            //      distance
            //      along dot
            //      orth dot





            // These two might need to be in a different class

            // Join the best matches together, looking for long chains of edges

            // Smooth it out with a bezier



        }

        private static EdgeMatch[] FindNearbyEdges(int segment_index, Point3D point0, Point3D point1, EdgeBackgroundWorker.WorkerResponse_Object[] objects, double search_radius)
        {
            var retVal = new List<EdgeMatch>();

            Vector3D dir_unit = (point1 - point0).ToUnit();
            Point3D center = point0 + ((point1 - point0) / 2);

            var (box, box_center) = GetSearchBox(point0, point1, search_radius);

            // Not sure if it's worth keeping the results of the objects separated.  For now, just merge results from all objects
            foreach (var obj in objects)
            {
                foreach (var edge in obj.Tree_Edges.GetColliding(box))
                {
                    Point3D edge_point0 = edge.Edge.EdgePoint0;
                    Point3D edge_point1 = edge.Edge.EdgePoint1;

                    Point3D edge_center = edge_point0 + ((edge_point1 - edge_point0) / 2);
                    Vector3D edge_dir_unit = (edge_point1 - edge_point0).ToUnit();

                    Vector3D edge_toward_segment_unit = (edge_center - center).ToUnit();
                    Vector3D orth_dir_unit = Vector3D.CrossProduct(Vector3D.CrossProduct(dir_unit, edge_toward_segment_unit), dir_unit);        // this is orthogonal to the segment's dir_unit, in the plane of dir_unit and edge center to segment center

                    retVal.Add(new EdgeMatch()
                    {
                        SegmentIndex = segment_index,
                        Segment_From = point0,
                        Segment_To = point1,

                        Edge = edge,
                        Edge_Dist = (edge_center - box_center).Length,
                        Edge_Dot_Along = Math.Abs(Vector3D.DotProduct(dir_unit, edge_dir_unit)),      // using absolute value, because +- doesn't matter, just need 0 to 1
                        Edge_Dot_Orth = Math.Abs(Vector3D.DotProduct(orth_dir_unit, edge_toward_segment_unit)),
                    });
                }
            }

            return retVal.ToArray();
        }

        private static (BoundingBox box, Point3D center) GetSearchBox(Point3D point0, Point3D point1, double search_radius)
        {
            double min_x = Math.Min(point0.X - search_radius, point1.X - search_radius);
            double min_y = Math.Min(point0.Y - search_radius, point1.Y - search_radius);
            double min_z = Math.Min(point0.Z - search_radius, point1.Z - search_radius);

            double max_x = Math.Max(point0.X + search_radius, point1.X + search_radius);
            double max_y = Math.Max(point0.Y + search_radius, point1.Y + search_radius);
            double max_z = Math.Max(point0.Z + search_radius, point1.Z + search_radius);

            double size_x = max_x - min_x;
            double size_y = max_y - min_y;
            double size_z = max_z - min_z;

            Point3D center_wpf = new Point3D(min_x + size_x / 2, min_y + size_y / 2, min_z + size_z / 2);
            Vector3 center = center_wpf.ToVector3();

            Vector3 size = new Vector3((float)size_x, (float)size_y, (float)size_z);

            return (new BoundingBox(center, size), center_wpf);
        }

        private static void Draw_AllEdgeMatches(Point3D[] points, EdgeMatch[][] matches_per_segment)
        {
            if (!SHOULD_DRAW)
                return;

            var deduped_edges = matches_per_segment.
                SelectMany(o => o).
                Select(o => o.Edge).
                DistinctBy(o => o.Token).
                ToArray();

            var used_points = points.
                Concat(deduped_edges.SelectMany(o => new Point3D[] { o.Edge.EdgePoint0, o.Edge.EdgePoint1 })).
                ToArray();

            Point3D center = Math3D.GetCenter(used_points);

            Point3D[] centered_points = points.
                Select(o => (o - center).ToPoint()).
                ToArray();

            Point3D[] centered_edge_points = deduped_edges.
                Select(o => new Point3D[] { (o.Edge.EdgePoint0 - center).ToPoint(), (o.Edge.EdgePoint1 - center).ToPoint() }).
                SelectMany(o => o).
                ToArray();

            var window = new Debug3DWindow()
            {
                Title = "All Edge Matches",
            };

            var sizes = Debug3DWindow.GetDrawSizes(centered_points.Concat(centered_edge_points));

            window.AddDots(centered_points, sizes.dot, Colors.DarkOliveGreen);
            window.AddLines(centered_points, sizes.line, Colors.DarkSeaGreen);

            var edges_centered = deduped_edges.
                Select(o => ((o.Edge.EdgePoint0 - center).ToPoint(), (o.Edge.EdgePoint1 - center).ToPoint()));

            window.AddLines(edges_centered, sizes.line * 0.5, Colors.MediumBlue);

            window.Show();
        }
    }
}
