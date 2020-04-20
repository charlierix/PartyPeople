using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace Game.Bepu.Monolisk
{
    public partial class ShardEditor1 : Window
    {
        #region class: ShardVisuals

        private class ShardVisuals
        {
            public ShardMap1 Shard { get; set; }

            public Model3DGroup TileGroup { get; set; }
            public Model3D[,] Tiles { get; set; }

            public Visual3D Visual { get; set; }
        }

        #endregion

        #region Declaration Section

        private const int SIZE = 48;
        private const int HALFSIZE = SIZE / 2;
        private const double MAXCLICKDIST = 36;
        private const double TILE_Z = .05;

        private const string FOLDER = @"Monolisk\v1";

        private TrackBallRoam _trackball = null;

        private List<Visual3D> _tempVisuals = new List<Visual3D>();

        private ShardVisuals _shard = null;

        #endregion

        #region Constructor

        public ShardEditor1()
        {
            InitializeComponent();

            // Trackball
            _trackball = new TrackBallRoam(_camera);
            _trackball.EventSource = grdViewPort;       //NOTE:  If this control doesn't have a background color set, the trackball won't see events (I think transparent is ok, just not null)
            _trackball.AllowZoomOnMouseWheel = true;
            _trackball.Mappings.AddRange(TrackBallMapping.GetPrebuilt(TrackBallMapping.PrebuiltMapping.MouseComplete_NoLeft));
            _trackball.ShouldHitTestOnOrbit = false;
        }

        #endregion

        #region Event Listeners

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                DrawGrid();

                //ShardMap1 shard = CreateRandomShard(SIZE);
                ShardMap1 shard = CreateEmptyShard(SIZE);

                LoadShard(shard);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void grdViewPort_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (_shard == null || e.ChangedButton != MouseButton.Left || e.LeftButton != MouseButtonState.Pressed)
                {
                    return;
                }

                // Fire a ray from the mouse point
                Point clickPoint = e.GetPosition(grdViewPort);
                var ray = UtilityWPF.RayFromViewportPoint(_camera, _viewport, clickPoint);

                // See where it intersects the plane
                var intersect = Math3D.GetIntersection_Plane_Ray(new Triangle_wpf(new Point3D(0, 0, 0), new Point3D(1, 0, 0), new Point3D(0, 1, 0)), ray.Origin, ray.Direction);
                if (intersect == null || (ray.Origin - intersect.Value).Length > MAXCLICKDIST)
                {
                    return;
                }

                // Convert that into a tile index
                VectorInt index = GetTileIndex(intersect.Value.ToPoint2D());
                if (index.X < 0 || index.X >= SIZE || index.Y < 0 || index.Y >= SIZE)
                {
                    return;
                }

                if (_shard.Tiles[index.X, index.Y] == null)
                {
                    _shard.Shard.Tiles[index.Y][index.X] = new ShardTile1()
                    {
                        GroundType = ShardGroundType1.Cement,
                    };

                    AddTileTop(index, _shard.Shard.Tiles[index.Y][index.X].GroundType, _shard.TileGroup, _shard.Tiles);
                }
                else
                {
                    _shard.Shard.Tiles[index.Y][index.X] = null;

                    RemoveTileTop(index, _shard.TileGroup, _shard.Tiles);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowDot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Visual3D visual = Debug3DWindow.GetDot(new Point3D(0, 0, 0), .5, Colors.Yellow);

                _viewport.Children.Add(visual);
                _tempVisuals.Add(visual);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ShowAxiis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Visual3D[] visuals = Debug3DWindow.GetAxisLines(HALFSIZE + 1, Debug3DWindow.GetDrawSizes(7).line);

                _viewport.Children.AddRange(visuals);
                _tempVisuals.AddRange(visuals);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ClearTempVisuals_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewport.Children.RemoveAll(_tempVisuals);

                _tempVisuals.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NewRandomShard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShardMap1 shard = CreateRandomShard(SIZE);

                LoadShard(shard);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string escaped = UtilityCore.EscapeFilename_Windows(txtShardName.Text);

                if (string.IsNullOrWhiteSpace(escaped))
                {
                    MessageBox.Show("Please select a name", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (_shard?.Shard == null)
                {
                    MessageBox.Show("No shard populated", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string filename = System.IO.Path.Combine(FOLDER, escaped + ".xaml");

                UtilityCore.SaveOptions(_shard.Shard, filename);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Load_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = System.IO.Path.Combine(UtilityCore.GetOptionsFolder(), FOLDER);

                // Prompt for file
                var dialog = new Microsoft.Win32.OpenFileDialog()
                {
                    InitialDirectory = folder,
                    //Filter = "*.xaml|*.xml|*.*",
                    Multiselect = false,
                };

                bool? result = dialog.ShowDialog();
                if(result == null || result.Value == false)
                {
                    return;
                }

                // Deserialize
                var shard = UtilityCore.ReadOptions<ShardMap1>(dialog.FileName);

                if(shard.Tiles == null || shard.Tiles.Length != SIZE)
                {
                    throw new ApplicationException("Unsupported tile size");
                }

                LoadShard(shard);

                txtShardName.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private void DrawGrid()
        {
            var sizes = Debug3DWindow.GetDrawSizes(6);

            var lines = Enumerable.Range(-HALFSIZE, SIZE + 1).
                SelectMany(o => new[]
                    {
                        (new Point3D(o, -HALFSIZE, 0), new Point3D(o, HALFSIZE, 0)),
                        (new Point3D(-HALFSIZE, o, 0), new Point3D(HALFSIZE, o, 0)),
                    });

            Visual3D visual = Debug3DWindow.GetLines(lines, sizes.line, UtilityWPF.ColorFromHex("EDECDD"));

            _viewport.Children.Add(visual);
        }

        private static ShardMap1 CreateRandomShard(int size)
        {
            ShardMap1 retVal = new ShardMap1()
            {
                Background = ShardBackgroundType1.Port,
                Tiles = Enumerable.Range(0, size).
                    Select(o => new ShardTile1[size]).
                    ToArray(),
            };

            Random rand = StaticRandom.GetRandomForThread();

            int count = Math.Max(4, (SIZE * SIZE) / 12);
            for (int cntr = 0; cntr < count; cntr++)
            {
                VectorInt index = new VectorInt(rand.Next(size), rand.Next(size));

                if (retVal.Tiles[index.Y][index.X] != null)
                {
                    continue;
                }

                retVal.Tiles[index.Y][index.X] = new ShardTile1()
                {
                    GroundType = ShardGroundType1.Cement,
                };
            }

            //TODO: Create start and end locations

            return retVal;
        }
        private static ShardMap1 CreateEmptyShard(int size)
        {
            ShardMap1 retVal = new ShardMap1()
            {
                Background = ShardBackgroundType1.Port,
                Tiles = Enumerable.Range(0, size).
                    Select(o => new ShardTile1[size]).
                    ToArray(),
            };

            return retVal;
        }

        private void LoadShard(ShardMap1 shard)
        {
            if (_shard != null)
            {
                _viewport.Children.Remove(_shard.Visual);
                _shard = null;
            }

            ShardVisuals visuals = new ShardVisuals()
            {
                Shard = shard,
                TileGroup = new Model3DGroup(),
                Tiles = new Model3D[shard.Tiles.Length, shard.Tiles.Length],        // it's square
            };

            for (int x = 0; x < shard.Tiles.Length; x++)
            {
                for (int y = 0; y < shard.Tiles.Length; y++)
                {
                    if (shard.Tiles[y][x] != null)
                    {
                        AddTileTop(new VectorInt(x, y), shard.Tiles[y][x].GroundType, visuals.TileGroup, visuals.Tiles);
                    }
                }
            }

            visuals.Visual = new ModelVisual3D
            {
                Content = visuals.TileGroup,
            };

            _viewport.Children.Add(visuals.Visual);

            _shard = visuals;
        }

        private static void RemoveTileTop(VectorInt index, Model3DGroup group, Model3D[,] models)
        {
            group.Children.Remove(models[index.X, index.Y]);
            models[index.X, index.Y] = null;
        }

        private static void AddTileTop(VectorInt index, ShardGroundType1 type, Model3DGroup group, Model3D[,] models)
        {
            if (models[index.X, index.Y] != null)
            {
                RemoveTileTop(index, group, models);
            }

            switch (type)
            {
                case ShardGroundType1.Cement:
                    AddTileTop_Cement(index, group, models);
                    break;

                default:
                    throw new ApplicationException($"Unknown {nameof(ShardGroundType1)}: {type}");
            }
        }
        private static void AddTileTop_Cement(VectorInt index, Model3DGroup group, Model3D[,] models)
        {
            // diffuse: 777
            // specular: 30602085, 3

            var pos = GetTilePos(index.X, index.Y);

            Random rand = StaticRandom.GetRandomForThread();

            Model3DGroup tileGroup = new Model3DGroup();

            #region top

            ColorHSV diffuse = new ColorHSV(0, 0, rand.Next(50 - 5, 50 + 5));
            ColorHSV spec = new ColorHSV(36, rand.Next(278 - 5, 278 + 5), rand.Next(76 - 10, 76 + 10), rand.Next(52 - 5, 52 + 5));

            MaterialGroup material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(diffuse.ToRGB())));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(spec.ToRGB()), 3));

            tileGroup.Children.Add(new GeometryModel3D
            {
                Material = material,
                BackMaterial = material,
                Geometry = UtilityWPF.GetSquare2D(pos.min, pos.max, TILE_Z),
            });

            #endregion

            #region sides

            diffuse = new ColorHSV(0, 0, rand.Next(43 - 5, 43 + 5));
            spec = new ColorHSV(24, rand.Next(278 - 5, 278 + 5), rand.Next(76 - 10, 76 + 10), rand.Next(52 - 5, 52 + 5));

            material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(diffuse.ToRGB())));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(spec.ToRGB()), 3));

            tileGroup.Children.Add(new GeometryModel3D
            {
                Material = material,
                BackMaterial = material,
                Geometry = GetCubeSides(pos.min, pos.max, TILE_Z),
            });

            #endregion

            models[index.X, index.Y] = tileGroup;
            group.Children.Add(models[index.X, index.Y]);
        }

        private static (Point min, Point max) GetTilePos(int x, int y)
        {
            int offsetX = x - HALFSIZE;
            int offsetY = y - HALFSIZE;

            return
            (
                new Point(offsetX, offsetY),
                new Point(offsetX + 1, offsetY + 1)
            );
        }
        private static VectorInt GetTileIndex(Point pos)
        {
            int x = (pos.X + HALFSIZE).ToInt_Floor();
            int y = (pos.Y + HALFSIZE).ToInt_Floor();

            return new VectorInt(x, y);
        }

        private static MeshGeometry3D GetCubeSides(Point min, Point max, double topZ)
        {
            // Copied from UtilityWPF.GetCube_IndependentFaces

            // Define 3D mesh object
            MeshGeometry3D retVal = new MeshGeometry3D();

            //retVal.Positions.Add(new Point3D(min.X, min.Y, topZ - 1));		// 0
            //retVal.Positions.Add(new Point3D(max.X, min.Y, topZ - 1));		// 1
            //retVal.Positions.Add(new Point3D(max.X, max.Y, topZ - 1));		// 2
            //retVal.Positions.Add(new Point3D(min.X, max.Y, topZ - 1));		// 3

            //retVal.Positions.Add(new Point3D(min.X, min.Y, topZ));		// 4
            //retVal.Positions.Add(new Point3D(max.X, min.Y, topZ));		// 5
            //retVal.Positions.Add(new Point3D(max.X, max.Y, topZ));		// 6
            //retVal.Positions.Add(new Point3D(min.X, max.Y, topZ));		// 7

            // Front face
            //retVal.Positions.Add(new Point3D(min.X, min.Y, topZ - 1));		// 0
            //retVal.Positions.Add(new Point3D(max.X, min.Y, topZ - 1));		// 1
            //retVal.Positions.Add(new Point3D(max.X, max.Y, topZ - 1));		// 2
            //retVal.Positions.Add(new Point3D(min.X, max.Y, topZ - 1));		// 3
            //retVal.TriangleIndices.Add(0);
            //retVal.TriangleIndices.Add(1);
            //retVal.TriangleIndices.Add(2);
            //retVal.TriangleIndices.Add(2);
            //retVal.TriangleIndices.Add(3);
            //retVal.TriangleIndices.Add(0);

            // Back face
            //retVal.Positions.Add(new Point3D(min.X, min.Y, topZ));		// 4
            //retVal.Positions.Add(new Point3D(max.X, min.Y, topZ));		// 5
            //retVal.Positions.Add(new Point3D(max.X, max.Y, topZ));		// 6
            //retVal.Positions.Add(new Point3D(min.X, max.Y, topZ));		// 7
            //retVal.TriangleIndices.Add(6);
            //retVal.TriangleIndices.Add(5);
            //retVal.TriangleIndices.Add(4);
            //retVal.TriangleIndices.Add(4);
            //retVal.TriangleIndices.Add(7);
            //retVal.TriangleIndices.Add(6);

            // Right face
            retVal.Positions.Add(new Point3D(max.X, min.Y, topZ - 1));		// 1-8
            retVal.Positions.Add(new Point3D(max.X, max.Y, topZ - 1));		// 2-9
            retVal.Positions.Add(new Point3D(max.X, min.Y, topZ));		// 5-10
            retVal.Positions.Add(new Point3D(max.X, max.Y, topZ));		// 6-11
            retVal.TriangleIndices.Add(8 - 8);		// 1
            retVal.TriangleIndices.Add(10 - 8);		// 5
            retVal.TriangleIndices.Add(9 - 8);		// 2
            retVal.TriangleIndices.Add(10 - 8);		// 5
            retVal.TriangleIndices.Add(11 - 8);		// 6
            retVal.TriangleIndices.Add(9 - 8);		// 2

            // Top face
            retVal.Positions.Add(new Point3D(max.X, max.Y, topZ - 1));		// 2-12
            retVal.Positions.Add(new Point3D(min.X, max.Y, topZ - 1));		// 3-13
            retVal.Positions.Add(new Point3D(max.X, max.Y, topZ));		// 6-14
            retVal.Positions.Add(new Point3D(min.X, max.Y, topZ));		// 7-15
            retVal.TriangleIndices.Add(12 - 8);		// 2
            retVal.TriangleIndices.Add(14 - 8);		// 6
            retVal.TriangleIndices.Add(13 - 8);		// 3
            retVal.TriangleIndices.Add(13 - 8);		// 3
            retVal.TriangleIndices.Add(14 - 8);		// 6
            retVal.TriangleIndices.Add(15 - 8);		// 7

            // Bottom face
            retVal.Positions.Add(new Point3D(min.X, min.Y, topZ - 1));		// 0-16
            retVal.Positions.Add(new Point3D(max.X, min.Y, topZ - 1));		// 1-17
            retVal.Positions.Add(new Point3D(min.X, min.Y, topZ));		// 4-18
            retVal.Positions.Add(new Point3D(max.X, min.Y, topZ));		// 5-19
            retVal.TriangleIndices.Add(19 - 8);		// 5
            retVal.TriangleIndices.Add(17 - 8);		// 1
            retVal.TriangleIndices.Add(16 - 8);		// 0
            retVal.TriangleIndices.Add(16 - 8);		// 0
            retVal.TriangleIndices.Add(18 - 8);		// 4
            retVal.TriangleIndices.Add(19 - 8);		// 5

            // Right face
            retVal.Positions.Add(new Point3D(min.X, min.Y, topZ - 1));		// 0-20
            retVal.Positions.Add(new Point3D(min.X, max.Y, topZ - 1));		// 3-21
            retVal.Positions.Add(new Point3D(min.X, min.Y, topZ));		// 4-22
            retVal.Positions.Add(new Point3D(min.X, max.Y, topZ));		// 7-23
            retVal.TriangleIndices.Add(22 - 8);		// 4
            retVal.TriangleIndices.Add(20 - 8);		// 0
            retVal.TriangleIndices.Add(21 - 8);		// 3
            retVal.TriangleIndices.Add(21 - 8);		// 3
            retVal.TriangleIndices.Add(23 - 8);		// 7
            retVal.TriangleIndices.Add(22 - 8);		// 4

            // shouldn't I set normals?
            //retVal.Normals

            //retVal.Freeze();
            return retVal;
        }


        #endregion
    }
}
