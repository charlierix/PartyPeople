using System;

namespace Game.Mathematics
{
    public static class Extenders
    {
        #region int

        /// <summary>
        /// This just does Convert.ToDouble().  It doesn't save much typing, but feels more natural
        /// </summary>
        public static double ToDouble(this int value)
        {
            return Convert.ToDouble(value);
        }

        public static byte ToByte(this int value)
        {
            if (value < 0) value = 0;
            else if (value > 255) value = 255;

            return Convert.ToByte(value);
        }

        #endregion

        #region long

        /// <summary>
        /// This just does Convert.ToDouble().  It doesn't save much typing, but feels more natural
        /// </summary>
        public static double ToDouble(this long value)
        {
            return Convert.ToDouble(value);
        }

        public static byte ToByte(this long value)
        {
            if (value < 0) value = 0;
            else if (value > 255) value = 255;

            return Convert.ToByte(value);
        }

        #endregion

        #region double

        public static bool IsNearZero(this double item, double threshold = UtilityMath.NEARZERO)
        {
            return Math.Abs(item) <= threshold;
        }

        public static bool IsNearValue(this double item, double compare, double threshold = UtilityMath.NEARZERO)
        {
            return item >= compare - threshold && item <= compare + threshold;
        }

        public static bool IsInvalid(this double item)
        {
            return Math1D.IsInvalid(item);
        }

        public static int ToInt_Round(this double value)
        {
            return ToIntSafe(Math.Round(value));
        }
        public static int ToInt_Floor(this double value)
        {
            return ToIntSafe(Math.Floor(value));
        }
        public static int ToInt_Ceiling(this double value)
        {
            return ToIntSafe(Math.Ceiling(value));
        }

        public static byte ToByte_Round(this double value)
        {
            return ToByteSafe(Math.Round(value));
        }
        public static byte ToByte_Floor(this double value)
        {
            return ToByteSafe(Math.Floor(value));
        }
        public static byte ToByte_Ceiling(this double value)
        {
            return ToByteSafe(Math.Ceiling(value));
        }

        #endregion

        #region Private Methods

        private static int ToIntSafe(double value)
        {
            double retVal = value;

            if (retVal < int.MinValue) retVal = int.MinValue;
            else if (retVal > int.MaxValue) retVal = int.MaxValue;
            else if (Math1D.IsInvalid(retVal)) retVal = int.MaxValue;

            return Convert.ToInt32(retVal);
        }
        private static byte ToByteSafe(double value)
        {
            int retVal = ToIntSafe(Math.Ceiling(value));

            if (retVal < 0) retVal = 0;
            else if (retVal > 255) retVal = 255;
            else if (Math1D.IsInvalid(retVal)) retVal = 255;

            return Convert.ToByte(retVal);
        }

        #endregion
    }
}
