using Accord;
using Accord.Math;
using Game.Core;
using Game.Math_WPF.Mathematics;
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
    public partial class ShardPlayer1 : Window
    {
        #region Declaration Section

        private const string FOLDER = @"Monolisk\v1";

        private const double PLAYERHEIGHT = .25;

        private readonly PlayerController1 _playerController;
        private readonly Physics1 _physics;

        private ShardVisuals1 _shard = null;

        #endregion

        #region Constructor

        public ShardPlayer1()
        {
            InitializeComponent();

            _physics = new Physics1();

            _playerController = new PlayerController1(_camera, grdViewPort);
            _playerController.IsActive = false;
        }

        #endregion

        #region Event Listeners

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

                lblShardName.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);

                LoadShard(shard);

                CreatePlayer();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DropBall_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(_shard == null)
                {
                    MessageBox.Show("Load a shard first", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Point3D pos = _camera.Position + (_camera.LookDirection.ToUnit(false) * .3);

                _physics.AddBall(pos, .06, Colors.Yellow, _viewport);
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
            _physics.Clear();

            if (_shard != null)
            {
                _viewport.Children.Remove(_shard.Visual);
                _shard = null;
            }

            // Visuals
            _shard = ShardRendering1.LoadShard(shard);
            _viewport.Children.Add(_shard.Visual);

            // Physics
            LoadPhysicsTiles(shard, _physics);
        }

        private static void LoadPhysicsTiles(ShardMap1 shard, Physics1 physics)
        {
            var isOpen = new Func<int, int, bool>((x, y) =>
            {
                if (x < 0 || y < 0 || x >= shard.Tiles.Length || y >= shard.Tiles.Length)
                    return true;
                else
                    return shard.Tiles[y][x] == null;
            });

            foreach (VectorInt2 index in shard.EnumerateIndices())
            {
                if (shard.Tiles[index.Y][index.X] != null)
                {
                    var pos = ShardRendering1.GetTilePos(index.X, index.Y);

                    double size = Math1D.Avg(pos.max.X - pos.min.X, pos.max.Y - pos.min.Y);

                    physics.AddTerrain
                    (
                        new Rect3D(pos.min.X, pos.min.Y, -size, size, size, size),
                        isOpen(index.X - 1, index.Y),
                        isOpen(index.X + 1, index.Y),
                        isOpen(index.X, index.Y - 1),
                        isOpen(index.X, index.Y + 1)
                    );
                }
            }
        }

        private void CreatePlayer()
        {
            VectorInt2[] starts = FindItems(ShardItemType1.StartLocation);
            if (starts.Length != 1)
            {
                throw new ApplicationException($"Need exactly one start point: {starts.Length}");
            }

            var tilePos = ShardRendering1.GetTilePos(starts[0].X, starts[0].Y);

            _camera.Position = tilePos.center.ToPoint3D(PLAYERHEIGHT);

            _camera.LookDirection = new Vector3D(1, 0, 0).GetRotatedVector(new Vector3D(0, 0, 1), _shard.Shard.Tiles[starts[0].Y][starts[0].X].Item.AngleDbl);
            _camera.UpDirection = new Vector3D(0, 0, 1);

            _playerController.IsActive = true;
        }

        private VectorInt2[] FindItems(ShardItemType1 type)
        {
            return _shard.Shard.EnumerateIndices().
                Where(o => _shard.Shard.Tiles[o.Y][o.X]?.Item?.ItemType == type).
                ToArray();
        }

        #endregion
    }
}
