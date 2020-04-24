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
        #region Declaration Section

        private const double MAXCLICKDIST = 1728;        // this just ended up being annoying

        private const string FOLDER = @"Monolisk\v1";

        private TrackBallRoam _trackball = null;

        private List<Visual3D> _tempVisuals = new List<Visual3D>();

        private ShardVisuals1 _shard = null;

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
                ShardMap1 shard = CreateEmptyShard(ShardRendering1.SIZE);

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
                Visual3D[] visuals = Debug3DWindow.GetAxisLines(ShardRendering1.HALFSIZE + 1, Debug3DWindow.GetDrawSizes(7).line);

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
                ShardMap1 shard = CreateRandomShard(ShardRendering1.SIZE);

                LoadShard(shard);

                txtShardName.Text = "";
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

                if (shard.Tiles == null || shard.Tiles.Length != ShardRendering1.SIZE)
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

        private void LoadShard(ShardMap1 shard)
        {
            if (_shard != null)
            {
                _viewport.Children.Remove(_shard.Visual);
                _shard = null;
            }

            _shard = ShardRendering1.LoadShard(shard);

            _viewport.Children.Add(_shard.Visual);
        }

        private void DrawGrid()
        {
            var sizes = Debug3DWindow.GetDrawSizes(1.5);

            var lines = Enumerable.Range(-ShardRendering1.HALFSIZE, ShardRendering1.SIZE + 1).
                SelectMany(o => new[]
                    {
                        (new Point3D(o, -ShardRendering1.HALFSIZE, 0), new Point3D(o, ShardRendering1.HALFSIZE, 0)),
                        (new Point3D(-ShardRendering1.HALFSIZE, o, 0), new Point3D(ShardRendering1.HALFSIZE, o, 0)),
                    });

            Visual3D visual = Debug3DWindow.GetLines(lines, sizes.line, UtilityWPF.ColorFromHex("EDECDD"));

            _viewport.Children.Add(visual);
        }

        private static ShardMap1 CreateRandomShard(int size)
        {
            ShardMap1 retVal = CreateEmptyShard(size);

            Random rand = StaticRandom.GetRandomForThread();

            int count = Math.Max(4, (ShardRendering1.SIZE * ShardRendering1.SIZE) / 12);
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
            VectorInt index = ShardRendering1.GetTileIndex(intersect.Value.ToPoint2D());
            if (index.X < 0 || index.X >= ShardRendering1.SIZE || index.Y < 0 || index.Y >= ShardRendering1.SIZE)
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
                        ShardRendering1.RemoveItemGraphic(index, _shard.ItemsGroup, _shard.Items);
                    }

                    _shard.Shard.Tiles[index.Y][index.X] = null;

                    ShardRendering1.RemoveTileGraphic(index, _shard.TileGroup, _shard.Tiles);
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

                ShardRendering1.AddTileGraphic(index, _shard.Shard.Tiles[index.Y][index.X].GroundType, _shard.TileGroup, _shard.Tiles);
            }
            else
            {
                // Change type
                if (_shard.Shard.Tiles[index.Y][index.X].GroundType != groundType)
                {
                    _shard.Shard.Tiles[index.Y][index.X].GroundType = groundType;

                    ShardRendering1.RemoveTileGraphic(index, _shard.TileGroup, _shard.Tiles);
                    ShardRendering1.AddTileGraphic(index, groundType, _shard.TileGroup, _shard.Tiles);
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

                    ShardRendering1.RemoveItemGraphic(index, _shard.ItemsGroup, _shard.Items);
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

                ShardRendering1.AddItemGraphic(index, _shard.Shard.Tiles[index.Y][index.X].Item, _shard.ItemsGroup, _shard.Items);
            }
            else
            {
                // Change type
                if (_shard.Shard.Tiles[index.Y][index.X].Item.ItemType != itemType)
                {
                    _shard.Shard.Tiles[index.Y][index.X].Item.ItemType = itemType;

                    ShardRendering1.RemoveItemGraphic(index, _shard.ItemsGroup, _shard.Items);
                    ShardRendering1.AddItemGraphic(index, _shard.Shard.Tiles[index.Y][index.X].Item, _shard.ItemsGroup, _shard.Items);
                }
            }
        }

        #endregion
    }
}
