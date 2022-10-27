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
        #region Event Listeners - find bunching

        private Point3D[] _endpoints = null;
        private void FindBunching_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _endpoints = Enumerable.Range(0, StaticRandom.Next(4, 7)).
                    Select(o => Math3D.GetRandomVector_Spherical(4).ToPoint()).
                    ToArray();

                FindBunching(_endpoints);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void RepeatPrev_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FindBunching(_endpoints);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void FindBunching(Point3D[] endpoints)
        {
            var beziers = BezierUtil.GetBezierSegments(endpoints, 0.3, false);

            Point3D[] samples = BezierUtil.GetPoints(72, beziers);

            var distances = Enumerable.Range(1, samples.Length - 2).
                Select(o =>
                {
                    double dist_left = (samples[o] - samples[o - 1]).Length;
                    double dist_right = (samples[o + 1] - samples[o]).Length;
                    //double diff = Math.Abs(dist_left - dist_right);
                    double diff = Math.Min(dist_left, dist_right) / (dist_left + dist_right);

                    return new
                    {
                        index = o,
                        dist_left,
                        dist_right,
                        diff,
                    };
                }).
                ToArray();

            var dist_sorted = distances.
                OrderBy(o => o.diff).
                ToArray();

            var avg = Math1D.Get_Average_StandardDeviation(distances.Select(o => o.diff));


            var sizes = Debug3DWindow.GetDrawSizes(8);

            // --------------- Points ---------------
            var window = new Debug3DWindow()
            {
                Title = "combined",
            };

            window.AddDots(endpoints, sizes.dot * 1.5, Colors.Black);

            window.AddDots(beziers.SelectMany(o => o.ControlPoints), sizes.dot * 1.25, UtilityWPF.ColorFromHex("BA655F"));

            window.AddDots(samples, sizes.dot, UtilityWPF.ColorFromHex("A2D7FC"));

            window.Show();

            // --------------- line diffs ---------------

            window = new Debug3DWindow();

            window.AddLines(Enumerable.Range(0, samples.Length - 1).Select(o => (samples[o], samples[o + 1])), sizes.line, Colors.White);

            window.AddDots(samples, sizes.dot * 0.33, UtilityWPF.ColorFromHex("A2D7FC"));

            // this is now fixed
            //foreach (var top_diff in dist_sorted.Take(2))
            //{
            //    window.AddDot(samples[top_diff.index], sizes.dot * 0.4, Colors.Red);
            //}

            double approx_len = beziers.Sum(o => o.Length_quick);
            for (int i = 0; i < beziers.Length; i++)
            {
                window.AddText($"{i}: {beziers[i].Length_quick} | {Math.Round((beziers[i].Length_quick / approx_len) * 100)}%");
            }

            window.Show();

            // --------------- Independent ---------------
            //window = new Debug3DWindow()
            //{
            //    Title = "independent",
            //};

            //window.AddDots(endpoints, sizes.dot * 1.5, Colors.Black);

            //window.AddDots(beziers.SelectMany(o => o.ControlPoints), sizes.dot * 1.25, UtilityWPF.ColorFromHex("BA655F"));

            //foreach(var bezier in beziers)
            //{
            //    window.AddDots(BezierUtil.GetPoints(24, bezier), sizes.dot, UtilityWPF.ColorFromHex("A2D7FC"));
            //}

            //window.Show();
        }

        #endregion
        #region Event Listeners - curve heatmap

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
        private void Heatmap(Point3D[] endpoints)
        {
            var beziers = BezierUtil.GetBezierSegments(endpoints, 0.3, false);

            Point3D[] uniform_samples = BezierUtil.GetPoints(beziers.Length * 12, beziers);


            // look at each local heatmap, distribute among neighbor boundries


            var sizes = Debug3DWindow.GetDrawSizes(8);
            double small_dot = sizes.dot * 0.36;

            var window = new Debug3DWindow();


            double[] distances_from_negone = beziers.
                SelectMany(o => o.Heatmap).
                Select(o => o.Dist_From_NegOne).
                ToArray();

            double max_dist_from_negone = distances_from_negone.Max();


            foreach (var bezier in beziers)
            {
                window.AddDot(bezier.EndPoint0, small_dot, Colors.Black);
                window.AddDot(bezier.EndPoint1, small_dot, Colors.Black);

                foreach (var heat in bezier.Heatmap)
                {
                    window.AddDot(heat.Point, small_dot * 1.5, UtilityWPF.AlphaBlend(Colors.DarkRed, Colors.DarkSeaGreen, heat.Dist_From_NegOne / max_dist_from_negone));
                }

                var lines = new List<(Point3D, Point3D)>();
                lines.Add((bezier.EndPoint0, bezier.Heatmap[0].Point));
                lines.AddRange(Enumerable.Range(0, bezier.Heatmap.Length - 1).Select(o => (bezier.Heatmap[o].Point, bezier.Heatmap[o + 1].Point)));
                lines.Add((bezier.Heatmap[^1].Point, bezier.EndPoint1));

                window.AddLines(lines, sizes.line * 0.75, Colors.White);
            }

            window.AddDots(uniform_samples, small_dot * 0.9, UtilityWPF.ColorFromHex("C0C0C0"));
            window.AddLines(uniform_samples, sizes.line * 0.5, UtilityWPF.ColorFromHex("AAA"));

            window.Show();
        }
        private void Heatmap2(Point3D[] endpoints)
        {
            var beziers = BezierUtil.GetBezierSegments(endpoints, 0.3, false);

            Point3D[] uniform_samples = BezierUtil.GetPoints(beziers.Length * 12, beziers);

            HM2[] heatmap = GetHeatmap(beziers);
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

            window = new Debug3DWindow();

            var graphs = new[]
            {
                Debug3DWindow.GetGraph(heatmap.Select(o => o.Dist_From_NegOne / max_dist_from_negone).ToArray()),
                Debug3DWindow.GetGraph(heatmap.Select(o => 1d - (o.Dist_From_NegOne / max_dist_from_negone)).ToArray()),        // 1 - %
            };
            window.AddGraphs(graphs, new Point3D(), 12);

            window.Show();


            //string report = GetHeatmapReport(heatmap, max_dist_from_negone);
            //Clipboard.SetText(report);
        }
        private void Heatmap3(Point3D[] endpoints)
        {
            var beziers = BezierUtil.GetBezierSegments(endpoints, 0.3, false);

            Point3D[] uniform_samples = BezierUtil.GetPoints(beziers.Length * 12, beziers);

            HM2[] heatmap = GetHeatmap(beziers);
            double max_dist_from_negone = heatmap.Max(o => o.Dist_From_NegOne);

            PathSnippet[] map = GetPinchedMapping(heatmap, endpoints.Length, beziers);

            var sizes = Debug3DWindow.GetDrawSizes(1);

            var window = new Debug3DWindow();

            window.AddLine(new Point3D(0, 0, 0), new Point3D(1, 0, 0), sizes.line, Colors.Black);
            window.AddLine(new Point3D(0, 0, 0), new Point3D(1, 1, 0), sizes.line, Colors.Black);

            for (int i = 0; i < endpoints.Length; i++)
            {
                window.AddDot(new Point3D((double)i / (endpoints.Length - 1), 0, 0), sizes.dot, Colors.Black);
            }

            Color[] colors = UtilityWPF.GetRandomColors(map.Length, 100, 200);

            for (int i = 0; i < map.Length; i++)
            {
                window.AddTriangle(new Point3D(map[i].From_X, map[i].From_Y, 0), new Point3D(map[i].To_X, map[i].From_Y, 0), new Point3D(map[i].To_X, map[i].To_Y, 0), colors[i]);
                window.AddSquare(new Point(map[i].From_X, 0), new Point(map[i].To_X, map[i].From_Y), colors[i]);
            }

            window.Show();
        }


        //TODO: Add total percent to this.  That will save a lot of bs logic below
        private record HM2
        {
            public int SegmentIndex { get; init; }
            public double Percent_Along_Segment { get; init; }

            public double Percent_Total { get; init; }

            public Point3D Point { get; init; }
            public double Dot { get; init; }
            public double Dist_From_NegOne { get; init; }
        }

        private record PathSnippet
        {
            public double From_X { get; init; }
            public double From_Y { get; init; }
            public double To_X { get; init; }
            public double To_Y { get; init; }
        }

        private static HM2[] GetHeatmap(BezierSegment3D_wpf[] beziers, BezierUtil.NormalizedPosPointer[] percents_actual = null)
        {
            var retVal = new List<HM2>();

            retVal.Add(new HM2()        // having endpoints at 0 and 1 makes processing these easier
            {
                Dist_From_NegOne = 0,
                Dot = -1,
                Percent_Along_Segment = 0,
                Point = beziers[0].EndPoint0,
                SegmentIndex = 0,
            });

            for (int i = 0; i < beziers.Length; i++)
            {
                if (i > 0)
                {
                    // Stitch last segment of prev bezier segment with first of current
                    // NOTE: beziers[i - 1].Samples[^1].Point == beziers[i].Samples[0].Point == beziers[i - 1].EndPoint1 == beziers[i].EndPoint0
                    retVal.Add(GetHeatmap_Item(
                        beziers[i - 1].Samples[^2].Point,
                        beziers[i].EndPoint0,
                        beziers[i].Samples[1].Point,
                        beziers[i - 1].Lengths[^1],
                        beziers[i].Lengths[0],
                        i,
                        0));
                }

                // samples is 12
                // lengths is 11
                // heatmap is 10

                var samples = beziers[i].Samples;
                double[] lengths = beziers[i].Lengths;

                for (int j = 1; j < samples.Length - 1; j++)
                {
                    retVal.Add(GetHeatmap_Item(samples[j - 1].Point, samples[j].Point, samples[j + 1].Point, lengths[j - 1], lengths[j], i, samples[j].Percent_Along));
                }
            }

            retVal.Add(new HM2()
            {
                Dist_From_NegOne = 0,
                Dot = -1,
                Percent_Along_Segment = 1,
                Point = beziers[^1].EndPoint1,
                SegmentIndex = beziers.Length - 1,
            });

            return retVal.ToArray();
        }
        private static HM2 GetHeatmap_Item(Point3D point_left, Point3D point_middle, Point3D point_right, double len_left, double len_right, int segment_index, double percent_middle)
        {
            Vector3D left = point_left - point_middle;
            Vector3D right = point_right - point_middle;

            double dot = Vector3D.DotProduct(left / len_left, right / len_right);
            double dist_from_negone = 1 + dot;

            return new HM2()
            {
                SegmentIndex = segment_index,
                Percent_Along_Segment = percent_middle,
                Point = point_middle,
                Dot = dot,
                Dist_From_NegOne = dist_from_negone,
            };
        }

        private static string GetHeatmapReport(HM2[] heatmap, double max_dist_from_negone)
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

        private static PathSnippet[] GetPinchedMapping(HM2[] heatmap, int endpoint_count, BezierSegment3D_wpf[] beziers)
        {
            int return_count = ((endpoint_count - 1) * 3) + 1;      // this is kind of arbitrary, but should give a good amount of snippets to play with

            double inc = 1d / return_count;

            var initial = Enumerable.Range(0, return_count).
                Select(o => new
                {
                    from = (double)o / return_count,
                    to = (double)(o + 1) / return_count,
                }).
                Select(o => new PathSnippet()
                {
                    From_X = o.from,
                    From_Y = o.from,
                    To_X = o.to,
                    To_Y = o.to,
                }).
                ToArray();

            var retVal = initial;

            for (int i = 0; i < 4; i++)
            {
                //double[] forces = GetForces(retVal, heatmap);
                //retVal = ApplyForces(retVal, forces);

                TestBuckets(retVal, heatmap, beziers);

            }

            return initial;
        }

        private static void TestBuckets(PathSnippet[] snippets, HM2[] heatmap, BezierSegment3D_wpf[] beziers)
        {
            var percents_desired = snippets.
                Skip(1).
                Select(o => o.From_Y).
                ToArray();

            var percents_actual = BezierUtil.ConvertToNormalizedPositions(percents_desired, beziers).
                ToArray();

            var populations = snippets.
                Select(o => GetPopulation(o, percents_actual, heatmap)).
                ToArray();




        }

        private static double GetPopulation(PathSnippet snippet, BezierUtil.NormalizedPosPointer[] percents_actual, HM2[] heatmap)
        {
            // Using Y from to
            var loc_from = GetPopulation_Local(snippet.From_Y, percents_actual);
            var loc_to = GetPopulation_Local(snippet.To_Y, percents_actual);

            // Find the set of heatmap entries that straddle from/to points
            HM2[] straddle_heats = GetPopulation_Heats(loc_from, loc_to, heatmap);

            // Convert heats into points (X is total percent, Y is heat value at X)
            Point[] straddle_heat_points = straddle_heats.
                Select(o => ConvertToPoint(o, percents_actual)).
                ToArray();



            // X is total percent, Y is heat value at X
            Point[] heat_points = GetPopulation_ConvertValues(snippet.From_Y, snippet.To_Y, straddle_heat_points);




            // Add up heatmap values

            double retVal = 0;


            // need to turn these heat spike points into area







            return retVal;
        }

        private static BezierUtil.NormalizedPosPointer GetPopulation_Local(double total_percent, BezierUtil.NormalizedPosPointer[] percents_actual)
        {
            if (total_percent.IsNearZero())
                return new BezierUtil.NormalizedPosPointer()
                {
                    Desired_Index = -1,
                    Desired_Total_Percent = total_percent,
                    Segment_Index = 0,
                    Segment_Local_Percent = 0,
                };

            if (total_percent.IsNearValue(1))
                return new BezierUtil.NormalizedPosPointer()
                {
                    Desired_Index = -1,
                    Desired_Total_Percent = total_percent,
                    Segment_Index = percents_actual[^1].Segment_Index,
                    Segment_Local_Percent = 1,
                };

            for (int i = 0; i < percents_actual.Length; i++)
            {
                if (total_percent.IsNearValue(percents_actual[i].Desired_Total_Percent))
                    return percents_actual[i];
            }

            throw new ApplicationException($"Couldn't find percent mapping: {total_percent}");
        }

        private static HM2[] GetPopulation_Heats(BezierUtil.NormalizedPosPointer from, BezierUtil.NormalizedPosPointer to, HM2[] heatmap)
        {
            int index_left = GetPopulation_Heats_Left(from, heatmap);
            if (index_left < 0)
                throw new ApplicationException("Couldn't find from entry");

            int index_right = GetPopulation_Heats_Right(to, heatmap);
            if (index_right < 0)
                throw new ApplicationException("Couldn't find to entry");

            return Enumerable.Range(index_left, index_right - index_left + 1).
                Select(o => heatmap[o]).
                ToArray();
        }
        private static int GetPopulation_Heats_Left(BezierUtil.NormalizedPosPointer from, HM2[] heatmap)
        {
            int retVal = -1;

            for (int i = 0; i < heatmap.Length; i++)
            {
                if (heatmap[i].SegmentIndex > from.Segment_Index)
                    break;      // went too far

                if (heatmap[i].SegmentIndex < from.Segment_Index)
                {
                    retVal = i;     // best so far
                    continue;
                }

                if (heatmap[i].Percent_Along_Segment.IsNearValue(from.Segment_Local_Percent))
                {
                    retVal = i;     // exact match
                    break;
                }

                if (heatmap[i].Percent_Along_Segment > from.Segment_Local_Percent)
                    break;      // went too far

                retVal = i;     // best so far
            }

            return retVal;
        }
        private static int GetPopulation_Heats_Right(BezierUtil.NormalizedPosPointer to, HM2[] heatmap)
        {
            int retVal = -1;

            for (int i = heatmap.Length - 1; i >= 0; i--)
            {
                if (heatmap[i].SegmentIndex < to.Segment_Index)
                    break;      // went too far

                if (heatmap[i].SegmentIndex > to.Segment_Index)
                {
                    retVal = i;     // best so far
                    continue;
                }

                if (heatmap[i].Percent_Along_Segment.IsNearValue(to.Segment_Local_Percent))
                {
                    retVal = i;     // exact match
                    break;
                }

                if (heatmap[i].Percent_Along_Segment < to.Segment_Local_Percent)
                    break;      // went too far

                retVal = i;     // best so far
            }

            return retVal;
        }

        private static Point ConvertToPoint(HM2 heat_point, BezierUtil.NormalizedPosPointer[] percents_actual)
        {
            // Convert these to into a total percent along the path
            //double total_percent;

            //heat_point.SegmentIndex;
            //heat_point.Percent_Along_Segment;


            //return new Point(total_percent, heat_point.Dist_From_NegOne);

            return new Point();
        }

        private static Point[] GetPopulation_ConvertValues(double from, double to, Point[] heat_points)
        {
            var retVal = new List<Point>();

            //int index = 0;

            //if (heats[0].SegmentIndex == from.Segment_Index && heats[0].Percent_Along_Segment.IsNearValue(from.Segment_Local_Percent))
            //{
            //    retVal.Add(new Point(from.Desired_Total_Percent, heats[0].Dist_From_NegOne));       // sitting exactly on the heat point, no need to lerp
            //    index = 1;
            //}
            //else
            //{
            //    retVal.Add(new Point(from.Desired_Total_Percent, LERP_Heat_Y(heats[0], heats[1], from.Segment_Index, from.Segment_Local_Percent)));
            //    retVal.Add(new Point(GetTotalPercentForLocal(), heats[1].Dist_From_NegOne));
            //    index = 2;
            //}

            //for(int i = index; i < heats.Length - 1; i++)
            //{
            //    retVal.Add(new Point(GetTotalPercentForLocal(), heats[i].Dist_From_NegOne));
            //}


            //TODO: Do the same with to that was done for from



            return retVal.ToArray();
        }
        //private static double LERP_Heat_Y(HM2 heat_left, HM2 heat_right, int segment_index, double segment_local_percent)
        //{

        //}

        private static double ConvertToTotalPercent(int segmentindex, double percent_along_segment, BezierUtil.NormalizedPosPointer[] percents_actual)
        {
            return 0;
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
