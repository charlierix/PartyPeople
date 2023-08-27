using System.Windows;
using System.Windows.Input;

namespace Game.Tester
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;
        }

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

        private void RefineBezier_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.BezierAnalysis().Show();
        }
        private void Pendulum_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.Pendulum().Show();
        }
        private void Distribution_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.EvenDistribution().Show();
        }
        private void DebugLogViewer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Math_WPF.WPF.DebugLogViewer.DebugLogWindow().Show();
        }
        private void WingInterference_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.WingInterference.WingInterferenceTester().Show();
        }
        private void WallJumpConfig_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.WallJumpConfig().Show();
        }
        private void CurveFitting_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.CurveFitting().Show();
        }
        private void AnimationCurveVisualizer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.AnimationCurveVisualizer().Show();
        }
        private void PasswordGenerator_MouseUp(object sender, MouseButtonEventArgs e)
        {
            new Game.Bepu.Testers.PasswordGenerator().Show();
        }
    }
}
