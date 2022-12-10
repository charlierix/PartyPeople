using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.Mathematics.GeneticSharp;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using GeneticSharp.Domain.Chromosomes;
using GeneticSharp.Domain.Crossovers;
using GeneticSharp.Domain.Fitnesses;
using GeneticSharp.Domain;
using GeneticSharp.Domain.Mutations;
using GeneticSharp.Domain.Populations;
using GeneticSharp.Domain.Selections;
using GeneticSharp.Domain.Terminations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Text.Json;
using System.IO;

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
        #region record: WallJumpSettings

        private record WallJumpSettings
        {
            public WallJumpSettings_KeyValue[] straightup_vert_percent { get; init; }

            public WallJumpSettings_KeyValue[] percent_vert_whenup { get; init; }
            public WallJumpSettings_KeyValue[] percent_horz_whenup { get; init; }

            public WallJumpSettings_KeyValue[] horz_percent_up { get; init; }
            public WallJumpSettings_KeyValue[] horz_percent_along { get; init; }
            public WallJumpSettings_KeyValue[] horz_percent_away { get; init; }
            public WallJumpSettings_KeyValue[] horz_strength { get; init; }

            public WallJumpSettings_KeyValue[] yaw_turn_percent { get; init; }

            public WallJumpSettings_KeyValue[] horizontal_percent_at_speed { get; init; }

            public WallJumpSettings_KeyValue[] horizontal_percent_look { get; init; }

            public double straightup_strength { get; init; }
            public WallJumpSettings_KeyValue[] straightup_percent_at_speed { get; init; }
        }

        #endregion
        #region record: WallJumpSettings_KeyValue

        private record WallJumpSettings_KeyValue
        {
            public double key { get; init; }
            public double value { get; init; }
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

            public double YawTurn_Percent { get; init; }

            public double Percent_Look { get; init; }
        }

        #endregion

        #region record: HorizontalPropsAngles

        private record HorizontalPropsAngles
        {
            public double FaceWall { get; init; }
            public double AlongStart { get; init; }
            public double AlongEnd { get; init; }

            public PropsAtAllAngles PropsAtAllAngles { get; init; }

            public double Speed_FullStrength { get; init; }
            public double Speed_ZeroStrength { get; init; }
        }

        #endregion
        #region record: VerticalPropsAngles

        private record VerticalPropsAngles
        {
            public double StraightUp { get; init; }
            public double Standard { get; init; }

            public double Strength { get; init; }

            public double Speed_FullStrength { get; init; }
            public double Speed_ZeroStrength { get; init; }
        }

        #endregion

        #region record: AllProps

        private record AllProps
        {
            public HorizontalPropsAngles Horizontal { get; init; }
            public VerticalPropsAngles Vertical { get; init; }
        }

        #endregion

        #region Declaration Section

        private const string COLOR_1 = "222";       // stationary,primary,static....?
        private const string COLOR_2 = "DDD";       // background,info....?
        private const string COLOR_HORZ_DIRECT_WALL = "B3837F";       // directly facing the wall
        private const string COLOR_HORZ_INDIRECT_WALL = "ADAC58";       // sort of facing the wall
        private const string COLOR_HORZ_ALONG_WALL = "7BBDAA";       // sort of facing the wall
        private const string COLOR_VERT_STRAIGHTUP = "5AA4E0";       // this and above are fully jumping straight up
        //private const string COLOR_VERT_DEADZONE = "B3837F";     // between this and straight up is a dead zone (doesn't make sense, may need another line?)
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
        private bool _swappingprops_horz = false;

        // Vertical
        private FrameworkElement _vert_stickman = null;
        private FrameworkElement _vert_base_arrows = null;
        private FrameworkElement _vert_wall = null;
        private RotatableLine _vert_straightup = null;
        //private RotatableLine _vert_deadzone = null;
        private RotatableLine _vert_standard = null;

        private bool _loaded = false;

        #endregion

        #region Contructor

        public WallJumpConfig()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;
        }

        #endregion

        #region Event Listeners

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                AllProps props = GetPreset_Attempt2();

                CreateImage_Horizontal();
                CreateImage_Vertical();

                _loaded = true;

                Present_HorizontalProps(props.Horizontal);
                Present_VerticalProps(props.Vertical);
                RefreshImage_Horizontal();
                RefreshImage_Vertical();
                HorizontalRadio_Checked(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtFileFolder_PreviewDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }
        private void txtFileFolder_Drop(object sender, DragEventArgs e)
        {
            try
            {
                string[] filenames = e.Data.GetData(DataFormats.FileDrop) as string[];

                if (filenames == null || filenames.Length == 0)
                {
                    MessageBox.Show("No folders selected", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (filenames.Length > 1)
                {
                    MessageBox.Show("Only one folder allowed", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                txtFileFolder.Text = filenames[0];
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

                var selected_props = GetWhichSelected_PropsAtAngle();

                PropsAtAngle props = GetProps(_props_horz, selected_props);

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

                var selected_props = GetWhichSelected_PropsAtAngle();
                PropsAtAngle props = Scrape_PropsAtAngle();

                _props_horz = SetProps(_props_horz, selected_props, props);

                RefreshStrengthPlots();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveFinal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (txtFileFolder.Text == "")
                {
                    MessageBox.Show("Please select an output folder", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (!Directory.Exists(txtFileFolder.Text))
                {
                    MessageBox.Show("Output folder doesn't exist", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var settings = GetSettings();

                var options = new JsonSerializerOptions()
                {
                    WriteIndented = true,
                };

                string serialized = JsonSerializer.Serialize(settings, options);

                string filename = System.IO.Path.Combine(txtFileFolder.Text, $"{DateTime.Now:yyyyMMdd HHmmss} - walljump.json");
                File.WriteAllText(filename, serialized);

                filename = System.IO.Path.Combine(txtFileFolder.Text, "walljump.json");       // this overwrites the current file that the mod is hardcoded to look for
                File.WriteAllText(filename, serialized);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (txtFileFolder.Text == "")
                {
                    MessageBox.Show("Please select an output folder", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (!Directory.Exists(txtFileFolder.Text))
                {
                    MessageBox.Show("Output folder doesn't exist", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var settings = new AllProps()
                {
                    Horizontal = Scrape_HorizontalProps(),
                    Vertical = Scrape_VerticalProps(),
                };

                var options = new JsonSerializerOptions()
                {
                    WriteIndented = true,
                };

                string serialized = JsonSerializer.Serialize(settings, options);

                string filename = System.IO.Path.Combine(txtFileFolder.Text, $"{DateTime.Now:yyyyMMdd HHmmss} - {txtPresetName.Text}.json");
                File.WriteAllText(filename, serialized);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadPreset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (txtFileFolder.Text == "")
                {
                    MessageBox.Show("Please select a settings file", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (!File.Exists(txtFileFolder.Text))
                {
                    MessageBox.Show("Settings file doesn't exist", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var props = JsonSerializer.Deserialize<AllProps>(File.ReadAllText(txtFileFolder.Text));

                Present_HorizontalProps(props.Horizontal);
                Present_VerticalProps(props.Vertical);
                RefreshImage_Horizontal();
                RefreshImage_Vertical();
                HorizontalRadio_Checked(this, new RoutedEventArgs());
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
                //switch (StaticRandom.Next(7))
                //switch (StaticRandom.Next(3, 6))
                switch (8)
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

                    case 8:
                        anim = TestAnim_HardCoded();
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

                window.AddLines(points, sizes.line * 0.75, Colors.White);

                if (anim.NumPoints > 2)
                    window.AddLines(BezierUtil.GetPoints(144, anim.Bezier), sizes.line * 0.5, Colors.DodgerBlue);


                var key_points = anim.KeyValues.
                    Select(o => new
                    {
                        o.key,
                        value2 = anim.Evaluate(o.key),
                    }).
                    ToArray();

                window.AddDots(key_points.Select(o => new Point3D(o.key, o.value2, 0)), sizes.dot, Colors.Blue);


                window.AddDots(anim.Bezier.SelectMany(o => o.ControlPoints), sizes.dot * 0.33, Colors.IndianRed);


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
        private static AnimationCurve TestAnim_HardCoded()
        {
            var retVal = new AnimationCurve();

            retVal.AddKeyValue(-0.24485917389393, 1.7146737575531);
            retVal.AddKeyValue(3.1759088039398, 15.488067626953);
            retVal.AddKeyValue(-2.7535283565521, 14.716904640198);
            retVal.AddKeyValue(7.8685173988342, -2.3514406681061);
            retVal.AddKeyValue(-0.10773362219334, -1.188885807991);

            return retVal;
        }

        #endregion
        #region Event Listeners - curve heatmap

        private Point3D[] _endpoints = null;
        private BezierSegment3D_wpf[] _beziers = null;

        private void CurveHeatmap_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _endpoints = Enumerable.Range(0, StaticRandom.Next(3, 8)).
                    Select(o => Math3D.GetRandomVector_Spherical(4).ToPoint()).
                    ToArray();

                _beziers = BezierUtil.GetBezierSegments(_endpoints, 0.3, false);

                if (chkExtraControls.IsChecked.Value)
                {
                    for (int i = 0; i < _beziers.Length; i++)
                    {
                        _beziers[i] = AddExtraControls(_beziers[i]);
                    }
                }

                //Heatmap(_endpoints, _beziers);
                Heatmap2(_endpoints, _beziers);
                //Heatmap3(_endpoints, _beziers);
                //Heatmap4(_endpoints, _beziers);
                Heatmap5(_endpoints, _beziers);
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
                //Heatmap(_endpoints, _beziers);
                Heatmap2(_endpoints, _beziers);
                //Heatmap3(_endpoints, _beziers);
                //Heatmap4(_endpoints, _beziers);
                Heatmap5(_endpoints, _beziers);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static void Heatmap2(Point3D[] endpoints, BezierSegment3D_wpf[] beziers)
        {
            Point3D[] uniform_samples = BezierUtil.GetPoints_UniformDistribution(beziers.Length * 12, beziers);

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
        private static void Heatmap3(Point3D[] endpoints, BezierSegment3D_wpf[] beziers)
        {
            Point3D[] uniform_samples = BezierUtil.GetPoints_UniformDistribution(beziers.Length * 12, beziers);

            BezierUtil.CurvatureSample[] heatmap = BezierUtil.GetCurvatureHeatmap(beziers);
            double max_dist_from_negone = heatmap.Max(o => o.Dist_From_NegOne);

            PathSnippet[] map = TempBezierUtil.GetPinchedMapping1(heatmap, endpoints.Length, beziers);

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

            window.AddDots(pinch_improved_samples, small_dot * 0.9, UtilityWPF.ColorFromHex("444"));
            window.AddLines(pinch_improved_samples, sizes.line * 0.5, UtilityWPF.ColorFromHex("EEE"));

            window.Show();
        }
        private static void Heatmap4(Point3D[] endpoints, BezierSegment3D_wpf[] beziers)
        {
            Point3D[] uniform_samples = BezierUtil.GetPoints_UniformDistribution(beziers.Length * 12, beziers);

            BezierUtil.CurvatureSample[] heatmap = BezierUtil.GetCurvatureHeatmap(beziers);
            double max_dist_from_negone = heatmap.Max(o => o.Dist_From_NegOne);

            PathSnippet[] map = TempBezierUtil.GetPinchedMapping2(heatmap, endpoints.Length, beziers);

            Point3D[] pinch_improved_samples = BezierUtil.GetPoints_PinchImproved(beziers.Length * 12, beziers, map);


            var sizes = Debug3DWindow.GetDrawSizes(8);
            double small_dot = sizes.dot * 0.36;

            var window = new Debug3DWindow();

            window.AddDots(pinch_improved_samples, small_dot * 0.9, UtilityWPF.ColorFromHex("444"));
            window.AddLines(pinch_improved_samples, sizes.line * 0.5, UtilityWPF.ColorFromHex("EEE"));

            window.Show();
        }
        private static void Heatmap5(Point3D[] endpoints, BezierSegment3D_wpf[] beziers)
        {
            Point3D[] uniform_samples = BezierUtil.GetPoints_UniformDistribution(beziers.Length * 12, beziers);

            BezierUtil.CurvatureSample[] heatmap = BezierUtil.GetCurvatureHeatmap(beziers);
            double max_dist_from_negone = heatmap.Max(o => o.Dist_From_NegOne);

            TempBezierUtil.GetPinchedMapping3(heatmap, endpoints.Length, beziers);
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

        #region stretch segment

        private static Point3D GetZeroStretchExample()
        {
            // Find a control point's X that returns a uniform distribution

            int bits = GeneticSharpUtil.GetChromosomeBits(1, 4);

            //NOTE: The arrays are length 2 because x and y
            var chromosome = new FloatingPointChromosome(
                new double[] { 0, 0 },
                new double[] { 1, 1 },
                new int[] { bits, bits },       // The total bits used to represent each number
                new int[] { 4, 4 });      // The number of fraction (scale or decimal) part of the number. In our case we will not use any.  TODO: See if this means that only integers are considered

            var population = new Population(72, 144, chromosome);

            var fitness = new FuncFitness(c =>
            {
                var fc = c as FloatingPointChromosome;

                double[] values = fc.ToFloatingPoints();
                double x = values[0];
                double y = values[1];

                double error = EvaluateStretch(x, y);

                return 1 - error;       // needs to be ascending score.  so zero error gives best score
            });

            var selection = new EliteSelection();       // the larger the score, the better

            var crossover = new UniformCrossover(0.5f);     // .5 will pull half from each parent

            var mutation = new FlipBitMutation();       // FloatingPointChromosome inherits from BinaryChromosomeBase, which is a series of bits.  This mutator will flip random bits

            var termination = new FitnessStagnationTermination(144);        // keeps going until it generates the same winner this many generations in a row

            var ga = new GeneticAlgorithm(population, fitness, selection, crossover, mutation)
            {
                Termination = termination,
            };

            StringBuilder log = new StringBuilder();

            double latestFitness = 0;
            var winners = new List<double[]>();

            ga.GenerationRan += (s1, e1) =>
            {
                var bestChromosome = ga.BestChromosome as FloatingPointChromosome;
                double bestFitness = bestChromosome.Fitness.Value;

                if (bestFitness != latestFitness)
                {
                    latestFitness = bestFitness;
                    double[] phenotype = bestChromosome.ToFloatingPoints();

                    log.AppendLine(string.Format(
                        "Generation {0,2}: ({1}, {2}) = {3}",
                        ga.GenerationsNumber,
                        phenotype[0],
                        phenotype[1],
                        Math.Round(bestFitness, 2)));

                    winners.Add(phenotype);
                }
            };

            ga.TerminationReached += (s2, e2) =>
            {
                //DrawStretch(new Point3D(winners[0][0], winners[0][1], 0), new Point3D(1 - winners[0][0], 1 - winners[0][1], 0), log.ToString());
            };

            ga.Start();

            return new Point3D(winners[0][0], winners[0][1], 0);
        }

        private static double EvaluateStretch(double x, double y)
        {
            Point3D[] controls = new[]
            {
                new Point3D(x, y, 0),
                new Point3D(1 - x, 1 - y, 0),
            };

            var bezier = new BezierSegment3D_wpf(new Point3D(0, 0, 0), new Point3D(1, 1, 0), controls);

            double max_dist = 0;

            double count = 12;

            for (int i = 0; i < count; i++)
            {
                double percent = i / (count - 1);
                double percent_stretched = BezierUtil.GetPoint(percent, bezier.Combined).Y;

                max_dist = Math.Max(Math.Abs(percent - percent_stretched), max_dist);
            }

            return max_dist;
        }

        private static void DrawStretch(Point3D control1, Point3D control2, string log = null)
        {
            Point3D[] controls = new[]
            {
                control1,
                control2,
            };

            var bezier = new BezierSegment3D_wpf(new Point3D(0, 0, 0), new Point3D(1, 1, 0), controls);

            // Draw dots at a regular interval from 0 to 1
            // Draw the same dots run through the stretch function

            var window = new Debug3DWindow();
            var sizes = Debug3DWindow.GetDrawSizes(1);

            window.AddText(control1.ToStringSignificantDigits(3));
            window.AddText(control2.ToStringSignificantDigits(3));

            if (!string.IsNullOrWhiteSpace(log))
                window.AddText(log);

            Point3D[] points = BezierUtil.GetPoints(12, bezier);

            window.AddLine(new Point3D(0, 0, 0), new Point3D(1, 0, 0), sizes.line, Colors.Black);
            window.AddLine(new Point3D(0, 1, 0), new Point3D(1, 1, 0), sizes.line, Colors.Black);
            window.AddLine(new Point3D(0, -0.5, 0), new Point3D(0, 1, 0), sizes.line, Colors.Black);
            window.AddLine(new Point3D(1, -0.5, 0), new Point3D(1, 1, 0), sizes.line, Colors.Black);


            Vector3D offset = new Vector3D(1.1, 0, 0);
            window.AddLine(new Point3D(0, 0, 0) + offset, controls[0] + offset, sizes.line, Colors.IndianRed);
            window.AddLine(new Point3D(1, 1, 0) + offset, controls[1] + offset, sizes.line, Colors.IndianRed);
            window.AddDots(points, sizes.dot, Colors.White);

            double count = 12;

            for (int i = 0; i < count; i++)
            {
                double percent = i / (count - 1);

                Point3D point = BezierUtil.GetPoint(percent, bezier.Combined);
                Vector3D vector = point.ToVector();
                double percent_stretched = point.Y;

                Point3D point_orig = new Point3D(percent, -0.35, 0);
                Point3D point_stretched = new Point3D(percent_stretched, -0.25, 0);

                window.AddDot(point_orig, sizes.dot, Colors.Gray);
                window.AddDot(point_stretched, sizes.dot, Colors.White);
                window.AddLine(point_orig, point_stretched, sizes.line, Colors.Gray);
            }

            window.Show();
        }

        #endregion

        #region Private Methods

        private WallJumpSettings GetSettings()
        {
            var angles = Scrape_HorizontalProps();

            //NOTE: all of the horizontal slider's angles have zero as facing the wall, 180 as away.  But the dot product is player
            //facing dot wall normal.  So -1 is facing wall, 1 is looking away

            return new WallJumpSettings()
            {
                straightup_vert_percent = GetSettings_StraightUpPercent(),

                percent_vert_whenup = GetSettings_PercentsWhenUp_Vert(angles),
                percent_horz_whenup = GetSettings_PercentsWhenUp_Horz(angles),

                horz_percent_up = GetSettings_KeyValues(angles, _props_horz, o => o.Percent_Up),
                horz_percent_along = GetSettings_KeyValues(angles, _props_horz, o => o.Percent_Along),
                horz_percent_away = GetSettings_KeyValues(angles, _props_horz, o => o.Percent_Away),
                horz_strength = GetSettings_KeyValues(angles, _props_horz, o => o.Strength),
                horizontal_percent_look = GetSettings_KeyValues(angles, _props_horz, o => o.Percent_Look),

                yaw_turn_percent = GetSettings_KeyValues(angles, _props_horz, o => o.YawTurn_Percent),

                horizontal_percent_at_speed = GetSettings_PercentAtSpeed(trkHorzSpeedFull, trkHorzSpeedZero),

                straightup_strength = trkUpStrength.Value,
                straightup_percent_at_speed = GetSettings_PercentAtSpeed(trkUpSpeedFull, trkUpSpeedZero),
            };
        }
        private WallJumpSettings_KeyValue[] GetSettings_StraightUpPercent()
        {
            // In the game, it uses a dot product (look dot up).  In the config, trkVertical_StraightUp has horizontal as 0 and up as 90

            double vertAngle = 90 - trkVertical_StraightUp.Value;
            double vertAngle_stop = 90 - Math1D.Avg(trkVertical_StraightUp.Value, trkVertical_Standard.Value);
            double vertAngle_stand = 90 - trkVertical_Standard.Value;

            return new (double degree, double value)[]
            {
                (0, 1),     // straight up
                (vertAngle, 1),     // the lowest the angle can be to still be full percent
                (vertAngle_stop, 0),        // where percent is zero
                (vertAngle_stand, 0),       // an extra point to force the curve to be S shaped
            }.
            Select(o => new WallJumpSettings_KeyValue()
            {
                key = Math1D.Degrees_to_Dot(o.degree),
                value = o.value,
            }).
            ToArray();
        }
        private WallJumpSettings_KeyValue[] GetSettings_PercentsWhenUp_Vert(HorizontalPropsAngles angles)
        {
            double half = Math1D.Avg(angles.FaceWall, angles.AlongStart);

            return new (double degree, double percent)[]
            {
                (0, 1),     // looking straight at the wall
                (angles.FaceWall, 1),       // the start of the transition
                (half, 0),      // halfway between, should be zero
                (angles.AlongStart, 0),     // an extra point to force the curve to be S shaped
                (180, 0),       // looking directly away from the wall (shouldn't be needed, just so there's no assumptions)
            }.
            Select(o => new WallJumpSettings_KeyValue()
            {
                key = Math1D.Degrees_to_Dot(180 - o.degree),        // 180 because looking at the wall is 180 degrees (look dot wall normal)
                value = o.percent,
            }).
            ToArray();
        }
        private WallJumpSettings_KeyValue[] GetSettings_PercentsWhenUp_Horz(HorizontalPropsAngles angles)
        {
            double half = Math1D.Avg(angles.FaceWall, angles.AlongStart);

            return new (double degree, double percent)[]
            {
                (0, 0),     // looking straight at the wall (shouldn't be needed, just so there's no assumptions)
                (angles.FaceWall, 0),       // an extra point to force the curve to be S shaped
                (half, 0),      // halfway between, should be zero
                (angles.AlongStart, 1),     // the start of the transition
                (angles.AlongEnd, 1),
                (180, 1),       // looking directly away from the wall 
            }.
            Select(o => new WallJumpSettings_KeyValue()
            {
                key = Math1D.Degrees_to_Dot(180 - o.degree),        // 180 because looking at the wall is 180 degrees (look dot wall normal)
                value = o.percent,
            }).
            ToArray();
        }
        private static WallJumpSettings_KeyValue[] GetSettings_KeyValues(HorizontalPropsAngles angles, PropsAtAllAngles props, Func<PropsAtAngle, double> getValue)
        {
            return new (double degree, PropsAtAngle prop)[]
            {
                (0, props.DirectFaceWall),
                (angles.FaceWall, props.FaceWall),
                (angles.AlongStart, props.AlongStart),
                (angles.AlongEnd, props.AlongEnd),
                (180, props.DirectAway),
            }.
            Select(o => new WallJumpSettings_KeyValue()
            {
                key = Math1D.Degrees_to_Dot(180 - o.degree),        // 180 because looking at the wall is 180 degrees (look dot wall normal)
                value = getValue(o.prop),
            }).
            ToArray();
        }
        private static WallJumpSettings_KeyValue[] GetSettings_PercentAtSpeed(Slider speed_full, Slider speed_zero)
        {
            return new (double speed, double percent)[]
            {
                (0, 1),
                (speed_full.Value, 1),
                (speed_zero.Value, 0),
                (speed_zero.Value * 2, 0),
            }.
            Select(o => new WallJumpSettings_KeyValue()
            {
                key = o.speed,
                value = o.percent,
            }).
            ToArray();
        }

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

            brush = UtilityWPF.BrushFromHex(COLOR_VERT_STANDARD);
            _vert_standard = GetGraphic_RotateableLine(center, brush, VERT_INNER2_RADIUS, VERT_OUTER2_RADIUS, true);
            canvas_vert.Children.Add(_vert_standard.Line);
        }

        private void RefreshImage_Horizontal()
        {
            var horz_props = Scrape_HorizontalProps();

            _horz_directwall_left.Rotate.Angle = horz_props.FaceWall;
            _horz_directwall_right.Rotate.Angle = -horz_props.FaceWall;

            _horz_indirectwall_left.Rotate.Angle = horz_props.AlongStart;
            _horz_indirectwall_right.Rotate.Angle = -horz_props.AlongStart;

            _horz_alongwall_left.Rotate.Angle = horz_props.AlongEnd;
            _horz_alongwall_right.Rotate.Angle = -horz_props.AlongEnd;
        }
        private void RefreshImage_Vertical()
        {
            var vert_props = Scrape_VerticalProps();

            _vert_straightup.Rotate.Angle = 90 - vert_props.StraightUp;
            _vert_standard.Rotate.Angle = 90 - vert_props.Standard;
        }

        private void RefreshStrengthPlots()
        {
            var angles = Scrape_HorizontalProps();

            panel_horz_plots.Children.Clear();

            panel_horz_plots.Children.Add(BuildPlot(angles, "Percent Up", trkPropsAtAngle_PercentUp, o => o.Percent_Up));
            panel_horz_plots.Children.Add(BuildPlot(angles, "Percent Along", trkPropsAtAngle_PercentAlong, o => o.Percent_Along));
            panel_horz_plots.Children.Add(BuildPlot(angles, "Percent Away", trkPropsAtAngle_PercentAway, o => o.Percent_Away));
            panel_horz_plots.Children.Add(BuildPlot(angles, "Strength", trkPropsAtAngle_Strength, o => o.Strength));
            panel_horz_plots.Children.Add(BuildPlot(angles, "Yaw Turn Percent", trkPropsAtAngle_YawTurnPercent, o => o.YawTurn_Percent));
            panel_horz_plots.Children.Add(BuildPlot(angles, "Percent Look", trkPropsAtAngle_PercentLook, o => o.Percent_Look));
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

        private HorizontalPropsAngles Scrape_HorizontalProps()
        {
            double direct = trkHorizontal_DirectWall.Value;
            double indirect = Math.Max(direct, trkHorizontal_InDirectWall.Value);
            double along = Math.Max(indirect, trkHorizontal_AlongWall.Value);

            return new HorizontalPropsAngles()
            {
                FaceWall = direct,
                AlongStart = indirect,
                AlongEnd = along,

                PropsAtAllAngles = _props_horz,

                Speed_FullStrength = trkHorzSpeedFull.Value,
                Speed_ZeroStrength = trkHorzSpeedZero.Value,
            };
        }
        private void Present_HorizontalProps(HorizontalPropsAngles angles)
        {
            trkHorizontal_DirectWall.Value = angles.FaceWall;
            trkHorizontal_InDirectWall.Value = angles.AlongStart;
            trkHorizontal_AlongWall.Value = angles.AlongEnd;

            var selected_props = GetWhichSelected_PropsAtAngle();

            _props_horz = angles.PropsAtAllAngles;
            PropsAtAngle props = GetProps(_props_horz, selected_props);

            _swappingprops_horz = true;
            Present_PropsAtAngle(props);
            _swappingprops_horz = false;

            trkHorzSpeedFull.Value = angles.Speed_FullStrength;
            trkHorzSpeedZero.Value = angles.Speed_ZeroStrength;
        }

        private VerticalPropsAngles Scrape_VerticalProps()
        {
            double straight_up = trkVertical_StraightUp.Value;
            double standard = Math.Min(straight_up, trkVertical_Standard.Value);

            double speed_full = trkUpSpeedFull.Value;
            double speed_zero = Math.Max(speed_full, trkUpSpeedZero.Value);

            return new VerticalPropsAngles()
            {
                StraightUp = straight_up,
                Standard = standard,

                Strength = trkUpStrength.Value,

                Speed_FullStrength = speed_full,
                Speed_ZeroStrength = speed_zero,
            };
        }
        private void Present_VerticalProps(VerticalPropsAngles props)
        {
            trkVertical_StraightUp.Value = props.StraightUp;
            trkVertical_Standard.Value = props.Standard;

            trkUpSpeedFull.Value = props.Strength;

            trkUpSpeedFull.Value = props.Speed_FullStrength;
            trkUpSpeedZero.Value = props.Speed_ZeroStrength;
        }

        private PropsAtAngle Scrape_PropsAtAngle()
        {
            return new PropsAtAngle()
            {
                Percent_Up = trkPropsAtAngle_PercentUp.Value,
                Percent_Along = trkPropsAtAngle_PercentAlong.Value,
                Percent_Away = trkPropsAtAngle_PercentAway.Value,
                Strength = trkPropsAtAngle_Strength.Value,
                YawTurn_Percent = trkPropsAtAngle_YawTurnPercent.Value,
                Percent_Look = trkPropsAtAngle_PercentLook.Value,
            };
        }
        private void Present_PropsAtAngle(PropsAtAngle props)
        {
            trkPropsAtAngle_PercentUp.Value = props.Percent_Up;
            trkPropsAtAngle_PercentAlong.Value = props.Percent_Along;
            trkPropsAtAngle_PercentAway.Value = props.Percent_Away;
            trkPropsAtAngle_Strength.Value = props.Strength;
            trkPropsAtAngle_YawTurnPercent.Value = props.YawTurn_Percent;
            trkPropsAtAngle_PercentLook.Value = props.Percent_Look;
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

        private static FrameworkElement BuildPlot(HorizontalPropsAngles angles, string title, Slider slider, Func<PropsAtAngle, double> getValue)
        {
            var retVal = new StackPanel()
            {
                Margin = new Thickness(0, 4, 0, 4),
            };

            retVal.Children.Add(DrawPlot(30, angles, slider.Minimum, slider.Maximum, getValue));

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

        private static FrameworkElement DrawPlot(double radius, HorizontalPropsAngles angles, double minimum, double maximum, Func<PropsAtAngle, double> getValue)
        {
            var values = new List<(double angle, double value, double x, double y)>();

            AnimationCurve curve = BuildAnimationCurve(angles, getValue);

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

        private static AnimationCurve BuildAnimationCurve(HorizontalPropsAngles angles, Func<PropsAtAngle, double> getValue)
        {
            var retVal = new AnimationCurve();

            retVal.AddKeyValue(0, getValue(angles.PropsAtAllAngles.DirectFaceWall));
            retVal.AddKeyValue(angles.FaceWall, getValue(angles.PropsAtAllAngles.FaceWall));
            retVal.AddKeyValue(angles.AlongStart, getValue(angles.PropsAtAllAngles.AlongStart));
            retVal.AddKeyValue(angles.AlongEnd, getValue(angles.PropsAtAllAngles.AlongEnd));
            retVal.AddKeyValue(180, getValue(angles.PropsAtAllAngles.DirectAway));

            return retVal;
        }

        private static PropsAtAllAngles GetPreset_Attempt1()
        {
            return new PropsAtAllAngles()
            {
                DirectFaceWall = new PropsAtAngle()
                {
                    Percent_Up = 0.66,
                    Percent_Along = 0.2,
                    Percent_Away = 0,
                    Strength = 11,
                    YawTurn_Percent = 0,
                    Percent_Look = 0.5,
                },

                FaceWall = new PropsAtAngle()
                {
                    Percent_Up = 0.6,
                    Percent_Along = 0.5,
                    Percent_Away = 0.2,
                    Strength = 11,
                    YawTurn_Percent = 0,
                    Percent_Look = 0.5,
                },

                AlongStart = new PropsAtAngle()
                {
                    Percent_Up = 0.4,
                    Percent_Along = 0.8,
                    Percent_Away = 0.2,
                    Strength = 11,
                    YawTurn_Percent = 0,
                    Percent_Look = 0.5,
                },

                AlongEnd = new PropsAtAngle()
                {
                    Percent_Up = 0.2,
                    Percent_Along = 0.8,
                    Percent_Away = 0.33,
                    Strength = 11,
                    YawTurn_Percent = 0,
                    Percent_Look = 0.5,
                },

                DirectAway = new PropsAtAngle()
                {
                    Percent_Up = 0,
                    Percent_Along = 1,
                    Percent_Away = 1,
                    Strength = 11,
                    YawTurn_Percent = 0,
                    Percent_Look = 0.5,
                },
            };
        }
        private static AllProps GetPreset_Attempt2()
        {
            return new AllProps()
            {
                Horizontal = new HorizontalPropsAngles()
                {
                    FaceWall = 20,
                    AlongStart = 60,
                    AlongEnd = 120,

                    PropsAtAllAngles = new PropsAtAllAngles()
                    {
                        DirectFaceWall = new PropsAtAngle()
                        {
                            Percent_Up = 0.7651404036440723,
                            Percent_Along = 0.04847767977036907,
                            Percent_Away = 0.06060892809185233,
                            Strength = 13.439509355697052,
                            YawTurn_Percent = 0,
                            Percent_Look = 0.5,
                        },

                        FaceWall = new PropsAtAngle()
                        {
                            Percent_Up = 0.7439462042181489,
                            Percent_Along = 0.18559118552351608,
                            Percent_Away = 0.07197310210907464,
                            Strength = 13.333443731536313,
                            YawTurn_Percent = 0,
                            Percent_Look = 0.5,
                        },

                        AlongStart = new PropsAtAngle()
                        {
                            Percent_Up = 0.5704626102583347,
                            Percent_Along = 0.2083431903157424,
                            Percent_Away = 0.06362991179333226,
                            Strength = 11.95459061744667,
                            YawTurn_Percent = 0,
                            Percent_Look = 0.5,
                        },

                        AlongEnd = new PropsAtAngle()
                        {
                            Percent_Up = 0.4272834803444462,
                            Percent_Along = 0.2999763432422181,
                            Percent_Away = 0.33000000000000007,
                            Strength = 11,
                            YawTurn_Percent = 0,
                            Percent_Look = 0.5,
                        },

                        DirectAway = new PropsAtAngle()
                        {
                            Percent_Up = 0.23485959635592776,
                            Percent_Along = 0.4507315891675882,
                            Percent_Away = 0.719683707575183,
                            Strength = 13.969837476500764,
                            YawTurn_Percent = 0,
                            Percent_Look = 0.5,
                        },
                    },

                    Speed_FullStrength = 4,
                    Speed_ZeroStrength = 8,
                },

                Vertical = new VerticalPropsAngles()
                {
                    StraightUp = 60,
                    Standard = 40,
                    Strength = 11,
                    Speed_FullStrength = 3,
                    Speed_ZeroStrength = 7,
                },
            };
        }

        #endregion
    }
}
