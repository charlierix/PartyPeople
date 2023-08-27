using Accord.Math;
using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers
{
    public partial class WingInterference : Window
    {
        #region records: Plane Definition from Unity

        private record PlaneDefinition
        {
            public EngineDefinition Engine_0 { get; init; }
            public EngineDefinition Engine_1 { get; init; }
            public EngineDefinition Engine_2 { get; init; }

            public WingDefinition Wing_0 { get; init; }
            public WingDefinition Wing_1 { get; init; }
            public WingDefinition Wing_2 { get; init; }

            public WingModifier[] WingModifiers_0 { get; init; }
            public WingModifier[] WingModifiers_1 { get; init; }
            public WingModifier[] WingModifiers_2 { get; init; }

            public TailDefinition Tail { get; init; }

            // head canard

            // spine
            //  this would be a single hinge joint, or a small chain of segments

            //TODO: maybe some way to define mass
        }

        private record EngineDefinition
        {
            public Vector3 Offset { get; init; }        // this is for the right wing.  The left will be mirroed
            public Quaternion Rotation { get; init; }

            public float Size { get; init; } = 1;
        }

        private record WingDefinition
        {
            public Vector3 Offset { get; init; }        // this is for the right wing.  The left will be mirroed
            public Quaternion Rotation { get; init; }

            /// <summary>
            /// There will be a root point, this many interior points, tip point
            /// </summary>
            /// <remarks>
            /// This doesn't change the total length of the wing, just how much it is chopped up
            /// 
            /// Each * is an unscaled anchor point
            /// Each - is a wing segment (vertical stabilizers are tied to the anchor points)
            /// 
            /// 0:
            ///     *-*
            /// 
            /// 1:
            ///     *-*-*
            /// 
            /// 2:
            ///     *-*-*-*
            ///     
            /// 3:
            ///     *-*-*-*-*
            /// </remarks>
            public int Inner_Segment_Count { get; init; } = 2;

            /// <summary>
            /// Total length of the wing (wing span)
            /// X scale
            /// </summary>
            public float Span { get; init; } = 1f;
            /// <summary>
            /// How the wing segments are spaced apart
            /// </summary>
            /// <remarks>
            /// If power is 1, then they are spaced linearly:
            /// *     *     *     *
            /// 
            /// If power is .5, then it's sqrt
            /// *       *     *   *
            /// 
            /// If power is 2, then it's ^2 (this would be an unnatural way to make a wing)
            /// *   *     *       *
            /// </remarks>
            public float Span_Power { get; init; } = 1f;       // all inputs to the power function are scaled from 0 to 1.  You generally want to set power between 0.5 and 1

            /// <summary>
            /// The part the runs parallel to the fuselage
            /// Z scale
            /// </summary>
            public float Chord_Base { get; init; } = 0.4f;
            public float Chord_Tip { get; init; } = 0.4f;
            public float Chord_Power { get; init; } = 1f;

            /// <summary>
            /// How much lift the wing generates
            /// </summary>
            /// <remarks>
            /// 0 is no extra lift, 1 is high lift/high drag
            /// 
            /// The way lift works is between about -20 to 20 degrees angle of attack, there is extra force applied along the wing's normal
            /// But that comes at a cost of extra drag and less effective at higher angles of attack
            /// </remarks>
            public float Lift_Base { get; init; } = 0.7f;
            public float Lift_Tip { get; init; } = 0.2f;
            public float Lift_Power { get; init; } = 1f;

            public float MIN_VERTICALSTABILIZER_HEIGHT { get; init; } = 0.1f;

            /// <summary>
            /// These are vertical pieces (lift is set to zero)
            /// </summary>
            /// <remarks>
            /// You would generally want a single stabalizer at the tip of the wing.  To accomplish that, use a high power
            /// (the height at each segment is near zero, then height approaches max close to the tip)
            /// </remarks>
            public float VerticalStabilizer_Base { get; init; } = 0;       // NOTE: a vertical stabilizer won't be created for a segment if it's less than MIN_VERTICALSTABILIZER_HEIGHT
            public float VerticalStabilizer_Tip { get; init; } = 0;
            public float VerticalStabilizer_Power { get; init; } = 16;
        }

        private record WingModifier
        {
            public MultAtAngle Lift { get; init; }
            public MultAtAngle Drag { get; init; }

            public float From { get; init; }
            public float To { get; init; }
        }

        private record MultAtAngle
        {
            public float Neg_90 { get; init; } = 1f;
            public float Zero { get; init; } = 1f;
            public float Pos_90 { get; init; } = 1f;
        }

        public record TailDefinition
        {
            public Vector3 Offset { get; init; }        //NOTE: if offset contains abs(X) > MIN, there will be two tails
            public Quaternion Rotation { get; init; }

            public TailDefinition_Boom Boom { get; init; }
            public TailDefinition_Tail Tail { get; init; }
        }

        public record TailDefinition_Boom
        {
            public int Inner_Segment_Count { get; init; } = 2;

            public float Length { get; init; } = 1f;
            public float Length_Power { get; init; } = 1f;

            public float Mid_Length { get; init; } = 0.7f;

            public float Span_Base { get; init; } = 0.5f;
            public float Span_Mid { get; init; } = 0.2f;
            public float Span_Tip { get; init; } = 0.3f;

            public float Vert_Base { get; init; } = 0.5f;
            public float Vert_Mid { get; init; } = 0.2f;
            public float Vert_Tip { get; init; } = 0.3f;

            public float Bezier_PinchPercent { get; init; } = 0.2f;       // 0 to 0.4 (affects the curviness of the bezier, 0 is linear)
        }

        public record TailDefinition_Tail
        {
            public float MIN_SIZE { get; init; } = 0.1f;

            public float Chord { get; init; } = 0.33f;        // if < MIN_TAIL_SIZE, there is no tail section
            public float Horz_Span { get; init; } = 0.5f;     // if < MIN_TAIL_SIZE, there is no horizontal wing
            public float Vert_Height { get; init; } = 0.5f;       // if < MIN_TAIL_SIZE, there is no vertical wing
        }

        #endregion

        #region record: WingDef

        private record WingDef
        {
            public double Span { get; init; }
            public double Chord { get; init; }

            public ITriangleIndexed_wpf[] Triangles { get; init; }
        }

        #endregion
        #region record: WingAABB

        private record WingAABB
        {
            public WingDef Wing { get; init; }

            public double CellSize { get; init; }
        }

        #endregion

        #region Declaration Section

        private const double CELL_SIZE = 0.1;

        #endregion

        #region Constructor

        public WingInterference()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;
        }

        #endregion

        #region Event Listeners

        private void Cells_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cells = Math3D.GetCells(1, 12, 12, 12);

                var window = new Debug3DWindow()
                {
                    Title = "Cells",
                };

                var sizes = Debug3DWindow.GetDrawSizes(12);


                var cell_lines = cells.
                    SelectMany(o => Polytopes.GetCubeLines(o.rect.Location, o.rect.Location + o.rect.Size.ToVector()));

                var distinct_lines = Math3D.GetDistinctLineSegments(cell_lines);

                window.AddLines(distinct_lines.index_pairs, distinct_lines.all_points_distinct, sizes.line / 4, Colors.White);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AxisAligned_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new Debug3DWindow()
                {
                    Title = "Axis Aligned Plate",
                };

                var sizes = Debug3DWindow.GetDrawSizes(1);

                var wing_primary = GetRandomWing();

                var cells_primary = Math3D.GetCells(CELL_SIZE, (wing_primary.Span / CELL_SIZE).ToInt_Ceiling(), (wing_primary.Chord / CELL_SIZE).ToInt_Ceiling(), 1);

                var wings_other = Enumerable.Range(0, 3).
                    Select(o => GetRandomWing(true, true)).
                    ToArray();

                var cells_other = GetIntersectingCells(wings_other, CELL_SIZE);
                var aabb_other = wings_other.
                    Select(o => GetWingAABB(o, CELL_SIZE)).
                    ToArray();




                var cell_lines = cells_primary.
                    SelectMany(o => Polytopes.GetCubeLines(o.rect.Location, o.rect.Location + o.rect.Size.ToVector()));

                var distinct_lines = Math3D.GetDistinctLineSegments(cell_lines);

                window.AddLines(distinct_lines.index_pairs, distinct_lines.all_points_distinct, sizes.line / 4, Colors.White);


                window.AddTriangle(wing_primary.Triangles[0], Colors.DodgerBlue);
                window.AddTriangle(wing_primary.Triangles[1], Colors.DodgerBlue);



                foreach (var wing in wings_other)
                {
                    window.AddTriangle(wing.Triangles[0], Colors.DarkKhaki);
                    window.AddTriangle(wing.Triangles[1], Colors.DarkKhaki);
                }





                cell_lines = cells_other.
                    SelectMany(o => Polytopes.GetCubeLines(o.rect.Location, o.rect.Location + o.rect.Size.ToVector()));


                // After waiting minutes for this to finish, I just decided to comment it out
                //distinct_lines = Math3D.GetDistinctLineSegments(cell_lines);
                //window.AddLines(distinct_lines.index_pairs, distinct_lines.all_points_distinct, sizes.line / 4, Colors.Wheat);





                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MarkWing_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = new SparseCellGrid(CELL_SIZE);

                var wing = GetRandomWing();

                foreach (var triangle in wing.Triangles)
                {
                    grid.Mark_Triangle(triangle, "wing1");
                }

                var marked_all = grid.GetMarked_All();


                var sizes = Debug3DWindow.GetDrawSizes(4);

                var window = new Debug3DWindow();

                var cell_lines = marked_all.
                    SelectMany(o => Polytopes.GetCubeLines(ToPoint(o, grid), ToPoint(o, grid, true)));

                var distinct_lines = Math3D.GetDistinctLineSegments(cell_lines);

                window.AddLines(distinct_lines.index_pairs, distinct_lines.all_points_distinct, sizes.line / 4, Colors.White);

                foreach (var triangle in wing.Triangles)
                {
                    window.AddTriangle(triangle, UtilityWPF.ColorFromHex("4000"), UtilityWPF.ColorFromHex("A000"));
                }

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Wing1SeesWing2_Click(object sender, RoutedEventArgs e)
        {
            const string WING1 = "wing1";
            const string WING2 = "wing2";

            try
            {
                var grid = new SparseCellGrid(CELL_SIZE, true);
                //var grid = new SparseCellGrid(CELL_SIZE * 3, true);

                var wing1 = GetRandomWing(true, true);
                var wing2 = GetRandomWing(true, true);

                foreach (var triangle in wing1.Triangles)
                    grid.Mark_Triangle(triangle, WING1);

                foreach (var triangle in wing2.Triangles)
                    grid.Mark_Triangle(triangle, WING2);

                var marked_wing1 = grid.GetMarked_All(new[] { WING1 });


                double search_radius = 1;

                VectorInt3[] nearby_2 = marked_wing1.
                    Select(o => grid.GetCell(o).center).
                    SelectMany(o => grid.GetMarked_Sphere(o, search_radius, except_keys: new[] { WING1 })).
                    Distinct().
                    ToArray();



                var sizes = Debug3DWindow.GetDrawSizes(4);

                var window = new Debug3DWindow();

                var cell_lines = nearby_2.
                    SelectMany(o => Polytopes.GetCubeLines(ToPoint(o, grid), ToPoint(o, grid, true)));

                var distinct_lines = Math3D.GetDistinctLineSegments(cell_lines);

                window.AddLines(distinct_lines.index_pairs, distinct_lines.all_points_distinct, sizes.line / 4, Colors.White);

                foreach (var triangle in wing1.Triangles)
                    window.AddTriangle(triangle, Colors.DodgerBlue, UtilityWPF.ColorFromHex("A000"));

                foreach (var triangle in wing2.Triangles)
                    window.AddTriangle(triangle, Colors.DarkGoldenrod, UtilityWPF.ColorFromHex("A000"));

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Wing1InterferedByWing2_Click(object sender, RoutedEventArgs e)
        {
            // These values look pretty good for a proof of concept test.  The final search radius will probably need to be reduced with
            // actual air frames.  Also probably need more of a power dropoff


            const string WING1 = "wing1";
            const string WING2 = "wing2";

            const double SEARCH_RADIUS = 0.66;
            const double POW = 0.66;
            const double MULT = 1d / 3d;

            try
            {
                //var grid = new SparseCellGrid(CELL_SIZE, true);
                var grid = new SparseCellGrid(CELL_SIZE * 2, true);

                var wing1 = GetRandomWing(true, true);
                var wing2 = GetRandomWing(true, true);

                foreach (var triangle in wing1.Triangles)
                    grid.Mark_Triangle(triangle, WING1);

                foreach (var triangle in wing2.Triangles)
                    grid.Mark_Triangle(triangle, WING2);

                var marked_wing1 = grid.GetMarked_All(new[] { WING1 });


                VectorInt3[] nearby_2_a = marked_wing1.
                    Select(o => grid.GetCell(o).center).
                    SelectMany(o => grid.GetMarked_Sphere(o, SEARCH_RADIUS, except_keys: new[] { WING1 })).
                    Distinct().
                    ToArray();




                var get_interference = new Func<Point3D, VectorInt3, double>((center1, index) =>
                {
                    Point3D center2 = grid.GetCell(index).center;
                    double dist = (center2 - center1).Length;
                    double dist_normalized = dist / SEARCH_RADIUS;

                    return Math.Pow(dist_normalized, POW);
                });


                var nearby_2_b = marked_wing1.
                    Select(o =>
                    {
                        Point3D center = grid.GetCell(o).center;
                        VectorInt3[] nearby = grid.GetMarked_Sphere(center, SEARCH_RADIUS, except_keys: new[] { WING1 });

                        double sum_interference = nearby.Sum(p => get_interference(center, p));

                        // Can't just use the sum, since smaller cells will give a larger result
                        // Need to adjust based on the density of cells
                        // I think multiplying by cell size should be all that's needed
                        sum_interference *= grid.CellSize;

                        // Reduce to reasonably fit 0 to 1, hopefully not exceeding 2
                        sum_interference *= MULT;

                        return new
                        {
                            index = o,
                            center,
                            interference = sum_interference,
                        };
                    }).
                    Where(o => o.interference > 0).
                    ToArray();



                var sizes = Debug3DWindow.GetDrawSizes(4);

                var window = new Debug3DWindow();

                var cell_lines = nearby_2_a.
                    SelectMany(o => Polytopes.GetCubeLines(ToPoint(o, grid), ToPoint(o, grid, true)));

                var distinct_lines = Math3D.GetDistinctLineSegments(cell_lines);

                window.AddLines(distinct_lines.index_pairs, distinct_lines.all_points_distinct, sizes.line / 4, Colors.White);

                foreach (var affected in nearby_2_b)
                {
                    Color color = affected.interference > 1d ?
                        UtilityWPF.AlphaBlend(Colors.Red, Colors.Black, Math.Clamp(affected.interference - 1, 0, 1)) :
                        UtilityWPF.AlphaBlend(Colors.Black, Colors.White, affected.interference);

                    window.AddDot(affected.center, sizes.dot, color);
                }

                if (nearby_2_b.Length > 0)
                    window.AddText($"max interference: {nearby_2_b.Max(o => o.interference).ToStringSignificantDigits(2)}");

                foreach (var triangle in wing2.Triangles)
                    window.AddTriangle(triangle, UtilityWPF.ColorFromHex("40B8860B"), UtilityWPF.ColorFromHex("A000"));

                foreach (var triangle in wing1.Triangles)
                    window.AddTriangle(triangle, UtilityWPF.ColorFromHex("401E90FF"), UtilityWPF.ColorFromHex("A000"));

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GetCell_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = new SparseCellGrid(CELL_SIZE);

                var indices = new[] { new VectorInt3() };

                int size = 3;

                indices = indices.Concat(Enumerable.Range(0, 48).
                    Select(o => new VectorInt3(StaticRandom.Next(-size, size + 1), StaticRandom.Next(-size, size + 1), StaticRandom.Next(-size, size + 1)))).
                    ToArray();

                //var test1 = grid.GetCell(indices[0]);

                var cells = indices.
                    Select(o => new
                    {
                        index = o,
                        cell = grid.GetCell(o),
                    }).
                    ToArray();


                var full_circle = cells.
                    Select(o =>
                    {
                        var sample_points = Enumerable.Range(0, 144).
                            Select(p => Math3D.GetRandomVector(o.cell.rect.Location.ToVector(), (o.cell.rect.Location + o.cell.rect.Size.ToVector()).ToVector()).ToPoint()).
                            ToArray();

                        return new
                        {
                            o.index,
                            o.cell,
                            samples = sample_points.
                                Select(p => new
                                {
                                    pt = p,
                                    index = grid.GetIndex_Point(p),
                                }).
                                ToArray(),
                        };
                    }).
                    ToArray();

                var fails = full_circle.
                    Where(o => o.samples.Any(p => o.index != p.index)).
                    ToArray();


                var line_segments = grid.GetDistinctLineSegments(indices);

                var window = new Debug3DWindow()
                {
                    Title = "Grid Tester",
                };

                var sizes = Debug3DWindow.GetDrawSizes(1);


                //window.AddLines(line_segments.index_pairs, line_segments.all_points_distinct, sizes.line, Colors.White);

                foreach (var segment in line_segments.index_pairs)
                {
                    window.AddLine(line_segments.all_points_distinct[segment.i1], line_segments.all_points_distinct[segment.i2], sizes.line, UtilityWPF.GetRandomColor(64, 255));
                }


                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CellIndices_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = new SparseCellGrid(CELL_SIZE);

                var points = new[] { new Point3D(0.05, -0.05, .15) };

                double radius = 12;

                points = points.Concat(Enumerable.Range(0, 12).
                    Select(o => Math3D.GetRandomVector_Spherical(radius).ToPoint())).
                    ToArray();

                //var test1 = grid.GetIndex_Point(points[0]);

                var indices = points.
                    Select(o => grid.GetIndex_Point(o)).
                    ToArray();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IndicesForTriangle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = new SparseCellGrid(CELL_SIZE);
                //var grid = new SparseCellGrid(CELL_SIZE / 3);
                //var grid = new SparseCellGrid(CELL_SIZE * 3);

                double radius = 1.5;
                var triangle = new Triangle_wpf(Math3D.GetRandomVector_Spherical(radius).ToPoint(), Math3D.GetRandomVector_Spherical(radius).ToPoint(), Math3D.GetRandomVector_Spherical(radius).ToPoint());

                var indices = grid.GetIndices_Triangle(triangle, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IndicesForRect2D_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = new SparseCellGrid(CELL_SIZE);

                double span = StaticRandom.NextDouble(0.5, 3);
                double chord = StaticRandom.NextDouble(0.1, 1.3);

                Rect rect = new Rect(new Point(-span / 2, -chord / 2), new Size(span, chord));

                var indices = grid.GetIndices_Rect2D(rect, 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MarkCells_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = new SparseCellGrid(CELL_SIZE, true, true);

                // ------------------------------------ mark ------------------------------------

                // Point
                Point3D point = Math3D.GetRandomVector_Spherical(12).ToPoint();
                var marked_point = grid.Mark_Point(point);

                // Triangle
                double radius = 1.5;
                Point3D vertex0 = Math3D.GetRandomVector_Spherical(radius).ToPoint();
                Point3D vertex1 = Math3D.GetRandomVector_Spherical(radius).ToPoint();
                Point3D vertex2 = Math3D.GetRandomVector_Spherical(radius).ToPoint();
                var triangle = new Triangle_wpf(vertex0, vertex1, vertex2);
                var marked_triangle = grid.Mark_Triangle(triangle, return_marked_cells: true);

                // Rect2D
                double span = StaticRandom.NextDouble(0.5, 3);
                double chord = StaticRandom.NextDouble(0.1, 1.3);
                Rect rect = new Rect(new Point(-span / 2, -chord / 2), new Size(span, chord));
                double rect_z = StaticRandom.NextDouble(-4, 4);
                var marked_rect = grid.Mark_Rect2D(rect, rect_z);


                // ------------------------------------ get marked ------------------------------------

                // sphere
                Point3D center = Math3D.GetRandomVector_Spherical(6).ToPoint();
                double radius2 = StaticRandom.NextDouble(1, 4);

                var marked_sphere = grid.GetMarked_Sphere(center, radius2);

                // aabb
                Point3D aabb1 = Math3D.GetRandomVector_Spherical(6).ToPoint();
                Point3D aabb2 = Math3D.GetRandomVector_Spherical(6).ToPoint();
                var aabb = Math3D.GetAABB(new[] { aabb1, aabb2 });
                var marked_aabb = grid.GetMarked_AABB(aabb.min, aabb.max);

                // all
                var marked_all = grid.GetMarked_All();


                // ------------------------------------ draw ------------------------------------

                MarkCells_Click_Draw(marked_all, grid, point, triangle, rect, rect_z, "All", true, true, true);
                MarkCells_Click_Draw(marked_point.MarkedCells, grid, point, triangle, rect, rect_z, "Point", true, false, false);
                MarkCells_Click_Draw(marked_triangle.MarkedCells, grid, point, triangle, rect, rect_z, "Triangle", false, true, false);
                MarkCells_Click_Draw(marked_rect.MarkedCells, grid, point, triangle, rect, rect_z, "Rect", false, false, true);

                var window = MarkCells_Click_Draw(marked_sphere, grid, point, triangle, rect, rect_z, "All - Sphere", true, true, true);
                window.AddDot(center, radius2, UtilityWPF.ColorFromHex("1FF0"), isHiRes: true);

                window = MarkCells_Click_Draw(marked_aabb, grid, point, triangle, rect, rect_z, "All - AABB", true, true, true);
                window.AddMesh(UtilityWPF.GetCube_IndependentFaces(aabb1, aabb2), UtilityWPF.ColorFromHex("1FF0"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static Debug3DWindow MarkCells_Click_Draw(VectorInt3[] cells, SparseCellGrid grid, Point3D point, Triangle_wpf triangle, Rect rect, double rect_z, string title_suffix, bool draw_point, bool draw_triangle, bool draw_rect)
        {
            //var sizes = Debug3DWindow.GetDrawSizes(12);
            var sizes = Debug3DWindow.GetDrawSizes(4);

            var window = new Debug3DWindow()
            {
                Title = $"Mark Shapes - {title_suffix}",
            };

            // Marked
            //window.AddDots(cells.Select(o => grid.GetCell(o).center), sizes.dot, Colors.White);

            var cell_lines = cells.
                SelectMany(o => Polytopes.GetCubeLines(ToPoint(o, grid), ToPoint(o, grid, true)));

            var distinct_lines = Math3D.GetDistinctLineSegments(cell_lines);

            window.AddLines(distinct_lines.index_pairs, distinct_lines.all_points_distinct, sizes.line / 4, Colors.White);

            // Point
            if (draw_point)
                window.AddDot(point, sizes.dot, Colors.Black);

            // Triangle
            if (draw_triangle)
                window.AddTriangle(triangle, UtilityWPF.ColorFromHex("4000"), UtilityWPF.ColorFromHex("A000"));

            // Rect2D
            if (draw_rect)
                window.AddSquare(rect, UtilityWPF.ColorFromHex("4000"), true, rect_z);

            window.Show();

            return window;
        }

        private void MarkCellsKeys_Click(object sender, RoutedEventArgs e)
        {
            const string POINT = "point";
            const string TRIANGLE = "triangle";
            const string RECT = "rect";

            try
            {
                var grid = new SparseCellGrid(CELL_SIZE, true, true);

                // ------------------------------------ mark ------------------------------------

                // Point
                Point3D point = Math3D.GetRandomVector_Spherical(12).ToPoint();
                var marked_point = grid.Mark_Point(point, POINT);

                // Triangle
                double radius = 1.5;
                Point3D vertex0 = Math3D.GetRandomVector_Spherical(radius).ToPoint();
                Point3D vertex1 = Math3D.GetRandomVector_Spherical(radius).ToPoint();
                Point3D vertex2 = Math3D.GetRandomVector_Spherical(radius).ToPoint();
                var triangle = new Triangle_wpf(vertex0, vertex1, vertex2);
                var marked_triangle = grid.Mark_Triangle(triangle, TRIANGLE, true);

                // Rect2D
                double span = StaticRandom.NextDouble(0.5, 3);
                double chord = StaticRandom.NextDouble(0.1, 1.3);
                Rect rect = new Rect(new Point(-span / 2, -chord / 2), new Size(span, chord));
                double rect_z = StaticRandom.NextDouble(-4, 4);
                var marked_rect = grid.Mark_Rect2D(rect, rect_z, RECT);


                // ------------------------------------ get marked ------------------------------------

                // sphere
                Point3D center = Math3D.GetRandomVector_Spherical(6).ToPoint();
                double radius2 = StaticRandom.NextDouble(4, 8);

                var marked_sphere = grid.GetMarked_Sphere(center, radius2, new[] { RECT });

                // aabb
                Point3D aabb1 = Math3D.GetRandomVector_Spherical(9).ToPoint();
                Point3D aabb2 = Math3D.GetRandomVector_Spherical(9).ToPoint();
                var aabb = Math3D.GetAABB(new[] { aabb1, aabb2 });
                var marked_aabb = grid.GetMarked_AABB(aabb.min, aabb.max, null, new[] { RECT });

                // all
                var marked_all = grid.GetMarked_All(new[] { POINT, TRIANGLE }, new[] { POINT });


                // ------------------------------------ draw ------------------------------------

                MarkCells_Click_Draw(marked_all, grid, point, triangle, rect, rect_z, "All", true, true, true);
                MarkCells_Click_Draw(marked_point.MarkedCells, grid, point, triangle, rect, rect_z, "Point", true, false, false);
                MarkCells_Click_Draw(marked_triangle.MarkedCells, grid, point, triangle, rect, rect_z, "Triangle", false, true, false);
                MarkCells_Click_Draw(marked_rect.MarkedCells, grid, point, triangle, rect, rect_z, "Rect", false, false, true);

                var window = MarkCells_Click_Draw(marked_sphere, grid, point, triangle, rect, rect_z, "All - Sphere", true, true, true);
                window.AddDot(center, radius2, UtilityWPF.ColorFromHex("1FF0"), isHiRes: true);

                window = MarkCells_Click_Draw(marked_aabb, grid, point, triangle, rect, rect_z, "All - AABB", true, true, true);
                window.AddMesh(UtilityWPF.GetCube_IndependentFaces(aabb1, aabb2), UtilityWPF.ColorFromHex("1FF0"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RandomPlane_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PlaneDefinition plane_def = GetRandomPlane();




            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods - Get Definition 1

        private static WingDef GetRandomWing(bool random_position = false, bool random_rotation = false)
        {
            Random rand = StaticRandom.GetRandomForThread();

            double span = rand.NextDouble(.5, 4);
            double chord = rand.NextDouble(.15, 1);

            double span_half = span / 2;
            double chord_half = chord / 2;

            var points = new Point3D[]
            {
                new Point3D(-span_half, -chord_half, 0),        // bl
                new Point3D(span_half, -chord_half, 0),     // br
                new Point3D(span_half, chord_half, 0),      // tp
                new Point3D(-span_half, chord_half, 0)      // tl
            };

            if (random_rotation)
                Math3D.GetRandomRotation().GetRotatedVector(points);

            if (random_position)
            {
                Vector3D offset = Math3D.GetRandomVector_Spherical(2);

                points = points.
                    Select(o => o + offset).
                    ToArray();
            }

            var triangles = new[]
            {
                new TriangleIndexed_wpf(0, 1, 2, points),
                new TriangleIndexed_wpf(2, 3, 0, points),
            };

            return new WingDef()
            {
                Span = span,
                Chord = chord,
                Triangles = triangles,
            };
        }

        #endregion
        #region Private Methods - Get Definition 2

        //NOTE: Using unity's coords, so Y is up, Z is along fuselage

        private static PlaneDefinition GetDefaultPlane()
        {
            return new PlaneDefinition()
            {
                Engine_0 = GetDefaultEngine(),
                Wing_0 = GetDefaultWing(),
                Tail = GetDefaultTail(),
            };
        }
        private static PlaneDefinition GetRandomPlane()
        {
            int num_engines = StaticRandom.Next(1, 4);      // this will return 1 2 or 3
            int num_wings = StaticRandom.Next(1, 4);
            bool build_tail = StaticRandom.NextBool();

            return new PlaneDefinition()
            {
                Engine_0 = GetRandomEngine(),
                Engine_1 = num_engines >= 2 ? GetRandomEngine() : null,
                Engine_2 = num_engines >= 3 ? GetRandomEngine() : null,

                Wing_0 = GetRandomWing2(),
                Wing_1 = num_wings >= 2 ? GetRandomWing2() : null,
                Wing_2 = num_wings >= 3 ? GetRandomWing2() : null,

                Tail = build_tail ? GetRandomTail() : null,
            };
        }

        private static EngineDefinition GetDefaultEngine()
        {
            return new EngineDefinition()
            {
                Offset = new Vector3(0, 0, 1),
                Rotation = Quaternion.Identity,
            };
        }
        private static EngineDefinition GetRandomEngine()
        {
            Random rand = StaticRandom.GetRandomForThread();

            // x = 0 will be along the centerline
            // x = pos with push to the right (don't use negative)

            // y = neg will be below (positive will be above)

            // z = neg will push toward back of plane (postive will push forward)


            // don't want engine to point backward or straight down, but allow a good range between forward and up with a little
            // beyond (up and slight back, forward and slight down)
            double pitch = rand.NextDouble(-10, 100);

            return new EngineDefinition()
            {
                Size = (float)rand.NextDouble(0.4, 1.6),

                Offset = new Vector3
                (
                    (float)rand.NextDouble(0, 2.5),
                    (float)rand.NextDouble(-1, 1),
                    (float)rand.NextDouble(-0.5, 1.75)
                ),

                Rotation = GetRotation(pitch: pitch),
            };
        }

        private static WingDefinition GetDefaultWing()
        {
            return new WingDefinition()
            {
                Offset = new Vector3(0.25f, 0, 0.5f),
                Rotation = Quaternion.Identity,
            };
        }
        private static WingDefinition GetRandomWing2()
        {
            Random rand = StaticRandom.GetRandomForThread();

            // segment inner count
            //  0 to 4

            // span is the main one
            //  2 is a good value
            //  4 should probably be max

            // span power
            //  1 is linear
            //  < 1 has most of the wing being base cord
            //  > 1 has most being tip cord
            //  1/4 to 4 is a good range

            // cord base / tip
            //  don't let tip be > base
            //  0.2 to 0.8
            const double CHORD_MIN = 0.2;
            const double CHORD_MAX = 0.8;
            double chord_base = rand.NextDouble(CHORD_MIN, CHORD_MAX);
            double chord_tip = rand.NextDouble(CHORD_MIN, CHORD_MAX);
            UtilityMath.MinMax(ref chord_tip, ref chord_base);

            // cord power
            //  I'm not sure exactly what this is
            //  just do 1/4 to 4

            // lift base / tip
            //  0 to 1.25 ???

            // Vertical stabalizer base / tip
            // 0 to 0.5
            double vert_base_percent = rand.NextPow(12);        // very little chance of having vertical stabalizers at the base
            double vert_base_height = rand.NextDouble(0.15, 0.5);

            double vert_tip_percent = rand.NextPow(4);          // better chance of vert stabalizer at tip
            double vert_tip_height = rand.NextDouble(0.15, 0.5);

            // Vertical stabalizer power
            //  1/32 to 32


            // random pitch tilt should be fairly small
            // random sweep could be larger (yaw)
            // random rotation about chassis axis should be moderate (roll)
            double pitch = 10 * rand.NextPow(16, isPlusMinus: true);        // by running it through a power, there's a higher chance of it being zero (especially such a large power)
            double yaw = 30 * rand.NextPow(4, isPlusMinus: true);
            double roll = 20 * rand.NextPow(6, isPlusMinus: true);


            return new WingDefinition()
            {
                Inner_Segment_Count = rand.Next(0, 5),

                Span = (float)rand.NextDouble(0.5, 4),
                Span_Power = (float)rand.NextPercent(1, 4),     // 1/4 to 4, but even chance between (1/4 to 1) and (1 to 4)

                Chord_Base = (float)chord_base,
                Chord_Tip = (float)chord_tip,
                Chord_Power = (float)rand.NextPercent(1, 4),

                VerticalStabilizer_Base = (float)(vert_base_height * vert_base_percent),
                VerticalStabilizer_Tip = (float)(vert_tip_height * vert_tip_percent),
                VerticalStabilizer_Power = (float)rand.NextPercent(1, 32),      // at the extremes, this would pretty much just make one stabalizer (x^1/32: base, x^32: tip)

                Offset = new Vector3
                (
                    (float)rand.NextDouble(0.25, 1),
                    (float)rand.NextDouble(-1, 1),
                    (float)rand.NextDouble(-1.2, 1.2)
                ),

                Rotation = GetRotation(roll, pitch, yaw),
            };
        }

        private static TailDefinition GetDefaultTail()
        {
            return new TailDefinition()
            {
                Boom = new TailDefinition_Boom(),
                Tail = new TailDefinition_Tail(),

                Offset = new Vector3(0, 0, -0.25f),
                Rotation = Quaternion.Identity,
            };
        }
        private static TailDefinition GetRandomTail()
        {
            const double SPAN_MIN = 0.05;
            const double SPAN_MAX = 0.5;
            const double VERT_MIN = 0.05;
            const double VERT_MAX = 0.5;

            Random rand = StaticRandom.GetRandomForThread();

            // segment inner count
            //  0 to 6

            // length
            //  1.5 is standard
            //  3 is max
            double length = (float)rand.NextDouble(0.25, 3);

            // length power
            //  1/4 to 4

            // mid length
            //  0 would be pinched up near base
            //  length would be pinched at tail
            //  length/100 to 1-length/100

            double mid_length_margin = length / 100;
            double mid_length = rand.NextDouble(mid_length_margin, length - mid_length_margin);

            // span base,mid,tip
            //  how wide it is

            // vert base,mid,tip
            //  this should have the same range of values as mids

            // bezier pinch percent
            //  doesn't seem to do much
            //  comment says 0 to 0.4, just do that


            // make the tail optional

            // tail coord
            //  0.15 to 0.85

            // tail horz span
            //  0.25 to 2

            // tail vert height
            //  0.15 to 1.5



            // Offset
            //  X: 0 or 0.5 to 1.5
            //  Y: -1 to 1
            //  Z: 0 to -1.25


            // Rotataion
            //  X axis only, -10 to 10
            double pitch = 10 * rand.NextPow(16, isPlusMinus: true);        // by running it through a power, there's a higher chance of it being zero (especially such a large power)


            return new TailDefinition()
            {
                Boom = new TailDefinition_Boom()
                {
                    Inner_Segment_Count = rand.Next(0, 7),

                    Length = (float)length,
                    Length_Power = (float)rand.NextPercent(1, 4),

                    Mid_Length = (float)mid_length,

                    Span_Base = (float)rand.NextDouble(SPAN_MIN, SPAN_MAX),
                    Span_Mid = (float)rand.NextDouble(SPAN_MIN, SPAN_MAX),
                    Span_Tip = (float)rand.NextDouble(SPAN_MIN, SPAN_MAX),

                    Vert_Base = (float)rand.NextDouble(VERT_MIN, VERT_MAX),
                    Vert_Mid = (float)rand.NextDouble(VERT_MIN, VERT_MAX),
                    Vert_Tip = (float)rand.NextDouble(VERT_MIN, VERT_MAX),

                    Bezier_PinchPercent = (float)rand.NextDouble(0, 0.4),
                },

                Tail = rand.NextDouble() > 0.8 ?        // have a small chance of no tip to the tail (just the boom)
                    null :
                    new TailDefinition_Tail()
                    {
                        Chord = (float)rand.NextDouble(0.15, 0.85),
                        Horz_Span = (float)rand.NextDouble(0.25, 2),
                        Vert_Height = (float)rand.NextDouble(0.15, 1.5),
                    },

                Offset = new Vector3
                (
                    rand.NextDouble() > 0.8 ?
                        0f :
                        (float)rand.NextDouble(0.5, 1.5),
                    (float)rand.NextDouble(-1, 1),
                    (float)rand.NextDouble(-1.25, 0)
                ),

                Rotation = GetRotation(pitch: pitch),
            };
        }

        // Angles are in degrees
        private static Quaternion GetRotation(double? roll = null, double? pitch = null, double? yaw = null)
        {
            // Using this to match the way unity works
            var quat_numerics = System.Numerics.Quaternion.CreateFromYawPitchRoll((float)(yaw ?? 0), (float)(pitch ?? 0), (float)(roll ?? 0));

            return new Quaternion(quat_numerics.X, quat_numerics.Y, quat_numerics.Z, quat_numerics.W);
        }

        #endregion
        #region Private Methods

        private static (Rect3D rect, Point3D center)[] GetIntersectingCells(WingDef[] wings, double cell_size)
        {
            var aabb = Math3D.GetAABB(wings.SelectMany(o => o.Triangles));

            var num_cells = GetNumCells(aabb.min, aabb.max, cell_size);

            var cells = Math3D.GetCells(cell_size, num_cells.x, num_cells.y, num_cells.z);

            Vector3D offset = aabb.min.ToVector();

            cells = cells.
                Select(o =>
                (
                    Rect3D.Offset(o.rect, offset),
                    o.center + offset
                )).
                ToArray();

            return cells;
        }

        private static WingAABB GetWingAABB(WingDef wing, double cell_size)
        {
            var aabb = Math3D.GetAABB(wing.Triangles);

            var num_cells = GetNumCells(aabb.min, aabb.max, cell_size);

            //var aabb_cell_aligned = 




            return new WingAABB()
            {
                Wing = wing,
                CellSize = cell_size,
            };
        }

        private static (int x, int y, int z) GetNumCells(Point3D min, Point3D max, double cell_size)
        {
            return
            (
                ((max.X - min.X) / cell_size).ToInt_Ceiling(),
                ((max.Y - min.Y) / cell_size).ToInt_Ceiling(),
                ((max.Z - min.Z) / cell_size).ToInt_Ceiling()
            );
        }

        private static Point3D ToPoint(VectorInt3 index, SparseCellGrid grid, bool is_bottomright = false)
        {
            var cell = grid.GetCell(index);

            return is_bottomright ?
                cell.rect.Location + cell.rect.Size.ToVector() :
                cell.rect.Location;
        }

        private static Point3D ToPositive(Point3D point)
        {
            return new Point3D(Math.Abs(point.X), Math.Abs(point.Y), Math.Abs(point.Z));
        }

        #endregion

        // ----------- function -----------
        // create a wing (plate) with random offset/rotation
        // find the aabb
        // see with grid cells intersect the plate
        // draw plate and touching cells



        // ----------- function -----------
        // define a wing on XZ plane (no offset/rotation)
        // find grid cells for this plate
        //
        // define a couple more wings with random offset/rotation
        // find grid cells for those
        //
        // so one grid set for primary wing, another for all other wings
        //
        // walk through each cell in primary and find cells in other for infront/above/below
    }
}
