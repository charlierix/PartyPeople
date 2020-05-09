using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Game.Tester
{
    public partial class MainWindow : Window
    {
        #region Constructor

        public MainWindow()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;
        }

        #endregion

        #region Event Listeners

        private void UnitTests_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.UnitTests().Show();
        }
        private void Bepu_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.BepuTester().Show();
        }

        private void ColorManipulations_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.ColorTools.ColorManipulationsWindow().Show();
        }

        private void MonoliskEditor1_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Monolisk.ShardEditor1().Show();
        }
        private void MonoliskPlayer1_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Monolisk.ShardPlayer1().Show();
        }

        #endregion
    }
}
