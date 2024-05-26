using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Viewers;
using NetOctree.Octree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers.EdgeDetect3D
{
    public static class StrokeEdgeLinker
    {
        private const bool SHOULD_DRAW = true;

        private const double SCORING_NORMAL_DOT = 0.33;
        private const double SCORING_DISTANCE = 0.33;
        private const double SCORING_ALONG_DOT = 0.66;
        private const double SCORING_ORTH_DOT = 0.8;

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

            /// <summary>
            /// The score for the current segment, according to SCORING_ constants
            /// </summary>
            public double Score { get; init; }
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
            Draw_EdgeMatchesPerSegment(points, matches_per_segment, search_radius);
            //Draw_EdgeMatchesAllSegments(points, matches_per_segment, search_radius);





            // Join the best matches together, looking for long chains of edges

            // There will sometimes be gaps between linked edges.  Fill those gaps with a bezier

            // Smooth it out the whole think with a bezier



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
                    Point3D edge_point0 = edge.EdgePoint0;
                    Point3D edge_point1 = edge.EdgePoint1;

                    Point3D edge_center = edge_point0 + ((edge_point1 - edge_point0) / 2);
                    Vector3D edge_dir_unit = (edge_point1 - edge_point0).ToUnit();

                    Vector3D edge_toward_segment_unit = (edge_center - center).ToUnit();
                    Vector3D orth_dir_unit = Vector3D.CrossProduct(Vector3D.CrossProduct(dir_unit, edge_toward_segment_unit), dir_unit);        // this is orthogonal to the segment's dir_unit, in the plane of dir_unit and edge center to segment center

                    double edge_dist = (edge_center - box_center).Length;
                    double edge_dot_along = Math.Abs(Vector3D.DotProduct(dir_unit, edge_dir_unit));     // using absolute value, because +- doesn't matter, just need 0 to 1




                    // TODO: this isn't working.  It relies on the centers being orthogonal, but if segments are close together, but centers are off, the orth will be really innacurate
                    double edge_dot_orth = Math.Abs(Vector3D.DotProduct(orth_dir_unit, edge_toward_segment_unit));




                    double score = GetEdgeScore(point0, point1, edge.Dot, edge_dist, edge_dot_along, edge_dot_orth, search_radius, SCORING_NORMAL_DOT, SCORING_DISTANCE, SCORING_ALONG_DOT, SCORING_ORTH_DOT);

                    retVal.Add(new EdgeMatch()
                    {
                        SegmentIndex = segment_index,
                        Segment_From = point0,
                        Segment_To = point1,

                        Edge = edge,
                        Edge_Dist = edge_dist,
                        Edge_Dot_Along = edge_dot_along,
                        Edge_Dot_Orth = edge_dot_orth,
                        Score = score,
                    });
                }
            }

            return retVal.ToArray();
        }

        /// <summary>
        /// Returns copies of the edges with a new score based on priority params
        /// NOTE: They are sorted decending by score to make it easy to find the winner
        /// </summary>
        private static EdgeMatch[] ScoreEdges_SingleSegment(Point3D seg_point0, Point3D seg_point1, EdgeMatch[] edges, double search_radius, double normal_dot_priority, double distance_priority, double along_dot_priority, double orth_dot_priority)
        {
            return edges.
                Select(o => o with
                {
                    Score = GetEdgeScore(seg_point0, seg_point1, o.Edge.Dot, o.Edge_Dist, o.Edge_Dot_Along, o.Edge_Dot_Orth, search_radius, normal_dot_priority, distance_priority, along_dot_priority, orth_dot_priority),
                }).
                OrderByDescending(o => o.Score).
                ToArray();
        }

        private static double GetEdgeScore(Point3D seg_point0, Point3D seg_point1, double edge_dot, double edge_dist, double edge_dot_along, double edge_dot_orth, double search_radius, double normal_dot_priority, double distance_priority, double along_dot_priority, double orth_dot_priority)
        {
            double normal_dot = UtilityMath.GetScaledValue(0, 1, 1, -1, edge_dot) * normal_dot_priority;

            double distance = ((search_radius - edge_dist) / search_radius) * distance_priority;

            double along_dot = edge_dot_along * along_dot_priority;

            double orth_dot = edge_dot_orth * orth_dot_priority;

            return (normal_dot + distance + along_dot + orth_dot) / 4;
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
                Concat(deduped_edges.SelectMany(o => new Point3D[] { o.EdgePoint0, o.EdgePoint1 })).
                ToArray();

            Point3D center = Math3D.GetCenter(used_points);

            Point3D[] centered_points = points.
                Select(o => (o - center).ToPoint()).
                ToArray();

            Point3D[] centered_edge_points = deduped_edges.
                Select(o => new Point3D[] { (o.EdgePoint0 - center).ToPoint(), (o.EdgePoint1 - center).ToPoint() }).
                SelectMany(o => o).
                ToArray();

            var edges_centered = deduped_edges.
                Select(o => ((o.EdgePoint0 - center).ToPoint(), (o.EdgePoint1 - center).ToPoint())).
                ToArray();

            var window = new Debug3DWindow()
            {
                Title = "All Edge Matches",
                Background = Brushes.White,
            };

            var sizes = Debug3DWindow.GetDrawSizes(used_points, center);

            window.AddDots(centered_points, sizes.dot * 0.3, UtilityWPF.ColorFromHex("CAE69A"));        // Colors.DarkOliveGreen
            window.AddLines(centered_points, sizes.line * 0.3, UtilityWPF.ColorFromHex("BAE6BA"));        // Colors.DarkSeaGreen

            var checkbox = new CheckBox()
            {
                Content = "Show Edge Steepness",
                IsChecked = true,       // it will get changed to false in loaded event, forcing the lines to draw
            };

            var edge_visuals = new List<Visual3D>();

            //var checkbox_changed = new Action<object, System.Windows.RoutedEventArgs>((s,e) =>        -- can't be action, it must be RoutedEventHandler
            var checkbox_changed = new RoutedEventHandler((s, e) =>
            {
                window.Visuals3D.RemoveAll(edge_visuals);
                edge_visuals.Clear();

                //foreach(var edge in deduped_edges)
                for (int i = 0; i < deduped_edges.Length; i++)
                {
                    Color color = deduped_edges[i].Direction switch
                    {
                        TriangleFoldDirection.Peak => Colors.DarkRed,
                        TriangleFoldDirection.Valley => Colors.MediumBlue,
                        TriangleFoldDirection.Single => Colors.Black,
                        _ => Colors.Magenta,
                    };

                    if (checkbox.IsChecked.Value)
                    {
                        //double percent = Math.Abs(deduped_edges[i].Dot);
                        double percent = UtilityMath.GetScaledValue(0.25, 1, 1, -1, deduped_edges[i].Dot);

                        Color final_color = UtilityWPF.AlphaBlend(color, Colors.Transparent, percent);
                        double line_thickness = sizes.line * percent;

                        edge_visuals.Add(window.AddLine(edges_centered[i].Item1, edges_centered[i].Item2, line_thickness, final_color));
                    }
                    else
                    {
                        edge_visuals.Add(window.AddLine(edges_centered[i].Item1, edges_centered[i].Item2, sizes.line * 0.5, color));
                    }
                }
            });

            checkbox.Checked += checkbox_changed;
            checkbox.Unchecked += checkbox_changed;

            window.Messages_Top.Add(checkbox);      // even though it's called messages, it's just a list of uielements, so any control can be added to it

            window.Loaded += (s, e) => { checkbox.IsChecked = false; };     // force the checkchange event to fire

            window.Show();
        }

        private static void Draw_EdgeMatchesPerSegment(Point3D[] points, EdgeMatch[][] matches_per_segment, double search_radius)
        {
            if (!SHOULD_DRAW)
                return;

            var deduped_edges = matches_per_segment.
                SelectMany(o => o).
                Select(o => o.Edge).
                DistinctBy(o => o.Token).
                ToArray();

            var used_points = points.
                Concat(deduped_edges.SelectMany(o => new Point3D[] { o.EdgePoint0, o.EdgePoint1 })).
                ToArray();

            Point3D center = Math3D.GetCenter(used_points);

            Point3D[] centered_points = points.
                Select(o => (o - center).ToPoint()).
                ToArray();

            var window = new Debug3DWindow()
            {
                Title = "Edge Matches per Segment",
                Background = Brushes.White,
            };

            var sizes = Debug3DWindow.GetDrawSizes(used_points, center);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Auto) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(8) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50) });

            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });

            Slider seg_index = Add_Label_Slider(grid, 0, "Segment Index", 0, points.Length - 2, 1, true);       // setting to one so that window loaded can set to zero, causing a change and redraw

            Slider normal_dot = Add_Label_Slider(grid, 2, "Edge Steepness", 0, 1, SCORING_NORMAL_DOT);
            Slider distance = Add_Label_Slider(grid, 3, "Distance From Segment", 0, 1, SCORING_DISTANCE);
            Slider along_dot = Add_Label_Slider(grid, 4, "Direction parallel to segment", 0, 1, SCORING_ALONG_DOT);
            Slider orth_dot = Add_Label_Slider(grid, 5, "Position perpendicular to segment", 0, 1, SCORING_ORTH_DOT);

            Slider maxscore_diff = Add_Label_Slider(grid, 7, "Best Score Diff", 0, 1, 0.1);

            CheckBox show_dots = Add_Checkbox(grid, 9, "Show Dots", false);
            CheckBox show_scores = Add_Checkbox(grid, 10, "Show Scores", false);
            CheckBox show_winner = Add_Checkbox(grid, 11, "Show Winner", true);

            var visuals = new List<Visual3D>();

            bool has_autoset = false;

            var redraw = new Action(() =>
            {
                // Apply a score to current segment's edge matches
                int index = seg_index.Value.ToInt_Floor();

                EdgeMatch[] edges_scored = ScoreEdges_SingleSegment(points[index], points[index + 1], matches_per_segment[index], search_radius, normal_dot.Value, distance.Value, along_dot.Value, orth_dot.Value);

                // Clear existing visuals
                window.Visuals3D.Clear();
                //window.Visuals3D.RemoveAll(visuals);      // this fails when removing all visuals
                visuals.Clear();

                // Draw everything
                visuals.Add(window.AddDots([centered_points[index], centered_points[index + 1]], sizes.dot * 0.3, Colors.DarkOliveGreen));
                visuals.Add(window.AddLine(centered_points[index], centered_points[index + 1], sizes.line * 0.3, Colors.DarkSeaGreen));

                if (show_dots.IsChecked.Value)
                {
                    var distinct_edge_points = matches_per_segment[index].
                        Select(o => new[] { (o.Edge.EdgePoint0 - center).ToPoint(), (o.Edge.EdgePoint1 - center).ToPoint() }).
                        SelectMany(o => o).
                        Distinct((o1, o2) => o1.IsNearValue(o2)).
                        ToArray();

                    visuals.Add(window.AddDots(distinct_edge_points, sizes.dot / 6, Colors.Silver));
                }

                for (int i = 0; i < edges_scored.Length; i++)
                {
                    Color color = edges_scored[i].Edge.Direction switch
                    {
                        TriangleFoldDirection.Peak => Colors.DarkRed,
                        TriangleFoldDirection.Valley => Colors.MediumBlue,
                        TriangleFoldDirection.Single => Colors.Black,
                        _ => Colors.Magenta,
                    };

                    Point3D edge_centered_0 = (edges_scored[i].Edge.EdgePoint0 - center).ToPoint();
                    Point3D edge_centered_1 = (edges_scored[i].Edge.EdgePoint1 - center).ToPoint();

                    if (i == 0 && show_winner.IsChecked.Value && edges_scored.Length > 1 && edges_scored[0].Score - edges_scored[1].Score > maxscore_diff.Value)
                        visuals.Add(window.AddDots([edge_centered_0, edge_centered_1], sizes.dot, Colors.Goldenrod));

                    Color final_color = UtilityWPF.AlphaBlend(color, Colors.Transparent, edges_scored[i].Score);
                    double line_thickness = sizes.line * edges_scored[i].Score;

                    visuals.Add(window.AddLine(edge_centered_0, edge_centered_1, line_thickness, final_color));

                    if (show_scores.IsChecked.Value)
                    {
                        Point3D edge_center = Math3D.GetCenter(edges_scored[i].Edge.EdgePoint0, edges_scored[i].Edge.EdgePoint1);
                        edge_center = (edge_center - center).ToPoint();
                        double height = (centered_points[index + 1] - centered_points[index]).Length / 3;

                        visuals.Add(window.AddText3D(edges_scored[i].Score.ToStringSignificantDigits(2), edge_center, -window.Camera_Look, height, Colors.DarkGreen, false, window.Camera_Right));
                    }
                }

                if (!has_autoset)
                {
                    window.AutoSetCamera();
                    has_autoset = true;
                }
            });

            var redraw_slider = new RoutedPropertyChangedEventHandler<double>((s, e) => redraw());
            var redraw_checkbox = new RoutedEventHandler((s, e) => redraw());

            seg_index.ValueChanged += redraw_slider;
            normal_dot.ValueChanged += redraw_slider;
            distance.ValueChanged += redraw_slider;
            along_dot.ValueChanged += redraw_slider;
            orth_dot.ValueChanged += redraw_slider;
            maxscore_diff.ValueChanged += redraw_slider;
            show_dots.Checked += redraw_checkbox;
            show_dots.Unchecked += redraw_checkbox;
            show_scores.Checked += redraw_checkbox;
            show_scores.Unchecked += redraw_checkbox;
            show_winner.Checked += redraw_checkbox;
            show_winner.Unchecked += redraw_checkbox;

            window.Messages_Top.Add(grid);      // even though it's called messages, it's just a list of uielements, so any control can be added to it

            window.Loaded += (s, e) => { seg_index.Value = 0; };     // force the value change event to fire

            window.Show();
        }

        private static Slider Add_Label_Slider(Grid grid, int row_index, string text, double min, double max, double value, bool is_integer = false)
        {
            TextBlock label = new TextBlock()
            {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Grid.SetColumn(label, 0);
            Grid.SetRow(label, row_index);

            grid.Children.Add(label);

            Slider slider = new Slider()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = value.ToString(),
                Minimum = min,
                Maximum = max,
                Value = value,
                Focusable = false,
            };

            if (is_integer)
            {
                slider.TickFrequency = 1;
                slider.IsSnapToTickEnabled = true;
            }

            slider.ValueChanged += (s, e) => slider.ToolTip = slider.Value.ToString();

            // Need to stop these events from being seen by the viewport.  Otherwise dragging a slider will cause the viewport's trackball to move the scene
            slider.MouseDown += (s, e) => e.Handled = true;
            slider.MouseUp += (s, e) => e.Handled = true;
            slider.MouseMove += (s, e) => e.Handled = true;

            Grid.SetColumn(slider, 2);
            Grid.SetRow(slider, row_index);

            grid.Children.Add(slider);

            return slider;
        }
        private static CheckBox Add_Checkbox(Grid grid, int row_index, string text, bool default_val)
        {
            CheckBox checkbox = new CheckBox()
            {
                Content = text,
                IsChecked = default_val,
            };

            Grid.SetColumn(checkbox, 0);
            Grid.SetRow(checkbox, row_index);

            grid.Children.Add(checkbox);

            return checkbox;
        }
    }
}
