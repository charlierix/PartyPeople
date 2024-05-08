using BepuPhysics.Collidables;
using Game.Core;
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
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers.EdgeDetect3D
{
    public partial class EdgeDetection3D : Window
    {
        #region Declaration Section

        private TrackBallRoam _trackball = null;

        private readonly DropShadowEffect _errorEffect;

        private string _filename = null;
        private Obj_File _parsed_file = null;
        //TODO: populate an octree, linked triangles, triangles by edge - on a background thread

        private List<Visual3D> _visuals = new List<Visual3D>();

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
        }

        #endregion

        #region Event Listeners

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
        private void LinkedTriangles_Click(object sender, RoutedEventArgs e)
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

                    var triangles_linked = TriangleIndexedLinked_wpf.ConvertToLinked(triangles, true, false);


                    // get distinct list of edges, along with the triangles that they are tied to
                    // sort of the inverse of triangles_linked


                    // make an alternate of this function - maybe use a dictionary
                    // dict<(int,int), list<triangle>>
                    var distinct_edges = TriangleIndexed_wpf.GetUniqueLines(triangles_linked);





                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LinkedTriangles2_Click(object sender, RoutedEventArgs e)
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

                    //var triangles_linked = TriangleIndexedLinked_wpf.ConvertToLinked(triangles, true, false);

                    var triangles_linked = triangles.
                        Select(o => new TriangleIndexedLinked_wpf(o.Index0, o.Index1, o.Index2, o.AllPoints)).
                        ToArray();

                    var tree = CreateOctreeWithTriangles(triangles_linked, 400);


                    //LinkTriangles_Edges(tree, true);



                    // get distinct list of edges, along with the triangles that they are tied to
                    // sort of the inverse of triangles_linked


                    // make an alternate of this function - maybe use a dictionary
                    // dict<(int,int), list<triangle>>
                    //var distinct_edges = TriangleIndexed_wpf.GetUniqueLines(triangles_linked);





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

                    var tree = CreateOctreeWithTriangles(triangles_linked, 200);





                    //foreach (var cell in tree.GetAllUsedNodes())
                    //{
                    //    var window = new Debug3DWindow()
                    //    {
                    //        Title = "octree - single cell",
                    //    };

                    //    var sizes = Debug3DWindow.GetDrawSizes(cell.Bounds.Size.Length());

                    //    var cell_triangles = cell.GetItems_ThisNodeOnly();

                    //    if (cell_triangles.Length > 0)
                    //        window.AddHull(cell_triangles, UtilityWPF.ColorFromHex("CCC"));

                    //    var boundary_lines = Polytopes.GetCubeLines(cell.Bounds.Min.ToPoint_wpf(), cell.Bounds.Max.ToPoint_wpf());

                    //    window.AddLines(boundary_lines, sizes.line, Colors.White);

                    //    window.Show();
                    //}



                    var window2 = new Debug3DWindow()
                    {
                        Title = "octree - all cells",
                        //Background = UtilityWPF.BrushFromHex("669"),
                    };

                    var sizes2 = Debug3DWindow.GetDrawSizes(diag_len);

                    var cells = tree.GetAllUsedNodes();

                    // with 5000 cells, this was taking way too long (it would probably have taken many minutes or longer)
                    //Color[] cell_colors = UtilityWPF.GetRandomColors(cells.Length, 128, 210);

                    for (int i = 0; i < cells.Length; i++)
                    {
                        var cell_triangles = cells[i].GetItems_ThisNodeOnly();

                        Color cell_color = UtilityWPF.GetRandomColor(128, 210);

                        if (cell_triangles.Length > 0)
                            window2.AddHull(cell_triangles, cell_color);

                        var boundary_lines = Polytopes.GetCubeLines(cells[i].Bounds.Min.ToPoint_wpf(), cells[i].Bounds.Max.ToPoint_wpf());

                        //window2.AddLines(boundary_lines, sizes2.line * 0.1, cell_color);
                        window2.AddLines_Flat(boundary_lines, 1, cell_color);
                    }

                    window2.Show();

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

                    var tree = CreateOctreeWithTriangles(triangles_linked, 100);

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

                    var triangles_linked = triangles.
                        Select(o => new TriangleIndexedLinked_wpf(o.Index0, o.Index1, o.Index2, o.AllPoints)).
                        ToArray();

                    // Get unique line segments (index_min, index_max)[]

                    // ----------- thought 1 -----------
                    // Iterate those in parallel
                    //  calculate bounding box of that segment
                    //  ask tree for triangles touching that box
                    //  find the two triangles      --- make sure tree can handle queries from multi threads
                    //      if one, ignore
                    //      if two, bind them and return the set ((int,int), (triangle,triangle))
                    //      if > two, throw exception

                    //var tree = CreateOctreeWithTriangles(triangles_linked, 250);



                    // ----------- thought 2 -----------
                    // While getting line segments, remember the triangle that it came from
                    //  ((int,int), triangle)
                    //
                    // Then do a tolookup on the line segments

                    var getLine = new Func<int, int, (int, int)>((i1, i2) => (Math.Min(i1, i2), Math.Max(i1, i2)));

                    var links_query = triangles_linked.
                        SelectMany(o => new[]
                        {
                            new { edge = getLine(o.Index0, o.Index1), o },
                            new { edge = getLine(o.Index1, o.Index2), o },
                            new { edge = getLine(o.Index2, o.Index0), o },
                        }).
                        ToLookup(o => o.edge);

                    var links = new List<((int index0, int index1), (TriangleIndexedLinked_wpf tri0, TriangleIndexedLinked_wpf tri1))>();

                    foreach (var link in links_query)
                    {
                        var tris = link.ToArray();

                        if (tris.Length < 2)
                            continue;

                        else if (tris.Length > 2)
                            throw new ApplicationException("Found multiple triangles tied together");

                        // Populate each triangle's Neighbor_XX prop
                        if (!Link_Set_T0(tris[0].o, tris[1].o, link.Key.Item1, link.Key.Item2))
                            throw new ApplicationException("links query found mismatching triangle and edge");

                        if (!Link_Set_T0(tris[1].o, tris[0].o, link.Key.Item1, link.Key.Item2))
                            throw new ApplicationException("links query found mismatching triangle and edge");

                        links.Add((link.Key, (tris[0].o, tris[1].o)));
                    }



                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// This sets one of t0's edges to t1
        /// </summary>
        private bool Link_Set_T0(TriangleIndexedLinked_wpf t0, TriangleIndexedLinked_wpf t1, int i0, int i1)
        {
            if ((t0.Index0 == i0 && t0.Index1 == i1) ||
                (t0.Index0 == i1 && t0.Index1 == i0))
            {
                t0.Neighbor_01 = t1;
                return true;
            }

            if ((t0.Index1 == i0 && t0.Index2 == i1) ||
                (t0.Index1 == i1 && t0.Index2 == i0))
            {
                t0.Neighbor_12 = t1;
                return true;
            }

            if ((t0.Index2 == i0 && t0.Index0 == i1) ||
                (t0.Index2 == i1 && t0.Index0 == i0))
            {
                t0.Neighbor_20 = t1;
                return true;
            }

            return false;
        }


        #endregion

        #region Private Methods

        private void UnloadFile()
        {
            _viewport.Children.RemoveAll(_visuals);
            _visuals.Clear();

            if (lblStats_Mesh != null)
                lblStats_Mesh.Content = "";

            _parsed_file = null;
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

            return true;
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
            _trackball.MouseWheelScale = distance * .0007;
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


        // TODO: don't ask for divider size.  Calculate based on triangle count

        // Populates an octree with triangles.  The larger the divider, the smaller the node sizes can be
        private static BoundsOctree<T> CreateOctreeWithTriangles<T>(T[] triangles, int size_divider) where T : ITriangleIndexed_wpf
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


        // TODO: move this to TriangleIndexedLinked_wpf

        // I don't think it's worth trying to improve this function
        // Do triangles by edge, which will link them as a byproduct.  That should be fewer overall lookups

        public static void LinkTriangles_Edges_SingleThread(BoundsOctree<TriangleIndexedLinked_wpf> triangles, bool setNullIfNoLink)
        {
            var all_nodes = triangles.GetAllUsedNodes();

            var stats = new List<(int tris, int neigs, int cand)>();

            foreach (var node in all_nodes)
            {
                var touching_nodes = triangles.GetTouchingUsedNodes(node);

                var candidate_triangles = node.
                    Concat(touching_nodes).
                    SelectMany(o => o.GetItems_ThisNodeOnly()).
                    ToArray();

                var node_triangles = node.GetItems_ThisNodeOnly();

                stats.Add((node_triangles.Length, touching_nodes.Length, candidate_triangles.Length));

                foreach (var triangle in node_triangles)
                {
                    TriangleIndexedLinked_wpf neighbor = FindLinkEdge(candidate_triangles, triangle.Token, triangle.Index0, triangle.Index1);
                    if (neighbor != null || setNullIfNoLink)
                        triangle.Neighbor_01 = neighbor;

                    neighbor = FindLinkEdge(candidate_triangles, triangle.Token, triangle.Index1, triangle.Index2);
                    if (neighbor != null || setNullIfNoLink)
                        triangle.Neighbor_12 = neighbor;

                    neighbor = FindLinkEdge(candidate_triangles, triangle.Token, triangle.Index2, triangle.Index0);
                    if (neighbor != null || setNullIfNoLink)
                        triangle.Neighbor_20 = neighbor;
                }
            }


            // div 200: a few nodes with a couple thousand.  a lot in the hundreds, a lot single digit
            // div 400: one node with a thousand. remainder is hundreds down to single digit


            var stats2 = stats.
                OrderByDescending(o => o.tris).
                ThenByDescending(o => o.cand).
                ThenByDescending(o => o.neigs).
                ToArray();


        }
        public static void LinkTriangles_Edges_MultiThread(BoundsOctree<TriangleIndexedLinked_wpf> triangles, bool setNullIfNoLink)
        {
            var all_nodes = triangles.GetAllUsedNodes();

            var stats = new List<(int tris, int neigs, int cand)>();


            //TODO: multi thread this

            foreach (var node in all_nodes)
            {
                var touching_nodes = triangles.GetTouchingUsedNodes(node);

                var candidate_triangles = node.
                    Concat(touching_nodes).
                    SelectMany(o => o.GetItems_ThisNodeOnly()).
                    ToArray();

                var node_triangles = node.GetItems_ThisNodeOnly();

                stats.Add((node_triangles.Length, touching_nodes.Length, candidate_triangles.Length));




                // this is the part that should really be multi threaded
                foreach (var triangle in node_triangles)
                {
                    TriangleIndexedLinked_wpf neighbor = FindLinkEdge(candidate_triangles, triangle.Token, triangle.Index0, triangle.Index1);
                    if (neighbor != null || setNullIfNoLink)
                        triangle.Neighbor_01 = neighbor;

                    neighbor = FindLinkEdge(candidate_triangles, triangle.Token, triangle.Index1, triangle.Index2);
                    if (neighbor != null || setNullIfNoLink)
                        triangle.Neighbor_12 = neighbor;

                    neighbor = FindLinkEdge(candidate_triangles, triangle.Token, triangle.Index2, triangle.Index0);
                    if (neighbor != null || setNullIfNoLink)
                        triangle.Neighbor_20 = neighbor;
                }



            }




            // div 200: a few nodes with a couple thousand.  a lot in the hundreds, a lot single digit
            // div 400: one node with a thousand. remainder is hundreds down to single digit


            var stats2 = stats.
                OrderByDescending(o => o.tris).
                ThenByDescending(o => o.cand).
                ThenByDescending(o => o.neigs).
                ToArray();


        }




        private static TriangleIndexedLinked_wpf FindLinkEdge(IEnumerable<TriangleIndexedLinked_wpf> triangles, long calling_token, int vertex1, int vertex2)
        {
            // Find the triangle that has vertex1 and 2
            foreach (var triangle in triangles)
            {
                if (triangle.Token == calling_token)
                    // This is the currently requested triangle, so ignore it
                    continue;

                if ((triangle.Index0 == vertex1 && triangle.Index1 == vertex2) ||
                    (triangle.Index0 == vertex1 && triangle.Index2 == vertex2) ||
                    (triangle.Index1 == vertex1 && triangle.Index0 == vertex2) ||
                    (triangle.Index1 == vertex1 && triangle.Index2 == vertex2) ||
                    (triangle.Index2 == vertex1 && triangle.Index0 == vertex2) ||
                    (triangle.Index2 == vertex1 && triangle.Index1 == vertex2))
                {
                    // Found it
                    //NOTE:  Just returning the first match.  Assuming that the list of triangles is a valid hull
                    //NOTE:  Not validating that the triangle has 3 unique points, and the 2 points passed in are unique
                    return triangle;
                }
            }

            // No neighbor found
            return null;
        }



        #endregion
    }
}
