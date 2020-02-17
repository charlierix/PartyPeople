using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace Game.Bepu.Testers.ColorTools
{
    public partial class ColorManipulationsWindow : Window
    {
        #region class: TheCache

        private class TheCache
        {
            #region class: RawResult

            public class RawResult
            {
                public RawResult(int hue, Point result)
                {
                    Hue = hue;
                    Result = result;
                }

                public int Hue { get; }
                public Point Result { get; }

                public override string ToString()
                {
                    return $"H={Hue}, S={Result.Y.ToStringSignificantDigits(1)}, V={Result.X.ToStringSignificantDigits(1)}";
                }
            }

            #endregion
            #region class: Interval

            private class Interval
            {
                public Interval(IEnumerable<RawResult> raw)
                {
                    // Make sure they are sorted
                    Raw = raw.
                        OrderBy(o => o.Hue).
                        ToArray();

                    From = Raw[0].Hue;
                    To = Raw[^1].Hue;

                    //TODO: Build a tree for faster lookups (need to reference accord.net)
                    //But only when Raw.Length > threshold
                    //
                    //On second thought, see if the tree can be added to.  When creating a new interval from existing intervals
                    //and raws, pass in the current trees and decide whether to add to one or start over


                }

                public int From { get; }
                public int To { get; }

                public RawResult[] Raw { get; }

                //private VPTree<double, RawResult> _rawTree;

                public bool Contains(double hue)
                {
                    return hue >= From && hue <= To;
                }

                public ColorHSV GetColor(double hue)
                {
                    if (!Contains(hue))
                    {
                        throw new ArgumentOutOfRangeException($"The hue passed in is outside this interval: hue={hue} from={From} to={To}");
                    }

                    //NOTE: This first draft is very unoptimized, it's just written to get all thoughts working
                    //TODO: Get this from a tree

                    RawResult exact = Raw.FirstOrDefault(o => hue.IsNearValue(o.Hue));
                    if (exact != null)
                    {
                        return ToHSV(hue, exact.Result);
                    }

                    var distances = Raw.
                        Select(o => new
                        {
                            o.Hue,
                            o.Result,
                            dist = Math.Abs(o.Hue - hue),
                        }).
                        OrderBy(o => o.dist).
                        ToArray();

                    var left = distances.
                        Where(o => o.Hue < hue).
                        FirstOrDefault();

                    var right = distances.
                        Where(o => o.Hue > hue).
                        FirstOrDefault();

                    if (left == null || right == null)
                    {
                        throw new ApplicationException("The hue is in range, but didn't find left and right");
                    }

                    double percent = (double)(hue - left.Hue) / (double)(right.Hue - left.Hue);

                    Point lerp = Math2D.LERP(left.Result, right.Result, percent);

                    return ToHSV(hue, lerp);
                }

                public override string ToString()
                {
                    return $"{From} - {To} | count={Raw.Length}";
                }
            }

            #endregion

            #region Declaration Section

            private readonly ColorHSV _sourceColor;

            private readonly int _maxDistance;

            private readonly List<RawResult> _rawResults = new List<RawResult>();
            private readonly List<Interval> _intervals = new List<Interval>();

            #endregion

            #region Constructor

            public TheCache(ColorHSV sourceColor, int maxDistance = 5)
            {
                _sourceColor = sourceColor;

                All = Enumerable.Range(0, 361).
                    Select(o =>
                    {
                        Point point = Math3D.GetRandomVector_Circular(5).ToPoint2D();

                        point.X = UtilityMath.Clamp(sourceColor.V + point.X, 0, 100);
                        point.Y = UtilityMath.Clamp(sourceColor.S + point.Y, 0, 100);

                        return new RawResult(o, point);
                    }).
                    ToArray();

                //TODO: Immediately calculate the value for hue=0 and store at 0 and 360.  That way the endpoints will always be there, which
                //simplify logic
                //
                //Make sure that 0 and 360 results are identical

                _rawResults.Add(All[0]);
                _rawResults.Add(All[^1]);

                _maxDistance = maxDistance;
            }

            #endregion

            public RawResult[] All { get; }

            public ColorHSV GetValue(int hue)
            {
                var interval = _intervals.FirstOrDefault(o => o.Contains(hue));
                if (interval != null)
                {
                    return interval.GetColor(hue);
                }

                // Make a new one
                //EquivalentColor.GetEquivalent(_sourceColor, hue);
                RawResult newEntry = All[hue];

                // Find neighbors
                var nearRaw = _rawResults.
                    Select((o, i) => new
                    {
                        index = i,
                        raw = o,
                        dist = Math.Abs(hue - o.Hue),
                    }).
                    Where(o => o.dist <= _maxDistance).
                    OrderByDescending(o => o.index).
                    ToArray();

                var nearInterval = _intervals.
                    Select((o, i) => new
                    {
                        index = i,
                        interval = o,
                        dist = Math.Min(Math.Abs(hue - o.From), Math.Abs(hue - o.To)),
                    }).
                    Where(o => o.dist <= _maxDistance).
                    OrderByDescending(o => o.index).
                    ToArray();

                if (nearRaw.Length == 0 && nearInterval.Length == 0)
                {
                    // There are no others close enough, store this as a loose point
                    _rawResults.Add(newEntry);
                }
                else
                {
                    // There are neighbors.  Remove them from the lists and build an interval out of them
                    var allNearRaw = new List<RawResult>();
                    allNearRaw.Add(newEntry);

                    foreach (var raw in nearRaw)
                    {
                        _rawResults.RemoveAt(raw.index);        // they are sorted descending so removing won't change the index
                        allNearRaw.Add(raw.raw);
                    }

                    foreach (var interval2 in nearInterval)
                    {
                        _intervals.RemoveAt(interval2.index);
                        allNearRaw.AddRange(interval2.interval.Raw);
                    }

                    _intervals.Add(new Interval(allNearRaw));
                }

                return ToHSV(hue, newEntry.Result);
            }

            #region Private Methods

            private static ColorHSV ToHSV(double hue, Point point)
            {
                return new ColorHSV(hue, point.Y, point.X);
            }

            #endregion
        }

        #endregion

        #region Declaration Section

        private const string FILE = "ColorManipulations Options.xml";

        private List<string> _grayFolders = new List<string>();
        private List<Tuple<Guid, BitmapSource>> _grayCache = new List<Tuple<Guid, BitmapSource>>();

        private Color _sourceColor = Colors.LimeGreen;

        private readonly DropShadowEffect _errorEffect;

        private bool _initialized = false;

        #endregion

        #region Constructor

        public ColorManipulationsWindow()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;

            radAverage.Tag = Guid.NewGuid();
            radDesaturate.Tag = Guid.NewGuid();
            rad601.Tag = Guid.NewGuid();
            rad709.Tag = Guid.NewGuid();

            _errorEffect = new DropShadowEffect()
            {
                Color = UtilityWPF.ColorFromHex("C02020"),
                Direction = 0,
                ShadowDepth = 0,
                BlurRadius = 8,
                Opacity = .8,
            };

            _initialized = true;
        }

        #endregion

        #region Event Listeners

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                #region grayscale viewer

                var options = UtilityCore.ReadOptions<ColorManipulationsOptions>(FILE);

                if (options?.ImageFolders_Gray?.Length > 0)
                {
                    foreach (string folder in options.ImageFolders_Gray)
                    {
                        if (AddFolder_Grayscale(folder))
                        {
                            _grayFolders.Add(folder);
                        }
                    }

                    // Select the first image (will fire the changed event)
                    if (lstImages.Items.Count > 0)
                    {
                        lstImages.SelectedIndex = StaticRandom.Next(lstImages.Items.Count);
                        lstImages.Focus();      // need to do this to highlight the selected item
                    }
                }

                #endregion

                RefreshSampleRect();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                try
                {
                    var options = new ColorManipulationsOptions()
                    {
                        ImageFolders_Gray = _grayFolders.ToArray(),
                    };

                    UtilityCore.SaveOptions(options, FILE);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
        #region Event Listeners - grayscale

        private void lstImages_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (lstImages == null)
                {
                    return;
                }

                Image selectedItem = lstImages.SelectedItem as Image;
                if (selectedItem == null)
                {
                    return;
                }

                BitmapImage source = (BitmapImage)selectedItem.Source;

                // Put this into the color image control

                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = source.UriSource;
                bitmap.EndInit();

                imageColor.Source = bitmap;

                _grayCache.Clear();

                // Make sure the gray image is showing
                RadioGray_Checked(this, new RoutedEventArgs());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.Description = "Please select root folder";
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                string selectedPath = dialog.SelectedPath;

                if (AddFolder_Grayscale(selectedPath))
                {
                    _grayFolders.Add(selectedPath);
                }

                // Select the first image (will fire the changed event)
                if (lstImages.Items.Count > 0 && lstImages.SelectedIndex < 0)
                {
                    lstImages.SelectedIndex = 0;
                    lstImages.Focus();      // need to do this to highlight the selected item
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ClearImages_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _grayFolders.Clear();
                _grayCache.Clear();

                imageColor.Source = null;
                imageGray.Source = null;
                lstImages.Items.Clear();

                lblNumImages.Text = lstImages.Items.Count.ToString("N0");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //TODO: Cache these by currently selected source image
        private void RadioGray_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                #region Get color image

                if (lstImages == null)
                {
                    return;
                }

                Image imageCtrl = lstImages.SelectedItem as Image;
                if (imageCtrl == null)
                {
                    return;
                }

                BitmapSource bitmap = imageCtrl.Source as BitmapSource;
                if (bitmap == null)
                {
                    return;
                }

                #endregion

                #region Decide which gray method

                Func<Color, Color> convertGray = null;
                Guid radioGuid = Guid.Empty;

                if (radAverage.IsChecked.Value)
                {
                    convertGray = ConvertToGray_Average;
                    radioGuid = (Guid)radAverage.Tag;
                }
                else if (radDesaturate.IsChecked.Value)
                {
                    convertGray = ConvertToGray_Desaturate;
                    radioGuid = (Guid)radDesaturate.Tag;
                }
                else if (rad601.IsChecked.Value)
                {
                    convertGray = ConvertToGray_BT601;
                    radioGuid = (Guid)rad601.Tag;
                }
                else if (rad709.IsChecked.Value)
                {
                    convertGray = ConvertToGray_BT709;
                    radioGuid = (Guid)rad709.Tag;
                }
                else
                {
                    throw new ApplicationException("Unknown gray selection");
                }

                #endregion

                // Try to pull a cached image
                var cached = _grayCache.FirstOrDefault(o => o.Item1 == radioGuid);
                if (cached != null)
                {
                    imageGray.Source = cached.Item2;
                    return;
                }

                // Turn the bitmap into an array of colors
                var colors = UtilityWPF.ConvertToColorArray(bitmap, true, Colors.Transparent);

                // Convert the colors to grays
                //Color[] grays = colors.GetColors(0, 0, colors.Width, colors.Height).
                //    Select(o => convertGray(o)).
                //    ToArray();

                Color[] grays = colors.GetColors(0, 0, colors.Width, colors.Height).
                    Select((o, i) => new { Color = o, Index = i }).
                    AsParallel().
                    Select(o => new { Color = convertGray(o.Color), Index = o.Index }).
                    OrderBy(o => o.Index).
                    Select(o => o.Color).
                    ToArray();

                // Cache gray
                var cacheImage = Tuple.Create(radioGuid, GetBitmap(grays, colors.Width, colors.Height));
                _grayCache.Add(cacheImage);

                // Show the gray
                imageGray.Source = cacheImage.Item2;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
        #region Event Listeners - matching grays

        private void RandSourceColor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtSourceColor.Text = UtilityWPF.GetRandomColor(0, 255).ToHex(false, false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void txtSourceColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                RefreshSampleRect();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //TODO: When saturation starts out pretty high, simple distance between the hue line and the sheet fails in the reds.  It tends to pick values that are too bright.
        //Play around with choosing other points on that sheet to get a better fit.  Maybe the computer can prompt the user with A/B tests, train up a little NN
        //
        //Try something with dot product?  Or an angle up or down?  It mainly seems to be reds that get too bright, so angle down for red.  But if the source color is
        //red, it may need to angle up the other hues?
        //
        //So the NN would have as inputs SourceH, S, V, RequestH.  The output would be angle.  That angle would then be used to pick a point on the sheet.  Training
        //would be prompting the user with A/B questions
        //
        //Instead of pseudo random A/B prompting, pick a random hue and present the user with a window:  gray background, source color sample, slider bar controlling
        //output color.  Do this lots of times with lots of random source colors and corresponding grays, logging all the results
        private void Attempt1_Click(object sender, RoutedEventArgs e)
        {
            const string MONO_BACK = "F5F5ED";
            const string MONO_FORE = "343635";

            try
            {
                #region calculate

                Color colorFore = UtilityWPF.ColorFromHex(MONO_FORE);

                ColorHSV sourceColor = _sourceColor.ToHSV();

                // Get a spread of other hues
                var aabb = (new VectorND(0d), new VectorND(359.9d));
                var staticHue = new[] { new VectorND(sourceColor.H) };

                var hues = MathND.GetRandomVectors_Cube_EventDist(96, aabb, existingStaticPoints: staticHue, stopIterationCount: 200).
                    Concat(staticHue).
                    Select(o => o[0]).
                    OrderBy(o => o).
                    ToArray();

                Color gray = _sourceColor.ToGray();

                // Get the S and V values that make the same gray
                var results_sheet = hues.
                    AsParallel().
                    Select(o => new
                    {
                        h = o,
                        matches = FindSettingForGray_BruteForce(o, gray.R, 0),
                    }).
                    Where(o => o.matches.Length > 0).
                    ToArray();

                // Get the stripe of S and V from those results that are the closest to the original S and V
                var results_line = results_sheet.
                    Select(o => new
                    {
                        o.h,
                        closest = o.matches.
                            Select(p => new
                            {
                                p,
                                dist = (new Point(sourceColor.S, sourceColor.V) - new Point(p.s, p.v)).Length,
                            }).
                            OrderBy(p => p.dist).
                            ToArray(),
                    }).
                    Select(o => new
                    {
                        o.h,
                        matches = o.closest.
                            Where(p => p.dist.IsNearValue(o.closest[0].dist)).
                            Select(p => p.p).
                            ToArray(),
                    });

                #endregion

                #region create windows

                Rect screen = UtilityWPF.GetCurrentScreen(PointToScreen(new Point()));
                Size windowSize = new Size()
                {
                    Width = screen.Width * .75,
                    Height = screen.Height * .75,
                };


                Debug3DWindow window_line = new Debug3DWindow()
                {
                    Background = new SolidColorBrush(gray),
                    Width = windowSize.Width,
                    Height = windowSize.Height,
                };

                Debug3DWindow window_sheet = new Debug3DWindow()
                {
                    Background = new SolidColorBrush(gray),
                    Width = windowSize.Width,
                    Height = windowSize.Height,
                };

                Debug3DWindow window_mono_line = new Debug3DWindow()
                {
                    Background = UtilityWPF.BrushFromHex(MONO_BACK),
                    Width = windowSize.Width,
                    Height = windowSize.Height,
                };

                Debug3DWindow window_mono_sheet = new Debug3DWindow()
                {
                    Background = UtilityWPF.BrushFromHex(MONO_BACK),
                    Width = windowSize.Width,
                    Height = windowSize.Height,
                };

                var sizes = Debug3DWindow.GetDrawSizes(360);
                sizes.line *= .1;

                #endregion

                #region draw axiis

                Color opposite = UtilityWPF.OppositeColor(gray);

                var axisLines = Polytopes.GetCubeLines(new Point3D(0, 0, 0), new Point3D(360, 100, 100));

                window_line.AddLines(axisLines, sizes.line, opposite);
                window_sheet.AddLines(axisLines, sizes.line, opposite);
                window_mono_line.AddLines(axisLines, sizes.line, colorFore);
                window_mono_sheet.AddLines(axisLines, sizes.line, colorFore);

                double labelOffest = 7;
                double labelSize = 15;

                Point3D pointS = new Point3D(-labelOffest, 50, -labelOffest);
                Point3D pointV = new Point3D(-labelOffest, -labelOffest, 50);
                Vector3D normal = new Vector3D(-1, 0, 0);

                window_line.AddText3D("S", pointS, normal, labelSize, opposite, false);
                window_sheet.AddText3D("S", pointS, normal, labelSize, opposite, false);
                window_mono_line.AddText3D("S", pointS, normal, labelSize, colorFore, false);
                window_mono_sheet.AddText3D("S", pointS, normal, labelSize, colorFore, false);

                window_line.AddText3D("V", pointV, normal, labelSize, opposite, false);
                window_sheet.AddText3D("V", pointV, normal, labelSize, opposite, false);
                window_mono_line.AddText3D("V", pointV, normal, labelSize, colorFore, false);
                window_mono_sheet.AddText3D("V", pointV, normal, labelSize, colorFore, false);

                #endregion

                #region reference line

                if (chkShowReferenceLine.IsChecked.Value)
                {
                    double refLineThickness = sizes.dot * .75;

                    for (int cntr = 0; cntr < hues.Length - 1; cntr++)
                    {
                        Point3D from = new Point3D(hues[cntr], sourceColor.S, sourceColor.V);
                        Point3D to = new Point3D(hues[cntr + 1], sourceColor.S, sourceColor.V);
                        Color fromC = UtilityWPF.HSVtoRGB(hues[cntr], sourceColor.S, sourceColor.V);
                        Color toC = UtilityWPF.HSVtoRGB(hues[cntr + 1], sourceColor.S, sourceColor.V);

                        window_line.AddLine(from, to, refLineThickness, fromC, toC);
                        window_sheet.AddLine(from, to, refLineThickness, fromC, toC);
                    }

                    Point3D fromAll = new Point3D(hues[0], sourceColor.S, sourceColor.V);
                    Point3D toAll = new Point3D(hues[hues.Length - 1], sourceColor.S, sourceColor.V);
                    refLineThickness = sizes.line * 2;

                    window_mono_line.AddLine(fromAll, toAll, refLineThickness, colorFore);
                    window_mono_sheet.AddLine(fromAll, toAll, refLineThickness, colorFore);
                }

                #endregion

                #region draw color dots

                foreach (var r in results_line)
                {
                    foreach (var m in r.matches)
                    {
                        window_line.AddDot(new Point3D(r.h, m.s, m.v), sizes.dot, m.color, false);
                        window_mono_line.AddDot(new Point3D(r.h, m.s, m.v), sizes.dot / 2, colorFore, true);
                    }
                }

                foreach (var r in results_sheet)
                {
                    foreach (var m in r.matches)
                    {
                        window_sheet.AddDot(new Point3D(r.h, m.s, m.v), sizes.dot, m.color, false);
                        window_mono_sheet.AddDot(new Point3D(r.h, m.s, m.v), sizes.dot / 2, colorFore, true);
                    }
                }

                #endregion

                #region show range

                for (int cntr = 0; cntr <= 1; cntr++)
                {
                    var many = results_sheet.SelectMany(o => o.matches);

                    double minS = many.Min(o => o.s);
                    double maxS = many.Max(o => o.s);

                    double minV = many.Min(o => o.v);
                    double maxV = many.Max(o => o.v);

                    string textS = $"S = {minS.ToStringSignificantDigits(1)} : {maxS.ToStringSignificantDigits(1)}";
                    string textV = $"V = {minV.ToStringSignificantDigits(1)} : {maxV.ToStringSignificantDigits(1)}";

                    window_sheet.AddText(textS);
                    window_mono_sheet.AddText(textS);

                    window_sheet.AddText(textV);
                    window_mono_sheet.AddText(textV);
                }

                #endregion

                #region show

                if (chkShowMonoSheet.IsChecked.Value)
                    window_mono_sheet.Show();

                if (chkShowMonoLine.IsChecked.Value)
                    window_mono_line.Show();

                if (chkShowColorSheet.IsChecked.Value)
                    window_sheet.Show();

                window_line.Show();

                #endregion
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AttemptOptimize1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ColorHSV sourceColor = _sourceColor.ToHSV();
                byte gray = _sourceColor.ToGray().R;

                double requestHue = sliderHue.Value;

                #region draw brute graph

                var brute = FindSettingForGray_BruteForce(requestHue, gray, 0);

                var window = new Debug3DWindow();

                var sizes = Debug3DWindow.GetDrawSizes(100);

                window.AddAxisLines(100, sizes.line);

                window.AddDot(new Point3D(sourceColor.V, sourceColor.S, 0), sizes.dot * 4, UtilityWPF.HSVtoRGB(requestHue, sourceColor.S, sourceColor.V), false);

                window.AddDots(brute.Select(o => new Point3D(o.v, o.s, 0)), sizes.dot, Colors.Black);

                window.AddText(string.Format("from: H={0} | S={1} | V={2}", sourceColor.H.ToInt_Round(), sourceColor.S.ToInt_Round(), sourceColor.V.ToInt_Round()));
                window.AddText($"to: H={requestHue.ToInt_Round()}");

                window.AddText("Value", color: Debug3DWindow.AXISCOLOR_X);
                window.AddText("Saturation", color: Debug3DWindow.AXISCOLOR_Y);

                window.Show();

                #endregion

                if (chkShowAllGrays.IsChecked.Value || chkShowOccurrenceGraph.IsChecked.Value)
                {
                    #region calculate all

                    var test = AllGridPoints(0, 100, 0, 100).
                        AsParallel().
                        Select(o =>
                        {
                            Color c = UtilityWPF.HSVtoRGB(requestHue, o.Item1, o.Item2);
                            byte g = c.ToGray().R;

                            return new
                            {
                                s = o.Item1,
                                v = o.Item2,
                                color = c,
                                distance = Math.Abs(gray - g),
                                //colorkey = c.ToHex(false, false),
                                gray = g,
                            };
                        }).
                        ToArray();

                    var grouped = test.
                        //ToLookup(o => o.colorkey).
                        ToLookup(o => o.gray).
                        //OrderByDescending(o => o.Count()).
                        ToArray();

                    #endregion

                    #region show all

                    if (chkShowAllGrays.IsChecked.Value)
                    {
                        Debug3DWindow windowAll = new Debug3DWindow()
                        {
                            Background = new SolidColorBrush(Color.FromRgb(gray, gray, gray)),
                        };

                        sizes = Debug3DWindow.GetDrawSizes(100);

                        windowAll.AddAxisLines(120, sizes.line);

                        foreach (var group in grouped)
                        {
                            windowAll.AddDots(group.Select(o => new Point3D(o.v, o.s, 0)), sizes.dot, Color.FromRgb(group.Key, group.Key, group.Key), false);

                            if (group.Key == gray)
                            {
                                var lowestV = group.OrderBy(o => o.v).First();
                                var highestV = group.OrderByDescending(o => o.v).First();

                                windowAll.AddDot(new Point3D(lowestV.v, lowestV.s, 0), sizes.dot * 1.5, Colors.Red);
                                windowAll.AddDot(new Point3D(highestV.v, highestV.s, 0), sizes.dot * 1.5, Colors.Red);
                            }
                        }

                        windowAll.AddDot(new Point3D(sourceColor.V, sourceColor.S, 0), sizes.dot * 1.5, UtilityWPF.HSVtoRGB(requestHue, sourceColor.S, sourceColor.V), false);

                        windowAll.AddText("Value", color: Debug3DWindow.AXISCOLOR_X);
                        windowAll.AddText("Saturation", color: Debug3DWindow.AXISCOLOR_Y);

                        windowAll.Show();
                    }

                    #endregion

                    #region occurrence graph

                    if (chkShowOccurrenceGraph.IsChecked.Value)
                    {
                        var byGray = grouped.
                            OrderBy(o => o.Key).
                            ToArray();

                        var grayCounts = Enumerable.Range(0, 256).
                            Select(o =>
                            {
                                byte key = (byte)o;
                                int count = byGray.FirstOrDefault(o => o.Key == key)?.Count() ?? 0;
                                return (double)count;
                            }).
                            ToArray();

                        Debug3DWindow windowCounts = new Debug3DWindow();

                        windowCounts.AddGraph(Debug3DWindow.GetGraph(grayCounts), new Point3D(), 100);

                        windowCounts.Show();
                    }

                    #endregion
                }

                var match = FindSettingForGray_Search_Analyze(requestHue, gray, 0, sourceColor, brute);



            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void AttemptOptimize2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ColorHSV sourceColor = _sourceColor.ToHSV();
                byte gray = _sourceColor.ToGray().R;

                double requestHue = sliderHue.Value;


                //TODO: also return the values along the axiis, so they don't need to be recalculated - but just the matching gray
                //values - or at least just the positions of potential matches
                //
                //also reduce the returned rectangle by those edges so there's no recalculating


                // Get the rectangle to search in
                var rect = FindSettingForGray_Search_Rect(requestHue, gray, sourceColor);



                // Get the closest match from within that rectangle
                var best = FindSettingForGray_Rectangle(requestHue, gray, sourceColor, rect);




                #region draw

                var brute = FindSettingForGray_BruteForce(requestHue, gray, 0);

                var window = new Debug3DWindow()
                {
                    Background = new SolidColorBrush(Color.FromRgb(gray, gray, gray)),
                };

                var sizes = Debug3DWindow.GetDrawSizes(100);

                window.AddAxisLines(100, sizes.line);

                window.AddDot(new Point3D(sourceColor.V, sourceColor.S, 0), sizes.dot * 2, UtilityWPF.HSVtoRGB(requestHue, sourceColor.S, sourceColor.V), false);

                if (best != null)
                {
                    window.AddDot(new Point3D(best.Value.v, best.Value.s, 0), sizes.dot * 2, best.Value.color, false);
                }

                window.AddDots(brute.Select(o => new Point3D(o.v, o.s, 0)), sizes.dot, Colors.Black);

                window.AddDot(new Point3D(rect.Left, rect.Top, 0), sizes.dot * 1.5, Colors.White);
                window.AddDot(new Point3D(rect.Right, rect.Bottom, 0), sizes.dot * 1.5, Colors.White);

                window.AddLine(new Point3D(rect.Left, rect.Top, 0), new Point3D(rect.Right, rect.Top, 0), sizes.line * .5, Colors.White);
                window.AddLine(new Point3D(rect.Right, rect.Top, 0), new Point3D(rect.Right, rect.Bottom, 0), sizes.line * .5, Colors.White);
                window.AddLine(new Point3D(rect.Right, rect.Bottom, 0), new Point3D(rect.Left, rect.Bottom, 0), sizes.line * .5, Colors.White);
                window.AddLine(new Point3D(rect.Left, rect.Bottom, 0), new Point3D(rect.Left, rect.Top, 0), sizes.line * .5, Colors.White);

                window.AddText(string.Format("from: H={0} | S={1} | V={2}", sourceColor.H.ToInt_Round(), sourceColor.S.ToInt_Round(), sourceColor.V.ToInt_Round()));
                window.AddText($"to: H={requestHue.ToInt_Round()}");

                window.AddText("Value", color: Debug3DWindow.AXISCOLOR_X);
                window.AddText("Saturation", color: Debug3DWindow.AXISCOLOR_Y);

                window.Show();

                #endregion
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MatchingGrayFinal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                EquivalentColor test = new EquivalentColor(_sourceColor.ToHSV());

                Debug3DWindow window = new Debug3DWindow()
                {
                    Background = Brushes.Black,
                };

                var sizes = Debug3DWindow.GetDrawSizes(360);
                sizes.line *= .33;

                for (int hue = 0; hue <= 360; hue++)
                {
                    ColorHSV approx = test.GetEquivalent(hue);
                    ColorHSV actual = EquivalentColor.GetEquivalent(_sourceColor.ToHSV(), hue);

                    window.AddLine(new Point3D(approx.V, approx.S, hue), new Point3D(actual.V, actual.S, hue), sizes.line, approx.ToRGB(), actual.ToRGB());
                }

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CacheByRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                #region excessive use

                int[] requests = Enumerable.Range(0, 400).
                    Select(o => StaticRandom.Next(361)).
                    ToArray();

                TheCache cache = new TheCache(_sourceColor.ToHSV());

                foreach (int req in requests)
                {
                    ColorHSV result = cache.GetValue(req);
                }

                #endregion

                cache = new TheCache(_sourceColor.ToHSV());

                Debug3DWindow window = new Debug3DWindow();

                var sizes = Debug3DWindow.GetDrawSizes(100);
                sizes.line *= .2;

                window.AddLines(cache.All.Select(o => new Point3D(o.Result.X, o.Result.Y, o.Hue)), sizes.line, Colors.Black);

                for (int cntr = 0; cntr < 100; cntr++)
                {
                    ColorHSV result = cache.GetValue(StaticRandom.Next(361));

                    window.AddDot(new Point3D(result.V, result.S, result.H), sizes.dot, Colors.DodgerBlue);
                }

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void CacheByRequest2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ColorHSV sourceColor = _sourceColor.ToHSV();

                var test0 = EquivalentColor.GetEquivalent(sourceColor, 0);
                var test360 = EquivalentColor.GetEquivalent(sourceColor, 360);

                #region excessive use

                int[] requests = Enumerable.Range(0, 400).
                    Select(o => StaticRandom.Next(361)).
                    ToArray();

                var equivalent = new EquivalentColor(sourceColor);

                foreach (int req in requests)
                {
                    ColorHSV result = equivalent.GetEquivalent(req);
                }

                #endregion

                equivalent = new EquivalentColor(_sourceColor.ToHSV());

                Debug3DWindow window = new Debug3DWindow();

                var sizes = Debug3DWindow.GetDrawSizes(360);
                sizes.dot *= .25;
                sizes.line *= .2;


                var staticPoints = Enumerable.Range(0, 360).
                    Select(o => EquivalentColor.GetEquivalent(sourceColor, o)).
                    Select(o => new Point3D(o.V, o.S, o.H)).
                    ToArray();

                window.AddLines(staticPoints, sizes.line, Colors.Black);


                for (int cntr = 0; cntr < 100; cntr++)
                {
                    ColorHSV result = equivalent.GetEquivalent(StaticRandom.Next(361));

                    window.AddDot(new Point3D(result.V, result.S, result.H), sizes.dot, Colors.DodgerBlue);
                }

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
        #region Event Listeners - misc

        private void alphablend_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_initialized)
                    RefreshOpacitySample();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void opacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (_initialized)
                    RefreshOpacitySample();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods - grayscale

        private bool AddFolder_Grayscale(string folder)
        {
            int prevCount = lstImages.Items.Count;

            string[] childFolders = Directory.GetDirectories(folder);
            if (childFolders.Length == 0)
            {
                // Single folder
                foreach (string filename in Directory.GetFiles(folder))
                {
                    try
                    {
                        AddImage_Grayscale(filename);
                    }
                    catch (Exception)
                    {
                        continue;       // probably not an image
                    }
                }
            }
            else
            {
                // Child folders
                foreach (string childFolder in childFolders)
                {
                    AddFolder_Grayscale(childFolder);
                }
            }

            lblNumImages.Text = lstImages.Items.Count.ToString("N0");

            return prevCount != lstImages.Items.Count;      // if the count is unchanged, then no images were added
        }
        private void AddImage_Grayscale(string filename)
        {
            string filenameFixed = System.IO.Path.GetFullPath(filename);

            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(filenameFixed, UriKind.Absolute);
            bitmap.EndInit();

            Image image = new Image()
            {
                Source = bitmap,
                Stretch = System.Windows.Media.Stretch.Fill,
                Width = 66,
                Height = 66,
                Margin = new Thickness(6),
                Opacity = .66,
            };

            lstImages.Items.Add(image);
        }

        private static Color ConvertToGray_Average(Color color)
        {
            return GetGray(color.A, ConvertToGray_Average_Double(color));
        }
        private static double ConvertToGray_Average_Double(Color color)
        {
            return (color.R + color.G + color.B) / 3d;
        }

        private static Color ConvertToGray_Desaturate(Color color)
        {
            return GetGray(color.A, ConvertToGray_Desaturate_Double(color));
        }
        private static double ConvertToGray_Desaturate_Double(Color color)
        {
            return (Math1D.Max(color.R, color.G, color.B) + Math1D.Min(color.R, color.G, color.B)) / 2d;
        }

        private static Color ConvertToGray_BT601(Color color)
        {
            return GetGray(color.A, ConvertToGray_BT601_Double(color));
        }
        private static double ConvertToGray_BT601_Double(Color color)
        {
            return color.R * 0.299 + color.G * 0.587 + color.B * 0.114;
        }

        private static Color ConvertToGray_BT709(Color color)
        {
            return GetGray(color.A, ConvertToGray_BT709_Double(color));
        }
        private static double ConvertToGray_BT709_Double(Color color)
        {
            return color.R * 0.2126 + color.G * 0.7152 + color.B * 0.0722;
        }

        private static Color GetGray(byte alpha, double rgb)
        {
            byte cast = Convert.ToByte(rgb);
            return Color.FromArgb(alpha, cast, cast, cast);
        }

        /// <summary>
        /// Converts the color array into a bitmap that can be set as an Image.Source
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://www.i-programmer.info/programming/wpf-workings/527-writeablebitmap.html
        /// </remarks>
        public static BitmapSource GetBitmap(Color[] colors, int width, int height)
        {
            if (colors.Length != width * height)
            {
                throw new ArgumentException(string.Format("The array isn't the same as width*height.  ArrayLength={0}, Width={1}, Height={2}", colors.Length, width, height));
            }

            WriteableBitmap retVal = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);      // may want Bgra32 if performance is an issue

            int pixelWidth = retVal.Format.BitsPerPixel / 8;
            int stride = retVal.PixelWidth * pixelWidth;      // this is the length of one row of pixels

            byte[] pixels = new byte[retVal.PixelHeight * stride];

            for (int rowCntr = 0; rowCntr < height; rowCntr++)
            {
                int rowOffset = rowCntr * stride;
                int yOffset = rowCntr * width;

                for (int columnCntr = 0; columnCntr < width; columnCntr++)
                {
                    int offset = rowOffset + (columnCntr * pixelWidth);

                    //_colors[columnCntr + yOffset] = Color.FromArgb(pixels[offset + 3], pixels[offset + 2], pixels[offset + 1], pixels[offset + 0]);

                    Color color = colors[columnCntr + yOffset];

                    pixels[offset + 3] = color.A;
                    pixels[offset + 2] = color.R;
                    pixels[offset + 1] = color.G;
                    pixels[offset + 0] = color.B;
                }
            }

            retVal.WritePixels(new Int32Rect(0, 0, retVal.PixelWidth, retVal.PixelHeight), pixels, stride, 0);

            return retVal;
        }

        //http://www.tannerhelland.com/3643/grayscale-image-algorithm-vb6/
        //private static double GetGray_double(Color color)
        //{
        //    //BT.709
        //    //Gray = (Red * 0.2126 + Green * 0.7152 + Blue * 0.0722)
        //    //BT.601
        //    //Gray = (Red * 0.299 + Green * 0.587 + Blue * 0.114)

        //    return color.R * 0.299 + color.G * 0.587 + color.B * 0.114;
        //}
        //private static byte GetGray_byte(Color color)
        //{
        //    return Convert.ToByte(GetGray_double(color));
        //}
        //private static Color GetGray_color(Color color)
        //{
        //    byte gray = GetGray_byte(color);

        //    return Color.FromArgb(color.A, gray, gray, gray);
        //}

        ///// <summary>
        ///// This takes the average of two decent methods (I figure, why not?)
        ///// </summary>
        //private static byte GetGray(Color color)
        //{
        //    double desaturated = (Math3D.Max(color.R, color.G, color.B) + Math3D.Min(color.R, color.G, color.B)) / 2d;
        //    double humanEye = GetGray_double(color);

        //    return Convert.ToByte((desaturated + humanEye) / 2d);
        //}

        #endregion
        #region Private Methods - matching grays

        private void RefreshSampleRect()
        {
            try
            {
                _sourceColor = UtilityWPF.ColorFromHex(txtSourceColor.Text);
            }
            catch (Exception)
            {
                txtSourceColor.Effect = _errorEffect;
                return;
            }

            txtSourceColor.Effect = null;

            sourceColorSample.Fill = new SolidColorBrush(_sourceColor);

            ColorHSV hsv = _sourceColor.ToHSV();

            lblSourceColorHSV.Text = string.Format("H={0} | S={1} | V={2}", hsv.H.ToInt_Round(), hsv.S.ToInt_Round(), hsv.V.ToInt_Round());
        }

        private static (double s, double v, Color color)[] FindSettingForGray_BruteForce(double hue, byte gray, double epsilon = 1)
        {
            return AllGridPoints(0, 100, 0, 100).
                AsParallel().
                Select(o =>
                {
                    Color c = UtilityWPF.HSVtoRGB(hue, o.s, o.v);
                    byte g = c.ToGray().R;

                    return new
                    {
                        o.s,
                        o.v,
                        color = c,
                        distance = Math.Abs(gray - g),
                    };
                }).
                Where(o => o.distance <= epsilon).
                OrderBy(o => o.distance).
                Select(o => ((double)o.s, (double)o.v, o.color)).
                ToArray();
        }

        //TODO: Break this into two functions internally, get rect, solve rect (solve rect is the same as brute force, just with a rectangle passed in)
        private static (double s, double v, Color color)? FindSettingForGray_Search_Analyze(double hue, byte gray, double epsilon, ColorHSV sourceColor, (double s, double v, Color color)[] matches)
        {
            byte sourceGray = UtilityWPF.HSVtoRGB(hue, sourceColor.S, sourceColor.V).ToGray().R;

            if (sourceGray == gray)
            {
                throw new ApplicationException("it was passed in");
            }

            Point3D sourcePos = new Point3D(sourceColor.V, sourceColor.S, 0);

            #region init window

            Debug3DWindow window = new Debug3DWindow()
            {
                Background = new SolidColorBrush(Color.FromRgb(gray, gray, gray)),
            };

            var sizes = Debug3DWindow.GetDrawSizes(100);

            window.AddAxisLines(100, sizes.line);

            window.AddDots(matches.Select(o => (new Point3D(o.v, o.s, 0), sizes.dot, o.color, false, false)));

            window.AddDot(sourcePos, sizes.dot * 2, UtilityWPF.HSVtoRGB(hue, sourceColor.S, sourceColor.V), false);

            window.AddText("Value", color: Debug3DWindow.AXISCOLOR_X);
            window.AddText("Saturation", color: Debug3DWindow.AXISCOLOR_Y);

            #endregion

            // Send out feelers
            // start with 4, all the way up down left right

            byte up = UtilityWPF.HSVtoRGB(hue, 100, sourceColor.V).ToGray().R;
            byte down = UtilityWPF.HSVtoRGB(hue, 0, sourceColor.V).ToGray().R;
            byte left = UtilityWPF.HSVtoRGB(hue, sourceColor.S, 0).ToGray().R;
            byte right = UtilityWPF.HSVtoRGB(hue, sourceColor.S, 100).ToGray().R;

            bool isUp = !IsSameSide(gray, sourceGray, up);
            bool isDown = !IsSameSide(gray, sourceGray, down);
            bool isLeft = !IsSameSide(gray, sourceGray, left);
            bool isRight = !IsSameSide(gray, sourceGray, right);

            Color isTrue = Colors.DarkSeaGreen;
            Color isFalse = Colors.Tomato;

            window.AddLine(new Point3D(sourceColor.V, 100, 0), sourcePos, sizes.line, isUp ? isTrue : isFalse);
            window.AddLine(new Point3D(sourceColor.V, 0, 0), sourcePos, sizes.line, isDown ? isTrue : isFalse);
            window.AddLine(new Point3D(0, sourceColor.S, 0), sourcePos, sizes.line, isLeft ? isTrue : isFalse);
            window.AddLine(new Point3D(100, sourceColor.S, 0), sourcePos, sizes.line, isRight ? isTrue : isFalse);

            if ((isUp && isDown) || (isLeft && isRight) || (!isUp && !isDown && !isLeft && !isRight))
            {
                // Should never happen, just brute force the whole square
                throw new ApplicationException("brute force the whole square");
            }


            // Walk along the yes lines until there is a match, or it's crossed over, then stop at that line
            // Those represent the corners of the box to scan.  If there are two lines, the request point is the other corner
            // If there is only one line, then scan everything inside the match distance

            // If it's cheap enough, use a bressenham arc.  Otherwise maybe just staight lines.  If the cost of testing those
            // at each loop point is too much, just do a box

            // After all points inside that region are found, return the closest one


            if ((isUp || isDown) && (isLeft || isRight))
            {
                // Two edges to walk
                AxisFor horz = isLeft ?
                    new AxisFor(Axis.X, sourceColor.V.ToInt_Round(), 0) :
                    new AxisFor(Axis.X, sourceColor.V.ToInt_Round(), 100);

                AxisFor vert = isDown ?
                    new AxisFor(Axis.Y, sourceColor.S.ToInt_Round(), 0) :
                    new AxisFor(Axis.Y, sourceColor.S.ToInt_Round(), 100);

                // Alternate back and forth until one axis finds a match or changes threshold
                //horz.

                var corner = Find_TwoAxiis(horz, vert, hue, sourceGray, gray);

                window.AddDot(new Point3D(corner.X, corner.Y, 0), sizes.dot * 1.5, Colors.White);


                //RectInt retVal2 = new RectInt();



            }
            else
            {
                // Only one edge to walk

                // use 3 axisfors.  The main one and two that start at the center line and go out

                // walk the main one util there's a match or threshold change.  Then set up the two sides with that distance

                AxisFor main =
                    isLeft ? new AxisFor(Axis.X, sourceColor.V.ToInt_Round(), 0) :
                    isRight ? new AxisFor(Axis.X, sourceColor.V.ToInt_Round(), 100) :
                    isDown ? new AxisFor(Axis.Y, sourceColor.S.ToInt_Round(), 0) :
                    isUp ? new AxisFor(Axis.Y, sourceColor.S.ToInt_Round(), 100) :
                    throw new ApplicationException("Should have exactly one direction to go");

                int end = Find_OneAxis(main, hue, sourceColor.V, sourceColor.S, sourceGray, gray);

                int dotX = sourceColor.V.ToInt_Round();
                int dotY = sourceColor.S.ToInt_Round();
                main.Set2DIndex(ref dotX, ref dotY, end);
                window.AddDot(new Point3D(dotX, dotY, 0), sizes.dot * 1.5, Colors.White);

                // Now create an axis perpendicular to this


                RectInt retVal1 = GetBox_OneAxis(main, end, sourceColor.V, sourceColor.S);


                window.AddDot(new Point3D(retVal1.Left, retVal1.Top, 0), sizes.dot * 1.5, Colors.White);
                window.AddDot(new Point3D(retVal1.Right, retVal1.Bottom, 0), sizes.dot * 1.5, Colors.White);

            }



            //TODO: The found rectangle should be increased in size slightly to help with slight drift (do that when getting the axiis)



            //ALMOST
            // keep track of which feelers straddle the curve.  Chop their length in half and see if they still straddle the curve, 1/4 else 3/4
            // this should be good enough, solve all points in that box and return the best match


            window.Show();


            return null;
        }

        private static VectorInt Find_TwoAxiis(AxisFor horz, AxisFor vert, double hue, byte sourceGray, byte gray)
        {
            VectorInt retVal = new VectorInt(horz.Start, vert.Start);

            var enumHz = horz.Iterate().GetEnumerator();
            var enumVt = vert.Iterate().GetEnumerator();

            enumHz.MoveNext();      // the first time through is Start, so prime them to do the item after start
            enumVt.MoveNext();

            bool stoppedH = false;
            bool stoppedV = false;

            // Walk one step at a time along the sides of the square until one of the edges finds the curve
            while (true)
            {
                bool foundH = false;
                if (!stoppedH && enumHz.MoveNext())
                {
                    retVal.X = enumHz.Current;
                    foundH = !IsSameSide(gray, sourceGray, hue, vert.Start, retVal.X);
                }
                else
                {
                    stoppedH = true;
                }

                bool foundV = false;
                if (!stoppedV && enumVt.MoveNext())
                {
                    retVal.Y = enumVt.Current;
                    foundV = !IsSameSide(gray, sourceGray, hue, retVal.Y, horz.Start);
                }
                else
                {
                    stoppedV = true;
                }

                if (foundH || foundV)
                {
                    break;
                }

                if (stoppedH && stoppedV)       // this should never happen
                {
                    break;
                }
            }

            return new VectorInt(UtilityMath.Clamp(retVal.X, 0, 100), UtilityMath.Clamp(retVal.Y, 0, 100));
        }
        private static int Find_OneAxis(AxisFor axis, double hue, double val, double sat, byte sourceGray, byte gray)
        {
            int x = val.ToInt_Round();
            int y = sat.ToInt_Round();

            foreach (int item in axis.Iterate())
            {
                axis.Set2DIndex(ref x, ref y, item);

                if (!IsSameSide(gray, sourceGray, hue, y, x))
                {
                    return item;
                }
            }

            // The curve wasn't found.  This should never happen
            //return axis.Stop;
            throw new ApplicationException("Didn't find curve");
        }

        //TODO: Since the curve only ever goes one way, only half of the box would be needed.  Do a similar dual feeler approach to see what direction to go (but pull back toward the start point a couple ticks to make sure math drift doesn't skew the result)
        private static RectInt GetBox_OneAxis(AxisFor axis, int axisStop, double val, double sat)
        {
            int fromX = val.ToInt_Round();
            int fromY = sat.ToInt_Round();

            VectorInt[] corners = null;

            switch (axis.Axis)
            {
                case Axis.X:
                    int distX = Math.Abs(fromX - axisStop);

                    corners = new[]
                    {
                        new VectorInt(fromX, fromY - distX),
                        new VectorInt(fromX, fromY + distX),
                        new VectorInt(axisStop, fromY - distX),
                        new VectorInt(axisStop, fromY + distX),
                    };
                    break;

                case Axis.Y:
                    int distY = Math.Abs(fromY - axisStop);

                    corners = new[]
                    {
                        new VectorInt(fromX - distY, fromY),
                        new VectorInt(fromX + distY, fromY),
                        new VectorInt(fromX - distY, axisStop),
                        new VectorInt(fromX + distY, axisStop),
                    };
                    break;

                case Axis.Z:
                    throw new ApplicationException("Didn't expect Z axis");

                default:
                    throw new ApplicationException($"Unknown Axis: {axis.Axis}");
            }

            var aabb = Math2D.GetAABB(corners);

            aabb =
            (
                new VectorInt(UtilityMath.Clamp(aabb.min.X, 0, 100), UtilityMath.Clamp(aabb.min.Y, 0, 100)),
                new VectorInt(UtilityMath.Clamp(aabb.max.X, 0, 100), UtilityMath.Clamp(aabb.max.Y, 0, 100))
            );

            return new RectInt(aabb.min.X, aabb.min.Y, aabb.max.X - aabb.min.X, aabb.max.Y - aabb.min.Y);
        }

        private static bool IsSameSide(byte target, byte current, double h, double s, double v)
        {
            byte test = UtilityWPF.HSVtoRGB(h, s, v).ToGray().R;

            return IsSameSide(target, current, test);
        }
        private static bool IsSameSide(byte target, byte current, byte test)
        {
            return (current >= target && test >= target) ||
                (current <= target && test <= target);
        }

        private static IEnumerable<(int s, int v)> AllGridPoints(int fromS, int toS, int fromV, int toV)
        {
            for (int s = fromS; s <= toS; s++)
            {
                for (int v = fromV; v <= toV; v++)
                {
                    yield return (s, v);
                }
            }
        }

        #endregion
        #region Private Methods - matching grays 2

        private static RectInt FindSettingForGray_Search_Rect(double hue, byte gray, ColorHSV sourceColor)
        {
            byte sourceGray = UtilityWPF.HSVtoRGB(hue, sourceColor.S, sourceColor.V).ToGray().R;

            int sourceV = sourceColor.V.ToInt_Round();
            int sourceS = sourceColor.S.ToInt_Round();

            if (sourceGray == gray)
            {
                return new RectInt(sourceV, sourceS, 1, 1);
            }

            // Send out feelers.  All the way up down left right and see which directions crossed over the boundry
            bool isUp = !IsSameSide(gray, sourceGray, hue, 100, sourceColor.V);
            bool isDown = !IsSameSide(gray, sourceGray, hue, 0, sourceColor.V);
            bool isLeft = !IsSameSide(gray, sourceGray, hue, sourceColor.S, 0);
            bool isRight = !IsSameSide(gray, sourceGray, hue, sourceColor.S, 100);

            if ((isUp && isDown) || (isLeft && isRight) || (!isUp && !isDown && !isLeft && !isRight))
            {
                // Should never happen, just brute force the whole square
                return new RectInt(0, 0, 100, 100);
            }

            // Walk along the yes lines until there is a match, or it's crossed over, then stop at that line

            if ((isUp || isDown) && (isLeft || isRight))
            {
                // Two edges to walk
                AxisFor horz = isLeft ?
                    new AxisFor(Axis.X, sourceColor.V.ToInt_Round(), 0) :
                    new AxisFor(Axis.X, sourceColor.V.ToInt_Round(), 100);

                AxisFor vert = isDown ?
                    new AxisFor(Axis.Y, sourceColor.S.ToInt_Round(), 0) :
                    new AxisFor(Axis.Y, sourceColor.S.ToInt_Round(), 100);

                VectorInt corner = Find_TwoAxiis_2(horz, vert, hue, sourceGray, gray);

                var aabb = Math2D.GetAABB(new[] { corner, new VectorInt(sourceV, sourceS) });

                return new RectInt(aabb.min.X, aabb.min.Y, aabb.max.X - aabb.min.X, aabb.max.Y - aabb.min.Y);
            }
            else
            {
                // Only one edge to walk.  Walk the main one until there's a match or threshold change
                AxisFor main =
                    isLeft ? new AxisFor(Axis.X, sourceColor.V.ToInt_Round(), 0) :
                    isRight ? new AxisFor(Axis.X, sourceColor.V.ToInt_Round(), 100) :
                    isDown ? new AxisFor(Axis.Y, sourceColor.S.ToInt_Round(), 0) :
                    isUp ? new AxisFor(Axis.Y, sourceColor.S.ToInt_Round(), 100) :
                    throw new ApplicationException("Should have exactly one direction to go");

                int end = Find_OneAxis_2(main, hue, sourceColor.V, sourceColor.S, sourceGray, gray);

                // Now create an axis perpendicular to this
                return GetBox_OneAxis_2(main, end, hue, sourceColor.V, sourceColor.S, sourceGray, gray);
            }
        }

        private static VectorInt Find_TwoAxiis_2(AxisFor horz, AxisFor vert, double hue, byte sourceGray, byte gray, int extra = 2)
        {
            VectorInt retVal = new VectorInt(horz.Start, vert.Start);

            var enumHz = horz.Iterate().GetEnumerator();
            var enumVt = vert.Iterate().GetEnumerator();

            enumHz.MoveNext();      // the first time through is Start, so prime them to do the item after start
            enumVt.MoveNext();

            bool stoppedH = false;
            bool stoppedV = false;

            // Walk one step at a time along the sides of the square until one of the edges finds the curve
            while (true)
            {
                bool foundH = false;
                if (!stoppedH && enumHz.MoveNext())
                {
                    retVal.X = enumHz.Current;
                    foundH = !IsSameSide(gray, sourceGray, hue, vert.Start, retVal.X);
                }
                else
                {
                    stoppedH = true;
                }

                bool foundV = false;
                if (!stoppedV && enumVt.MoveNext())
                {
                    retVal.Y = enumVt.Current;
                    foundV = !IsSameSide(gray, sourceGray, hue, retVal.Y, horz.Start);
                }
                else
                {
                    stoppedV = true;
                }

                if (foundH || foundV)
                {
                    break;
                }

                if (stoppedH && stoppedV)       // this should never happen
                {
                    break;
                }
            }

            return new VectorInt(UtilityMath.Clamp(retVal.X + (horz.Increment * extra), 0, 100), UtilityMath.Clamp(retVal.Y + (vert.Increment * extra), 0, 100));
        }
        private static int Find_OneAxis_2(AxisFor axis, double hue, double val, double sat, byte sourceGray, byte gray, int extra = 2)
        {
            int x = val.ToInt_Round();
            int y = sat.ToInt_Round();

            foreach (int item in axis.Iterate())
            {
                axis.Set2DIndex(ref x, ref y, item);

                if (!IsSameSide(gray, sourceGray, hue, y, x))
                {
                    return item + (axis.Increment * extra);
                }
            }

            // The curve wasn't found.  This should never happen
            //return axis.Stop;
            throw new ApplicationException("Didn't find curve");
        }

        private static RectInt GetBox_OneAxis_2(AxisFor axis, int axisStop, double hue, double val, double sat, byte sourceGray, byte gray)
        {
            int fromX = val.ToInt_Round();
            int fromY = sat.ToInt_Round();

            int direction = GetBox_OneAxis_Direction(axis, axisStop, hue, fromX, fromY, sourceGray, gray);

            var corners = new List<VectorInt>();

            corners.Add(new VectorInt(fromX, fromY));

            switch (axis.Axis)
            {
                case Axis.X:
                    int distX = Math.Abs(fromX - axisStop);

                    if (direction <= 0)
                    {
                        corners.Add(new VectorInt(fromX, fromY - distX));
                        corners.Add(new VectorInt(axisStop, fromY - distX));
                    }

                    if (direction >= 0)
                    {
                        corners.Add(new VectorInt(fromX, fromY + distX));
                        corners.Add(new VectorInt(axisStop, fromY + distX));
                    }
                    break;

                case Axis.Y:
                    int distY = Math.Abs(fromY - axisStop);

                    if (direction <= 0)
                    {
                        corners.Add(new VectorInt(fromX - distY, fromY));
                        corners.Add(new VectorInt(fromX - distY, axisStop));
                    }

                    if (direction >= 0)
                    {
                        corners.Add(new VectorInt(fromX + distY, fromY));
                        corners.Add(new VectorInt(fromX + distY, axisStop));
                    }

                    break;

                case Axis.Z:
                    throw new ApplicationException("Didn't expect Z axis");

                default:
                    throw new ApplicationException($"Unknown Axis: {axis.Axis}");
            }

            var aabb = Math2D.GetAABB(corners);

            aabb =
            (
                new VectorInt(UtilityMath.Clamp(aabb.min.X, 0, 100), UtilityMath.Clamp(aabb.min.Y, 0, 100)),
                new VectorInt(UtilityMath.Clamp(aabb.max.X, 0, 100), UtilityMath.Clamp(aabb.max.Y, 0, 100))
            );

            return new RectInt(aabb.min.X, aabb.min.Y, aabb.max.X - aabb.min.X, aabb.max.Y - aabb.min.Y);
        }
        private static int GetBox_OneAxis_Direction(AxisFor axis, int axisStop, double hue, int fromX, int fromY, byte sourceGray, byte gray)
        {
            // After this, fromX and fromY will be toX and toY.  It's difficult to name these meaningfully
            axis.Set2DIndex(ref fromX, ref fromY, axisStop);

            // Get posistions of feelers
            AxisFor perpAxis = new AxisFor(axis.Axis == Axis.X ? Axis.Y : Axis.X, 66, 88);      // the ints don't matter, just using this for Set2DIndex

            int negX = fromX;
            int negY = fromY;
            perpAxis.Set2DIndex(ref negX, ref negY, 0);

            int posX = fromX;
            int posY = fromY;
            perpAxis.Set2DIndex(ref posX, ref posY, 100);

            // Handle cases where fromX,fromY is sitting on the edge of the square
            if (negX == fromX && negY == fromY)
            {
                return 1;
            }
            else if (posX == fromX && posY == fromY)
            {
                return -1;
            }

            // See which crossed over
            bool isNeg = !IsSameSide(gray, sourceGray, hue, negY, negX);
            bool isPos = !IsSameSide(gray, sourceGray, hue, posY, posX);

            if (isNeg && !isPos)
            {
                return -1;      // The curve is in box within the negative side
            }
            else if (isPos && !isNeg)
            {
                return 1;
            }
            else
            {
                return 0;       // undetermined, search both sides
            }
        }

        private static (double s, double v, Color color)? FindSettingForGray_Rectangle(double hue, byte gray, ColorHSV sourceColor, RectInt rectangle)
        {
            return AllGridPoints(rectangle.Top, rectangle.Bottom, rectangle.Left, rectangle.Right).     // x is value, y is saturation
                AsParallel().
                Select(o =>
                {
                    Color c = UtilityWPF.HSVtoRGB(hue, o.s, o.v);
                    byte g = c.ToGray().R;

                    return new
                    {
                        o.s,
                        o.v,
                        color = c,
                        grayDistance = Math.Abs(gray - g),      // wrong distance.  Go by distance from request point
                        pointDistance = Math2D.LengthSquared(o.v, o.s, sourceColor.V, sourceColor.S),
                    };
                }).
                OrderBy(o => o.grayDistance).
                ThenBy(o => o.pointDistance).
                Select(o => ((double)o.s, (double)o.v, o.color)).
                First();
        }


        #endregion
        #region Private Methods - misc

        private void RefreshOpacitySample()
        {
            opacityValue.Content = opacitySlider.Value.ToStringSignificantDigits(2);

            bool hadError = false;

            Color from;
            try
            {
                from = UtilityWPF.ColorFromHex(alphablendFrom.Text);
                alphablendFromSample.Fill = new SolidColorBrush(from);
                alphablendFrom.Effect = null;
            }
            catch (Exception)
            {
                alphablendFrom.Effect = _errorEffect;
                alphablendFromSample.Fill = Brushes.Red;
                hadError = true;
            }

            Color to;
            try
            {
                to = UtilityWPF.ColorFromHex(alphablendTo.Text);
                alphablendToSample.Fill = new SolidColorBrush(to);
                alphablendTo.Effect = null;
            }
            catch (Exception)
            {
                alphablendTo.Effect = _errorEffect;
                alphablendToSample.Fill = Brushes.Red;
                hadError = true;
            }

            if (hadError)
            {
                outputSample.Fill = Brushes.Red;
                outputSampleHex.Text = "";
            }
            else
            {
                Color blend = UtilityWPF.AlphaBlend(to, from, opacitySlider.Value);
                outputSample.Fill = new SolidColorBrush(blend);
                outputSampleHex.Text = blend.ToHex(from.A < 255 || to.A < 255, false);
            }
        }

        #endregion
    }

    #region class: ColorManipulationsOptions

    /// <summary>
    /// This gets serialized to file
    /// </summary>
    public class ColorManipulationsOptions
    {
        public string[] ImageFolders_Gray { get; set; }
    }

    #endregion
}
