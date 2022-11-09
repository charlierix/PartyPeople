using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
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
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace Game.Bepu.Testers
{
    public partial class BezierAnalysis : Window
    {
        #region class: ControlDot

        private class ControlDot
        {
            public Visual3D Visual { get; set; }

            private Point3D _center = new Point3D();
            public Point3D Center
            {
                get
                {
                    return _center;
                }
                set
                {
                    _center = value;

                    if (Translate != null)
                    {
                        Translate.OffsetX = _center.X;
                        Translate.OffsetY = _center.Y;
                        Translate.OffsetZ = _center.Z;
                    }
                }
            }
            public TranslateTransform3D Translate { get; set; }

            public double DetectRadius { get; set; }
        }

        #endregion

        #region Declaration Section

        private TrackBallRoam _trackball = null;

        private (double dot, double line) _sizes;

        private List<Visual3D> _temp_visuals = new List<Visual3D>();
        private ControlDot _control_left = null;
        private ControlDot _control_right = null;

        private ControlDot _dragging_dot = null;
        private Vector3D _dragging_offset = new Vector3D();

        private Debug3DWindow _window_offset1D = null;
        private Debug3DWindow _window_curve = null;

        #endregion

        #region Constructor

        public BezierAnalysis()
        {
            InitializeComponent();

            _sizes = Debug3DWindow.GetDrawSizes(1);

            _window_offset1D = new Debug3DWindow()
            {
                Title = "1D Offset",
            };
            _window_offset1D.SetCamera(new Point3D(0.5, 0, 1.45), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0));
            _window_offset1D.Show();

            _window_curve = new Debug3DWindow()
            {
                Title = "Curve",
            };
            _window_curve.SetCamera(new Point3D(0.5, 0.5, 1.45), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0));
            _window_curve.Show();
        }

        #endregion

        #region Event Listeners

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _trackball = new TrackBallRoam(_camera);
                //_trackball.KeyPanScale = 15d;
                _trackball.EventSource = grdViewPort;		//NOTE:  If this control doesn't have a background color set, the trackball won't see events (I think transparent is ok, just not null)
                _trackball.AllowZoomOnMouseWheel = true;
                _trackball.Mappings.AddRange(TrackBallMapping.GetPrebuilt(TrackBallMapping.PrebuiltMapping.MouseComplete_NoLeft_RightRotateInPlace));
                //_trackball.GetOrbitRadius += new GetOrbitRadiusHandler(Trackball_GetOrbitRadius);
                _trackball.ShouldHitTestOnOrbit = false;
                //_trackball.InertiaPercentRetainPerSecond_Linear = _trackball_InertiaPercentRetainPerSecond_Linear;
                //_trackball.InertiaPercentRetainPerSecond_Angular = _trackball_InertiaPercentRetainPerSecond_Angular;

                DrawInitialScene();
                RefreshBezier();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void grdViewPort_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
                    return;

                //var hits = UtilityWPF.CastRay(out RayHitTestParameters ray, e.GetPosition(grdViewPort), grdViewPort, _camera, _viewport, true);       // ignoring hits, since they might have just clicked close
                var ray = UtilityWPF.RayFromViewportPoint(_camera, _viewport, e.GetPosition(grdViewPort));

                Point3D? click_point = Math3D.GetIntersection_Plane_Ray(new Triangle_wpf(new Vector3D(0, 0, 1), new Point3D()), ray.Origin, ray.Direction);

                double dist1 = (click_point.Value - _control_left.Center).Length;
                double dist2 = (click_point.Value - _control_right.Center).Length;

                _dragging_dot = null;

                if (dist1 < dist2)
                {
                    if (dist1 <= _control_left.DetectRadius)
                        _dragging_dot = _control_left;
                }
                else
                {
                    if (dist2 <= _control_right.DetectRadius)
                        _dragging_dot = _control_right;
                }

                if (_dragging_dot != null)
                    _dragging_offset = click_point.Value - _dragging_dot.Center;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void grdViewPort_MouseUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Released)
                    _dragging_dot = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void grdViewPort_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (_dragging_dot == null)
                    return;

                var ray = UtilityWPF.RayFromViewportPoint(_camera, _viewport, e.GetPosition(grdViewPort));

                Point3D? click_point = Math3D.GetIntersection_Plane_Ray(new Triangle_wpf(new Vector3D(0, 0, 1), new Point3D()), ray.Origin, ray.Direction);

                _dragging_dot.Center = click_point.Value - _dragging_offset;

                RefreshBezier();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private void DrawInitialScene()
        {
            const double ONE_THIRD = 1d / 3d;

            Color color_left = UtilityWPF.ColorFromHex("BAA");
            Color color_right = UtilityWPF.ColorFromHex("9A9");

            _viewport.Children.Add(Debug3DWindow.GetLine(new Point3D(0.5, 0, 0), new Point3D(0.5, 1, 0), _sizes.line / 2, Colors.Gray));
            _viewport.Children.Add(Debug3DWindow.GetLine(new Point3D(0, 0.5, 0), new Point3D(1, 0.5, 0), _sizes.line / 2, Colors.Gray));

            // Left
            _viewport.Children.Add(Debug3DWindow.GetArc(new Point3D(0, 0, 0), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), ONE_THIRD, 0, 90, _sizes.line / 2, color_left, num_segments: 72));
            _viewport.Children.Add(Debug3DWindow.GetArc(new Point3D(0, 0, 0), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), ONE_THIRD * 2, 0, 90, _sizes.line / 2, color_left, num_segments: 104));
            _viewport.Children.Add(Debug3DWindow.GetArc(new Point3D(0, 0, 0), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), 1, 0, 90, _sizes.line / 2, color_left, num_segments: 144));

            _viewport.Children.Add(Debug3DWindow.GetLine(new Point3D(0, ONE_THIRD, 0), new Point3D(1, ONE_THIRD, 0), _sizes.line / 2, color_left));

            // Right
            _viewport.Children.Add(Debug3DWindow.GetArc(new Point3D(1, 1, 0), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), ONE_THIRD, 180, 270, _sizes.line / 2, color_right, num_segments: 72));
            _viewport.Children.Add(Debug3DWindow.GetArc(new Point3D(1, 1, 0), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), ONE_THIRD * 2, 180, 270, _sizes.line / 2, color_right, num_segments: 104));
            _viewport.Children.Add(Debug3DWindow.GetArc(new Point3D(1, 1, 0), new Vector3D(1, 0, 0), new Vector3D(0, 1, 0), 1, 180, 270, _sizes.line / 2, color_right, num_segments: 144));

            _viewport.Children.Add(Debug3DWindow.GetLine(new Point3D(0, 1 - ONE_THIRD, 0), new Point3D(1, 1 - ONE_THIRD, 0), _sizes.line / 2, color_right));

            // Outer
            _viewport.Children.Add(Debug3DWindow.GetLine(new Point3D(0, 0, 0), new Point3D(1, 0, 0), _sizes.line, Colors.Black));
            _viewport.Children.Add(Debug3DWindow.GetLine(new Point3D(0, 1, 0), new Point3D(1, 1, 0), _sizes.line, Colors.Black));
            _viewport.Children.Add(Debug3DWindow.GetLine(new Point3D(0, 0, 0), new Point3D(0, 1, 0), _sizes.line, Colors.Black));
            _viewport.Children.Add(Debug3DWindow.GetLine(new Point3D(1, 0, 0), new Point3D(1, 1, 0), _sizes.line, Colors.Black));

            // control dots
            _control_left = GetControlDot(new Point3D(0.5, ONE_THIRD, 0));
            _control_right = GetControlDot(new Point3D(1 - 0.5, 1 - ONE_THIRD, 0));
        }

        private void RefreshBezier()
        {
            _viewport.Children.RemoveAll(_temp_visuals);
            messages.Children.Clear();
            _window_offset1D.Clear();
            _window_curve.Clear();

            Point3D[] controls = new[]
            {
                _control_left.Center,
                _control_right.Center,
            };

            var bezier = new BezierSegment3D_wpf(new Point3D(0, 0, 0), new Point3D(1, 1, 0), controls);

            RefreshBezier_MainWindow(controls, bezier);
            RefreshBezier_1DOffsets(bezier);
            RefreshBezier_Curve(bezier);
        }
        private void RefreshBezier_MainWindow(Point3D[] controls, BezierSegment3D_wpf bezier)
        {
            messages.Children.Add(new TextBlock() { Text = controls[0].ToStringSignificantDigits(3) });
            messages.Children.Add(new TextBlock() { Text = controls[1].ToStringSignificantDigits(3) });

            Point3D[] points = BezierUtil.GetPoints(144, bezier);

            _temp_visuals.Add(Debug3DWindow.GetLine(new Point3D(0, 0, 0), controls[0], _sizes.line, Colors.IndianRed));
            _viewport.Children.Add(_temp_visuals[^1]);

            _temp_visuals.Add(Debug3DWindow.GetLine(new Point3D(1, 1, 0), controls[1], _sizes.line, Colors.IndianRed));
            _viewport.Children.Add(_temp_visuals[^1]);

            _temp_visuals.Add(Debug3DWindow.GetDots(points, _sizes.dot, Colors.White));
            _viewport.Children.Add(_temp_visuals[^1]);
        }
        private void RefreshBezier_1DOffsets(BezierSegment3D_wpf bezier)
        {
            double count = 12;

            double max_dist = 0;

            for (int i = 0; i < count; i++)
            {
                double percent = i / (count - 1);
                double percent_stretched = BezierUtil.GetPoint(percent, bezier.Combined).Y;

                Point3D point_orig = new Point3D(percent, -0.05, 0);
                Point3D point_stretched = new Point3D(percent_stretched, 0.05, 0);

                _window_offset1D.AddDot(point_orig, _sizes.dot, Colors.Gray);
                _window_offset1D.AddDot(point_stretched, _sizes.dot, Colors.White);
                _window_offset1D.AddLine(point_orig, point_stretched, _sizes.line, Colors.Gray);

                max_dist = Math.Max(Math.Abs(percent - percent_stretched), max_dist);
            }

            _window_offset1D.AddText($"max distance: {max_dist}");
        }
        private void RefreshBezier_Curve(BezierSegment3D_wpf bezier)
        {
            _window_curve.AddLine(new Point3D(0, 0, 0), new Point3D(1, 0, 0), _sizes.line, Colors.Black);
            _window_curve.AddLine(new Point3D(0, 1, 0), new Point3D(1, 1, 0), _sizes.line, Colors.Black);
            _window_curve.AddLine(new Point3D(0, 0, 0), new Point3D(0, 1, 0), _sizes.line, Colors.Black);
            _window_curve.AddLine(new Point3D(1, 0, 0), new Point3D(1, 1, 0), _sizes.line, Colors.Black);

            double count = 144;

            for (int i = 0; i < count; i++)
            {
                double percent = i / (count - 1);
                double percent_stretched = BezierUtil.GetPoint(percent, bezier.Combined).Y;

                _window_curve.AddDot(new Point3D(percent, percent_stretched, 0), _sizes.dot, Colors.White);
            }
        }

        private ControlDot GetControlDot(Point3D position)
        {
            double dot_radius = _sizes.dot * 3;
            double click_radius = dot_radius * 3;

            Visual3D visual = Debug3DWindow.GetDot(new Point3D(), dot_radius, Colors.IndianRed);

            var translate = new TranslateTransform3D(position.X, position.Y, position.Z);
            visual.Transform = translate;

            _viewport.Children.Add(visual);

            return new ControlDot()
            {
                Center = position,
                DetectRadius = click_radius,
                Translate = translate,
                Visual = visual,
            };
        }

        #endregion
    }
}
