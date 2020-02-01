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
        #region Declaration Section

        private const string FILE = "ColorManipulations Options.xml";

        private List<string> _grayFolders = new List<string>();
        private List<Tuple<Guid, BitmapSource>> _grayCache = new List<Tuple<Guid, BitmapSource>>();

        private Color _sourceColor = Colors.LimeGreen;

        private readonly DropShadowEffect _errorEffect;

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
                var aabb = Tuple.Create(new VectorND(0d), new VectorND(359.9d));
                var staticHue = new[] { new VectorND(sourceColor.H) };

                var hues = MathND.GetRandomVectors_Cube_EventDist(96, aabb, existingStaticPoints: staticHue, stopIterationCount: 100).
                    Concat(staticHue).
                    Select(o => o[0]).
                    OrderBy(o => o).
                    ToArray();

                Color gray = sourceColor.ToRGB().ToGray();

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
                        Color fromC = new ColorHSV(hues[cntr], sourceColor.S, sourceColor.V).ToRGB();
                        Color toC = new ColorHSV(hues[cntr + 1], sourceColor.S, sourceColor.V).ToRGB();

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

        private static (double s, double v, Color color)[] FindSettingForGray_BruteForce(double hue, byte gray, double epsilon = 2)
        {
            return AllGridPoints(0, 100, 0, 100).
                AsParallel().
                Select(o =>
                {
                    Color c = new ColorHSV(hue, o.Item1, o.Item2).ToRGB();
                    byte g = c.ToGray().R;

                    return new
                    {
                        s = o.Item1,
                        v = o.Item2,
                        color = c,
                        distance = Math.Abs(gray - g),
                    };
                }).
                Where(o => o.distance <= epsilon).
                OrderBy(o => o.distance).
                Select(o => ((double)o.s, (double)o.v, o.color)).
                ToArray();
        }

        private static IEnumerable<(int, int)> AllGridPoints(int from1, int to1, int from2, int to2)
        {
            for (int one = from1; one <= to1; one++)
            {
                for (int two = from2; two <= to2; two++)
                {
                    yield return (one, two);
                }
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
