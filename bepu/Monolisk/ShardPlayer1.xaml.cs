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

        private ShardVisuals1 _shard = null;

        #endregion

        #region Constructor

        public ShardPlayer1()
        {
            InitializeComponent();

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

        private void CreatePlayer()
        {
            VectorInt[] starts = FindItems(ShardItemType1.StartLocation);
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

        private VectorInt[] FindItems(ShardItemType1 type)
        {
            var retVal = new List<VectorInt>();

            for (int y = 0; y < _shard.Shard.Tiles.Length; y++)
            {
                for (int x = 0; x < _shard.Shard.Tiles[y].Length; x++)
                {
                    if(_shard.Shard.Tiles[y][x]?.Item?.ItemType == type)
                    {
                        retVal.Add(new VectorInt(x, y));
                    }
                }
            }

            return retVal.ToArray();
        }

        #endregion
    }
}
