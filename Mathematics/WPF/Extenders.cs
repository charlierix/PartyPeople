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
    }
}
