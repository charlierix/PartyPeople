using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

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

        private readonly DropShadowEffect _errorEffect;

        private (double dot, double line) _sizes;

        private BezierSegment3D_wpf[] _beziers = null;
        private BezierUtil.CurvatureSample[] _heatmap = null;
        private Point3D[] _inflection_points = null;

        private List<Visual3D> _temp_visuals = new List<Visual3D>();
        private ControlDot[] _controls = null;

        private ControlDot _dragging_dot = null;
        private Vector3D _dragging_offset = new Vector3D();

        private Debug3DWindow _window_offset1D = null;
        private Debug3DWindow _window_curve = null;
        private Debug3DWindow _window_sample_uniform = null;        // these are only used if they click the sample button
        private Debug3DWindow _window_sample_stretched = null;
        private Debug3DWindow _window_sample_heat = null;

        private bool _initialized = false;

        #endregion

        #region Constructor

        public BezierAnalysis()
        {
            InitializeComponent();

            _errorEffect = new DropShadowEffect()
            {
                Color = UtilityWPF.ColorFromHex("C02020"),
                Direction = 0,
                ShadowDepth = 0,
                BlurRadius = 8,
                Opacity = .8,
            };

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

            _initialized = true;
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

                RefreshControls(2);
                RefreshBezier();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtNumControls_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (!_initialized)
                    return;

                if (int.TryParse(txtNumControls.Text, out int num_controls))
                {
                    txtNumControls.Effect = null;
                    RefreshControls(num_controls);
                    RefreshBezier();
                }
                else
                {
                    txtNumControls.Effect = _errorEffect;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RandomSample_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Point3D[] endpoints = Enumerable.Range(0, StaticRandom.Next(3, 6)).
                    Select(o => Math3D.GetRandomVector_Spherical(4).ToPoint()).
                    ToArray();

                _beziers = BezierUtil.GetBezierSegments(endpoints, 0.3, false);

                if (chkExtraControls.IsChecked.Value)
                {
                    for (int i = 0; i < _beziers.Length; i++)
                    {
                        _beziers[i] = AddExtraControls(_beziers[i]);
                    }
                }

                _heatmap = BezierUtil.GetCurvatureHeatmap(_beziers);
                _inflection_points = TempBezierUtil.GetPinchedMapping3(_heatmap, endpoints.Length, _beziers);

                txtNumControls.Text = "";       // force a text change (in case it's the same count)

                //NOTE: the count can't be determined just by the number of points.  It depends how spaced out they are, severity of pinch vs straight
                txtNumControls.Text = _inflection_points.Length.ToString();
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
                if (!_initialized)
                    return;

                if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
                    return;

                //var hits = UtilityWPF.CastRay(out RayHitTestParameters ray, e.GetPosition(grdViewPort), grdViewPort, _camera, _viewport, true);       // ignoring hits, since they might have just clicked close
                var ray = UtilityWPF.RayFromViewportPoint(_camera, _viewport, e.GetPosition(grdViewPort));

                Point3D? click_point = Math3D.GetIntersection_Plane_Ray(new Triangle_wpf(new Vector3D(0, 0, 1), new Point3D()), ray.Origin, ray.Direction);

                _dragging_dot = _controls.
                    Select(o => new
                    {
                        control = o,
                        dist_sqr = (click_point.Value - o.Center).LengthSquared,
                    }).
                    Where(o => o.dist_sqr <= o.control.DetectRadius * o.control.DetectRadius).
                    OrderBy(o => o.dist_sqr).
                    Select(o => o.control).
                    FirstOrDefault();

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
                if (!_initialized)
                    return;

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
                if (!_initialized)
                    return;

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

        private void RefreshControls(int count)
        {
            if (_controls != null)
                _viewport.Children.RemoveAll(_controls.Select(o => o.Visual));

            var controls = new List<ControlDot>();

            for (int i = 0; i < count; i++)
            {
                double pos = (double)(i + 1) / (count + 1);

                controls.Add(GetControlDot(new Point3D(pos, pos, 0)));
            }

            _controls = controls.ToArray();
        }

        private void RefreshBezier()
        {
            RefreshBezier_Clear();

            Point3D[] controls = _controls.
                Select(o => o.Center).
                ToArray();

            var bezier = new BezierSegment3D_wpf(new Point3D(0, 0, 0), new Point3D(1, 1, 0), controls);

            RefreshBezier_MainWindow(controls, bezier);
            RefreshBezier_1DOffsets(bezier);
            RefreshBezier_Curve(bezier);

            if (_beziers != null)
            {
                if (_window_sample_uniform == null)
                {
                    _window_sample_uniform = new Debug3DWindow()
                    {
                        Title = "Uniform",
                    };
                    _window_sample_uniform.Show();
                }

                if (_window_sample_stretched == null)
                {
                    _window_sample_stretched = new Debug3DWindow()
                    {
                        Title = "Stretched",
                    };
                    _window_sample_stretched.Show();
                }

                if (_window_sample_heat == null)
                {
                    _window_sample_heat = new Debug3DWindow()
                    {
                        Title = "Heat",
                    };
                    _window_sample_heat.Show();
                }

                RefreshBezier_Uniform();
                RefreshBezier_Stretch(bezier);
                RefreshBezier_Heat();
            }
        }
        private void RefreshBezier_Clear()
        {
            _viewport.Children.RemoveAll(_temp_visuals);
            messages.Children.Clear();

            _window_offset1D.Clear();
            _window_curve.Clear();

            if (_window_sample_stretched != null)
                _window_sample_stretched.Clear();

            if (_window_sample_uniform != null)
                _window_sample_uniform.Clear();

            if (_window_sample_heat != null)
                _window_sample_heat.Clear();
        }
        private void RefreshBezier_MainWindow(Point3D[] controls, BezierSegment3D_wpf bezier)
        {
            // Control Positions
            foreach (Point3D control in controls)
            {
                messages.Children.Add(new TextBlock() { Text = control.ToStringSignificantDigits(3) });
            }

            // Control Lines
            var control_points = new List<Point3D>();

            control_points.Add(new Point3D(0, 0, 0));
            control_points.AddRange(controls);
            control_points.Add(new Point3D(1, 1, 0));

            _temp_visuals.Add(Debug3DWindow.GetLines(control_points, _sizes.line, Colors.IndianRed));
            _viewport.Children.Add(_temp_visuals[^1]);

            // Control Vertical Lines
            for (int i = 0; i < controls.Length; i++)
            {
                double pos = (double)(i + 1) / (controls.Length + 1);

                _temp_visuals.Add(Debug3DWindow.GetLine(new Point3D(pos, 0, 0), new Point3D(pos, 1, 0), _sizes.line, UtilityWPF.ColorFromHex("BAA")));
                _viewport.Children.Add(_temp_visuals[^1]);

                _temp_visuals.Add(Debug3DWindow.GetLine(new Point3D(0, pos, 0), new Point3D(1, pos, 0), _sizes.line, Colors.Gray));
                _viewport.Children.Add(_temp_visuals[^1]);
            }

            // Outer
            _temp_visuals.Add(Debug3DWindow.GetLine(new Point3D(0, 0, 0), new Point3D(1, 0, 0), _sizes.line, Colors.Black));
            _viewport.Children.Add(_temp_visuals[^1]);

            _temp_visuals.Add(Debug3DWindow.GetLine(new Point3D(0, 1, 0), new Point3D(1, 1, 0), _sizes.line, Colors.Black));
            _viewport.Children.Add(_temp_visuals[^1]);

            _temp_visuals.Add(Debug3DWindow.GetLine(new Point3D(0, 0, 0), new Point3D(0, 1, 0), _sizes.line, Colors.Black));
            _viewport.Children.Add(_temp_visuals[^1]);

            _temp_visuals.Add(Debug3DWindow.GetLine(new Point3D(1, 0, 0), new Point3D(1, 1, 0), _sizes.line, Colors.Black));
            _viewport.Children.Add(_temp_visuals[^1]);

            // Bezier
            Point3D[] points = BezierUtil.GetPoints(144, bezier);

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
        private void RefreshBezier_Uniform()
        {
            Point3D[] points = BezierUtil.GetPoints(_beziers.Length * 12, _beziers);

            var sizes = Debug3DWindow.GetDrawSizes(points);

            _window_sample_uniform.AddDots(points, sizes.dot, Colors.Black);
            _window_sample_uniform.AddLines(points, sizes.line, Colors.White);
        }
        private void RefreshBezier_Stretch(BezierSegment3D_wpf bezier)
        {
            Point3D[] points = BezierUtil.GetPoints_PinchImproved2(_beziers.Length * 12, _beziers, bezier);

            var sizes = Debug3DWindow.GetDrawSizes(points);

            _window_sample_stretched.AddDots(points, sizes.dot, Colors.Black);
            _window_sample_stretched.AddLines(points, sizes.line, Colors.White);
        }
        private void RefreshBezier_Heat()
        {
            var sizes = Debug3DWindow.GetDrawSizes(_beziers.SelectMany(o => o.Combined));

            double max_dist_from_negone = _heatmap.Max(o => o.Dist_From_NegOne);

            _window_sample_heat.AddDot(_beziers[0].EndPoint0, sizes.dot, Colors.Black);
            _window_sample_heat.AddDot(_beziers[^1].EndPoint1, sizes.dot, Colors.Black);

            foreach (var heat in _heatmap)
            {
                _window_sample_heat.AddDot(heat.Point, sizes.dot, UtilityWPF.AlphaBlend(Colors.DarkRed, Colors.DarkSeaGreen, heat.Dist_From_NegOne / max_dist_from_negone));
            }

            var lines = new List<(Point3D, Point3D)>();
            lines.Add((_beziers[0].EndPoint0, _heatmap[0].Point));
            lines.AddRange(Enumerable.Range(0, _heatmap.Length - 1).Select(o => (_heatmap[o].Point, _heatmap[o + 1].Point)));
            lines.Add((_heatmap[^1].Point, _beziers[^1].EndPoint1));

            _window_sample_heat.AddLines(lines, sizes.line * 0.75, Colors.White);
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

        private static BezierSegment3D_wpf AddExtraControls(BezierSegment3D_wpf bezier)
        {
            int count = StaticRandom.Next(1, 2);

            Point3D center = Math3D.GetCenter(bezier.EndPoint0, bezier.EndPoint1);
            double radius = (bezier.EndPoint1 - bezier.EndPoint0).Length * 1.5;

            var control_points = bezier.ControlPoints.ToList();

            for (int i = 0; i < count; i++)
            {
                Point3D point = center + Math3D.GetRandomVector_Spherical(radius);

                control_points.Insert(control_points.Count - 1, point);
            }

            return new BezierSegment3D_wpf(bezier.EndPoint0, bezier.EndPoint1, control_points.ToArray());
        }

        #endregion
    }
}
