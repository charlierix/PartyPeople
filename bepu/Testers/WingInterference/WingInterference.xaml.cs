﻿using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Linq;
using System.Numerics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers.WingInterference
{
    public partial class WingInterferenceTester : Window
    {
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

        public WingInterferenceTester()
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
                //var cells = Math3D.GetCells(1, 3, 3, 3);

                var window = new Debug3DWindow()
                {
                    Title = "Cells",
                };

                var sizes = Debug3DWindow.GetDrawSizes(12);


                var cell_lines = cells.
                    SelectMany(o => Polytopes.GetCubeLines(o.rect.Location, o.rect.Location + o.rect.Size.ToVector()));

                var distinct_lines = Math3D.GetDistinctLineSegments(cell_lines);

                //window.AddLines(distinct_lines.index_pairs, distinct_lines.all_points_distinct, sizes.line / 4, Colors.White);
                foreach (var line in distinct_lines.index_pairs)
                {
                    window.AddLine(distinct_lines.all_points_distinct[line.i1], distinct_lines.all_points_distinct[line.i2], sizes.line / 4, UtilityWPF.GetRandomColor(128, 225));
                }

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

        private void Mark_Sphere(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = new SparseCellGrid(CELL_SIZE, true, true);

                Point3D center = Math3D.GetRandomVector_Spherical(12).ToPoint();
                double radius = StaticRandom.NextDouble(0.5, 1.5);
                bool is_hollow = StaticRandom.NextBool();

                VectorInt3 center_index = grid.GetIndex_Point(center);

                var marked_sphere = grid.Mark_Sphere(center, radius, is_hollow);

                var marked_sphere_pos = marked_sphere with
                {
                    MarkedCells = marked_sphere.MarkedCells.
                        Where(o => o.X >= center_index.X && o.Y >= center_index.Y && o.Z >= center_index.Z).
                        ToArray(),
                };

                var window = MarkCells_Click_Draw(marked_sphere.MarkedCells, grid, new Point3D(), null, new Rect(), 0, "Sphere", false, false, false);
                window.AddDot(center, radius, UtilityWPF.ColorFromHex("14D4"), isHiRes: true);
                window.AddText($"is hollow: {is_hollow}");

                window = MarkCells_Click_Draw(marked_sphere_pos.MarkedCells, grid, new Point3D(), null, new Rect(), 0, "Sphere", false, false, false);
                window.AddDot(center, radius, UtilityWPF.ColorFromHex("14D4"), isHiRes: true);
                window.AddText($"is hollow: {is_hollow}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Mark_Capsule(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = new SparseCellGrid(CELL_SIZE, true, true);

                Point3D point0 = Math3D.GetRandomVector_Spherical(4).ToPoint();
                Point3D point1 = Math3D.GetRandomVector_Spherical(4).ToPoint();
                double radius = StaticRandom.NextDouble(0.5, 1.5);
                bool is_hollow = StaticRandom.NextBool();

                VectorInt3 center_index = grid.GetIndex_Point(Math3D.GetCenter(point0, point1));

                var marked_capsule = grid.Mark_Capsule(point0, point1, radius, is_hollow, points_are_interior: false);

                var marked_capsule_pos = marked_capsule with
                {
                    MarkedCells = marked_capsule.MarkedCells.
                        Where(o => o.X >= center_index.X && o.Y >= center_index.Y && o.Z >= center_index.Z).
                        ToArray(),
                };

                //Vector3D dir_0to1 = (point1 - point0).ToUnit();
                //Point3D point0_extended = point0 + (dir_0to1 * -radius);
                //Point3D point1_extended = point1 + (dir_0to1 * radius);
                //var mesh = UtilityWPF.GetCapsule(24, 24, point0_extended, point1_extended, radius);     // this function uses the points at the tips instead of boundry between cylinder and dome portions

                var mesh = UtilityWPF.GetCapsule(24, 24, point0, point1, radius);     // this function uses the points at the tips instead of boundry between cylinder and dome portions

                var window = MarkCells_Click_Draw(marked_capsule.MarkedCells, grid, new Point3D(), null, new Rect(), 0, "Capsule", false, false, false);
                window.AddMesh(mesh, UtilityWPF.ColorFromHex("14D4"));
                window.AddText($"is hollow: {is_hollow}");

                window = MarkCells_Click_Draw(marked_capsule_pos.MarkedCells, grid, new Point3D(), null, new Rect(), 0, "Capsule", false, false, false);
                window.AddMesh(mesh, UtilityWPF.ColorFromHex("14D4"));
                window.AddText($"is hollow: {is_hollow}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Mark_Cylinder(object sender, RoutedEventArgs e)
        {
            try
            {
                var grid = new SparseCellGrid(CELL_SIZE, true, true);

                Point3D point0 = Math3D.GetRandomVector_Spherical(4).ToPoint();
                Point3D point1 = Math3D.GetRandomVector_Spherical(4).ToPoint();
                double radius = StaticRandom.NextDouble(0.5, 1.5);
                bool is_hollow = StaticRandom.NextBool();

                VectorInt3 center_index = grid.GetIndex_Point(Math3D.GetCenter(point0, point1));

                var marked_cylinder = grid.Mark_Cylinder(point0, point1, radius, is_hollow);

                var marked_cylinder_pos = marked_cylinder with
                {
                    MarkedCells = marked_cylinder.MarkedCells.
                        Where(o => o.X >= center_index.X && o.Y >= center_index.Y && o.Z >= center_index.Z).
                        ToArray(),
                };

                var mesh = UtilityWPF.GetCylinder(24, point0, point1, radius);

                var window = MarkCells_Click_Draw(marked_cylinder.MarkedCells, grid, new Point3D(), null, new Rect(), 0, "Cylinder", false, false, false);
                window.AddMesh(mesh, UtilityWPF.ColorFromHex("14D4"));
                window.AddText($"is hollow: {is_hollow}");

                window = MarkCells_Click_Draw(marked_cylinder_pos.MarkedCells, grid, new Point3D(), null, new Rect(), 0, "Cylinder", false, false, false);
                window.AddMesh(mesh, UtilityWPF.ColorFromHex("14D4"));
                window.AddText($"is hollow: {is_hollow}");
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
                //PlaneDefinition plane_def = GetPlaneDefinition.GetRandomPlane();
                PlaneDefinition plane_def = GetPlaneDefinition.GetDefaultPlane();

                PlaneDefinition plane_built = PlaneBuilder.BuildPlane(plane_def);

                // Look for parts intersecting each other
                // validated = Validate(plane_built);

                // Break into cells, see which cells are too close to other cells, populate wing modifiers
                // marked = MarkNearby(validated);

                var window = new Debug3DWindow()
                {
                    Title = "Random Plane",
                };

                // -------- Engines --------

                var add_engine = new Action<EngineDefinition>(def =>
                {
                    if (def != null)
                    {
                        var mesh = UtilityWPF.GetCapsule(24, 24, def.Meshes.Cylinder_From_Tip.ToPoint_wpf(), def.Meshes.Cylinder_To_Tip.ToPoint_wpf(), def.Meshes.Cylinder_Radius);
                        window.AddMesh(mesh, UtilityWPF.ColorFromHex("96BE59"));
                    }
                });

                add_engine(plane_built.Engine_0);
                add_engine(plane_built.Engine_1);
                add_engine(plane_built.Engine_2);

                // -------- Wings --------

                var add_wing = new Action<WingDefinition>(def =>
                {
                    if (def != null)
                    {
                        foreach (var triangle in def.Meshes.Triangles)
                        {
                            window.AddTriangle(triangle, UtilityWPF.ColorFromHex("B2B295"));
                        }
                    }
                });

                add_wing(plane_built.Wing_0);
                add_wing(plane_built.Wing_1);
                add_wing(plane_built.Wing_2);

                // -------- Tails --------

                var add_tail = new Action<TailDefinition>(def =>
                {
                    if (def != null)
                    {
                        foreach (var triangle in def.Boom.Meshes.Triangles)
                        {
                            window.AddTriangle(triangle, UtilityWPF.ColorFromHex("B2B295"));
                        }

                        if (def.Tail != null)
                        {
                            foreach (var triangle in def.Tail.Meshes.Triangles)
                            {
                                window.AddTriangle(triangle, UtilityWPF.ColorFromHex("B2B295"));
                            }
                        }
                    }
                });

                add_tail(plane_built.Tail);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

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
