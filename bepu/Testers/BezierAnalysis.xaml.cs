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

            public Color Color { get; set; }

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

            public double AtTotalPercent { get; set; }
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
        private Debug3DWindow _window_abovebelow = null;
        private Debug3DWindow _window_sample_uniform = null;        // these are only used if they click the sample button
        private Debug3DWindow _window_sample_stretched = null;
        private Debug3DWindow _window_sample_heat = null;
        private Debug3DWindow _window_sample_need_current = null;

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

            //_window_curve = new Debug3DWindow()
            //{
            //    Title = "Curve",
            //};
            //_window_curve.SetCamera(new Point3D(0.5, 0.5, 1.45), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0));
            //_window_curve.Show();

            _window_abovebelow = new Debug3DWindow()
            {
                Title = "Above / Below",
            };
            _window_abovebelow.SetCamera(new Point3D(0.5, 0, 1.45), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0));
            _window_abovebelow.Show();

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

        private void txtNumSegments_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (!_initialized)
                    return;

                if (int.TryParse(txtNumSegments.Text, out int num_controls))
                {
                    txtNumSegments.Effect = null;
                }
                else
                {
                    txtNumSegments.Effect = _errorEffect;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SimpleClick_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(txtNumSegments.Text, out int count))
                {
                    MessageBox.Show("Couldn't parse number of segments", this.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Point3D[] endpoints = Enumerable.Range(0, count).
                    Select(o => Math3D.GetRandomVector_Spherical(4).ToPoint()).
                    ToArray();

                bool is_closed = StaticRandom.NextBool();

                var beziers_initial = BezierUtil.GetBezierSegments(endpoints, 0.3, is_closed);

                //var lengths = BezierUtil.GetCumulativeLengths(beziers_initial);       // not needed for this

                double min = beziers_initial.Min(o => o.Length_quick);
                double max = beziers_initial.Max(o => o.Length_quick);

                double ratio = max / min;

                double[] ratios = beziers_initial.
                    Select(o => o.Length_quick / min).
                    ToArray();

                var beziers_final = BezierUtil.GetBezierSegments(endpoints, 0.3, is_closed, ratios);


                int total_count = 48;

                Point3D[] samples_initial_1 = BezierUtil.GetPoints(total_count, beziers_initial);
                Point3D[] samples_final_1 = BezierUtil.GetPoints(total_count, beziers_final);

                var all_samples = samples_initial_1.
                    Concat(samples_final_1).
                    ToArray();

                var aabb = Math3D.GetAABB(all_samples);
                Point3D center = Math3D.GetCenter(all_samples);
                var sizes = Debug3DWindow.GetDrawSizes(all_samples);

                double offset_x = (aabb.max.X - aabb.min.X) / 2;
                offset_x *= 1.2;

                double offset_z = (aabb.max.Z - aabb.min.Z) / 2;
                offset_z *= 1.2;

                var window = new Debug3DWindow();

                // Initial 1
                Vector3D offset = new Vector3D(-offset_x, 0, -offset_z);

                var points = samples_initial_1.
                    Select(o => o + offset).
                    ToArray();

                window.AddDots(points, sizes.dot, Colors.Black);
                window.AddLines(points, sizes.line, Colors.Yellow);

                // Final 1
                offset = new Vector3D(-offset_x, 0, offset_z);

                points = samples_final_1.
                    Select(o => o + offset).
                    ToArray();

                window.AddDots(points, sizes.dot, Colors.Black);
                window.AddLines(points, sizes.line, Colors.Yellow);


                double total_length = beziers_initial.Sum(o => o.Length_quick);

                double[] ratios2 = beziers_initial.
                    Select(o => o.Length_quick / total_length).
                    ToArray();



                //TODO: points get duped where beziers come together.  add extra counts in anticipation and throw out the first of each trailing segment


                var counts = ratios2.
                    Select(o => (total_count * o).ToInt_Round()).
                    ToArray();

                int current_count = counts.Sum();

                while (current_count != total_count)
                {
                    var coverages = Enumerable.Range(0, counts.Length).
                        Select(o => new
                        {
                            index = o,
                            coverage = beziers_initial[o].Length_quick / counts[o],
                        }).
                        ToArray();

                    if (current_count < total_count)
                    {
                        int index = coverages.
                            OrderByDescending(o => o.coverage).
                            First().
                            index;

                        counts[index]++;
                        current_count++;
                    }
                    else
                    {
                        int index = coverages.
                            OrderBy(o => o.coverage).
                            First().
                            index;

                        counts[index]--;
                        current_count--;
                    }
                }


                Point3D[] samples_final_2 = Enumerable.Range(0, beziers_initial.Length).
                    SelectMany(o => BezierUtil.GetPoints(counts[o], beziers_initial[o])).
                    ToArray();

                // Final 2
                offset = new Vector3D(offset_x, 0, 0);

                samples_final_2 = samples_final_2.
                    Select(o => o + offset).
                    ToArray();

                window.AddDots(samples_final_2, sizes.dot, Colors.Black);
                window.AddLines(samples_final_2, sizes.line, Colors.White);



                var analysis = samples_final_2.
                    Select((o, i) => new
                    {
                        index = i,
                        point = o,
                    }).
                    ToLookup(o => o.point, (p1, p2) => p1.IsNearValue(p2)).
                    Where(o => o.Count() > 1).
                    ToArray();






                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SimpleClick2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(txtNumSegments.Text, out int count))
                {
                    MessageBox.Show("Couldn't parse number of segments", this.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Point3D[] endpoints = Enumerable.Range(0, count).
                    Select(o => Math3D.GetRandomVector_Spherical(4).ToPoint()).
                    ToArray();

                bool is_closed = StaticRandom.NextBool();

                var beziers_initial = BezierUtil.GetBezierSegments(endpoints, 0.3, is_closed);

                int total_count = 48;

                double total_length = beziers_initial.Sum(o => o.Length_quick);

                double[] ratios = beziers_initial.
                    Select(o => o.Length_quick / total_length).
                    ToArray();

                var counts = ratios.
                    Select(o => (total_count * o).ToInt_Round()).
                    ToArray();

                int current_count = counts.Sum();

                while (current_count != total_count)
                {
                    var densities = Enumerable.Range(0, beziers_initial.Length).
                        Select(o => new AdjustmentDensity()
                        {
                            Index = o,
                            Density_Minus = (counts[o] - 1) / beziers_initial[o].Length_quick,
                            Density_Current = counts[o] / beziers_initial[o].Length_quick,
                            Density_Plus = (counts[o] + 1) / beziers_initial[o].Length_quick,
                        }).
                        ToArray();

                    if (current_count < total_count)
                        AddCountToSegment(ref current_count, counts, densities);
                    else
                        RemoveCountFromSegment(ref current_count, counts, densities);
                }

                Point3D[] samples_final = GetSamples(beziers_initial, counts, is_closed);

                Point3D[] samples_initial = BezierUtil.GetPoints(total_count, beziers_initial);

                var window = new Debug3DWindow();

                var all_samples = samples_initial.
                    Concat(samples_final).
                    ToArray();

                var aabb = Math3D.GetAABB(all_samples);
                Point3D center = Math3D.GetCenter(all_samples);
                var sizes = Debug3DWindow.GetDrawSizes(all_samples);

                double offset_z = (aabb.max.Z - aabb.min.Z) / 2;
                offset_z *= 1.2;

                // Initial
                Vector3D offset = new Vector3D(0, 0, -offset_z);

                var points = samples_initial.
                    Select(o => o + offset).
                    ToArray();

                window.AddDots(points, sizes.dot, Colors.Black);
                window.AddLines(points, sizes.line, Colors.Yellow);

                // Final
                offset = new Vector3D(0, 0, offset_z);

                points = samples_final.
                    Select(o => o + offset).
                    ToArray();

                window.AddDots(points, sizes.dot, Colors.Black);
                window.AddLines(points, sizes.line, Colors.White);


                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        private static void AddCountToSegment(ref int current_count, int[] counts, AdjustmentDensity[] densities)
        {
            // In this case, add to the one with the lowest density
            var best = densities.
                OrderBy(o => o.Density_Current).
                ToArray();

            //var best = densities.
            //    OrderBy(o => o.Density_Plus).
            //    ToArray();

            //var projections = Enumerable.Range(0, densities.Length).
            //    Select(o => densities.
            //        Select(p => new
            //        {
            //            item = p,
            //            density = p.Index == o ?
            //                p.Density_Plus :
            //                p.Density_Current,
            //        }).
            //        OrderBy(p => p.density).
            //        ToArray()).
            //    Select((o,i) => new
            //    {
            //        index = i,
            //        count_prev = counts[i],
            //        count_new = counts[i] + 1,
            //        projected_densities = o,
            //        gap = o[^1].density - o[0].density,
            //    }).
            //    OrderBy(o => o.gap).
            //    ToArray();


            //var window = new Debug3DWindow();

            //var graphs = projections.
            //    Select(o => Debug3DWindow.GetGraph(o.projected_densities.Select(p => p.density).ToArray(), o.index.ToString())).
            //    ToArray();

            //window.AddGraphs(graphs, new Point3D(), 1);

            //window.Show();

            //counts[projections[0].index]++;


            counts[best[0].Index]++;
            current_count++;
        }
        private static void RemoveCountFromSegment(ref int current_count, int[] counts, AdjustmentDensity[] densities)
        {
            //var best = densities.
            //    OrderByDescending(o => o.Density_Current).
            //    ToArray();

            // In this case, use the one that after removing, it's still the highest density (it's the segment that will have the least impact of removal)
            //NO: need to use aspects of the projected query
            //var best = densities.
            //    OrderByDescending(o => o.Density_Minus).        
            //    ToArray();


            //var projections = Enumerable.Range(0, densities.Length).
            //    Select(o => densities.
            //        Select(p => new
            //        {
            //            item = p,
            //            density = p.Index == o ?
            //                p.Density_Minus :
            //                p.Density_Current,
            //        }).
            //        OrderByDescending(p => p.density).
            //        ToArray()).
            //    Select((o, i) => new
            //    {
            //        index = i,
            //        count_prev = counts[i],
            //        count_new = counts[i] - 1,
            //        projected_densities = o,
            //        gap = o[0].density - o[^1].density,
            //    }).
            //    OrderBy(o => o.gap).
            //    ToArray();


            //var window = new Debug3DWindow();

            //var graphs = projections.
            //    Select(o => Debug3DWindow.GetGraph(o.projected_densities.Select(p => p.density).ToArray(), o.index.ToString())).
            //    ToArray();

            //window.AddGraphs(graphs, new Point3D(), 1);

            //window.Show();

            //counts[projections[0].index]--;


            //counts[best[0].Index]--;



            var projections = Enumerable.Range(0, densities.Length).
                Select(o => densities.
                    Select(p => new
                    {
                        item = p,
                        density = p.Index == o ?
                            p.Density_Minus :
                            p.Density_Current,
                    }).
                    OrderByDescending(p => p.density).
                    ToArray()).
                Select((o,i) => new
                {
                    index = i,
                    projected_densities = o,
                    lowest_density = o.Min(p => p.density),
                }).
                OrderByDescending(o => o.lowest_density).
                ToArray();





            counts[projections[0].index]--;
            current_count--;
        }

        private static Point3D[] GetSamples(BezierSegment3D_wpf[] beziers, int[] counts, bool is_closed)
        {
            var retVal = new List<Point3D>();

            for (int i = 0; i < beziers.Length; i++)
            {
                int count_adjusted = counts[i];
                bool take_first = true;

                if (i > 0)  // || is_closed)        // turns out the first point of the first segment is needed
                {
                    // The first point of i is the same as the last point of i-1.  If this is closed, then the last
                    // point of ^1 will be used as the first point of 0
                    count_adjusted++;
                    take_first = false;
                }

                Point3D[] points = BezierUtil.GetPoints(count_adjusted, beziers[i]);

                if (take_first)
                    retVal.AddRange(points);
                else
                    retVal.AddRange(points.Skip(1));
            }

            return retVal.ToArray();
        }

        private record AdjustmentDensity
        {
            public int Index { get; init; }

            public double Density_Minus { get; init; }
            public double Density_Current { get; init; }
            public double Density_Plus { get; init; }
        }

        #region Private Methods

        private void RefreshControls(int count)
        {
            if (_controls != null)
                _viewport.Children.RemoveAll(_controls.Select(o => o.Visual));

            var controls = new List<ControlDot>();

            Color[] colors = UtilityWPF.GetRandomColors(count, 96, 160);

            for (int i = 0; i < count; i++)
            {
                double pos = (double)(i + 1) / (count + 1);

                controls.Add(GetControlDot(new Point3D(pos, pos, 0), pos, colors[i]));
            }

            _controls = controls.ToArray();
        }

        private void RefreshBezier()
        {
            RefreshBezier_Clear();

            Point3D[] controls = _controls.
                Select(o => o.Center).
                ToArray();

            var stretch_transform = new BezierSegment3D_wpf(new Point3D(0, 0, 0), new Point3D(1, 1, 0), controls);

            RefreshBezier_MainWindow(_controls, stretch_transform);
            RefreshBezier_1DOffsets(stretch_transform);
            //RefreshBezier_Curve(bezier);
            RefreshBezier_AboveBelow(_controls, stretch_transform);

            if (_beziers != null)
            {
                EnsureWindowCreated(ref _window_sample_uniform, "Uniform");
                EnsureWindowCreated(ref _window_sample_stretched, "Stretched");
                EnsureWindowCreated(ref _window_sample_heat, "Heat");
                EnsureWindowCreated(ref _window_sample_need_current, "Need / Current");

                RefreshBezier_Uniform();
                RefreshBezier_Stretch(stretch_transform, _controls);
                RefreshBezier_Heat(_heatmap);
                RefreshBezier_NeedCurrent(_controls, _heatmap, stretch_transform);
            }
        }
        private void RefreshBezier_Clear()
        {
            _viewport.Children.RemoveAll(_temp_visuals);
            messages.Children.Clear();

            _window_offset1D.Clear();
            //_window_curve.Clear();
            _window_abovebelow.Clear();

            if (_window_sample_stretched != null)
                _window_sample_stretched.Clear();

            if (_window_sample_uniform != null)
                _window_sample_uniform.Clear();

            if (_window_sample_heat != null)
                _window_sample_heat.Clear();

            if (_window_sample_need_current != null)
                _window_sample_need_current.Clear();
        }
        private void RefreshBezier_MainWindow(ControlDot[] controls, BezierSegment3D_wpf stretch_transform)
        {
            Point3D[] control_centers = controls.
                Select(o => o.Center).
                ToArray();

            // Control Positions
            foreach (Point3D control in control_centers)
            {
                messages.Children.Add(new TextBlock() { Text = control.ToStringSignificantDigits(3) });
            }

            // Control Lines
            var control_points = new List<Point3D>();

            control_points.Add(new Point3D(0, 0, 0));
            control_points.AddRange(control_centers);
            control_points.Add(new Point3D(1, 1, 0));

            _temp_visuals.Add(Debug3DWindow.GetLines(control_points, _sizes.line, Colors.IndianRed));
            _viewport.Children.Add(_temp_visuals[^1]);

            // Control Vertical Lines
            foreach (ControlDot control in controls)
            {
                _temp_visuals.Add(Debug3DWindow.GetLine(new Point3D(control.AtTotalPercent, 0, 0), new Point3D(control.AtTotalPercent, 1, 0), _sizes.line, control.Color));
                _viewport.Children.Add(_temp_visuals[^1]);

                _temp_visuals.Add(Debug3DWindow.GetLine(new Point3D(0, control.AtTotalPercent, 0), new Point3D(1, control.AtTotalPercent, 0), _sizes.line, Colors.Gray));
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
            Point3D[] points = BezierUtil.GetPoints(144, stretch_transform);

            _temp_visuals.Add(Debug3DWindow.GetDots(points, _sizes.dot, Colors.White));
            _viewport.Children.Add(_temp_visuals[^1]);
        }
        private void RefreshBezier_1DOffsets(BezierSegment3D_wpf stretch_transform)
        {
            double count = 12;

            double max_dist = 0;

            for (int i = 0; i < count; i++)
            {
                double percent = i / (count - 1);
                double percent_stretched = BezierUtil.GetPoint(percent, stretch_transform.Combined).Y;

                Point3D point_orig = new Point3D(percent, -0.05, 0);
                Point3D point_stretched = new Point3D(percent_stretched, 0.05, 0);

                _window_offset1D.AddDot(point_orig, _sizes.dot, Colors.Gray);
                _window_offset1D.AddDot(point_stretched, _sizes.dot, Colors.White);
                _window_offset1D.AddLine(point_orig, point_stretched, _sizes.line, Colors.Gray);

                max_dist = Math.Max(Math.Abs(percent - percent_stretched), max_dist);
            }

            _window_offset1D.AddText($"max distance: {max_dist}");
        }
        private void RefreshBezier_Curve(BezierSegment3D_wpf stretch_transform)
        {
            _window_curve.AddLine(new Point3D(0, 0, 0), new Point3D(1, 0, 0), _sizes.line, Colors.Black);
            _window_curve.AddLine(new Point3D(0, 1, 0), new Point3D(1, 1, 0), _sizes.line, Colors.Black);
            _window_curve.AddLine(new Point3D(0, 0, 0), new Point3D(0, 1, 0), _sizes.line, Colors.Black);
            _window_curve.AddLine(new Point3D(1, 0, 0), new Point3D(1, 1, 0), _sizes.line, Colors.Black);

            double count = 144;

            for (int i = 0; i < count; i++)
            {
                double percent = i / (count - 1);
                double percent_stretched = BezierUtil.GetPoint(percent, stretch_transform.Combined).Y;

                _window_curve.AddDot(new Point3D(percent, percent_stretched, 0), _sizes.dot, Colors.White);
            }
        }
        private void RefreshBezier_AboveBelow(ControlDot[] controls, BezierSegment3D_wpf stretch_transform)
        {
            double draw_width_half = _sizes.dot * (3d / 2d);
            double arrow_half = _sizes.dot * (7d / 2d);
            double arrow_offset_y = 0.1;

            _window_abovebelow.AddLine(new Point3D(0, 0, 0), new Point3D(1, 0, 0), _sizes.line, Colors.Black);

            //for (int i = 0; i < controls.Length; i++)
            foreach (ControlDot control in controls)
            {
                double bezier_val = BezierUtil.GetPoint(control.AtTotalPercent, stretch_transform.Combined).Y;
                double control_val = control.Center.Y;

                double diff = control_val - bezier_val;

                Point p1 = new Point(control.AtTotalPercent - draw_width_half, 0);
                Point p2 = new Point(control.AtTotalPercent + draw_width_half, diff);

                _window_abovebelow.AddSquare(p1, p2, control.Color);

                if (diff.IsNearZero())
                    continue;

                //TODO: Draw an arrow pointing left or right
                // Below the line is left
                // Above the line is right

                double y = diff > 0 ?
                    diff + arrow_offset_y :
                    diff - arrow_offset_y;

                _window_abovebelow.AddLine(new Point3D(control.AtTotalPercent - arrow_half, y, 0), new Point3D(control.AtTotalPercent + arrow_half, y, 0), _sizes.line, control.Color, diff < 0, diff > 0);
            }
        }
        private void RefreshBezier_Uniform()
        {
            Point3D[] points = BezierUtil.GetPoints(_beziers.Length * 12, _beziers);

            var sizes = Debug3DWindow.GetDrawSizes(points);

            _window_sample_uniform.AddDots(points, sizes.dot, Colors.Black);
            _window_sample_uniform.AddLines(points, sizes.line, Colors.White);
        }
        private void RefreshBezier_Stretch(BezierSegment3D_wpf stretch_transform, ControlDot[] controls)
        {
            // Stretched bezier
            Point3D[] points = BezierUtil.GetPoints_PinchImproved2(_beziers.Length * 12, _beziers, stretch_transform);

            var sizes = Debug3DWindow.GetDrawSizes(points);

            _window_sample_stretched.AddDots(points, sizes.dot, Colors.Black);
            _window_sample_stretched.AddLines(points, sizes.line, Colors.White);

            // Control point locations
            points = BezierUtil.GetPoints(controls.Length + 2, _beziers);

            for (int i = 1; i < points.Length - 1; i++)
            {
                _window_sample_stretched.AddDot(points[i], sizes.dot * 6, Color.FromArgb(32, controls[i - 1].Color.R, controls[i - 1].Color.G, controls[i - 1].Color.B), false, true);
            }
        }
        private void RefreshBezier_Heat(BezierUtil.CurvatureSample[] heatmap)
        {
            var sizes = Debug3DWindow.GetDrawSizes(_beziers.SelectMany(o => o.Combined));

            double max_dist_from_negone = heatmap.Max(o => o.Dist_From_NegOne);

            _window_sample_heat.AddDot(_beziers[0].EndPoint0, sizes.dot, Colors.Black);
            _window_sample_heat.AddDot(_beziers[^1].EndPoint1, sizes.dot, Colors.Black);

            foreach (var heat in heatmap)
            {
                _window_sample_heat.AddDot(heat.Point, sizes.dot, UtilityWPF.AlphaBlend(Colors.DarkRed, Colors.DarkSeaGreen, heat.Dist_From_NegOne / max_dist_from_negone));
            }

            var lines = new List<(Point3D, Point3D)>();
            lines.Add((_beziers[0].EndPoint0, heatmap[0].Point));
            lines.AddRange(Enumerable.Range(0, heatmap.Length - 1).Select(o => (heatmap[o].Point, heatmap[o + 1].Point)));
            lines.Add((heatmap[^1].Point, _beziers[^1].EndPoint1));

            _window_sample_heat.AddLines(lines, sizes.line * 0.75, Colors.White);
        }
        private void RefreshBezier_NeedCurrent(ControlDot[] controls, BezierUtil.CurvatureSample[] heatmap, BezierSegment3D_wpf stretch_transform)
        {
            const double Y_NEED = 0.5;
            const double Y_CURRENT = -0.5;
            const double GRAPH_HEIGHT_HALF = 0.33;


            // This is a way to visualize stretched 1D space

            // Zero is 1:1
            // Positive is pinched
            // Negative is expanded

            _window_sample_need_current.AddLine(new Point3D(0, Y_NEED, 0), new Point3D(1, Y_NEED, 0), _sizes.line, Colors.Black);
            _window_sample_need_current.AddLine(new Point3D(0, Y_CURRENT, 0), new Point3D(1, Y_CURRENT, 0), _sizes.line, Colors.Black);

            var (min_y, max_y) = Math1D.MinMax(new[] { Y_NEED + GRAPH_HEIGHT_HALF, Y_NEED - GRAPH_HEIGHT_HALF, Y_CURRENT + GRAPH_HEIGHT_HALF, Y_CURRENT - GRAPH_HEIGHT_HALF });

            foreach (var control in controls)
            {
                _window_sample_need_current.AddLine(new Point3D(control.AtTotalPercent, min_y, 0), new Point3D(control.AtTotalPercent, max_y, 0), _sizes.line, control.Color);
            }

            // The need graph shows how space should be stretched according to the heatmap
            //  Don't use weight, use the actual dots
            //  Or more accurately, make a weight that isn't normalized 0 to 1, but normalized min to max according to the heatmap's values
            RefreshBezier_NeedCurrent_Need(heatmap, GRAPH_HEIGHT_HALF, Y_NEED);

            // The current graph is the result of the current bezier transform
            //  take N sample points
            //  figure out what the average distance between points should be
            //  for each line segment, drag the bar up or down based on distance relative to average
            RefreshBezier_NeedCurrent_Current(stretch_transform, GRAPH_HEIGHT_HALF, Y_CURRENT);
        }
        private void RefreshBezier_NeedCurrent_Need_ATTEMPT1(BezierUtil.CurvatureSample[] heatmap, double graph_height_half, double y)
        {
            double MAX_DIST = 0.2;

            // max_dist of .15 is a fairly tight pinch
            // .25 is really tight

            // I think .2 is a good max

            double min_dot = heatmap.Min(o => o.Dot);
            double max_dot = heatmap.Max(o => o.Dot);

            double min_dist = heatmap.Min(o => o.Dist_From_NegOne);
            double max_dist = heatmap.Max(o => o.Dist_From_NegOne);


            _window_sample_need_current.AddText($"max dot: {max_dot}");
            _window_sample_need_current.AddText($"min dot: {min_dot}");

            _window_sample_need_current.AddText($"min dist: {min_dist}");
            _window_sample_need_current.AddText($"max dist: {max_dist}");



            // -------- First Pass --------
            // Get a weight based on dist=0 to dist=.2

            var weighted = heatmap.
                Select(o => new
                {
                    heat = o,
                    weight = UtilityMath.GetScaledValue_Capped(0, 1, 0, MAX_DIST, o.Dist_From_NegOne),
                }).
                ToArray();


            // -------- Second Pass --------
            // Some way to find the mid point
            // Everything > is above zero
            // Everything < is below zero
            // This shouldn't normalize -1 to 1.  If it's a mild curve, then this will also be mild
            //
            // Maybe just be based on 

            double max_weight = weighted.Max(o => o.weight);
            double mid_weight = max_weight / 2;

            // this isn't right, it's not about values above and below, it should be area above and below
            var adjusted_average = weighted.
                Select(o => new
                {
                    o.heat,
                    o.weight,
                    adjusted = (o.weight - mid_weight) * 2,
                }).
                ToArray();


            // TODO: Find the dividing line that evenly splits area above and below that line





        }
        private void RefreshBezier_NeedCurrent_Need(BezierUtil.CurvatureSample[] heatmap, double graph_height_half, double y)
        {
            // max_dist of .15 is a fairly tight pinch
            // .25 is really tight
            // I think .2 is a good standard max

            // Need to define a curve that approaches 1


            // -------- Get Weight --------
            // run the Dist_From_NegOne through the curve function to get a weight


            // -------- Get slice line --------
            // find a line the cuts the weighted curve in half, so that the same amount of area is above and below


            // -------- Recenter --------
            // shift the weights so the slice line is at y=0


            // -------- Draw --------
            // draw as bar and line graph

        }
        private void RefreshBezier_NeedCurrent_Current(BezierSegment3D_wpf stretch_transform, double graph_height_half, double y)
        {
            int sample_count = 18;

            //double ideal_avg = 1d / (sample_count + 1);
            double ideal_avg = 1d / sample_count;
            double max_mult = 3;

            double[] samples = BezierUtil.GetPoints(sample_count, stretch_transform).
                Select(o => o.Y).
                ToArray();

            double[] heights = Enumerable.Range(0, sample_count - 1).
                Select(o =>
                {
                    double diff = samples[o + 1] - samples[o];
                    double mult = diff / ideal_avg;

                    return mult < 1 ?
                        UtilityMath.GetScaledValue_Capped(0, graph_height_half, 1, 1d / max_mult, mult) :
                        UtilityMath.GetScaledValue_Capped(0, -graph_height_half, 1, max_mult, mult);
                }).
                ToArray();

            var points = new List<Point3D>();

            double bar_width = 1d / (sample_count - 1);
            double bar_width_half = bar_width / 2; ;

            double x = bar_width_half;

            for (int i = 0; i < heights.Length; i++)
            {
                points.Add(new Point3D(x, y + heights[i], 0));

                _window_sample_need_current.AddSquare(new Point(x - bar_width_half, y), new Point(x + bar_width_half, y + heights[i]), Colors.White);

                x += bar_width;
            }

            _window_sample_need_current.AddLines(points, _sizes.line, Colors.Black);
        }

        private static void EnsureWindowCreated(ref Debug3DWindow window, string title)
        {
            if (window == null)
            {
                window = new Debug3DWindow()
                {
                    Title = title,
                };

                window.Show();
            }
        }

        private ControlDot GetControlDot(Point3D position, double at_total_percent, Color color)
        {
            double dot_radius = _sizes.dot * 3;
            double click_radius = dot_radius * 3;

            Visual3D visual = Debug3DWindow.GetDot(new Point3D(), dot_radius, color);

            var translate = new TranslateTransform3D(position.X, position.Y, position.Z);
            visual.Transform = translate;

            _viewport.Children.Add(visual);

            return new ControlDot()
            {
                Center = position,
                DetectRadius = click_radius,
                Translate = translate,
                Visual = visual,
                Color = color,
                AtTotalPercent = at_total_percent,
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

        private static (double below, double above) GetArea(Point[] points, double center_line)
        {

            //Math2D.GetAreaPolygon();


            var polys = SliceGraph(points, new Point(), new Vector(1, 0));





            return (0, 0);
        }

        /// <summary>
        /// This will fire a line through a graph and return polygons that are above and below that line
        /// </summary>
        private static (Point[][] below, Point[][] above) SliceGraph(Point[] points, Point line_point, Vector line_direction)
        {
            for (int i = 0; i < points.Length - 1; i++)
            {





            }

            return (null, null);
        }

        #endregion

    }
}
