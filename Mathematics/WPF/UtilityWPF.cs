using Game.Core;
using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.WPF
{
    public static class UtilityWPF
    {
        #region Declaration Section

        private const double INV256 = 1d / 256d;

        [StructLayout(LayoutKind.Sequential)]
        public class POINT
        {
            public int x = 0; public int y = 0;
        }

        [DllImport("User32", EntryPoint = "ClientToScreen", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern int ClientToScreen(IntPtr hWnd, [In, Out] POINT pt);

        #endregion

        #region color

        /// <summary>
        /// This returns a color that is the result of the two colors blended
        /// </summary>
        /// <remarks>
        /// NOTE: This is sort of a mashup (AlphaBlend was written years before OverlayColors and AverageColors)
        /// </remarks>
        /// <param name="alpha">0 is all back color, 1 is all fore color, .5 is half way between</param>
        public static Color AlphaBlend(Color foreColor, Color backColor, double alpha)
        {
            // Figure out the new color
            double a, r, g, b;
            if (foreColor.A == 0)
            {
                // Fore is completely transparent, so only worry about blending the alpha
                a = backColor.A + (((foreColor.A - backColor.A) * INV256) * alpha * 255d);
                r = backColor.R;
                g = backColor.G;
                b = backColor.B;
            }
            else if (backColor.A == 0)
            {
                // Back is completely transparent, so only worry about blending the alpha
                a = backColor.A + (((foreColor.A - backColor.A) * INV256) * alpha * 255d);
                r = foreColor.R;
                g = foreColor.G;
                b = foreColor.B;
            }
            else
            {
                a = backColor.A + (((foreColor.A - backColor.A) * INV256) * alpha * 255d);
                r = backColor.R + (((foreColor.R - backColor.R) * INV256) * alpha * 255d);
                g = backColor.G + (((foreColor.G - backColor.G) * INV256) * alpha * 255d);
                b = backColor.B + (((foreColor.B - backColor.B) * INV256) * alpha * 255d);
            }

            return GetColorCapped(a, r, g, b);
        }
        /// <summary>
        /// Profiling shows that creating a color is 6.5 times slower than a byte array
        /// </summary>
        /// <remarks>
        /// Code is copied for speed reasons
        /// </remarks>
        public static byte[] AlphaBlend(byte[] foreColor, byte[] backColor, double alpha)
        {
            if (backColor.Length == 4)
            {
                #region ARGB

                // Figure out the new color
                if (foreColor[0] == 0)
                {
                    // Fore is completely transparent, so only worry about blending the alpha
                    return new byte[]
                        {
                            GetByteCapped(backColor[0] + (((foreColor[0] - backColor[0]) * INV256) * alpha * 255d)),
                            backColor[1],
                            backColor[2],
                            backColor[3]
                        };
                }
                else if (backColor[0] == 0)
                {
                    // Back is completely transparent, so only worry about blending the alpha
                    return new byte[]
                        {
                            GetByteCapped(backColor[0] + (((foreColor[0] - backColor[0]) * INV256) * alpha * 255d)),
                            foreColor[1],
                            foreColor[2],
                            foreColor[3]
                        };
                }
                else
                {
                    return new byte[]
                        {
                            GetByteCapped(backColor[0] + (((foreColor[0] - backColor[0]) * INV256) * alpha * 255d)),
                            GetByteCapped(backColor[1] + (((foreColor[1] - backColor[1]) * INV256) * alpha * 255d)),
                            GetByteCapped(backColor[2] + (((foreColor[2] - backColor[2]) * INV256) * alpha * 255d)),
                            GetByteCapped(backColor[3] + (((foreColor[3] - backColor[3]) * INV256) * alpha * 255d))
                        };
                }

                #endregion
            }
            else
            {
                #region RGB

                return new byte[]
                    {
                        GetByteCapped(backColor[0] + (((foreColor[0] - backColor[0]) * INV256) * alpha * 255d)),
                        GetByteCapped(backColor[1] + (((foreColor[1] - backColor[1]) * INV256) * alpha * 255d)),
                        GetByteCapped(backColor[2] + (((foreColor[2] - backColor[2]) * INV256) * alpha * 255d))
                    };

                #endregion
            }
        }

        /// <summary>
        /// This lays the colors on top of each other, and returns the result
        /// </summary>
        /// <remarks>
        /// This treats each color like a plate of glass.  So setting a fully opaque plate halfway up the stack will completely block everything
        /// under it.  I tested this in wpf by placing rectangles on top of each other, and got the same color that wpf got.
        /// 
        /// This is a simplified copy of the other overload.  The code was copied for speed reasons
        /// </remarks>
        public static Color OverlayColors(IEnumerable<Color> colors)
        {
            bool isFirst = true;

            //  This represents the running return color (values from 0 to 1)
            double a = 0, r = 0, g = 0, b = 0;

            //  Shoot through the colors, and lay them on top of the running return color
            foreach (var color in colors)
            {
                if (color.A == 0)
                {
                    //  Ignore transparent colors
                    continue;
                }

                if (isFirst)
                {
                    //  Store the first color
                    a = color.A * INV256;
                    r = color.R * INV256;
                    g = color.G * INV256;
                    b = color.B * INV256;

                    isFirst = false;
                    continue;
                }

                double a2 = color.A * INV256;
                double r2 = color.R * INV256;
                double g2 = color.G * INV256;
                double b2 = color.B * INV256;

                //  Alpha is a bit funny, it's a control more than a color
                a = Math.Max(a, a2);

                //  Add the weighted difference between this color and the running color
                r += (r2 - r) * a2;
                g += (g2 - g) * a2;
                b += (b2 - b) * a2;
            }

            if (isFirst)
            {
                //  The list was empty, or all the colors were transparent
                return Colors.Transparent;
            }

            return GetColorCapped(a * 256d, r * 256d, g * 256d, b * 256d);
        }
        /// <summary>
        /// This overload adds an extra dial for transparency (the same result could be achieved by pre multiplying each color by its
        /// corresponding percent)
        /// </summary>
        /// <param name="colors">
        /// Item1=The color
        /// Item2=Percent (0 to 1)
        /// </param>
        public static Color OverlayColors(IEnumerable<Tuple<Color, double>> colors)
        {
            const double INV255 = 1d / 255d;

            bool isFirst = true;

            //  This represents the running return color (values from 0 to 1)
            double a = 0, r = 0, g = 0, b = 0;

            //  Shoot through the colors, and lay them on top of the running return color
            foreach (var color in colors)
            {
                if (color.Item1.A == 0 || color.Item2 == 0d)
                {
                    //  Ignore transparent colors
                    continue;
                }

                if (isFirst)
                {
                    //  Store the first color
                    a = (color.Item1.A * INV255) * color.Item2;
                    r = color.Item1.R * INV255;
                    g = color.Item1.G * INV255;
                    b = color.Item1.B * INV255;

                    isFirst = false;
                    continue;
                }

                double a2 = (color.Item1.A * INV255) * color.Item2;
                double r2 = color.Item1.R * INV255;
                double g2 = color.Item1.G * INV255;
                double b2 = color.Item1.B * INV255;

                //  Alpha is a bit funny, it's a control more than a color
                a = Math.Max(a, a2);

                //  Add the weighted difference between this color and the running color
                r += (r2 - r) * a2;
                g += (g2 - g) * a2;
                b += (b2 - b) * a2;
            }

            if (isFirst)
            {
                //  The list was empty, or all the colors were transparent
                return Colors.Transparent;
            }

            return GetColorCapped(a * 255d, r * 255d, g * 255d, b * 255d);
        }
        /// <summary>
        /// Made an overload for byte array, because byte arrays are faster than the color struct (6.5 times faster)
        /// </summary>
        public static byte[] OverlayColors(IEnumerable<byte[]> colors)
        {
            bool isFirst = true;

            //  This represents the running return color (values from 0 to 1)
            double a = 0, r = 0, g = 0, b = 0;

            //  Shoot through the colors, and lay them on top of the running return color
            foreach (var color in colors)
            {
                if (color[0] == 0)
                {
                    //  Ignore transparent colors
                    continue;
                }

                if (isFirst)
                {
                    //  Store the first color
                    a = color[0] * INV256;
                    r = color[1] * INV256;
                    g = color[2] * INV256;
                    b = color[3] * INV256;

                    isFirst = false;
                    continue;
                }

                double a2 = color[0] * INV256;
                double r2 = color[1] * INV256;
                double g2 = color[2] * INV256;
                double b2 = color[3] * INV256;

                //  Alpha is a bit funny, it's a control more than a color
                a = Math.Max(a, a2);

                //  Add the weighted difference between this color and the running color
                r += (r2 - r) * a2;
                g += (g2 - g) * a2;
                b += (b2 - b) * a2;
            }

            if (isFirst)
            {
                //  The list was empty, or all the colors were transparent
                return new byte[] { 0, 0, 0, 0 };
            }

            return new byte[]
            {
                GetByteCapped(a * 256d),
                GetByteCapped(r * 256d),
                GetByteCapped(g * 256d),
                GetByteCapped(b * 256d)
            };
        }

        /// <summary>
        /// This takes the weighted average of all the colors (using alpha as the weight multiplier)
        /// </summary>
        /// <remarks>
        /// This is a simplified copy of the other overload.  The code was copied for speed reasons
        /// </remarks>
        public static Color AverageColors(IEnumerable<Color> colors)
        {
            byte[] retVal = AverageColors(colors.Select(o => new[] { o.A, o.R, o.G, o.B }));
            return Color.FromArgb(retVal[0], retVal[1], retVal[2], retVal[3]);
        }
        public static byte[] AverageColors(IEnumerable<byte[]> colors)
        {
            const double INV255 = 1d / 255d;
            const double NEARZERO = .001d;

            #region convert to doubles

            List<Tuple<double, double, double, double>> doubles = new List<Tuple<double, double, double, double>>();

            double minAlpha = double.MaxValue;
            bool isAllTransparent = true;

            //  Convert to doubles from 0 to 1 (throw out fully transparent colors)
            foreach (var color in colors)
            {
                double a = color[0] * INV255;

                doubles.Add(Tuple.Create(a, color[1] * INV255, color[2] * INV255, color[3] * INV255));

                if (a > NEARZERO && a < minAlpha)
                {
                    isAllTransparent = false;
                    minAlpha = a;
                }
            }

            #endregion

            if (isAllTransparent)
            {
                return new byte[] { 0, 0, 0, 0 };       // Colors.Transparent;
            }

            #region weighted sum

            double sumA = 0, sumR = 0, sumG = 0, sumB = 0;
            double sumWeight = 0;

            foreach (var dbl in doubles)
            {
                if (dbl.Item1 <= NEARZERO)
                {
                    //  This is fully transparent.  It doesn't affect sumWeight, but the final alpha is divided by doubles.Count, so it does affect the final alpha (sumWeight
                    //  affects the color, not alpha)
                    continue;
                }

                double multiplier = dbl.Item1 / minAlpha;       //  dividing by min so that multiplier is always greater or equal to 1
                sumWeight += multiplier;

                sumA += dbl.Item1;      //  this one isn't weighted, it's a simple average
                sumR += dbl.Item2 * multiplier;
                sumG += dbl.Item3 * multiplier;
                sumB += dbl.Item4 * multiplier;
            }

            double divisor = 1d / sumWeight;

            #endregion

            return GetColorCapped_Bytes((sumA / doubles.Count) * 255d, sumR * divisor * 255d, sumG * divisor * 255d, sumB * divisor * 255d);
        }

        /// <summary>
        /// This takes the weighted average of all the colors (using alpha as the weight multiplier)
        /// </summary>
        /// <param name="colors">
        /// Item1=Color
        /// Item2=% (0 to 1)
        /// </param>
        /// <remarks>
        /// The overlay method is what you would normally think of when alpha blending.  This method doesn't care about
        /// the order of the colors, it just averages them (so if several colors are passed in that are different hues, you'll just get gray)
        /// 
        /// http://www.investopedia.com/terms/w/weightedaverage.asp
        /// </remarks>
        public static Color AverageColors(IEnumerable<Tuple<Color, double>> colors)
        {
            byte[] retVal = AverageColors(colors.Select(o => Tuple.Create(new[] { o.Item1.A, o.Item1.R, o.Item1.G, o.Item1.B }, o.Item2)));
            return Color.FromArgb(retVal[0], retVal[1], retVal[2], retVal[3]);
        }
        public static byte[] AverageColors(IEnumerable<Tuple<byte[], double>> colors)
        {
            const double INV255 = 1d / 255d;
            const double NEARZERO = .001d;

            #region convert to doubles

            //  A, R, G, B, %
            List<Tuple<double, double, double, double, double>> doubles = new List<Tuple<double, double, double, double, double>>();        //  I hate using such an ugly tuple, but the alternative is linq and anonymous types, and this method needs to be as fast as possible

            double minAlpha = double.MaxValue;
            bool isAllTransparent = true;

            //  Convert to doubles from 0 to 1 (throw out fully transparent colors)
            foreach (var color in colors)
            {
                double a = (color.Item1[0] * INV255);
                double a1 = a * color.Item2;

                doubles.Add(Tuple.Create(a, color.Item1[1] * INV255, color.Item1[2] * INV255, color.Item1[3] * INV255, color.Item2));

                if (a1 > NEARZERO && a1 < minAlpha)
                {
                    isAllTransparent = false;
                    minAlpha = a1;
                }
            }

            #endregion

            if (isAllTransparent)
            {
                return new byte[] { 0, 0, 0, 0 };       // Colors.Transparent;
            }

            #region weighted sum

            double sumA = 0, sumR = 0, sumG = 0, sumB = 0;
            double sumAlphaWeight = 0, sumWeight = 0;

            foreach (var dbl in doubles)
            {
                sumAlphaWeight += dbl.Item5;        //  Item5 should already be from 0 to 1

                if ((dbl.Item1 * dbl.Item5) <= NEARZERO)
                {
                    //  This is fully transparent.  It doesn't affect the sum of the color's weight, but does affect the sum of the alpha's weight
                    continue;
                }

                double multiplier = (dbl.Item1 * dbl.Item5) / minAlpha;       //  dividing by min so that multiplier is always greater or equal to 1
                sumWeight += multiplier;

                sumA += dbl.Item1;      //  alphas have their own weighting
                sumR += dbl.Item2 * multiplier;
                sumG += dbl.Item3 * multiplier;
                sumB += dbl.Item4 * multiplier;
            }

            double divisor = 1d / sumWeight;

            #endregion

            return GetColorCapped_Bytes((sumA / sumAlphaWeight) * 255d, sumR * divisor * 255d, sumG * divisor * 255d, sumB * divisor * 255d);
        }

        /// <summary>
        /// This makes a gray version of the color
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://www.tannerhelland.com/3643/grayscale-image-algorithm-vb6/
        /// </remarks>
        public static Color ConvertToGray(Color color)
        {
            byte gray = Convert.ToByte(ConvertToGray(color.R, color.G, color.B));
            return Color.FromArgb(color.A, gray, gray, gray);
        }
        /// <summary>
        /// This converts the color into a value from 0 to 255
        /// </summary>
        public static double ConvertToGray(byte r, byte g, byte b)
        {
            // These are some other approaches that could be used (they don't look as good though)
            //return (r + g + b) / 3d;        // Averge
            //return (Math3D.Max(r, g, b) + Math3D.Min(r, g, b)) / 2d;      // Desaturate
            //return r * 0.2126 + g * 0.7152 + b * 0.0722;        // BT.709

            return r * 0.299 + g * 0.587 + b * 0.114;     // BT.601
        }

        public static Color GetRandomColor(byte min, byte max)
        {
            return GetRandomColor(255, min, max);
        }
        public static Color GetRandomColor(byte alpha, byte min, byte max)
        {
            Random rand = StaticRandom.GetRandomForThread();
            return Color.FromArgb(alpha, Convert.ToByte(rand.Next(min, max + 1)), Convert.ToByte(rand.Next(min, max + 1)), Convert.ToByte(rand.Next(min, max + 1)));
        }
        public static Color GetRandomColor(byte alpha, byte minRed, byte maxRed, byte minGreen, byte maxGreen, byte minBlue, byte maxBlue)
        {
            Random rand = StaticRandom.GetRandomForThread();
            return Color.FromArgb(alpha, Convert.ToByte(rand.Next(minRed, maxRed + 1)), Convert.ToByte(rand.Next(minGreen, maxGreen + 1)), Convert.ToByte(rand.Next(minBlue, maxBlue + 1)));
        }
        public static Color GetRandomColor(byte alpha, byte baseR, byte baseG, byte baseB, int drift)
        {
            Random rand = StaticRandom.GetRandomForThread();

            int newR = rand.Next(baseR - drift, baseR + drift + 1);
            int newG = rand.Next(baseG - drift, baseG + drift + 1);
            int newB = rand.Next(baseB - drift, baseB + drift + 1);

            return Color.FromArgb(alpha, newR.ToByte(), newG.ToByte(), newB.ToByte());
        }

        //TODO: Make a version that chooses random points in an HSV cone
        /// <summary>
        /// This returns random colors that are as far from each other as possible
        /// </summary>
        public static Color[] GetRandomColors(int count, byte min, byte max)
        {
            return GetRandomColors(count, 255, min, max);
        }
        public static Color[] GetRandomColors(int count, byte alpha, byte min, byte max)
        {
            return GetRandomColors(count, alpha, min, max, min, max, min, max);
        }
        public static Color[] GetRandomColors(int count, byte alpha, byte minRed, byte maxRed, byte minGreen, byte maxGreen, byte minBlue, byte maxBlue)
        {
            if (count < 1)
            {
                return new Color[0];
            }

            Color staticColor = GetRandomColor(alpha, minRed, maxRed, minGreen, maxGreen, minBlue, maxBlue);

            if (count == 1)
            {
                return new[] { staticColor };
            }

            Tuple<VectorND, VectorND> aabb = Tuple.Create(new double[] { minRed, minGreen, minBlue }.ToVectorND(), new double[] { maxRed, maxGreen, maxBlue }.ToVectorND());

            VectorND[] existingStatic = new[] { new double[] { staticColor.R, staticColor.G, staticColor.B }.ToVectorND() };

            // Treat each RGB value as a 3D vector.  Now inject the items in a cube defined by aabb.  Each point
            // pushes the others away, so the returned items are as far from each other as possible
            //NOTE: Using a single static color to get unique values across runs (otherwise they get stuck in corners,
            //and could be about the same from run to run)
            VectorND[] colors = MathND.GetRandomVectors_Cube_EventDist(count - 1, aabb, existingStaticPoints: existingStatic);

            return colors.
                Concat(existingStatic).
                Select(o => Color.FromArgb(alpha, o[0].ToByte_Round(), o[1].ToByte_Round(), o[2].ToByte_Round())).
                ToArray();
        }

        public static Color GetColorEGA(int number)
        {
            return GetColorEGA(255, number);
        }
        public static Color GetColorEGA(byte alpha, int number)
        {
            //http://en.wikipedia.org/wiki/Enhanced_Graphics_Adapter

            return number switch
            {
                0 => Color.FromArgb(alpha, 0, 0, 0),// black
                1 => Color.FromArgb(alpha, 0, 0, 170),// blue
                2 => Color.FromArgb(alpha, 0, 170, 0),// green
                3 => Color.FromArgb(alpha, 0, 170, 170),// cyan
                4 => Color.FromArgb(alpha, 170, 0, 0),// red
                5 => Color.FromArgb(alpha, 170, 0, 170),// magenta
                6 => Color.FromArgb(alpha, 170, 85, 0),// brown
                7 => Color.FromArgb(alpha, 170, 170, 170),// light gray
                8 => Color.FromArgb(alpha, 85, 85, 85),// dark gray
                9 => Color.FromArgb(alpha, 85, 85, 255),// bright blue
                10 => Color.FromArgb(alpha, 85, 255, 85),// bright green
                11 => Color.FromArgb(alpha, 85, 255, 255),// bright cyan
                12 => Color.FromArgb(alpha, 255, 85, 85),// bright red
                13 => Color.FromArgb(alpha, 255, 85, 255),// bright magenta
                14 => Color.FromArgb(alpha, 255, 255, 85),// bright yellow
                15 => Color.FromArgb(alpha, 255, 255, 255),// bright white
                _ => throw new ArgumentException("The number must be between 0 and 15: " + number.ToString()),
            };
        }

        /// <summary>
        /// This returns a color that is opposite of what is passed in (yellow becomes purple, white becomes black, etc)
        /// </summary>
        /// <param name="discourageGray">
        /// True: If the source color is near gray, the returned will tend toward white or black instead (useful if you don't want the two colors similar to each other)
        /// False: Simply returns value on the other side of 50
        /// </param>
        public static Color OppositeColor(Color color, bool discourageGray = true)
        {
            ColorHSV hsv = RGBtoHSV(color);

            // Hue (no need to cap between 0:360.  The ToRGB method will do that)
            double hue = hsv.H + 180;

            // Value
            double distanceFrom50 = hsv.V - 50;
            double value = 50 - distanceFrom50;

            if (discourageGray && Math.Abs(distanceFrom50) < 25)
            {
                // Instead of converging on 50, converge on 0 or 100
                if (distanceFrom50 < 0)
                {
                    value = 100 + distanceFrom50;       // dist is negative, so is actually subtraction
                }
                else
                {
                    value = 0 + distanceFrom50;
                }
            }

            // Leave saturation alone
            return HSVtoRGB(hue, hsv.S, value);
        }
        /// <summary>
        /// This returns either black or white
        /// </summary>
        public static Color OppositeColor_BW(Color color)
        {
            ColorHSV oppositeColor = UtilityWPF.OppositeColor(color).ToHSV();

            if (oppositeColor.V > 50)
            {
                return Colors.White;
            }
            else
            {
                return Colors.Black;
            }
        }

        /// <summary>
        /// This is just a wrapper to the color converter (why can't they have a method off the color class with all
        /// the others?)
        /// </summary>
        public static Color ColorFromHex(string hexValue)
        {
            string final = hexValue;

            if (!final.StartsWith("#"))
            {
                final = "#" + final;
            }

            if (final.Length == 4)      // compressed format, no alpha
            {
                // #08F -> #0088FF
                final = new string(new[] { '#', final[1], final[1], final[2], final[2], final[3], final[3] });
            }
            else if (final.Length == 5)     // compressed format, has alpha
            {
                // #8A4F -> #88AA44FF
                final = new string(new[] { '#', final[1], final[1], final[2], final[2], final[3], final[3], final[4], final[4] });
            }

            return (Color)ColorConverter.ConvertFromString(final);
        }
        public static string ColorToHex(Color color, bool includeAlpha = true, bool includePound = true)
        {
            // I think color.ToString does the same thing, but this is explicit
            return string.Format("{0}{1}{2}{3}{4}",
                includePound ? "#" : "",
                includeAlpha ? color.A.ToString("X2") : "",
                color.R.ToString("X2"),
                color.G.ToString("X2"),
                color.B.ToString("X2"));
        }

        public static Brush BrushFromHex(string hexValue)
        {
            return new SolidColorBrush(ColorFromHex(hexValue));
        }

        /// <summary>
        /// Returns the color as hue/saturation/value
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://stackoverflow.com/questions/4123998/algorithm-to-switch-between-rgb-and-hsb-color-values
        /// </remarks>
        /// <param name="h">from 0 to 360</param>
        /// <param name="s">from 0 to 100</param>
        /// <param name="v">from 0 to 100</param>
        public static ColorHSV RGBtoHSV(Color color)
        {
            // Normalize the RGB values by scaling them to be between 0 and 1
            double red = color.R / 255d;
            double green = color.G / 255d;
            double blue = color.B / 255d;

            double minValue = Math1D.Min(red, green, blue);
            double maxValue = Math1D.Max(red, green, blue);
            double delta = maxValue - minValue;

            double h, s, v;

            v = maxValue;       // this should be the average(min,max), not the max?

            #region Get Hue

            // Calculate the hue (in degrees of a circle, between 0 and 360)
            if (red >= green && red >= blue)
            {
                if (green >= blue)
                {
                    if (delta <= 0)
                    {
                        h = 0d;
                    }
                    else
                    {
                        h = 60d * (green - blue) / delta;
                    }
                }
                else
                {
                    h = 60d * (green - blue) / delta + 360d;
                }
            }
            else if (green >= red && green >= blue)
            {
                h = 60d * (blue - red) / delta + 120d;
            }
            else //if (blue >= red && blue >= green)
            {
                h = 60d * (red - green) / delta + 240d;
            }

            #endregion

            // Calculate the saturation (between 0 and 1)
            if (maxValue == 0d)
            {
                s = 0d;
            }
            else
            {
                s = 1d - (minValue / maxValue);
            }

            // Scale the saturation and value to a percentage between 0 and 100
            s *= 100d;
            v *= 100d;

            #region Cap Values

            if (h < 0d)
            {
                h = 0d;
            }
            else if (h > 360d)
            {
                h = 360d;
            }

            if (s < 0d)
            {
                s = 0d;
            }
            else if (s > 100d)
            {
                s = 100d;
            }

            if (v < 0d)
            {
                v = 0d;
            }
            else if (v > 100d)
            {
                v = 100d;
            }

            #endregion

            return new ColorHSV(color.A, h, s, v);
        }

        public static Color HSVtoRGB(double h, double s, double v)
        {
            return HSVtoRGB(255, h, s, v);
        }
        /// <summary>
        /// Converts hue/saturation/value to a color
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://stackoverflow.com/questions/4123998/algorithm-to-switch-between-rgb-and-hsb-color-values
        /// </remarks>
        /// <param name="a">0 to 255</param>
        /// <param name="h">0 to 360</param>
        /// <param name="s">0 to 100</param>
        /// <param name="v">0 to 100</param>
        public static Color HSVtoRGB(byte a, double h, double s, double v)
        {
            // Scale the Saturation and Value components to be between 0 and 1
            double hue = GetHueCapped(h);
            double sat = s / 100d;
            double val = v / 100d;

            double r, g, b;		// these go between 0 and 1

            if (sat == 0d)
            {
                #region gray

                // If the saturation is 0, then all colors are the same.
                // (This is some flavor of gray.)
                r = val;
                g = val;
                b = val;

                #endregion
            }
            else
            {
                #region color

                // Calculate the appropriate sector of a 6-part color wheel
                double sectorPos = hue / 60d;
                int sectorNumber = Convert.ToInt32(Math.Floor(sectorPos));

                // Get the fractional part of the sector (that is, how many degrees into the sector you are)
                double fractionalSector = sectorPos - sectorNumber;

                // Calculate values for the three axes of the color
                double p = val * (1d - sat);
                double q = val * (1d - (sat * fractionalSector));
                double t = val * (1d - (sat * (1d - fractionalSector)));

                // Assign the fractional colors to red, green, and blue
                // components based on the sector the angle is in
                switch (sectorNumber)
                {
                    case 0:
                    case 6:
                        r = val;
                        g = t;
                        b = p;
                        break;

                    case 1:
                        r = q;
                        g = val;
                        b = p;
                        break;

                    case 2:
                        r = p;
                        g = val;
                        b = t;
                        break;

                    case 3:
                        r = p;
                        g = q;
                        b = val;
                        break;

                    case 4:
                        r = t;
                        g = p;
                        b = val;
                        break;

                    case 5:
                        r = val;
                        g = p;
                        b = q;
                        break;

                    default:
                        throw new ArgumentException("Invalid hue: " + h.ToString());
                }

                #endregion
            }

            #region scale/cap 255

            // Scale to 255 (using int to make it easier to handle overflow)
            int rNew = Convert.ToInt32(Math.Round(r * 255d));
            int gNew = Convert.ToInt32(Math.Round(g * 255d));
            int bNew = Convert.ToInt32(Math.Round(b * 255d));

            // Make sure the values are in range
            if (rNew < 0)
            {
                rNew = 0;
            }
            else if (rNew > 255)
            {
                rNew = 255;
            }

            if (gNew < 0)
            {
                gNew = 0;
            }
            else if (gNew > 255)
            {
                gNew = 255;
            }

            if (bNew < 0)
            {
                bNew = 0;
            }
            else if (bNew > 255)
            {
                bNew = 255;
            }

            #endregion

            return Color.FromArgb(a, Convert.ToByte(rNew), Convert.ToByte(gNew), Convert.ToByte(bNew));
        }

        public static bool IsTransparent(Color color)
        {
            return color.A == 0;
        }
        public static bool IsTransparent(Brush brush)
        {
            if (brush is SolidColorBrush)
            {
                return IsTransparent(((SolidColorBrush)brush).Color);
            }
            else if (brush is GradientBrush)
            {
                GradientBrush brushCast = (GradientBrush)brush;
                if (brushCast.Opacity == 0d)
                {
                    return true;
                }

                return !brushCast.GradientStops.Any(o => !IsTransparent(o.Color));		// if any are non-transparent, return false
            }

            // Not sure what it is, probably a bitmap or something, so just assume it's not transparent
            return false;
        }

        public static Color ExtractColor(Brush brush)
        {
            if (brush is SolidColorBrush)
            {
                return ((SolidColorBrush)brush).Color;
            }
            else if (brush is GradientBrush)
            {
                GradientBrush brushCast = (GradientBrush)brush;

                Color average = AverageColors(brushCast.GradientStops.Select(o => o.Color));

                if (brushCast.Opacity.IsNearZero())
                {
                    return Color.FromArgb(0, average.R, average.G, average.B);
                }
                else if (brushCast.Opacity.IsNearValue(1))
                {
                    return average;
                }
                else
                {
                    double opacity = average.A / 255d;
                    opacity *= brushCast.Opacity;

                    return Color.FromArgb((opacity * 255d).ToByte_Round(), average.R, average.G, average.B);
                }
            }
            else
            {
                throw new ArgumentException("Unsupported brush type: " + brush.GetType().ToString());
            }
        }

        /// <summary>
        /// This returns the distance between the two hues
        /// </summary>
        /// <remarks>
        /// It gets a bit complicated, because hue wraps at 360
        /// 
        /// examples:
        ///     80, 90 -> 10
        ///     40, 30 -> 10
        ///     0, 360 -> 0
        ///     350, 10 -> 20
        /// </remarks>
        public static double GetHueDistance(double hue1, double hue2)
        {
            if (hue1 < 0 || hue1 > 360 || hue2 < 0 || hue2 > 360)
            {
                throw new ArgumentException(string.Format("The hues must be between 0 and 360.  hue1={0}, hue2={1}", hue1.ToString(), hue2.ToString()));
            }

            double retVal = Math.Abs(hue1 - hue2);

            if (retVal <= 180)
            {
                return retVal;
            }

            // They straddle the 0 degree line.  Add 360 to the smaller one to bring it closer to the larger one

            double min = Math.Min(hue1, hue2);
            double max = Math.Max(hue1, hue2);

            return Math.Abs(min + 360 - max);
        }

        #endregion

        #region math

        //---------------------------------------------------------------------------
        //
        // (c) Copyright Microsoft Corporation.
        // This source is subject to the Microsoft Limited Permissive License.
        // See http://www.microsoft.com/resources/sharedsource/licensingbasics/limitedpermissivelicense.mspx
        // All other rights reserved.
        //
        // This file is part of the 3D Tools for Windows Presentation Foundation
        // project.  For more information, see:
        // 
        // http://CodePlex.com/Wiki/View.aspx?ProjectName=3DTools
        //
        //---------------------------------------------------------------------------

        private static Matrix3D GetViewMatrix(ProjectionCamera camera)
        {
            if (camera == null) throw new ArgumentNullException("camera");

            // This math is identical to what you find documented for
            // D3DXMatrixLookAtRH with the exception that WPF uses a
            // LookDirection vector rather than a LookAt point.

            Vector3D zAxis = -camera.LookDirection;
            zAxis.Normalize();

            Vector3D xAxis = Vector3D.CrossProduct(camera.UpDirection, zAxis);
            xAxis.Normalize();

            Vector3D yAxis = Vector3D.CrossProduct(zAxis, xAxis);

            Vector3D position = (Vector3D)camera.Position;
            double offsetX = -Vector3D.DotProduct(xAxis, position);
            double offsetY = -Vector3D.DotProduct(yAxis, position);
            double offsetZ = -Vector3D.DotProduct(zAxis, position);

            Matrix3D m = new Matrix3D(
                xAxis.X, yAxis.X, zAxis.X, 0,
                xAxis.Y, yAxis.Y, zAxis.Y, 0,
                xAxis.Z, yAxis.Z, zAxis.Z, 0,
                offsetX, offsetY, offsetZ, 1);

            return m;
        }
        /// <summary>
        ///     Computes the effective view matrix for the given
        ///     camera.
        /// </summary>
        public static Matrix3D GetViewMatrix(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException("camera");

            ProjectionCamera projectionCamera = camera as ProjectionCamera;

            if (projectionCamera != null)
            {
                return GetViewMatrix(projectionCamera);
            }

            MatrixCamera matrixCamera = camera as MatrixCamera;

            if (matrixCamera != null)
            {
                return matrixCamera.ViewMatrix;
            }

            throw new ArgumentException(String.Format("Unsupported camera type '{0}'.", camera.GetType().FullName), "camera");
        }

        /// <summary>
        /// This converts the position into screen coords
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://blogs.msdn.com/llobo/archive/2006/05/02/Code-for-getting-screen-relative-Position-in-WPF.aspx
        /// </remarks>
        public static Point TransformToScreen(Point point, Visual relativeTo)
        {
            HwndSource hwndSource = PresentationSource.FromVisual(relativeTo) as HwndSource;
            Visual root = hwndSource.RootVisual;

            // Translate the point from the visual to the root.
            GeneralTransform transformToRoot = relativeTo.TransformToAncestor(root);
            Point pointRoot = transformToRoot.Transform(point);

            // Transform the point from the root to client coordinates.
            Matrix m = Matrix.Identity;
            Transform transform = VisualTreeHelper.GetTransform(root);

            if (transform != null)
            {
                m = Matrix.Multiply(m, transform.Value);
            }

            Vector offset = VisualTreeHelper.GetOffset(root);
            m.Translate(offset.X, offset.Y);

            Point pointClient = m.Transform(pointRoot);

            // Convert from “device-independent pixels” into pixels.
            pointClient = hwndSource.CompositionTarget.TransformToDevice.Transform(pointClient);

            POINT pointClientPixels = new POINT();
            pointClientPixels.x = (0 < pointClient.X) ? (int)(pointClient.X + 0.5) : (int)(pointClient.X - 0.5);
            pointClientPixels.y = (0 < pointClient.Y) ? (int)(pointClient.Y + 0.5) : (int)(pointClient.Y - 0.5);

            // Transform the point into screen coordinates.
            POINT pointScreenPixels = pointClientPixels;
            ClientToScreen(hwndSource.Handle, pointScreenPixels);
            return new Point(pointScreenPixels.x, pointScreenPixels.y);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// This takes values from 0 to 255
        /// </summary>
        private static Color GetColorCapped(double a, double r, double g, double b)
        {
            if (a < 0)
            {
                a = 0;
            }
            else if (a > 255)
            {
                a = 255;
            }

            if (r < 0)
            {
                r = 0;
            }
            else if (r > 255)
            {
                r = 255;
            }

            if (g < 0)
            {
                g = 0;
            }
            else if (g > 255)
            {
                g = 255;
            }

            if (b < 0)
            {
                b = 0;
            }
            else if (b > 255)
            {
                b = 255;
            }

            return Color.FromArgb(Convert.ToByte(a), Convert.ToByte(r), Convert.ToByte(g), Convert.ToByte(b));
        }
        private static byte[] GetColorCapped_Bytes(double a, double r, double g, double b)
        {
            if (a < 0)
            {
                a = 0;
            }
            else if (a > 255)
            {
                a = 255;
            }

            if (r < 0)
            {
                r = 0;
            }
            else if (r > 255)
            {
                r = 255;
            }

            if (g < 0)
            {
                g = 0;
            }
            else if (g > 255)
            {
                g = 255;
            }

            if (b < 0)
            {
                b = 0;
            }
            else if (b > 255)
            {
                b = 255;
            }

            return new[] { Convert.ToByte(a), Convert.ToByte(r), Convert.ToByte(g), Convert.ToByte(b) };
        }
        private static Color GetColorCapped(int a, int r, int g, int b)
        {
            if (a < 0)
            {
                a = 0;
            }
            else if (a > 255)
            {
                a = 255;
            }

            if (r < 0)
            {
                r = 0;
            }
            else if (r > 255)
            {
                r = 255;
            }

            if (g < 0)
            {
                g = 0;
            }
            else if (g > 255)
            {
                g = 255;
            }

            if (b < 0)
            {
                b = 0;
            }
            else if (b > 255)
            {
                b = 255;
            }

            return Color.FromArgb(Convert.ToByte(a), Convert.ToByte(r), Convert.ToByte(g), Convert.ToByte(b));
        }
        private static byte[] GetColorCapped_Bytes(int a, int r, int g, int b)
        {
            if (a < 0)
            {
                a = 0;
            }
            else if (a > 255)
            {
                a = 255;
            }

            if (r < 0)
            {
                r = 0;
            }
            else if (r > 255)
            {
                r = 255;
            }

            if (g < 0)
            {
                g = 0;
            }
            else if (g > 255)
            {
                g = 255;
            }

            if (b < 0)
            {
                b = 0;
            }
            else if (b > 255)
            {
                b = 255;
            }

            return new[] { Convert.ToByte(a), Convert.ToByte(r), Convert.ToByte(g), Convert.ToByte(b) };
        }

        private static double GetHueCapped(double hue)
        {
            double retVal = hue;

            while (true)
            {
                if (retVal < 0)
                {
                    retVal += 360;
                }
                else if (retVal >= 360)
                {
                    retVal -= 360;
                }
                else
                {
                    return retVal;
                }
            }
        }

        private static byte GetByteCapped(double value)
        {
            if (value < 0)
            {
                return 0;
            }
            else if (value > 255)
            {
                return 255;
            }
            else
            {
                return Convert.ToByte(value);
            }
        }

        #endregion
    }

    #region struct: ColorHSV

    public struct ColorHSV
    {
        #region Constructor

        public ColorHSV(double h, double s, double v)
            : this(255, h, s, v) { }
        public ColorHSV(byte a, double h, double s, double v)
        {
            this.A = a;
            this.H = h;
            this.S = s;
            this.V = v;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Alpha: 0 to 255
        /// </summary>
        public readonly byte A;

        /// <summary>
        /// Hue: 0 to 360
        /// </summary>
        public readonly double H;

        /// <summary>
        /// Saturation: 0 to 100
        /// </summary>
        public readonly double S;

        /// <summary>
        /// Value: 0 to 100
        /// </summary>
        public readonly double V;

        #endregion

        #region Public Methods

        public Color ToRGB()
        {
            return UtilityWPF.HSVtoRGB(this.A, this.H, this.S, this.V);
        }

        public override string ToString()
        {
            return string.Format("A {1}{0}H {2}{0}S {3}{0}V {4}", "  |  ", GetFormatedNumber(this.A), GetFormatedNumber(this.H), GetFormatedNumber(this.S), GetFormatedNumber(this.V));
        }

        #endregion

        #region Private Methods

        private static string GetFormatedNumber(double value)
        {
            return Math.Round(value).
                ToString().
                PadLeft(3, ' ');     // padding left so columns line up (when viewing a list of colors)
        }

        #endregion
    }

    #endregion
}
