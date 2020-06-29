using Game.Core;
using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace Game.Math_WPF.WPF
{
    /// <summary>
    /// These are extension methods of various wpf types
    /// </summary>
    public static class Extenders
    {
        #region Color

        public static ColorHSV ToHSV(this Color color)
        {
            return UtilityWPF.RGBtoHSV(color);
        }

        public static string ToHex(this Color color, bool includeAlpha = true, bool includePound = true)
        {
            return UtilityWPF.ColorToHex(color, includeAlpha, includePound);
        }

        public static Color ToGray(this Color color)
        {
            return UtilityWPF.ConvertToGray(color);
        }

        #endregion

        #region Random

        public static ColorHSV ColorHSV(this Random rand)
        {
            return ColorHSV(rand, 0, 360, 0, 100, 0, 100);
        }
        public static ColorHSV ColorHSV(this Random rand, double hueMin, double hueMax)
        {
            return ColorHSV(rand, hueMin, hueMax, 0, 100, 0, 100);
        }
        public static ColorHSV ColorHSV(this Random rand, double hueMin, double hueMax, double saturationMin, double saturationMax, double valueMin, double valueMax)
        {
            return new ColorHSV
            (
                UtilityMath.Clamp(rand.NextDouble(hueMin, hueMax), 0, 360),
                UtilityMath.Clamp(rand.NextDouble(saturationMin, saturationMax), 0, 100),
                UtilityMath.Clamp(rand.NextDouble(valueMin, valueMax), 0, 100)
            );
        }

        public static ColorHSV ColorHSVA(this Random rand, double alphaMin, double alphaMax)
        {
            return ColorHSVA(rand, 0, 360, 0, 100, 0, 100, alphaMin, alphaMax);
        }
        public static ColorHSV ColorHSVA(this Random rand, double hueMin, double hueMax, double alphaMin, double alphaMax)
        {
            return ColorHSVA(rand, hueMin, hueMax, 0, 100, 0, 100, alphaMin, alphaMax);
        }
        public static ColorHSV ColorHSVA(this Random rand, double hueMin, double hueMax, double saturationMin, double saturationMax, double valueMin, double valueMax, double alphaMin, double alphaMax)
        {
            double alpha = rand.NextDouble(alphaMin, alphaMax);
            alpha = UtilityMath.GetScaledValue_Capped(0, 245, 0, 100, alpha);

            return new ColorHSV
            (
                alpha.ToByte_Round(),
                UtilityMath.Clamp(rand.NextDouble(hueMin, hueMax), 0, 360),
                UtilityMath.Clamp(rand.NextDouble(saturationMin, saturationMax), 0, 100),
                UtilityMath.Clamp(rand.NextDouble(valueMin, valueMax), 0, 100)
            );
        }

        public static ColorHSV ColorHSV(this Random rand, string hex, double driftH = 0d, double driftS = 0d, double driftV = 0d, double driftA = 0d)
        {
            ColorHSV color = UtilityWPF.ColorFromHex(hex).ToHSV();

            if (color.A == 255 && driftA.IsNearZero())
            {
                return new ColorHSV
                (
                    rand.NextDrift(color.H, driftH),
                    rand.NextDrift(color.S, driftS),
                    rand.NextDrift(color.V, driftV)
                );
            }
            else
            {
                double a = UtilityMath.GetScaledValue(0, 100, 0, 255, color.A);
                a = rand.NextDrift(a, driftA);

                return new ColorHSV
                (
                    UtilityMath.GetScaledValue_Capped(0, 255, 0, 100, a).ToByte_Round(),
                    rand.NextDrift(color.H, driftH),
                    rand.NextDrift(color.S, driftS),
                    rand.NextDrift(color.V, driftV)
                );
            }

        }

        #endregion
    }
}
