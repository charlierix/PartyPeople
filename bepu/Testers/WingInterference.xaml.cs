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

                distinct_lines = Math3D.GetDistinctLineSegments(cell_lines);

                window.AddLines(distinct_lines.index_pairs, distinct_lines.all_points_distinct, sizes.line / 4, Colors.Wheat);





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
                var grid = new SparseCellGrid(CELL_SIZE);

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
                var marked_triangle = grid.Mark_Triangle(triangle, true);

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

                //var marked_sphere = grid.GetMarked_Sphere(center, radius2, true);
                var marked_sphere = grid.GetMarked_Sphere(center, radius2, false);

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
