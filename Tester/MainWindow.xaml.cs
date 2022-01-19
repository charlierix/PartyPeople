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
            new Bepu.Testers.UnitTests().Show();
        }
        private void Bepu_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Bepu.Testers.BepuTester().Show();
        }
        private void Drawing_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Bepu.Testers.BasicDrawingTests().Show();
        }
        private void TrackballGrabber_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Bepu.Testers.TrackballGrabberTester().Show();
        }

        private void ColorPicker_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.ColorTools.ColorPickerWindow().Show();
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

        private void AnalyzeVRPoints_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.AnalyzeVRPoints().Show();
        }
        private void AnalyzeIKMeshChains_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.AnalyzeIKMeshChains().Show();
        }
        private void ChaseRotation_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Bepu.Testers.ChaseRotationWindow().Show();
        }
        private void PlanesThruBezier_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Bepu.Testers.PlanesThruBezier().Show();
        }

        private void GeneticSharp_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.GeneticSharpTester().Show();
        }

        private void FindDistinctStrings_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.FindDistinctStrings().Show();
        }

        #endregion
    }
}
