using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF.Controls3D;
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
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace Game.Bepu.Testers
{
    public partial class TrackballGrabberTester : Window
    {
        #region class: ItemColors

        // This was copied from WindTunnel2
        private class ItemColors
        {
            //public Color ForceLine = UtilityWPF.AlphaBlend(Colors.HotPink, Colors.Plum, .25d);

            //public Color HullFace = UtilityWPF.AlphaBlend(Colors.Ivory, Colors.Transparent, .2d);
            //public SpecularMaterial HullFaceSpecular = new SpecularMaterial(new SolidColorBrush(Color.FromArgb(255, 86, 68, 226)), 100d);
            //public Color HullWireFrame = UtilityWPF.AlphaBlend(Colors.Ivory, Colors.Transparent, .3d);

            //public Color GhostBodyFace = Color.FromArgb(40, 192, 192, 192);
            //public SpecularMaterial GhostBodySpecular = new SpecularMaterial(new SolidColorBrush(Color.FromArgb(96, 86, 68, 226)), 25);

            //public Color Anchor = Colors.Gray;
            //public SpecularMaterial AnchorSpecular = new SpecularMaterial(new SolidColorBrush(Colors.Silver), 50d);
            //public Color Rope = Colors.Silver;
            //public SpecularMaterial RopeSpecular = new SpecularMaterial(new SolidColorBrush(UtilityWPF.AlphaBlend(Colors.Silver, Colors.White, .5d)), 25d);

            //public Color FluidLine => UtilityWPF.GetRandomColor(255, 153, 168, 186, 191, 149, 166);

            public DiffuseMaterial TrackballAxisMajor = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(255, 147, 98, 229)));
            public DiffuseMaterial TrackballAxisMinor = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(255, 127, 112, 153)));
            public Color TrackballAxisLine = Color.FromArgb(96, 117, 108, 97);
            public SpecularMaterial TrackballAxisSpecular = new SpecularMaterial(Brushes.White, 100d);

            public Color TrackballGrabberHoverLight = Color.FromArgb(255, 74, 37, 138);

            //public Color BlockedCell = UtilityWPF.ColorFromHex("60BAE5B1");
            //public Color FieldBoundry = UtilityWPF.ColorFromHex("40B5C9B1");
        }

        #endregion

        private readonly ItemColors _colors = new ItemColors();

        private TrackballGrabber _trackball = null;

        public TrackballGrabberTester()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //foreach (var axis_line in Debug3DWindow.GetAxisLines(1, 0.05))
                //{
                //    _viewportFlowRotate.Children.Add(axis_line);
                //}

                //SetupFlowTrackball_Z_UP();
                //SetupFlowTrackball_Z_DOWN();
                SetupFlowTrackball_Y_DOWN();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FlowOrientationTrackball_RotationChanged(object sender, EventArgs e)
        {
            try
            {
                //TODO: update items that depend on this vector
                //something.direction = _flowOrientationTrackball.CurrentDirection;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _trackball.ResetToDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupFlowTrackball_Z_UP()
        {
            //var default_direction = new DoubleVector_wpf(new Vector3D(0, 0, -1), new Vector3D(0, 1, 0));
            var default_direction = new DoubleVector_wpf(new Vector3D(0, 0, -1), new Vector3D(0, -1, 0));

            // Big purple arrow
            var permanentVisuals = new List<Visual3D>();
            permanentVisuals.Add(new ModelVisual3D() { Content = TrackballGrabber.GetMajorArrow(Axis.Z, true, _colors.TrackballAxisMajor, _colors.TrackballAxisSpecular) });

            // Faint lines
            var hoverVisuals = new List<Visual3D>();
            hoverVisuals.Add(TrackballGrabber.GetGuideLine(Axis.Z, false, _colors.TrackballAxisLine));
            hoverVisuals.Add(TrackballGrabber.GetGuideLineDouble(Axis.X, _colors.TrackballAxisLine));
            hoverVisuals.Add(TrackballGrabber.GetGuideLineDouble(Axis.Y, _colors.TrackballAxisLine));

            // Create the trackball
            _trackball = new TrackballGrabber(grdFlowRotateViewport, _viewportFlowRotate, permanentVisuals.ToArray(), hoverVisuals.ToArray(), 1d, _colors.TrackballGrabberHoverLight, default_direction);

            _trackball.RotationChanged += new EventHandler(FlowOrientationTrackball_RotationChanged);
        }

        private void SetupFlowTrackball_Z_DOWN()
        {
            var default_direction = new DoubleVector_wpf(new Vector3D(0, 0, 1), new Vector3D(0, 1, 0));

            // Big purple arrow
            var permanentVisuals = new List<Visual3D>();
            permanentVisuals.Add(new ModelVisual3D() { Content = TrackballGrabber.GetMajorArrow(Axis.Z, false, _colors.TrackballAxisMajor, _colors.TrackballAxisSpecular) });

            // Faint lines
            var hoverVisuals = new List<Visual3D>();
            hoverVisuals.Add(TrackballGrabber.GetGuideLine(Axis.Z, true, _colors.TrackballAxisLine));
            hoverVisuals.Add(TrackballGrabber.GetGuideLineDouble(Axis.X, _colors.TrackballAxisLine));
            hoverVisuals.Add(TrackballGrabber.GetGuideLineDouble(Axis.Y, _colors.TrackballAxisLine));

            // Create the trackball
            _trackball = new TrackballGrabber(grdFlowRotateViewport, _viewportFlowRotate, permanentVisuals.ToArray(), hoverVisuals.ToArray(), 1d, _colors.TrackballGrabberHoverLight, default_direction);

            _trackball.RotationChanged += new EventHandler(FlowOrientationTrackball_RotationChanged);
        }

        private void SetupFlowTrackball_Y_DOWN()
        {
            var default_direction = new DoubleVector_wpf(new Vector3D(0, -1, 0), new Vector3D(0, 0, 1));

            // Big purple arrow
            var permanentVisuals = new List<Visual3D>();
            permanentVisuals.Add(new ModelVisual3D() { Content = TrackballGrabber.GetMajorArrow(Axis.Y, false, _colors.TrackballAxisMajor, _colors.TrackballAxisSpecular) });

            // Faint lines
            var hoverVisuals = new List<Visual3D>();
            hoverVisuals.Add(TrackballGrabber.GetGuideLine(Axis.Y, true, _colors.TrackballAxisLine));
            hoverVisuals.Add(TrackballGrabber.GetGuideLineDouble(Axis.X, _colors.TrackballAxisLine));
            hoverVisuals.Add(TrackballGrabber.GetGuideLineDouble(Axis.Z, _colors.TrackballAxisLine));

            // Create the trackball
            _trackball = new TrackballGrabber(grdFlowRotateViewport, _viewportFlowRotate, permanentVisuals.ToArray(), hoverVisuals.ToArray(), 1d, _colors.TrackballGrabberHoverLight, default_direction);

            _trackball.RotationChanged += new EventHandler(FlowOrientationTrackball_RotationChanged);
        }
    }
}
