using Accord.Diagnostics;
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

            public Model3DGroup ItemsGroup { get; set; }
            public Model3D[,] Items { get; set; }

            public Visual3D Visual { get; set; }
        }

        #endregion

        #region Declaration Section

        private const int SIZE = 48;
        private const int HALFSIZE = SIZE / 2;
        private const double MAXCLICKDIST = 1728;        // this just ended up being annoying
        private const double TILE_Z = .05;

        private const string FOLDER = @"Monolisk\v1";

        private TrackBallRoam _trackball = null;

        private List<Visual3D> _tempVisuals = new List<Visual3D>();

        private ShardVisuals _shard = null;

        private bool _isDragging = false;

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

                VectorInt? index = GetClickedIndex(e);
                if (index == null)
                {
                    return;
                }

                _isDragging = true;

                if (radCement.IsChecked.Value)
                {
                    ApplyDrag_Tile(index.Value);
                }

                if (radStart.IsChecked.Value || radEnd.IsChecked.Value)
                {
                    ApplyDrag_Item(index.Value);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void grdViewPort_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (!_isDragging)
                {
                    return;
                }

                VectorInt? index = GetClickedIndex(e);
                if (index == null)
                {
                    return;
                }

                if (radCement.IsChecked.Value)
                {
                    ApplyDrag_Tile(index.Value);
                }


                //TODO: Have a selected item



            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void grdViewPort_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (_isDragging && e.ChangedButton == MouseButton.Left)
                {
                    _isDragging = false;
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
                if (result == null || result.Value == false)
                {
                    return;
                }

                // Deserialize
                var shard = UtilityCore.ReadOptions<ShardMap1>(dialog.FileName);

                if (shard.Tiles == null || shard.Tiles.Length != SIZE)
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
            var sizes = Debug3DWindow.GetDrawSizes(1.5);

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
            ShardMap1 retVal = CreateEmptyShard(size);

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
                ItemsGroup = new Model3DGroup(),
                Items = new Model3D[shard.Tiles.Length, shard.Tiles.Length],        // it's square
            };

            for (int x = 0; x < shard.Tiles.Length; x++)
            {
                for (int y = 0; y < shard.Tiles.Length; y++)
                {
                    if (shard.Tiles[y][x] != null)
                    {
                        var index = new VectorInt(x, y);

                        AddTileGraphic(index, shard.Tiles[y][x].GroundType, visuals.TileGroup, visuals.Tiles);

                        if (shard.Tiles[y][x].Item != null)
                        {
                            AddItemGraphic(index, shard.Tiles[y][x].Item, visuals.ItemsGroup, visuals.Items);
                        }
                    }
                }
            }

            Model3DGroup finalGroup = new Model3DGroup();
            finalGroup.Children.Add(visuals.TileGroup);
            finalGroup.Children.Add(visuals.ItemsGroup);

            visuals.Visual = new ModelVisual3D
            {
                Content = finalGroup,
            };

            _viewport.Children.Add(visuals.Visual);

            _shard = visuals;
        }

        private VectorInt? GetClickedIndex(MouseEventArgs e)
        {
            // Fire a ray from the mouse point
            Point clickPoint = e.GetPosition(grdViewPort);
            var ray = UtilityWPF.RayFromViewportPoint(_camera, _viewport, clickPoint);

            // See where it intersects the plane
            var intersect = Math3D.GetIntersection_Plane_Ray(new Triangle_wpf(new Point3D(0, 0, 0), new Point3D(1, 0, 0), new Point3D(0, 1, 0)), ray.Origin, ray.Direction);
            if (intersect == null || (ray.Origin - intersect.Value).Length > MAXCLICKDIST)
            {
                return null;
            }

            // Convert that into a tile index
            VectorInt index = GetTileIndex(intersect.Value.ToPoint2D());
            if (index.X < 0 || index.X >= SIZE || index.Y < 0 || index.Y >= SIZE)
            {
                return null;
            }

            return index;
        }

        private void ApplyDrag_Tile(VectorInt index)
        {
            // Try delete first, it's easiest
            if (chkDelete.IsChecked.Value)
            {
                if (_shard.Tiles[index.X, index.Y] != null)
                {
                    if (_shard.Items[index.X, index.Y] != null)      // also need remove an item if it's sitting on the tile
                    {
                        RemoveItemGraphic(index, _shard.ItemsGroup, _shard.Items);
                    }

                    _shard.Shard.Tiles[index.Y][index.X] = null;

                    RemoveTileGraphic(index, _shard.TileGroup, _shard.Tiles);
                }

                return;
            }

            var groundType = radCement.IsChecked.Value ? ShardGroundType1.Cement :
                throw new ApplicationException("Unknown tile type");

            if (_shard.Tiles[index.X, index.Y] == null)
            {
                // Create
                _shard.Shard.Tiles[index.Y][index.X] = new ShardTile1()
                {
                    GroundType = groundType,
                };

                AddTileGraphic(index, _shard.Shard.Tiles[index.Y][index.X].GroundType, _shard.TileGroup, _shard.Tiles);
            }
            else
            {
                // Change type
                if (_shard.Shard.Tiles[index.Y][index.X].GroundType != groundType)
                {
                    _shard.Shard.Tiles[index.Y][index.X].GroundType = groundType;

                    RemoveTileGraphic(index, _shard.TileGroup, _shard.Tiles);
                    AddTileGraphic(index, groundType, _shard.TileGroup, _shard.Tiles);
                }
            }
        }
        private void ApplyDrag_Item(VectorInt index)
        {
            if (chkDelete.IsChecked.Value)
            {
                if (_shard.Items[index.X, index.Y] != null)
                {
                    _shard.Shard.Tiles[index.Y][index.X].Item = null;

                    RemoveItemGraphic(index, _shard.ItemsGroup, _shard.Items);
                }

                return;
            }

            var itemType = radStart.IsChecked.Value ? ShardItemType1.StartLocation :
                radEnd.IsChecked.Value ? ShardItemType1.EndGate :
                throw new ApplicationException("Unknown item type");

            if (_shard.Items[index.X, index.Y] == null)
            {
                if (_shard.Tiles[index.X, index.Y] == null)
                {
                    // No tile to place the item on
                    return;
                }

                // Create
                _shard.Shard.Tiles[index.Y][index.X].Item = new ShardItem1()
                {
                    ItemType = itemType,
                    Angle = ShardAngle1._0,
                };

                AddItemGraphic(index, _shard.Shard.Tiles[index.Y][index.X].Item, _shard.ItemsGroup, _shard.Items);
            }
            else
            {
                // Change type
                if (_shard.Shard.Tiles[index.Y][index.X].Item.ItemType != itemType)
                {
                    _shard.Shard.Tiles[index.Y][index.X].Item.ItemType = itemType;

                    RemoveItemGraphic(index, _shard.ItemsGroup, _shard.Items);
                    AddItemGraphic(index, _shard.Shard.Tiles[index.Y][index.X].Item, _shard.ItemsGroup, _shard.Items);
                }
            }
        }

        private static void RemoveTileGraphic(VectorInt index, Model3DGroup group, Model3D[,] models)
        {
            group.Children.Remove(models[index.X, index.Y]);
            models[index.X, index.Y] = null;
        }
        private static void RemoveItemGraphic(VectorInt index, Model3DGroup group, Model3D[,] models)
        {
            group.Children.Remove(models[index.X, index.Y]);
            models[index.X, index.Y] = null;
        }

        private static void AddTileGraphic(VectorInt index, ShardGroundType1 type, Model3DGroup group, Model3D[,] models)
        {
            if (models[index.X, index.Y] != null)
            {
                RemoveTileGraphic(index, group, models);
            }

            switch (type)
            {
                case ShardGroundType1.Cement:
                    AddTileGraphic_Cement(index, group, models);
                    break;

                default:
                    throw new ApplicationException($"Unknown {nameof(ShardGroundType1)}: {type}");
            }
        }
        private static void AddTileGraphic_Cement(VectorInt index, Model3DGroup group, Model3D[,] models)
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

        private static void AddItemGraphic(VectorInt index, ShardItem1 item, Model3DGroup group, Model3D[,] models)
        {
            if (models[index.X, index.Y] != null)
            {
                RemoveItemGraphic(index, group, models);
            }

            //TODO: For start and end location, remove any other instance of that
            //TODO: For other item types, only add if count isn't exceeded

            switch (item.ItemType)
            {
                case ShardItemType1.StartLocation:
                    AddItemGraphic_StartLocation(index, group, models);
                    break;

                case ShardItemType1.EndGate:
                    AddItemGraphic_EndGate(index, group, models);
                    break;

                default:
                    throw new ApplicationException($"Unknown {nameof(ShardItemType1)}: {item.ItemType}");
            }


            //TODO: Apply rotation


        }
        private static void AddItemGraphic_StartLocation(VectorInt index, Model3DGroup group, Model3D[,] models)
        {
            var pos = GetTilePos(index.X, index.Y);

            MaterialGroup material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(UtilityWPF.BrushFromHex("555")));
            material.Children.Add(new SpecularMaterial(UtilityWPF.BrushFromHex("40EEEEEE"), 1.5));

            GeometryModel3D model = new GeometryModel3D
            {
                Material = material,
                BackMaterial = material,
                Geometry = UtilityWPF.GetCylinder_AlongX(8, .4, TILE_Z, new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), -90d))),
                Transform = new TranslateTransform3D(pos.min.X + .5, pos.min.Y + .5, TILE_Z * 1.5),     //TODO: expose a porition of this transform so the item can be rotated
            };

            models[index.X, index.Y] = model;
            group.Children.Add(models[index.X, index.Y]);
        }
        private static void AddItemGraphic_EndGate(VectorInt index, Model3DGroup group, Model3D[,] models)
        {
            // This is a good start, but the endcaps cover the entire face
            //UtilityWPF.GetMultiRingedTube

            // This works, but an ellipse is needed
            //Debug3DWindow.GetCircle


            // Also, the interior of the ring should come to an edge instead of flat.  So what is needed is a squared ring that's black,
            // an inner ring that's a triangle (like st louis arch), and if you want to get fancy, an exterior ring


            var pos = GetTilePos(index.X, index.Y);

            MaterialGroup material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(UtilityWPF.BrushFromHex("4C8FF5")));
            material.Children.Add(new SpecularMaterial(UtilityWPF.BrushFromHex("A08B49EB"), 1));
            material.Children.Add(new EmissiveMaterial(UtilityWPF.BrushFromHex("4049B4EB")));

            var rings = new List<TubeRingBase>();
            rings.Add(new TubeRingRegularPolygon(0, false, .7, 1.2, true));
            rings.Add(new TubeRingRegularPolygon(.1, false, .7, 1.2, true));

            Transform3DGroup transform = new Transform3DGroup();
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 90)));
            transform.Children.Add(new TranslateTransform3D(pos.min.X + .5, pos.min.Y + .5, 1));

            GeometryModel3D model = new GeometryModel3D
            {
                Material = material,
                BackMaterial = material,

                Geometry = UtilityWPF.GetMultiRingedTube(24, rings, true, true, transform),
            };

            models[index.X, index.Y] = model;
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
