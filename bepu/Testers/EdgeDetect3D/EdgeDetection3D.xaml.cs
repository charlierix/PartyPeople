﻿using Game.Core;
using Game.Core.Threads;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.FileHandlers3D;
using Game.Math_WPF.WPF.Viewers;
using NetOctree.Octree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers.EdgeDetect3D
{
    public partial class EdgeDetection3D : Window
    {
        #region record: FileAnalyzed

        private record FileAnalyzed
        {
            // These are the inputs
            public string Filename { get; init; }
            public Obj_File ParsedFile { get; init; }

            // These are the results
            public ObjectAnalyzed[] Objects { get; init; }
        }

        #endregion
        #region record: ObjectAnalyzed

        private record ObjectAnalyzed
        {
            public Obj_Object ParsedObject { get; init; }

            // edges

            // octree<trianglelinked>
            // octree<edge>

        }

        #endregion

        #region Declaration Section

        private TrackBallRoam _trackball = null;

        private readonly DropShadowEffect _errorEffect;

        private string _filename = null;
        private Obj_File _parsed_file = null;
        // Linked triangles, edges, stored in octrees (populated from background task)
        private EdgeBackgroundWorker.WorkerResponse _edges = null;

        private readonly BackgroundTaskWorker<EdgeBackgroundWorker.WorkerRequest, EdgeBackgroundWorker.WorkerResponse> _backgroundWorker;

        private List<Visual3D> _visuals = new List<Visual3D>();
        private List<Visual3D> _stroke_visuals = new List<Visual3D>();
        private List<Point3D> _stroke_points = new List<Point3D>();
        private bool _dragging_stroke = false;

        private double _stroke_thickness = 0;

        #endregion

        #region Constructor

        public EdgeDetection3D()
        {
            InitializeComponent();

            _trackball = new TrackBallRoam(_camera);
            _trackball.EventSource = grdViewPort;       //NOTE:  If this control doesn't have a background color set, the trackball won't see events (I think transparent is ok, just not null)
            _trackball.AllowZoomOnMouseWheel = true;
            _trackball.Mappings.AddRange(TrackBallMapping.GetPrebuilt(TrackBallMapping.PrebuiltMapping.MouseComplete_NoLeft));
            _trackball.ShouldHitTestOnOrbit = true;

            _errorEffect = new DropShadowEffect()
            {
                Color = UtilityWPF.ColorFromHex("C02020"),
                Direction = 0,
                ShadowDepth = 0,
                BlurRadius = 8,
                Opacity = .8,
            };

            txtObjFile.Effect = _errorEffect;       // textchange sets this, but the first time must be happening before window load event

            _backgroundWorker = new BackgroundTaskWorker<EdgeBackgroundWorker.WorkerRequest, EdgeBackgroundWorker.WorkerResponse>(EdgeBackgroundWorker.DoWork, FinishedBackgroundWork, ExceptionBackgroundWork);
        }

        #endregion

        #region Event Listeners

        private void grdViewPort_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // When the are editing a textbox and click in the editor area, focus doesn't change without some assistance
            grdViewPort.Focus();
        }
        private void grdViewPort_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (_edges == null || _edges.AABB_DiagLen.IsNearZero())
                    return;

                _viewport.Children.RemoveAll(_stroke_visuals);
                _stroke_visuals.Clear();
                _stroke_points.Clear();

                Point3D? point = RayCast(e);
                if (point != null)
                    _stroke_points.Add(point.Value);

                var sizes = Debug3DWindow.GetDrawSizes(_edges.AABB_DiagLen);
                _stroke_thickness = sizes.line;

                _dragging_stroke = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void grdViewPort_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (_dragging_stroke && _stroke_points != null)
                    StrokeAnalyzer.Stroke(_stroke_points.ToArray(), _edges);

                _viewport.Children.RemoveAll(_stroke_visuals);
                _stroke_visuals.Clear();
                _stroke_points.Clear();

                _dragging_stroke = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void grdViewPort_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (!_dragging_stroke)
                    return;

                Point3D? point = RayCast(e);
                if (point == null)
                    return;

                if (_stroke_points.Count > 0)
                {
                    //var line_segment = Debug3DWindow.GetLine(_stroke_points[^1], point.Value, _stroke_thickness, Colors.Orange);
                    var line_segment = Debug3DWindow.GetLines_Flat([_stroke_points[^1], point.Value], 4, Colors.Orange, true);
                    _stroke_visuals.Add(line_segment);
                    _viewport.Children.Add(line_segment);
                }

                _stroke_points.Add(point.Value);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtObjFile_PreviewDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }
        private void txtObjFile_Drop(object sender, DragEventArgs e)
        {
            try
            {
                string[] filenames = e.Data.GetData(DataFormats.FileDrop) as string[];

                if (filenames == null || filenames.Length == 0)
                {
                    MessageBox.Show("No files selected", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (filenames.Length > 1)
                {
                    MessageBox.Show("Only one file allowed", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                txtObjFile.Text = filenames[0];
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void txtObjFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (txtObjFile.Text.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                {
                    if (_filename != null && _filename.EndsWith(txtObjFile.Text, StringComparison.OrdinalIgnoreCase))
                    {
                        // textbox is showing the filename portion without folder, ignore this
                    }
                    else if (LoadFile(txtObjFile.Text))
                    {
                        _filename = txtObjFile.Text;
                        lblObjFile.Visibility = Visibility.Collapsed;
                        txtObjFile.Effect = null;
                        txtObjFile.Text = System.IO.Path.GetFileName(txtObjFile.Text);
                        txtObjFile.ToolTip = _filename;
                    }
                    else
                    {
                        UnloadFile();
                        _filename = null;
                        lblObjFile.Visibility = Visibility.Visible;
                        txtObjFile.Effect = _errorEffect;
                        txtObjFile.ToolTip = null;
                    }
                }
                else
                {
                    UnloadFile();
                    _filename = null;
                    lblObjFile.Visibility = Visibility.Visible;
                    txtObjFile.Effect = _errorEffect;
                    txtObjFile.ToolTip = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IndexedTriangles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_parsed_file == null)
                {
                    MessageBox.Show("Need to load a .obj file first", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                foreach (var obj in _parsed_file.Objects)
                {
                    var triangles = Obj_Util.ToTrianglesIndexed(obj);

                    if (triangles.Length == 0)
                        continue;

                    var sizes = Debug3DWindow.GetDrawSizes(triangles[0].AllPoints);

                    // Faces
                    var window = new Debug3DWindow()
                    {
                        Title = "triangle faces",
                    };

                    window.AddHull(triangles, UtilityWPF.ColorFromHex("CCC"));

                    var normals = triangles.
                        Select(o =>
                        {
                            var center = o.GetCenterPoint();
                            return (center, center + (o.Normal * 100));
                        });

                    window.AddLines_Flat(normals, 1, Colors.DarkOliveGreen);

                    window.Show();

                    // Edges
                    window = new Debug3DWindow()
                    {
                        Title = "triangle edges",
                    };

                    window.AddHull(triangles, null, UtilityWPF.ColorFromHex("888"), sizes.line * 0.2);

                    window.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Octree_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_parsed_file == null)
                {
                    MessageBox.Show("Need to load a .obj file first", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                foreach (var obj in _parsed_file.Objects)
                {
                    var triangles = Obj_Util.ToTrianglesIndexed(obj);

                    if (triangles.Length == 0)
                        continue;

                    var triangles_linked = triangles.
                        Select(o => new TriangleIndexedLinked_wpf(o.Index0, o.Index1, o.Index2, o.AllPoints)).
                        ToArray();


                    var aabb = Math3D.GetAABB(triangles[0].AllPoints);
                    double diag_len = (aabb.max - aabb.min).Length;

                    var tree = CreateOctreeWithTriangles(triangles_linked);

                    var window = new Debug3DWindow()
                    {
                        Title = "octree - all cells",
                    };

                    var sizes2 = Debug3DWindow.GetDrawSizes(diag_len);

                    var cells = tree.GetAllUsedNodes();

                    // with 5000 cells, this was taking way too long (it would probably have taken many minutes or longer)
                    //Color[] cell_colors = UtilityWPF.GetRandomColors(cells.Length, 128, 210);

                    var triangle_counts = new List<int>();

                    for (int i = 0; i < cells.Length; i++)
                    {
                        var cell_triangles = cells[i].GetItems_ThisNodeOnly();
                        triangle_counts.Add(cell_triangles.Length);

                        Color cell_color = UtilityWPF.GetRandomColor(128, 210);

                        if (cell_triangles.Length > 0)
                            window.AddHull(cell_triangles, cell_color);

                        if (chkOctreeLines.IsChecked.Value)
                        {
                            var boundary_lines = Polytopes.GetCubeLines(cells[i].Bounds.Min.ToPoint_wpf(), cells[i].Bounds.Max.ToPoint_wpf());

                            //window.AddLines(boundary_lines, sizes2.line * 0.1, cell_color);
                            window.AddLines_Flat(boundary_lines, 1, cell_color);
                        }
                    }

                    var avg_stddev = Math1D.Get_Average_StandardDeviation(triangle_counts);

                    window.AddText($"Number of nodes: {cells.Length:N0}");
                    window.AddText("");
                    window.AddText($"Min # triangles: {triangle_counts.Min():N0}");
                    window.AddText($"Max # triangles: {triangle_counts.Max():N0}");
                    window.AddText("");
                    window.AddText($"Avg # triangles: {avg_stddev.avg:N0}");
                    window.AddText($"Std Dev: {avg_stddev.stdDev:N0}");

                    // TODO: give depth stats
                    // There doesn't appear to be any functions, so a custom function would be needed to recurse from tree.root

                    window.Show();

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void OctreeNodeTouching_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_parsed_file == null)
                {
                    MessageBox.Show("Need to load a .obj file first", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                foreach (var obj in _parsed_file.Objects)
                {
                    var triangles = Obj_Util.ToTrianglesIndexed(obj);

                    if (triangles.Length == 0)
                        continue;

                    var triangles_linked = triangles.
                        Select(o => new TriangleIndexedLinked_wpf(o.Index0, o.Index1, o.Index2, o.AllPoints)).
                        ToArray();

                    var tree = CreateOctreeWithTriangles(triangles_linked);

                    var cells = tree.GetAllUsedNodes();

                    // Pick a random one
                    var selected_cell = cells[StaticRandom.Next(cells.Length)];

                    // Get used nodes that are touching this node
                    var touching_cells = tree.GetTouchingUsedNodes(selected_cell);


                    var window = new Debug3DWindow()
                    {
                        Title = "octree - selected cell",
                        Trackball_ShouldHitTestOnOrbit = true,
                    };

                    var cells_aabb = Math3D.GetAABB(selected_cell.GetItems_ThisNodeOnly().Concat(touching_cells.SelectMany(o => o.GetItems_ThisNodeOnly())));
                    var sizes = Debug3DWindow.GetDrawSizes((cells_aabb.max - cells_aabb.min).Length);

                    Color[] cell_colors = UtilityWPF.GetRandomColors(touching_cells.Length, 192, 230);
                    Color selected_color = Colors.Goldenrod;

                    // Selected Cell
                    var cell_triangles = selected_cell.GetItems_ThisNodeOnly();

                    if (cell_triangles.Length > 0)
                        window.AddHull(cell_triangles, selected_color);

                    var boundary_lines = Polytopes.GetCubeLines(selected_cell.Bounds.Min.ToPoint_wpf(), selected_cell.Bounds.Max.ToPoint_wpf());
                    window.AddLines_Flat(boundary_lines, 2, selected_color);

                    // Touching Cells
                    for (int i = 0; i < touching_cells.Length; i++)
                    {
                        var touching_triangles = touching_cells[i].GetItems_ThisNodeOnly();

                        if (touching_triangles.Length > 0)
                            window.AddHull(touching_triangles, cell_colors[i]);

                        var boundary_lines2 = Polytopes.GetCubeLines(touching_cells[i].Bounds.Min.ToPoint_wpf(), touching_cells[i].Bounds.Max.ToPoint_wpf());
                        //window.AddLines(boundary_lines2, sizes.line, cell_colors[i]);
                        window.AddLines_Flat(boundary_lines2, 1, cell_colors[i]);
                    }

                    window.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void TrianglesByEdge_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_parsed_file == null)
                {
                    MessageBox.Show("Need to load a .obj file first", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                foreach (var obj in _parsed_file.Objects)
                {
                    var triangles = Obj_Util.ToTrianglesIndexed(obj);

                    if (triangles.Length == 0)
                        continue;

                    //var tri2 = TriangleIndexedLinked_wpf.ConvertToLinked(triangles, true, true);
                    var tri2 = TriangleIndexedLinked_wpf.ConvertToLinked(triangles, true, false);



                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void NormalDot_POC_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_parsed_file == null)
                {
                    MessageBox.Show("Need to load a .obj file first", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                foreach (var obj in _parsed_file.Objects)
                {
                    var triangles_fromobj = Obj_Util.ToTrianglesIndexed(obj);

                    if (triangles_fromobj.Length == 0)
                        continue;

                    var (triangles, by_edge) = TriangleIndexedLinked_wpf.ConvertToLinked(triangles_fromobj, true, false);

                    var dot_diffs = by_edge.EdgePairs.
                        Select(o => EdgeUtil.GetNormalDot(o)).
                        ToArray();

                    var grouped = dot_diffs.
                        ToLookup(o => o.Direction).
                        Select(o => new
                        {
                            dir = o.Key,
                            edges = o.
                                OrderBy(p => p.Dot).
                                ToArray(),
                        }).
                        ToArray();

                    var drawExample = new Action<NormalDot>(nd =>
                    {
                        var window = new Debug3DWindow()
                        {
                            Title = nd.Direction.ToString(),
                        };

                        var aabb = Math3D.GetAABB([nd.Edge.Triangle0, nd.Edge.Triangle1]);
                        Point3D center = Math3D.GetCenter(aabb.min, aabb.max);

                        var sizes = Debug3DWindow.GetDrawSizes([aabb.min, aabb.max], center);

                        var triangle0 = new Triangle_wpf(nd.Edge.Triangle0.Point0 - center.ToVector(), nd.Edge.Triangle0.Point1 - center.ToVector(), nd.Edge.Triangle0.Point2 - center.ToVector());
                        var triangle1 = new Triangle_wpf(nd.Edge.Triangle1.Point0 - center.ToVector(), nd.Edge.Triangle1.Point1 - center.ToVector(), nd.Edge.Triangle1.Point2 - center.ToVector());

                        window.AddTriangle(triangle0, Colors.Linen);
                        window.AddTriangle(triangle1, Colors.Linen);

                        window.AddLine(triangle0.GetCenterPoint(), triangle0.GetCenterPoint() + triangle0.Normal, sizes.line, Colors.Orange);
                        window.AddLine(triangle1.GetCenterPoint(), triangle1.GetCenterPoint() + triangle1.Normal, sizes.line, Colors.Orange);

                        window.AddText($"dot: {nd.Dot}");

                        window.Show();
                    });


                    // Most of the edge pairs are nearly flat
                    //foreach(var group in grouped)
                    //    foreach(int index in UtilityCore.RandomRange(0, group.edges.Length, 3))
                    //        drawExample(group.edges[index]);


                    // Picking the midpoint doesn't work either, since the interesting parts aren't right in the middle of the set
                    //foreach (var group in grouped)
                    //    drawExample(group.edges[group.edges.Length / 2]);


                    // AddGraph tries to draw all the points.  It would look like a near vertical step from -1 to 1 anyway
                    //foreach (var group in grouped)
                    //{
                    //    var window = new Debug3DWindow()
                    //    {
                    //        Title = group.dir.ToString(),
                    //    };

                    //    var graph = Debug3DWindow.GetGraph(group.edges.Select(o => o.Dot).ToArray());
                    //    window.AddGraph(graph, new Point3D(), 1);

                    //    window.Show();
                    //}


                    foreach (var group in grouped)
                    {
                        // Group the list of samples into sets of dot product range
                        double step = 0.15;
                        for (double target = -1d; target <= 1d; target += step)
                        {
                            var inrange = group.edges.
                                Where(o => Math.Abs(o.Dot - target) <= step / 2).
                                ToArray();

                            if (inrange.Length > 0)
                                drawExample(inrange[StaticRandom.Next(inrange.Length)]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void NormalDot_Isolated_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_parsed_file == null)
                {
                    MessageBox.Show("Need to load a .obj file first", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                foreach (var obj in _parsed_file.Objects)
                {
                    var triangles_fromobj = Obj_Util.ToTrianglesIndexed(obj);

                    if (triangles_fromobj.Length == 0)
                        continue;

                    var (triangles, by_edge) = TriangleIndexedLinked_wpf.ConvertToLinked(triangles_fromobj, true, false);

                    // May also want to throw out tiny triangles

                    var dot_diffs = by_edge.EdgePairs.
                        Select(o => EdgeUtil.GetNormalDot(o)).
                        Where(o => o.Dot < 0.97).       // throw out the mostly parallel joins
                        ToArray();

                    var grouped = dot_diffs.
                        ToLookup(o => o.Direction).
                        Select(o => new
                        {
                            dir = o.Key,
                            edges = o.
                                OrderBy(p => p.Dot).
                                ToArray(),
                        }).
                        ToArray();

                    var drawScene = new Action<string, bool, Color, NormalDot[]>((title, show_hull, max_color, nd) =>
                    {
                        var window = new Debug3DWindow()
                        {
                            Title = title,
                            Background = UtilityWPF.BrushFromHex("000"),
                            Width = 1500,
                            Height = 1500,
                        };

                        if (show_hull)
                            window.AddHull(triangles, UtilityWPF.ColorFromHex("000"), isIndependentFaces: false);

                        // Draw the lines in sets (should reduce the total number of visuals)
                        double step = 0.05;
                        for (double target = -1d; target <= 1d; target += step)
                        {
                            var inrange = nd.
                                Where(o => Math.Abs(o.Dot - target) <= step / 2).
                                ToArray();

                            Color color = UtilityWPF.AlphaBlend(max_color, Colors.Black, Math.Pow(UtilityMath.GetScaledValue_Capped(0, 1, 1, -1, target), 0.66));

                            if (inrange.Length > 0)
                                window.AddLines_Flat(inrange.Select(o => (by_edge.AllPoints[o.Edge.EdgeIndex0], by_edge.AllPoints[o.Edge.EdgeIndex1])), 2, color);
                        }

                        window.Show();
                    });


                    foreach (var group in grouped)
                    {
                        drawScene(group.dir.ToString(), true, Colors.Chartreuse, group.edges);
                        drawScene(group.dir.ToString(), false, Colors.Chartreuse, group.edges);
                        drawScene(group.dir.ToString(), false, Colors.White, group.edges);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void NormalDot_Combined_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_parsed_file == null)
                {
                    MessageBox.Show("Need to load a .obj file first", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                foreach (var obj in _parsed_file.Objects)
                {
                    var triangles_fromobj = Obj_Util.ToTrianglesIndexed(obj);

                    if (triangles_fromobj.Length == 0)
                        continue;

                    var (triangles, by_edge) = TriangleIndexedLinked_wpf.ConvertToLinked(triangles_fromobj, true, false);

                    // May also want to throw out tiny triangles

                    var dot_diffs = by_edge.EdgePairs.
                        Select(o => EdgeUtil.GetNormalDot(o)).
                        Where(o => o.Dot < 0.97).       // throw out the mostly parallel joins
                        ToArray();

                    // Delegate to draw a scene
                    var drawScene = new Action<bool, Color, Color, NormalDot[]>((show_hull, color_valley, color_peak, nd) =>
                    {
                        var window = new Debug3DWindow()
                        {
                            //Title = title,
                            Background = UtilityWPF.BrushFromHex("000"),
                            Width = 1500,
                            Height = 1500,
                        };

                        if (show_hull)
                            window.AddHull(triangles, UtilityWPF.ColorFromHex("000"), isIndependentFaces: false);

                        // Delegate to draw either peak or valley lines
                        var drawLines = new Action<Color, TriangleFoldDirection>((max_color, dir) =>
                        {
                            double step = 0.05;
                            for (double target = -1d; target <= 1d; target += step)     // Draw the lines in sets (should reduce the total number of visuals)
                            {
                                var inrange = nd.
                                    Where(o => o.Direction == dir && Math.Abs(o.Dot - target) <= step / 2).
                                    ToArray();

                                Color color = UtilityWPF.AlphaBlend(max_color, Colors.Black, Math.Pow(UtilityMath.GetScaledValue_Capped(0, 1, 1, -1, target), 0.66));

                                if (inrange.Length > 0)
                                    window.AddLines_Flat(inrange.Select(o => (by_edge.AllPoints[o.Edge.EdgeIndex0], by_edge.AllPoints[o.Edge.EdgeIndex1])), 2, color);
                            }
                        });

                        drawLines(color_valley, TriangleFoldDirection.Valley);
                        drawLines(color_peak, TriangleFoldDirection.Peak);

                        window.Show();
                    });

                    drawScene(true, Colors.White, Colors.Chartreuse, dot_diffs);
                    drawScene(false, Colors.White, Colors.Chartreuse, dot_diffs);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private void UnloadFile()
        {
            if (_backgroundWorker != null)
                _backgroundWorker.Cancel();

            _dragging_stroke = false;
            _viewport.Children.RemoveAll(_stroke_visuals);
            _stroke_visuals.Clear();
            _stroke_points.Clear();

            _viewport.Children.RemoveAll(_visuals);
            _visuals.Clear();

            if (lblStats_Mesh != null)
                lblStats_Mesh.Content = "";

            if (lblAnalyzing != null)
                lblAnalyzing.Visibility = Visibility.Collapsed;

            if (lblAnalyzeError != null)
                lblAnalyzeError.Visibility = Visibility.Collapsed;

            _parsed_file = null;
            _edges = null;
        }
        private bool LoadFile(string filename)
        {
            if (!File.Exists(filename))
                return false;

            UnloadFile();

            _parsed_file = Obj_FileReaderWriter.ReadFile(filename);

            foreach (var obj in _parsed_file.Objects)
            {
                _visuals.Add(new ModelVisual3D()
                {
                    Content = Obj_Util.ToModel3D(obj),
                });

                _viewport.Children.Add(_visuals[^1]);
            }

            StringBuilder report = new StringBuilder();
            report.AppendLine($"{_parsed_file.Objects.Length:N0} object{(_parsed_file.Objects.Length == 1 ? "" : "s")}");
            report.AppendLine($"{_parsed_file.Objects.Sum(o => o.Vertices.Length):N0} vertices");
            report.AppendLine($"{_parsed_file.Objects.Sum(o => o.Faces.Length):N0} faces");

            int triangle_count = _parsed_file.Objects.
                SelectMany(o => o.Faces).
                Select(o => o.Points.Length == 3 ? 1 : o.Points.Length == 4 ? 2 : 0).
                Sum();

            report.AppendLine($"{triangle_count:N0} triangles");

            lblStats_Mesh.Content = report.ToString();

            AimCamera();

            lblAnalyzing.Visibility = Visibility.Visible;

            _backgroundWorker.Start(new EdgeBackgroundWorker.WorkerRequest()
            {
                Filename = filename,
                ParsedFile = _parsed_file,
            });

            return true;
        }

        private void FinishedBackgroundWork(EdgeBackgroundWorker.WorkerRequest request, EdgeBackgroundWorker.WorkerResponse response)
        {
            lblAnalyzing.Visibility = Visibility.Collapsed;
            _edges = response;
        }
        private void ExceptionBackgroundWork(EdgeBackgroundWorker.WorkerRequest request, Exception ex)
        {
            lblAnalyzing.Visibility = Visibility.Collapsed;
            lblAnalyzeError.Content = ex.Message;
            lblAnalyzeError.Visibility = Visibility.Visible;
        }

        private Point3D? RayCast(MouseEventArgs e)
        {
            // Fire a ray at the mouse point
            Point clickPoint = e.GetPosition(grdViewPort);

            List<MyHitTestResult> hits = UtilityWPF.CastRay(out RayHitTestParameters clickRay, clickPoint, grdViewPort, _camera, _viewport, false, _stroke_visuals);

            return hits.Count > 0 ?
                hits[0].Point :
                null;
        }

        // Copied from Debug3DWindow
        private void AimCamera()
        {
            Point3D[] points = TryGetVisualPoints(_visuals);

            Tuple<Point3D, Vector3D, Vector3D> cameraPos = GetCameraPosition(points);      // this could return null
            if (cameraPos == null)
                cameraPos = Tuple.Create(new Point3D(0, 0, 7), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0));

            _camera.Position = cameraPos.Item1;
            _camera.LookDirection = cameraPos.Item2;
            _camera.UpDirection = cameraPos.Item3;

            double distance = _camera.Position.ToVector().Length;
            double scale = distance * .0214;

            _trackball.PanScale = scale / 10;
            _trackball.ZoomScale = scale;
            //_trackball.MouseWheelScale = distance * .0007;
            _trackball.MouseWheelScale = distance * .00005;
        }
        private static Tuple<Point3D, Vector3D, Vector3D> GetCameraPosition(Point3D[] points)
        {
            if (points == null || points.Length == 0)
                return null;

            else if (points.Length == 1)
                return Tuple.Create(new Point3D(points[0].X, points[0].Y, points[0].Z + 7), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0));

            Point3D center = Math3D.GetCenter(points);

            double[] distances = points.
                Select(o => (o - center).Length).
                ToArray();

            //TODO: Use this instead
            //Math1D.Get_Average_StandardDeviation(distances);

            double avgDist = distances.Average();
            double maxDist = distances.Max();

            double threeQuarters = UtilityMath.GetScaledValue(avgDist, maxDist, 0, 1, .75);

            double cameraDist = threeQuarters * 2.5;

            // Set camera to look at center, at a distance of X times average
            return Tuple.Create(new Point3D(center.X, center.Y, center.Z + cameraDist), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0));
        }

        private static Point3D[] TryGetVisualPoints(IEnumerable<Visual3D> visuals)
        {
            IEnumerable<Point3D> retVal = new Point3D[0];

            foreach (Visual3D visual in visuals)
            {
                Point3D[] points = null;
                try
                {
                    if (visual is ModelVisual3D)
                    {
                        ModelVisual3D visualCast = (ModelVisual3D)visual;
                        points = UtilityWPF.GetPointsFromMesh(visualCast.Content);        // this throws an exception if it doesn't know what kind of model it is
                    }
                }
                catch (Exception)
                {
                    points = null;
                }

                if (points != null)
                    retVal = retVal.Concat(points);
            }

            return retVal.ToArray();
        }

        // Populates an octree with triangles.  The larger the divider, the smaller the node sizes can be
        // TODO: don't ask for divider size.  Calculate based on triangle count
        private static BoundsOctree<T> CreateOctreeWithTriangles<T>(T[] triangles, int size_divider = 150) where T : ITriangleIndexed_wpf
        {
            var aabb = Math3D.GetAABB(triangles[0].AllPoints);


            // It is consistently off center (with standing characters).  I'm guessing it's because center
            // is weighted
            //Point3D center = Math3D.GetCenter(triangles[0].AllPoints);
            Point3D center = aabb.min + ((aabb.max - aabb.min) / 2);



            //TODO: may want max of width,height,depth
            double diag_len = (aabb.max - aabb.min).Length;

            double min_size = diag_len / size_divider;

            var tree = new BoundsOctree<T>((float)diag_len, center.ToVector3(), (float)min_size, 1.25f);

            foreach (var triangle in triangles)
            {
                var tri_aabb = Math3D.GetAABB(triangle);
                Vector3 size = new Vector3((float)(tri_aabb.max.X - tri_aabb.min.X), (float)(tri_aabb.max.Y - tri_aabb.min.Y), (float)(tri_aabb.max.Z - tri_aabb.min.Z));

                tree.Add(triangle, new BoundingBox(triangle.GetCenterPoint().ToVector3(), size));
            }

            return tree;
        }

        #endregion
    }
}
