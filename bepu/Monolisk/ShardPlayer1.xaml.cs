using Game.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Game.Bepu.Monolisk
{
    public partial class ShardPlayer1 : Window
    {
        #region Declaration Section

        private const string FOLDER = @"Monolisk\v1";

        private ShardVisuals1 _shard = null;

        #endregion

        #region Constructor

        public ShardPlayer1()
        {
            InitializeComponent();
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

                if (_shard != null)
                {
                    _viewport.Children.Remove(_shard.Visual);
                    _shard = null;
                }

                _shard = ShardRendering1.LoadShard(shard);

                _viewport.Children.Add(_shard.Visual);

                lblShardName.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
