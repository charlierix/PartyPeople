using Game.Core;
using System;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace Game.Mathematics
{
    public static partial class Extenders
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

        /// <summary>
        /// This is useful for displaying a double value in a textbox when you don't know the range (could be
        /// 1000001 or .1000001 or 10000.5 etc)
        /// </summary>
        public static string ToStringSignificantDigits(this double value, int significantDigits)
        {
            int numDecimals = GetNumDecimals(value);

            if (numDecimals < 0)
            {
                return ToStringSignificantDigits_PossibleScientific(value, significantDigits);
            }
            else
            {
                return ToStringSignificantDigits_Standard(value, significantDigits, true);
            }
        }

        #endregion

        #region decimal

        /// <summary>
        /// This is useful for displaying a double value in a textbox when you don't know the range (could be
        /// 1000001 or .1000001 or 10000.5 etc)
        /// </summary>
        public static string ToStringSignificantDigits(this decimal value, int significantDigits)
        {
            int numDecimals = GetNumDecimals(value);

            if (numDecimals < 0)
            {
                return ToStringSignificantDigits_PossibleScientific(value, significantDigits);
            }
            else
            {
                return ToStringSignificantDigits_Standard(value, significantDigits, true);
            }
        }

        #endregion

        #region VectorND

        public static bool IsNearZero(this VectorND vector)
        {
            if (vector.VectorArray == null)
            {
                return true;
            }

            return vector.VectorArray.All(o => o.IsNearZero());
        }
        public static bool IsNearValue(this VectorND vector, VectorND other)
        {
            return MathND.IsNearValue(vector.VectorArray, other.VectorArray);
        }

        public static bool IsInvalid(this VectorND vector)
        {
            double[] arr = vector.VectorArray;
            if (arr == null)
            {
                return false;       // it could be argued that this is invalid, but all the other types that have IsInvalid only consider the values that Math1D.IsInvalid looks at
            }

            foreach (double value in arr)
            {
                if (Math1D.IsInvalid(value))
                {
                    return true;
                }
            }

            return false;
        }

        public static string ToStringSignificantDigits(this VectorND vector, int significantDigits)
        {
            double[] arr = vector.VectorArray;
            if (arr == null)
            {
                return "<null>";
            }

            return arr.
                Select(o => o.ToStringSignificantDigits(significantDigits)).
                ToJoin(", ");
        }

        /// <summary>
        /// Returns the portion of this vector that lies along the other vector
        /// NOTE: The return will be the same direction as alongVector, but the length from zero to this vector's full length
        /// </summary>
        /// <remarks>
        /// This is copied from the Vector3D version
        /// </remarks>
        public static VectorND GetProjectedVector(this VectorND vector, VectorND alongVector, bool eitherDirection = true)
        {
            // c = (a dot unit(b)) * unit(b)

            if (vector.IsNearZero() || alongVector.IsNearZero())
            {
                return MathND.GetZeroVector(vector, alongVector);
            }

            VectorND alongVectorUnit = alongVector.ToUnit();

            double length = VectorND.DotProduct(vector, alongVectorUnit);

            if (!eitherDirection && length < 0)
            {
                // It's in the oppositie direction, and that isn't allowed
                return MathND.GetZeroVector(vector, alongVector);
            }

            return alongVectorUnit * length;
        }

        #endregion

        #region double[]

        public static VectorND ToVectorND(this double[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException("values");
            }
            else if (values.Length == 0)
            {
                throw new ArgumentOutOfRangeException("values", "This method requires the double array to be greater than length 0");
            }

            return new VectorND(values);
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

        private static int GetNumDecimals(double value)
        {
            return GetNumDecimals_ToString(value.ToString(System.Globalization.CultureInfo.InvariantCulture));      // I think this forces decimal to always be a '.' ?
        }
        private static int GetNumDecimals(decimal value)
        {
            return GetNumDecimals_ToString(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        private static int GetNumDecimals_ToString(string text)
        {
            if (Regex.IsMatch(text, "[a-z]", RegexOptions.IgnoreCase))
            {
                // This is in exponential notation, just give up (or maybe NaN)
                return -1;
            }

            int decimalIndex = text.IndexOf(".");

            if (decimalIndex < 0)
            {
                // It's an integer
                return 0;
            }
            else
            {
                // Just count the decimals
                return (text.Length - 1) - decimalIndex;
            }
        }

        private static string ToStringSignificantDigits_PossibleScientific(double value, int significantDigits)
        {
            return ToStringSignificantDigits_PossibleScientific_ToString(
                value.ToString(System.Globalization.CultureInfo.InvariantCulture),      // I think this forces decimal to always be a '.' ?
                value.ToString(),
                significantDigits);
        }
        private static string ToStringSignificantDigits_PossibleScientific(decimal value, int significantDigits)
        {
            return ToStringSignificantDigits_PossibleScientific_ToString(
                value.ToString(System.Globalization.CultureInfo.InvariantCulture),      // I think this forces decimal to always be a '.' ?
                value.ToString(),
                significantDigits);
        }
        private static string ToStringSignificantDigits_PossibleScientific_ToString(string textInvariant, string text, int significantDigits)
        {
            Match match = Regex.Match(textInvariant, @"^(?<num>\d\.\d+)(?<exp>E(-|)\d+)$");
            if (!match.Success)
            {
                // Unknown
                return text;
            }

            string standard = ToStringSignificantDigits_Standard(Convert.ToDouble(match.Groups["num"].Value), significantDigits, false);

            return standard + match.Groups["exp"].Value;
        }

        private static string ToStringSignificantDigits_Standard(double value, int significantDigits, bool useN)
        {
            return ToStringSignificantDigits_Standard(Convert.ToDecimal(value), significantDigits, useN);
        }
        private static string ToStringSignificantDigits_Standard(decimal value, int significantDigits, bool useN)
        {
            // Get the integer portion
            //long intPortion = Convert.ToInt64(Math.Truncate(value));		// going directly against the value for this (min could go from 1 to 1000.  1 needs two decimal places, 10 needs one, 100+ needs zero)
            BigInteger intPortion = new BigInteger(Math.Truncate(value));       // ran into a case that didn't fit in a long
            int numInt;
            if (intPortion == 0)
            {
                numInt = 0;
            }
            else
            {
                numInt = intPortion.ToString().Length;
            }

            // Limit the number of significant digits
            int numPlaces;
            if (numInt == 0)
            {
                numPlaces = significantDigits;
            }
            else if (numInt >= significantDigits)
            {
                numPlaces = 0;
            }
            else
            {
                numPlaces = significantDigits - numInt;
            }

            // I was getting an exception from round, but couldn't recreate it, so I'm just throwing this in to avoid the exception
            if (numPlaces < 0)
            {
                numPlaces = 0;
            }
            else if (numPlaces > 15)
            {
                numPlaces = 15;
            }

            // Show a rounded number
            decimal rounded = Math.Round(value, numPlaces);
            int numActualDecimals = GetNumDecimals(rounded);
            if (numActualDecimals < 0 || !useN)
            {
                return rounded.ToString();		// it's weird, don't try to make it more readable
            }
            else
            {
                return rounded.ToString("N" + numActualDecimals);
            }
        }

        #endregion
    }
}
