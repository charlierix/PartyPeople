using Accord.Math.Distances;
using BepuUtilities;
using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using GeneticSharp.Domain.Mutations;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Net;
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
    public partial class WallJumpConfig : Window
    {
        #region record: RotatableLine

        private record RotatableLine
        {
            public FrameworkElement Line { get; init; }
            public RotateTransform Rotate { get; init; }
        }

        #endregion

        #region enum: SelectedPropsAtAngle

        private enum SelectedPropsAtAngle
        {
            DirectFaceWall,
            FaceWall,
            AlongStart,
            AlongEnd,
            DirectSway,
        }

        #endregion
        #region record: PropsAtAllAngles

        private record PropsAtAllAngles
        {
            public PropsAtAngle DirectFaceWall { get; init; }
            public PropsAtAngle FaceWall { get; init; }
            public PropsAtAngle AlongStart { get; init; }
            public PropsAtAngle AlongEnd { get; init; }
            public PropsAtAngle DirectAway { get; init; }
        }

        #endregion
        #region record: PropsAtAngle

        private record PropsAtAngle
        {
            public double Percent_Up { get; init; }
            public double Percent_Along { get; init; }
            public double Percent_Away { get; init; }

            public double Strength { get; init; }

            public double Velocity_Mult { get; init; }

            public double YawTurn_Percent { get; init; }

            // along correction amount
            //  this will apply a force that pushes them along the wall.  Needs to account for current velocity

        }

        #endregion

        #region record: HorizontalAngles

        private record HorizontalAngles
        {
            public double FaceWall { get; init; }
            public double AlongStart { get; init; }
            public double AlongEnd { get; init; }
        }

        #endregion

        #region Declaration Section

        private const string COLOR_1 = "222";       // stationary,primary,static....?
        private const string COLOR_2 = "DDD";       // background,info....?
        private const string COLOR_HORZ_DIRECT_WALL = "B3837F";       // directly facing the wall
        private const string COLOR_HORZ_INDIRECT_WALL = "ADAC58";       // sort of facing the wall
        private const string COLOR_HORZ_ALONG_WALL = "7BBDAA";       // sort of facing the wall
        private const string COLOR_VERT_STRAIGHTUP = "5AA4E0";       // this and above are fully jumping straight up
        private const string COLOR_VERT_DEADZONE = "B3837F";     // between this and straight up is a dead zone (doesn't make sense, may need another line?)
        private const string COLOR_VERT_STANDARD = "30A030";

        private const double ARROW_LENGTH = 9;
        private const double ARROW_WIDTH = 7;

        private const double HORZ_INNER1_RADIUS = 54;
        private const double HORZ_OUTER1_RADIUS = 48;

        private const double HORZ_INNER2_RADIUS = 36;
        private const double HORZ_OUTER2_RADIUS = 90;

        private const double HORZ_RADIUS1 = HORZ_INNER1_RADIUS + HORZ_OUTER1_RADIUS;
        private const double HORZ_RADIUS2 = HORZ_INNER2_RADIUS + HORZ_OUTER2_RADIUS;

        private const double VERT_INNER1_RADIUS = 54;
        private const double VERT_OUTER1_RADIUS = 48;

        private const double VERT_INNER2_RADIUS = 36;
        private const double VERT_OUTER2_RADIUS = 90;

        private const double VERT_RADIUS1 = VERT_INNER1_RADIUS + VERT_OUTER1_RADIUS;
        private const double VERT_RADIUS2 = VERT_INNER2_RADIUS + VERT_OUTER2_RADIUS;

        // Horizontal
        private FrameworkElement _horz_stickman = null;
        private FrameworkElement _horz_base_arrows = null;
        private FrameworkElement _horz_wall = null;
        private RotatableLine _horz_directwall_left = null;
        private RotatableLine _horz_directwall_right = null;
        private RotatableLine _horz_indirectwall_left = null;
        private RotatableLine _horz_indirectwall_right = null;
        private RotatableLine _horz_alongwall_left = null;
        private RotatableLine _horz_alongwall_right = null;

        private PropsAtAllAngles _props_horz = null;
        private SelectedPropsAtAngle _selected_props = SelectedPropsAtAngle.DirectFaceWall;
        private bool _swappingprops_horz = false;

        // Vertical
        private FrameworkElement _vert_stickman = null;
        private FrameworkElement _vert_base_arrows = null;
        private FrameworkElement _vert_wall = null;
        private RotatableLine _vert_straightup = null;
        private RotatableLine _vert_deadzone = null;
        private RotatableLine _vert_standard = null;

        private bool _loaded = false;

        #endregion

        #region Contructor

        public WallJumpConfig()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;

            _props_horz = GetPreset_Attempt1();
        }

        #endregion

        #region Event Listeners

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                CreateImage_Horizontal();
                CreateImage_Vertical();

                _loaded = true;

                RefreshImage_Horizontal();
                RefreshImage_Vertical();
                HorizontalRadio_Checked(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HorizontalAngleSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (!_loaded)
                    return;

                RefreshImage_Horizontal();
                RefreshStrengthPlots();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void VerticalAngleSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (!_loaded)
                    return;

                RefreshImage_Vertical();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HorizontalRadio_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_loaded)
                    return;

                _selected_props = GetWhichSelected_PropsAtAngle();

                PropsAtAngle props = GetProps(_props_horz, _selected_props);

                _swappingprops_horz = true;
                Present_PropsAtAngle(props);
                _swappingprops_horz = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void PropsAtAngleSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (!_loaded || _swappingprops_horz)
                    return;

                PropsAtAngle props = Scrape_PropsAtAngle();

                _props_horz = SetProps(_props_horz, _selected_props, props);

                RefreshStrengthPlots();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Anim_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AnimationCurve anim = null;
                switch (StaticRandom.Next(7))
                //switch (StaticRandom.Next(3, 6))
                //switch (2)
                {
                    case 0:
                        anim = new AnimationCurve();        // empty
                        break;

                    case 1:
                        anim = TestAnim_Random_XY(1);
                        break;

                    case 2:
                        anim = TestAnim_Random_XY(2);
                        break;

                    case 3:
                        anim = TestAnim_Random_XY(3);
                        break;

                    case 4:
                        anim = TestAnim_Squared();
                        break;

                    case 5:
                        anim = TestAnim_Random_XY();
                        break;

                    case 6:
                        anim = TestAnim_Random_Y();
                        break;

                    default:
                        throw new ApplicationException("bad switch");
                }

                var window = new Debug3DWindow();

                var sizes = Debug3DWindow.GetDrawSizes(32);

                double min = anim.Min_Key - 1;
                double max = anim.Max_Key + 1;

                int steps = 144;

                var points = Enumerable.Range(0, steps).
                    Select(o =>
                    {
                        double x = UtilityMath.GetScaledValue(min, max, 0, steps - 1, o);

                        return new
                        {
                            key = x,
                            value = anim.Evaluate(x),
                        };
                    }).
                    Select(o => new Point3D(o.key, o.value, 0)).
                    ToArray();

                window.AddLines(anim.KeyValues.Select(o => new Point3D(o.key, o.value, 0)), sizes.line * 0.25, Colors.Black);

                window.AddLines(points, sizes.line, Colors.White);

                if (anim.NumPoints > 2)
                    window.AddLines(BezierUtil.GetPoints(144, anim.Bezier), sizes.line * 0.5, Colors.DodgerBlue);

                //foreach(var point in points)
                //{
                //    window.AddDot(new Point3D(point.X, 0, -1), sizes.dot, Colors.PowderBlue);
                //}

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static AnimationCurve TestAnim_Squared()
        {
            var retVal = new AnimationCurve();

            retVal.AddKeyValue(0, 0);
            retVal.AddKeyValue(1, 1);
            retVal.AddKeyValue(2, 4);
            retVal.AddKeyValue(3, 8);
            retVal.AddKeyValue(4, 16);
            retVal.AddKeyValue(5, 32);

            return retVal;
        }
        private static AnimationCurve TestAnim_Random_XY(int count = 6)
        {
            var retVal = new AnimationCurve();

            for (int i = 0; i < count; i++)
            {
                Vector3D point = Math3D.GetRandomVector_Circular(16);
                retVal.AddKeyValue(point.X, point.Y);
            }

            return retVal;
        }
        private static AnimationCurve TestAnim_Random_Y()
        {
            var retVal = new AnimationCurve();

            double max_x = 16;
            double min_x = -max_x;

            double max_y = 16;
            double min_y = -max_y;

            int count = 5;
            //int count = 12;

            for (int i = 0; i < count; i++)
            {
                double x = UtilityMath.GetScaledValue(min_x, max_x, 0, count - 1, i);
                double y = StaticRandom.NextDouble(min_y, max_y);

                retVal.AddKeyValue(x, y);
            }

            return retVal;
        }

        #endregion
        #region Event Listeners - curve heatmap

        private Point3D[] _endpoints = null;

        private void CurveHeatmap_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _endpoints = Enumerable.Range(0, StaticRandom.Next(3, 8)).
                    Select(o => Math3D.GetRandomVector_Spherical(4).ToPoint()).
                    ToArray();

                //Heatmap(_endpoints);
                Heatmap2(_endpoints);
                Heatmap3(_endpoints);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void RepeatPrev2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //Heatmap(_endpoints);
                Heatmap2(_endpoints);
                Heatmap3(_endpoints);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Heatmap2(Point3D[] endpoints)
        {
            var beziers = BezierUtil.GetBezierSegments(endpoints, 0.3, false);

            Point3D[] uniform_samples = BezierUtil.GetPoints(beziers.Length * 12, beziers);

            BezierUtil.CurvatureSample[] heatmap = BezierUtil.GetCurvatureHeatmap(beziers);
            double max_dist_from_negone = heatmap.Max(o => o.Dist_From_NegOne);


            var sizes = Debug3DWindow.GetDrawSizes(8);
            double small_dot = sizes.dot * 0.36;

            // --------------- Points ---------------
            var window = new Debug3DWindow();

            window.AddDot(beziers[0].EndPoint0, small_dot, Colors.Black);
            window.AddDot(beziers[^1].EndPoint1, small_dot, Colors.Black);

            foreach (var heat in heatmap)
            {
                window.AddDot(heat.Point, small_dot * 1.5, UtilityWPF.AlphaBlend(Colors.DarkRed, Colors.DarkSeaGreen, heat.Dist_From_NegOne / max_dist_from_negone));
            }


            var lines = new List<(Point3D, Point3D)>();
            lines.Add((beziers[0].EndPoint0, heatmap[0].Point));
            lines.AddRange(Enumerable.Range(0, heatmap.Length - 1).Select(o => (heatmap[o].Point, heatmap[o + 1].Point)));
            lines.Add((heatmap[^1].Point, beziers[^1].EndPoint1));

            window.AddLines(lines, sizes.line * 0.75, Colors.White);


            window.AddDots(uniform_samples, small_dot * 0.9, UtilityWPF.ColorFromHex("C0C0C0"));
            window.AddLines(uniform_samples, sizes.line * 0.5, UtilityWPF.ColorFromHex("AAA"));

            window.Show();

            // --------------- Graph ---------------

            //window = new Debug3DWindow();

            //var graphs = new[]
            //{
            //    Debug3DWindow.GetGraph(heatmap.Select(o => o.Dist_From_NegOne / max_dist_from_negone).ToArray()),
            //    Debug3DWindow.GetGraph(heatmap.Select(o => 1d - (o.Dist_From_NegOne / max_dist_from_negone)).ToArray()),        // 1 - %
            //};
            //window.AddGraphs(graphs, new Point3D(), 12);

            //window.Show();


            //string report = GetHeatmapReport(heatmap, max_dist_from_negone);
            //Clipboard.SetText(report);
        }
        private void Heatmap3(Point3D[] endpoints)
        {
            var beziers = BezierUtil.GetBezierSegments(endpoints, 0.3, false);

            Point3D[] uniform_samples = BezierUtil.GetPoints(beziers.Length * 12, beziers);

            BezierUtil.CurvatureSample[] heatmap = BezierUtil.GetCurvatureHeatmap(beziers);
            double max_dist_from_negone = heatmap.Max(o => o.Dist_From_NegOne);

            PathSnippet[] map = TempBezierUtil.GetPinchedMapping(heatmap, endpoints.Length, beziers);

            Point3D[] pinch_improved_samples = BezierUtil.GetPoints_PinchImproved(beziers.Length * 12, beziers, map);

            //var sizes = Debug3DWindow.GetDrawSizes(1);

            //var window = new Debug3DWindow();

            //window.AddLine(new Point3D(0, 0, 0), new Point3D(1, 0, 0), sizes.line, Colors.Black);
            //window.AddLine(new Point3D(0, 0, 0), new Point3D(1, 1, 0), sizes.line, Colors.Black);

            //for (int i = 0; i < endpoints.Length; i++)
            //{
            //    window.AddDot(new Point3D((double)i / (endpoints.Length - 1), 0, 0), sizes.dot, Colors.Black);
            //}

            //Color[] colors = UtilityWPF.GetRandomColors(map.Length, 100, 200);

            //for (int i = 0; i < map.Length; i++)
            //{
            //    window.AddTriangle(new Point3D(map[i].From_Percent_In, map[i].From_Percent_Out, 0), new Point3D(map[i].To_Percent_In, map[i].From_Percent_Out, 0), new Point3D(map[i].To_Percent_In, map[i].To_Percent_Out, 0), colors[i]);
            //    window.AddSquare(new Point(map[i].From_Percent_In, 0), new Point(map[i].To_Percent_In, map[i].From_Percent_Out), colors[i]);
            //}

            //window.Show();



            var sizes = Debug3DWindow.GetDrawSizes(8);
            double small_dot = sizes.dot * 0.36;

            var window = new Debug3DWindow();

            window.AddDots(uniform_samples, small_dot * 0.9, UtilityWPF.ColorFromHex("666"));
            window.AddLines(uniform_samples, sizes.line * 0.5, UtilityWPF.ColorFromHex("AAA"));

            window.Show();

        }

        private static string GetHeatmapReport(BezierUtil.CurvatureSample[] heatmap, double max_dist_from_negone)
        {
            var retVal = new List<string>();

            for (int i = 0; i < heatmap.Length; i++)
            {
                double x = (double)i / (heatmap.Length - 1);
                double y = heatmap[i].Dist_From_NegOne / max_dist_from_negone;

                retVal.Add($"{x.ToStringSignificantDigits(3)}\t{y.ToStringSignificantDigits(3)}");
            }

            return retVal.ToJoin("\r\n");
        }

        #endregion

        #region Private Methods

        private void CreateImage_Horizontal()
        {
            double width = canvas_horz.ActualWidth;
            double height = canvas_horz.ActualHeight;
            Point center = new Point(width / 2, height / 2);

            _horz_stickman = GetGraphic_Stickman(center);
            canvas_horz.Children.Add(_horz_stickman);

            _horz_base_arrows = GetGraphic_Horizontal_Arrows_Four(center);
            canvas_horz.Children.Add(_horz_base_arrows);

            _horz_wall = GetGraphic_Horizontal_Wall(center + new Vector(0, -(Math.Max(HORZ_RADIUS1, HORZ_RADIUS2) + 24)));
            canvas_horz.Children.Add(_horz_wall);

            Brush brush = UtilityWPF.BrushFromHex(COLOR_HORZ_DIRECT_WALL);
            _horz_directwall_left = GetGraphic_RotateableLine(center, brush, HORZ_INNER2_RADIUS, HORZ_OUTER2_RADIUS, true);
            _horz_directwall_right = GetGraphic_RotateableLine(center, brush, HORZ_INNER2_RADIUS, HORZ_OUTER2_RADIUS, true);
            canvas_horz.Children.Add(_horz_directwall_left.Line);
            canvas_horz.Children.Add(_horz_directwall_right.Line);

            brush = UtilityWPF.BrushFromHex(COLOR_HORZ_INDIRECT_WALL);
            _horz_indirectwall_left = GetGraphic_RotateableLine(center, brush, HORZ_INNER2_RADIUS, HORZ_OUTER2_RADIUS, true);
            _horz_indirectwall_right = GetGraphic_RotateableLine(center, brush, HORZ_INNER2_RADIUS, HORZ_OUTER2_RADIUS, true);
            canvas_horz.Children.Add(_horz_indirectwall_left.Line);
            canvas_horz.Children.Add(_horz_indirectwall_right.Line);

            brush = UtilityWPF.BrushFromHex(COLOR_HORZ_ALONG_WALL);
            _horz_alongwall_left = GetGraphic_RotateableLine(center, brush, HORZ_INNER2_RADIUS, HORZ_OUTER2_RADIUS, true);
            _horz_alongwall_right = GetGraphic_RotateableLine(center, brush, HORZ_INNER2_RADIUS, HORZ_OUTER2_RADIUS, true);
            canvas_horz.Children.Add(_horz_alongwall_left.Line);
            canvas_horz.Children.Add(_horz_alongwall_right.Line);
        }
        private void CreateImage_Vertical()
        {
            double width = canvas_vert.ActualWidth;
            double height = canvas_vert.ActualHeight;
            Point center = new Point(width / 2, height / 2);

            _vert_stickman = GetGraphic_Stickman(center);
            canvas_vert.Children.Add(_vert_stickman);

            _vert_base_arrows = GetGraphic_Vertical_Arrows_Three(center);
            canvas_vert.Children.Add(_vert_base_arrows);

            _vert_wall = GetGraphic_Vertical_Wall(center + new Vector(Math.Max(VERT_RADIUS1, VERT_RADIUS2) + 24, 0));
            canvas_vert.Children.Add(_vert_wall);

            Brush brush = UtilityWPF.BrushFromHex(COLOR_VERT_STRAIGHTUP);
            _vert_straightup = GetGraphic_RotateableLine(center, brush, VERT_INNER2_RADIUS, VERT_OUTER2_RADIUS, true);
            canvas_vert.Children.Add(_vert_straightup.Line);

            brush = UtilityWPF.BrushFromHex(COLOR_VERT_DEADZONE);
            _vert_deadzone = GetGraphic_RotateableLine(center, brush, VERT_INNER2_RADIUS, VERT_OUTER2_RADIUS, true);
            canvas_vert.Children.Add(_vert_deadzone.Line);

            brush = UtilityWPF.BrushFromHex(COLOR_VERT_STANDARD);
            _vert_standard = GetGraphic_RotateableLine(center, brush, VERT_INNER2_RADIUS, VERT_OUTER2_RADIUS, true);
            canvas_vert.Children.Add(_vert_standard.Line);
        }

        private void RefreshImage_Horizontal()
        {
            double direct = trkHorizontal_DirectWall.Value;
            double indirect = Math.Max(direct, trkHorizontal_InDirectWall.Value);
            double along = Math.Max(indirect, trkHorizontal_AlongWall.Value);

            _horz_directwall_left.Rotate.Angle = direct;
            _horz_directwall_right.Rotate.Angle = -direct;

            _horz_indirectwall_left.Rotate.Angle = indirect;
            _horz_indirectwall_right.Rotate.Angle = -indirect;

            _horz_alongwall_left.Rotate.Angle = along;
            _horz_alongwall_right.Rotate.Angle = -along;
        }
        private void RefreshImage_Vertical()
        {
            double straight_up = trkVertical_StraightUp.Value;
            double dead_zone = Math.Min(straight_up, trkVertical_DeadZone.Value);
            double standard = Math.Min(dead_zone, trkVertical_Standard.Value);

            _vert_straightup.Rotate.Angle = 90 - straight_up;
            _vert_deadzone.Rotate.Angle = 90 - dead_zone;
            _vert_standard.Rotate.Angle = 90 - standard;
        }

        private void RefreshStrengthPlots()
        {
            var angles = Scrape_HorizontalAngles();

            panel_horz_plots.Children.Clear();

            panel_horz_plots.Children.Add(BuildPlot(angles, _props_horz, "Percent Up", trkPropsAtAngle_PercentUp, o => o.Percent_Up));
            panel_horz_plots.Children.Add(BuildPlot(angles, _props_horz, "Percent Along", trkPropsAtAngle_PercentAlong, o => o.Percent_Along));
            panel_horz_plots.Children.Add(BuildPlot(angles, _props_horz, "Percent Away", trkPropsAtAngle_PercentAway, o => o.Percent_Away));
            panel_horz_plots.Children.Add(BuildPlot(angles, _props_horz, "Strength", trkPropsAtAngle_Strength, o => o.Strength));
            panel_horz_plots.Children.Add(BuildPlot(angles, _props_horz, "Velocity Mult", trkPropsAtAngle_VelocityMult, o => o.Velocity_Mult));
            panel_horz_plots.Children.Add(BuildPlot(angles, _props_horz, "Yaw Turn Percent", trkPropsAtAngle_YawTurnPercent, o => o.YawTurn_Percent));
        }

        private SelectedPropsAtAngle GetWhichSelected_PropsAtAngle()
        {
            if (radHorizontalRadio_DirectFaceWall.IsChecked.Value)
                return SelectedPropsAtAngle.DirectFaceWall;

            if (radHorizontalRadio_FaceWall.IsChecked.Value)
                return SelectedPropsAtAngle.FaceWall;

            if (radHorizontalRadio_AlongStart.IsChecked.Value)
                return SelectedPropsAtAngle.AlongStart;

            if (radHorizontalRadio_AlongEnd.IsChecked.Value)
                return SelectedPropsAtAngle.AlongEnd;

            if (radHorizontalRadio_DirectSway.IsChecked.Value)
                return SelectedPropsAtAngle.DirectSway;

            throw new ApplicationException($"Unknown PropsAtAngle");
        }

        private static PropsAtAngle GetProps(PropsAtAllAngles set, SelectedPropsAtAngle selected)
        {
            switch (selected)
            {
                case SelectedPropsAtAngle.DirectFaceWall:
                    return set.DirectFaceWall;

                case SelectedPropsAtAngle.FaceWall:
                    return set.FaceWall;

                case SelectedPropsAtAngle.AlongStart:
                    return set.AlongStart;

                case SelectedPropsAtAngle.AlongEnd:
                    return set.AlongEnd;

                case SelectedPropsAtAngle.DirectSway:
                    return set.DirectAway;

                default:
                    throw new ApplicationException($"Unknown {nameof(SelectedPropsAtAngle)}: {selected}");
            }
        }
        private static PropsAtAllAngles SetProps(PropsAtAllAngles set, SelectedPropsAtAngle selected, PropsAtAngle value)
        {
            switch (selected)
            {
                case SelectedPropsAtAngle.DirectFaceWall:
                    return set with { DirectFaceWall = value };

                case SelectedPropsAtAngle.FaceWall:
                    return set with { FaceWall = value };

                case SelectedPropsAtAngle.AlongStart:
                    return set with { AlongStart = value };

                case SelectedPropsAtAngle.AlongEnd:
                    return set with { AlongEnd = value };

                case SelectedPropsAtAngle.DirectSway:
                    return set with { DirectAway = value };

                default:
                    throw new ApplicationException($"Unknown {nameof(SelectedPropsAtAngle)}: {selected}");
            }
        }

        private HorizontalAngles Scrape_HorizontalAngles()
        {
            return new HorizontalAngles()
            {
                FaceWall = trkHorizontal_DirectWall.Value,
                AlongStart = trkHorizontal_InDirectWall.Value,
                AlongEnd = trkHorizontal_AlongWall.Value,
            };
        }
        private void Present_HorizontalAngles(HorizontalAngles angles)
        {
            trkHorizontal_DirectWall.Value = angles.FaceWall;
            trkHorizontal_InDirectWall.Value = angles.AlongStart;
            trkHorizontal_AlongWall.Value = angles.AlongEnd;
        }

        private PropsAtAngle Scrape_PropsAtAngle()
        {
            return new PropsAtAngle()
            {
                Percent_Up = trkPropsAtAngle_PercentUp.Value,
                Percent_Along = trkPropsAtAngle_PercentAlong.Value,
                Percent_Away = trkPropsAtAngle_PercentAway.Value,
                Strength = trkPropsAtAngle_Strength.Value,
                Velocity_Mult = trkPropsAtAngle_VelocityMult.Value,
                YawTurn_Percent = trkPropsAtAngle_YawTurnPercent.Value,
            };
        }
        private void Present_PropsAtAngle(PropsAtAngle props)
        {
            trkPropsAtAngle_PercentUp.Value = props.Percent_Up;
            trkPropsAtAngle_PercentAlong.Value = props.Percent_Along;
            trkPropsAtAngle_PercentAway.Value = props.Percent_Away;
            trkPropsAtAngle_Strength.Value = props.Strength;
            trkPropsAtAngle_VelocityMult.Value = props.Velocity_Mult;
            trkPropsAtAngle_YawTurnPercent.Value = props.YawTurn_Percent;
        }

        private static FrameworkElement GetGraphic_Stickman(Point offset)
        {
            Brush brush = UtilityWPF.BrushFromHex(COLOR_1);
            double thickness = 1;

            var retVal = new Canvas()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            offset = new Point(offset.X - (46 / 2d), offset.Y - (83 / 2d));

            // Head
            retVal.Children.Add(new Ellipse()
            {
                Width = 20,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Stroke = brush,
                StrokeThickness = thickness,
                Margin = new Thickness(offset.X + 23 - 10, offset.Y, 0, 0),
            });

            // Arms
            retVal.Children.Add(new Line()
            {
                X1 = offset.X + 0,
                Y1 = offset.Y + 31,
                X2 = offset.X + 46,
                Y2 = offset.Y + 31,
                Stroke = brush,
                StrokeThickness = thickness,
            });

            // Body
            retVal.Children.Add(new Line()
            {
                X1 = offset.X + 23,
                Y1 = offset.Y + 20,
                X2 = offset.X + 23,
                Y2 = offset.Y + 50,
                Stroke = brush,
                StrokeThickness = thickness,
            });

            // Left Leg
            retVal.Children.Add(new Line()
            {
                X1 = offset.X + 23,
                Y1 = offset.Y + 50,
                X2 = offset.X + 4,
                Y2 = offset.Y + 83,
                Stroke = brush,
                StrokeThickness = thickness,
            });

            // Right Leg
            retVal.Children.Add(new Line()
            {
                X1 = offset.X + 23,
                Y1 = offset.Y + 50,
                X2 = offset.X + 43,
                Y2 = offset.Y + 83,
                Stroke = brush,
                StrokeThickness = thickness,
            });

            return retVal;
        }

        private static FrameworkElement GetGraphic_Horizontal_Arrows_Four(Point offset)
        {
            Brush brush = UtilityWPF.BrushFromHex(COLOR_2);

            var retVal = new Canvas()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            // Left
            var line = GetGraphic_RotateableLine(offset, brush, HORZ_INNER1_RADIUS, HORZ_OUTER1_RADIUS, false);
            line.Rotate.Angle = -90;
            retVal.Children.Add(line.Line);

            // Right
            line = GetGraphic_RotateableLine(offset, brush, HORZ_INNER1_RADIUS, HORZ_OUTER1_RADIUS, false);
            line.Rotate.Angle = 90;
            retVal.Children.Add(line.Line);

            // Up
            line = GetGraphic_RotateableLine(offset, brush, HORZ_INNER1_RADIUS, HORZ_OUTER1_RADIUS, false);
            line.Rotate.Angle = 0;
            retVal.Children.Add(line.Line);

            // Down
            line = GetGraphic_RotateableLine(offset, brush, HORZ_INNER1_RADIUS, HORZ_OUTER1_RADIUS, false);
            line.Rotate.Angle = 180;
            retVal.Children.Add(line.Line);

            return retVal;
        }
        private static FrameworkElement GetGraphic_Vertical_Arrows_Three(Point offset)
        {
            Brush brush = UtilityWPF.BrushFromHex(COLOR_2);

            var retVal = new Canvas()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            // Right
            var line = GetGraphic_RotateableLine(offset, brush, VERT_INNER1_RADIUS, VERT_OUTER1_RADIUS, false);
            line.Rotate.Angle = 90;
            retVal.Children.Add(line.Line);

            // Up
            line = GetGraphic_RotateableLine(offset, brush, VERT_INNER1_RADIUS, VERT_OUTER1_RADIUS, false);
            line.Rotate.Angle = 0;
            retVal.Children.Add(line.Line);

            // Down
            line = GetGraphic_RotateableLine(offset, brush, VERT_INNER1_RADIUS, VERT_OUTER1_RADIUS, false);
            line.Rotate.Angle = 180;
            retVal.Children.Add(line.Line);

            return retVal;
        }

        private static FrameworkElement GetGraphic_Horizontal_Wall(Point offset)
        {
            Brush brush = UtilityWPF.BrushFromHex(COLOR_1);
            double thickness = 3;

            var retVal = new Canvas()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            double size = Math.Max(HORZ_RADIUS1, HORZ_RADIUS2) * 2 * 1.2;

            offset = new Point(offset.X - (size / 2), offset.Y);

            retVal.Children.Add(new Line()
            {
                X1 = offset.X + 0,
                Y1 = offset.Y,
                X2 = offset.X + size,
                Y2 = offset.Y,
                Stroke = brush,
                StrokeThickness = thickness,
            });

            return retVal;
        }
        private static FrameworkElement GetGraphic_Vertical_Wall(Point offset)
        {
            Brush brush = UtilityWPF.BrushFromHex(COLOR_1);
            double thickness = 3;

            var retVal = new Canvas()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };

            double size = Math.Max(VERT_RADIUS1, VERT_RADIUS2) * 2 * 1.2;

            offset = new Point(offset.X, offset.Y - (size / 2));

            retVal.Children.Add(new Line()
            {
                X1 = offset.X,
                Y1 = offset.Y + 0,
                X2 = offset.X,
                Y2 = offset.Y + size,
                Stroke = brush,
                StrokeThickness = thickness,
            });

            return retVal;
        }

        private static RotatableLine GetGraphic_RotateableLine(Point offset, Brush brush, double inner_radius, double outer_radius, bool show_arrow)
        {
            double thickness = 1;

            var transform = new TransformGroup();
            var rotate = new RotateTransform();
            transform.Children.Add(rotate);

            transform.Children.Add(new TranslateTransform(offset.X, offset.Y));

            var canvas = new Canvas()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                RenderTransform = transform,
            };

            // Line
            var line = new Line()
            {
                X1 = 0,
                Y1 = -inner_radius,
                X2 = 0,
                Y2 = -(inner_radius + outer_radius),
                Stroke = brush,
                StrokeThickness = thickness,
            };
            canvas.Children.Add(line);

            // Arrow
            if (show_arrow)
            {
                var arrow = GetArrowCoords((Line)canvas.Children[^1], ARROW_LENGTH, ARROW_WIDTH);
                var polygon = new Polygon()
                {
                    Fill = brush,
                };
                polygon.Points.AddRange(new[] { arrow.tip, arrow.base1, arrow.base2 });
                canvas.Children.Add(polygon);
            }

            return new RotatableLine()
            {
                Line = canvas,
                Rotate = rotate,
            };
        }

        private static (Point tip, Point base1, Point base2) GetArrowCoords(Line line, double length, double width)
        {
            double magnitude = Math.Sqrt(Math2D.LengthSquared(line.X1, line.Y1, line.X2, line.Y2));

            // Get a unit vector that points from the to point back to the base of the arrow head
            double baseDir_x = (line.X1 - line.X2) / magnitude;
            double baseDir_y = (line.Y1 - line.Y2) / magnitude;

            // Now get two unit vectors that point from the shaft out to the tips
            double edgeDir1_x = -baseDir_y;
            double edgeDir1_y = baseDir_x;

            double edgeDir2_x = baseDir_y;
            double edgeDir2_y = -baseDir_x;

            // Get the point at the base of the arrow that is on the shaft
            double base_x = line.X2 + (baseDir_x * length);
            double base_y = line.Y2 + (baseDir_y * length);

            double halfWidth = width / 2;

            return
            (
                new Point(line.X2, line.Y2),
                new Point(base_x + (edgeDir1_x * halfWidth), base_y + (edgeDir1_y * halfWidth)),
                new Point(base_x + (edgeDir2_x * halfWidth), base_y + (edgeDir2_y * halfWidth))
            );
        }

        private static FrameworkElement BuildPlot(HorizontalAngles angles, PropsAtAllAngles props, string title, Slider slider, Func<PropsAtAngle, double> getValue)
        {
            var retVal = new StackPanel()
            {
                Margin = new Thickness(0, 4, 0, 4),
            };

            retVal.Children.Add(DrawPlot(30, angles, props, slider.Minimum, slider.Maximum, getValue));

            retVal.Children.Add(new TextBlock()
            {
                Text = title,
                FontSize = 10,
                Foreground = UtilityWPF.BrushFromHex("999"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
            });

            return retVal;
        }

        private static FrameworkElement DrawPlot(double radius, HorizontalAngles angles, PropsAtAllAngles props, double minimum, double maximum, Func<PropsAtAngle, double> getValue)
        {
            var values = new List<(double angle, double value, double x, double y)>();

            AnimationCurve curve = BuildAnimationCurve(angles, props, getValue);

            for (int angle = 0; angle <= 180; angle += 5)
            {
                double value = curve.Evaluate(angle);
                double x = radius * Math.Cos(Math1D.DegreesToRadians(angle + 90));      // 0 should be +y
                double y = radius * Math.Sin(Math1D.DegreesToRadians(angle + 90));
                values.Add((angle, value, x, y));
            }

            var retVal = new Canvas()
            {
                Width = radius * 2,
                Height = radius * 2,
            };

            for (int i = 0; i < values.Count - 1; i++)
            {
                double percent = UtilityMath.GetScaledValue(0, 1, minimum, maximum, (values[i].value + values[i + 1].value) / 2);
                Brush brush = new SolidColorBrush(UtilityWPF.AlphaBlend(Colors.Black, Color.FromArgb(0, 255, 255, 255), percent));
                double thickness = UtilityMath.GetScaledValue(0.25, 4, 0, 1, percent);

                retVal.Children.Add(new Line()
                {
                    X1 = radius + values[i].x,
                    Y1 = radius - values[i].y,
                    X2 = radius + values[i + 1].x,
                    Y2 = radius - values[i + 1].y,
                    Stroke = brush,
                    StrokeThickness = thickness,
                });

                retVal.Children.Add(new Line()
                {
                    X1 = radius - values[i].x,
                    Y1 = radius - values[i].y,
                    X2 = radius - values[i + 1].x,
                    Y2 = radius - values[i + 1].y,
                    Stroke = brush,
                    StrokeThickness = thickness,
                });
            }

            return retVal;
        }

        private static AnimationCurve BuildAnimationCurve(HorizontalAngles angles, PropsAtAllAngles props, Func<PropsAtAngle, double> getValue)
        {
            var retVal = new AnimationCurve();

            retVal.AddKeyValue(0, getValue(props.DirectFaceWall));
            retVal.AddKeyValue(angles.FaceWall, getValue(props.FaceWall));
            retVal.AddKeyValue(angles.AlongStart, getValue(props.AlongStart));
            retVal.AddKeyValue(angles.AlongEnd, getValue(props.AlongEnd));
            retVal.AddKeyValue(180, getValue(props.DirectAway));

            return retVal;
        }

        private static PropsAtAllAngles GetPreset_Attempt1()
        {
            return new PropsAtAllAngles()
            {
                DirectFaceWall = new PropsAtAngle()
                {
                    Percent_Up = 1,
                    Percent_Along = 1,
                    Percent_Away = 1,
                    Strength = 1,
                    Velocity_Mult = 1,
                    YawTurn_Percent = 1,
                },

                FaceWall = new PropsAtAngle()
                {
                    Percent_Up = 1,
                    Percent_Along = 1,
                    Percent_Away = 1,
                    Strength = 1,
                    Velocity_Mult = 1,
                    YawTurn_Percent = 1,
                },

                AlongStart = new PropsAtAngle()
                {
                    Percent_Up = 1,
                    Percent_Along = 1,
                    Percent_Away = 1,
                    Strength = 1,
                    Velocity_Mult = 1,
                    YawTurn_Percent = 1,
                },

                AlongEnd = new PropsAtAngle()
                {
                    Percent_Up = 1,
                    Percent_Along = 1,
                    Percent_Away = 1,
                    Strength = 1,
                    Velocity_Mult = 1,
                    YawTurn_Percent = 1,
                },

                DirectAway = new PropsAtAngle()
                {
                    Percent_Up = 1,
                    Percent_Along = 1,
                    Percent_Away = 1,
                    Strength = 1,
                    Velocity_Mult = 1,
                    YawTurn_Percent = 1,
                },
            };
        }

        #endregion
    }
}
