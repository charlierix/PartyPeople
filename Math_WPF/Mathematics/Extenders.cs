using Game.Core;
using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Game.Math_WPF.Mathematics
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

        #region float

        public static bool IsNearZero(this float item, float threshold = UtilityMath.NEARZERO_F)
        {
            return Math.Abs(item) <= threshold;
        }

        public static bool IsNearValue(this float item, float compare, float threshold = UtilityMath.NEARZERO_F)
        {
            return item >= compare - threshold && item <= compare + threshold;
        }

        public static bool IsInvalid(this float item)
        {
            return Math1D.IsInvalid(item);
        }

        public static int ToInt_Round(this float value)
        {
            return ToIntSafe(Math.Round(value));
        }
        public static int ToInt_Floor(this float value)
        {
            return ToIntSafe(Math.Floor(value));
        }
        public static int ToInt_Ceiling(this float value)
        {
            return ToIntSafe(Math.Ceiling(value));
        }

        public static byte ToByte_Round(this float value)
        {
            return ToByteSafe(Math.Round(value));
        }
        public static byte ToByte_Floor(this float value)
        {
            return ToByteSafe(Math.Floor(value));
        }
        public static byte ToByte_Ceiling(this float value)
        {
            return ToByteSafe(Math.Ceiling(value));
        }

        /// <summary>
        /// This is useful for displaying a value in a textbox when you don't know the range (could be
        /// 1000001 or .1000001 or 10000.5 etc)
        /// </summary>
        public static string ToStringSignificantDigits(this float value, int significantDigits)
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

        #region Vector3

        public static bool IsZero(this Vector3 vector)
        {
            return vector.X == 0f && vector.Y == 0f && vector.Z == 0f;
        }

        public static bool IsNearZero(this Vector3 vector)
        {
            return vector.X.IsNearZero() &&
                        vector.Y.IsNearZero() &&
                        vector.Z.IsNearZero();
        }
        public static bool IsNearValue(this Vector3 vector, Vector3 compare)
        {
            return vector.X.IsNearValue(compare.X) &&
                        vector.Y.IsNearValue(compare.Y) &&
                        vector.Z.IsNearValue(compare.Z);
        }

        public static bool IsInvalid(this Vector3 vector)
        {
            return Math3D.IsInvalid(vector);
        }

        public static System.Windows.Media.Media3D.Vector3D ToVector_wpf(this Vector3 vector)
        {
            return new System.Windows.Media.Media3D.Vector3D(vector.X, vector.Y, vector.Z);
        }
        public static System.Windows.Media.Media3D.Point3D ToPoint_wpf(this Vector3 vector)
        {
            return new System.Windows.Media.Media3D.Point3D(vector.X, vector.Y, vector.Z);
        }

        public static System.Windows.Vector ToVector2D_wpf(this Vector3 vector)
        {
            return new System.Windows.Vector(vector.X, vector.Y);
        }
        public static System.Windows.Point ToPoint2D_wpf(this Vector3 vector)
        {
            return new System.Windows.Point(vector.X, vector.Y);
        }

        public static System.Windows.Media.Media3D.Size3D ToSize(this Vector3 vector)
        {
            return new System.Windows.Media.Media3D.Size3D(Math.Abs(vector.X), Math.Abs(vector.Y), Math.Abs(vector.Z));
        }

        public static VectorND ToVectorND(this Vector3 vector, int? dimensions = null)
        {
            if (dimensions == null)
            {
                return new VectorND(vector.X, vector.Y, vector.Z);
            }
            else
            {
                double[] arr = new double[dimensions.Value];

                for (int cntr = 0; cntr < Math.Min(3, dimensions.Value); cntr++)
                {
                    switch (cntr)
                    {
                        case 0: arr[cntr] = vector.X; break;
                        case 1: arr[cntr] = vector.Y; break;
                        case 2: arr[cntr] = vector.Z; break;
                    }
                }

                return new VectorND(arr);
            }
        }

        public static float[] ToArray(this Vector3 vector)
        {
            return new[] { vector.X, vector.Y, vector.Z };
        }
        public static double[] ToArray_d(this Vector3 vector)
        {
            return new double[] { vector.X, vector.Y, vector.Z };
        }

        public static string ToString(this Vector3 vector, bool extensionsVersion)
        {
            return vector.X.ToString() + ", " + vector.Y.ToString() + ", " + vector.Z.ToString();
        }
        public static string ToString(this Vector3 vector, int significantDigits)
        {
            return vector.X.ToString("N" + significantDigits.ToString()) + ", " + vector.Y.ToString("N" + significantDigits.ToString()) + ", " + vector.Z.ToString("N" + significantDigits.ToString());
        }

        public static string ToStringSignificantDigits(this Vector3 vector, int significantDigits)
        {
            return string.Format("{0}, {1}, {2}", vector.X.ToStringSignificantDigits(significantDigits), vector.Y.ToStringSignificantDigits(significantDigits), vector.Z.ToStringSignificantDigits(significantDigits));
        }

        /// <summary>
        /// Rotates the vector around the angle in degrees
        /// </summary>
        public static Vector3 GetRotatedVector_degrees(this Vector3 vector, Vector3 axis, float angle)
        {
            Vector3 axisUnit = vector.LengthSquared().IsNearValue(1) ?
                axis :
                axis.ToUnit();

            Quaternion quaternion = Quaternion.CreateFromAxisAngle(axisUnit, Math1D.DegreesToRadians(angle));

            return Vector3.Transform(vector, quaternion);
        }
        /// <summary>
        /// Rotates the vector around the angle in degrees
        /// </summary>
        public static Vector3 GetRotatedVector_degrees(this Vector3 vector, Vector3 axis, double angle)
        {
            return GetRotatedVector_degrees(vector, axis, (float)angle);
        }
        /// <summary>
        /// Rotates the vector around the angle in radians
        /// </summary>
        public static Vector3 GetRotatedVector_radians(this Vector3 vector, Vector3 axis, float radians)
        {
            Vector3 axisUnit = vector.LengthSquared().IsNearValue(1) ?
                axis :
                axis.ToUnit();

            Quaternion quaternion = Quaternion.CreateFromAxisAngle(axisUnit, radians);

            return Vector3.Transform(vector, quaternion);
        }
        /// <summary>
        /// Rotates the vector around the angle in radians
        /// </summary>
        public static Vector3 GetRotatedVector_radians(this Vector3 vector, Vector3 axis, double radians)
        {
            return GetRotatedVector_radians(vector, axis, (float)radians);
        }

        /// <summary>
        /// Returns the portion of this vector that lies along the other vector
        /// NOTE: The return will be the same direction as alongVector, but the length from zero to this vector's full length
        /// </summary>
        /// <remarks>
        /// Lookup "vector projection" to see the difference between this and dot product
        /// http://en.wikipedia.org/wiki/Vector_projection
        /// </remarks>
        public static Vector3 GetProjectedVector(this Vector3 vector, Vector3 alongVector, bool eitherDirection = true)
        {
            // c = (a dot unit(b)) * unit(b)

            if (vector.IsNearZero() || alongVector.IsNearZero())
            {
                return new Vector3(0, 0, 0);
            }

            Vector3 alongVectorUnit = Vector3.Normalize(alongVector);

            float length = Vector3.Dot(vector, alongVectorUnit);

            if (!eitherDirection && length < 0)
            {
                // It's in the oppositie direction, and that isn't allowed
                return new Vector3(0, 0, 0);
            }

            return alongVectorUnit * length;
        }
        public static Vector3 GetProjectedVector(this Vector3 vector, ITriangle_wpf alongPlane)
        {
            // Get a line that is parallel to the plane, but along the direction of the vector
            Vector3 planeNormal = alongPlane.Normal.ToVector3();
            var alongLine = Vector3.Cross(planeNormal, Vector3.Cross(vector, planeNormal));

            // Use the other overload to get the portion of the vector along this line
            return vector.GetProjectedVector(alongLine);
        }

        /// <summary>
        /// I was getting tired of needing two statements to get a unit vector
        /// </summary>
        /// <param name="useNaNIfInvalid">
        /// True=Standard behavior.  By definition a unit vector always has length of one, so if the initial length is zero, then the length becomes NaN
        /// False=Vector just goes to zero
        /// </param>
        public static Vector3 ToUnit(this Vector3 vector, bool useNaNIfInvalid = false)
        {
            Vector3 retVal = Vector3.Normalize(vector);

            if (!useNaNIfInvalid && retVal.IsInvalid())
            {
                retVal = new Vector3(0, 0, 0);
            }

            return retVal;
        }

        public static float Coord(this Vector3 vector, Axis axis)
        {
            return axis switch
            {
                Axis.X => vector.X,
                Axis.Y => vector.Y,
                Axis.Z => vector.Z,
                _ => throw new ApplicationException($"Unknown Axis: {axis}"),
            };
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

        #region Quaternion

        public static bool IsNearValue(this Quaternion quaternion, Quaternion compare)
        {
            return quaternion.X.IsNearValue(compare.X) &&
                        quaternion.Y.IsNearValue(compare.Y) &&
                        quaternion.Z.IsNearValue(compare.Z) &&
                        quaternion.W.IsNearValue(compare.W);
        }

        // The code is copied in each of these overloads, rather than make a private method to increase speed
        public static Vector3 GetRotatedVector(this Quaternion quaternion, Vector3 vector)
        {
            return Vector3.Transform(vector, quaternion);
        }
        public static void GetRotatedVector(this Quaternion quaternion, Vector3[] vectors)
        {
            for (int cntr = 0; cntr < vectors.Length; cntr++)
            {
                vectors[cntr] = Vector3.Transform(vectors[cntr], quaternion);
            }
        }

        /// <summary>
        /// Computes the angle change represented by a normalized quaternion.
        /// </summary>
        /// <remarks>
        /// Copied from BepuUtilities.Quaternion
        /// </remarks>
        /// <returns>Angle around the axis represented by the quaternion.</returns>
        public static float GetRadians(this Quaternion quaternion)
        {
            if (!quaternion.LengthSquared().IsNearValue(1f))
            {
                return quaternion.ToUnit().GetRadians();
            }

            float qw = Math.Abs(quaternion.W);
            if (qw > 1)
                return 0;
            return 2 * (float)Math.Acos(qw);
        }
        /// <summary>
        /// Computes the axis angle representation of a normalized quaternion.
        /// </summary>
        /// <remarks>
        /// Copied from BepuUtilities.Quaternion
        /// </remarks>
        /// <param name="quaternion">Quaternion to be converted.</param>
        /// <param name="axis">Axis represented by the quaternion.</param>
        /// <param name="angle">Angle around the axis represented by the quaternion.</param>
        public static (Vector3 axis, float radians) GetAxisRadians(this Quaternion quaternion)
        {
            if (!quaternion.LengthSquared().IsNearValue(1f))        // this isn't the same as the length of the axis
            {
                return quaternion.ToUnit().GetAxisRadians();
            }

            Vector3 axis;

            float qw = quaternion.W;
            if (qw > 0)
            {
                axis.X = quaternion.X;
                axis.Y = quaternion.Y;
                axis.Z = quaternion.Z;
            }
            else
            {
                axis.X = -quaternion.X;
                axis.Y = -quaternion.Y;
                axis.Z = -quaternion.Z;
                qw = -qw;
            }

            float radians;

            float lengthSquared = axis.LengthSquared();
            if (lengthSquared > UtilityMath.NEARZERO_F)
            {
                axis /= (float)Math.Sqrt(lengthSquared);
                radians = 2 * (float)Math.Acos(UtilityMath.Clamp(qw, -1, 1));
            }
            else
            {
                axis = Vector3.UnitY;
                radians = 0;
            }

            return (axis, radians);
        }

        public static float GetAngle(this Quaternion quaternion)
        {
            return Math1D.RadiansToDegrees(quaternion.GetRadians());
        }
        public static (Vector3 axis, float angle) GetAxisAngle(this Quaternion quaternion)
        {
            var retVal = quaternion.GetAxisRadians();
            return (retVal.axis, Math1D.RadiansToDegrees(retVal.radians));
        }

        /// <summary>
        /// This returns the current quaternion rotated by the delta
        /// </summary>
        /// <remarks>
        /// This method is really simple, but I'm tired of trial and error with multiplication order every time I need
        /// to rotate quaternions
        /// </remarks>
        public static Quaternion RotateBy(this Quaternion quaternion, Quaternion delta)
        {
            //return delta.ToUnit() * quaternion.ToUnit();		// this is the one that's backward (I think)

            //return Quaternion.Normalize(quaternion) * Quaternion.Normalize(delta);
            return
            (
                quaternion.LengthSquared().IsNearValue(1) ?
                    quaternion :
                    Quaternion.Normalize(quaternion)
            )

            *

            (
                delta.LengthSquared().IsNearValue(1) ?
                    delta :
                    Quaternion.Normalize(delta)
            );
        }

        /// <summary>
        /// This returns a quaternion that will rotate in the opposite direction
        /// </summary>
        public static Quaternion ToReverse(this Quaternion quaternion)
        {
            if (quaternion.IsIdentity)
            {
                return Quaternion.Identity;
            }
            else
            {
                var axis = quaternion.GetAxisRadians();

                //NOTE: axis needs to be a unit vector (which it is coming from the above function)
                return Quaternion.CreateFromAxisAngle(axis.axis, -axis.radians);
            }
        }

        public static Quaternion ToUnit(this Quaternion quaternion)
        {
            return Quaternion.Normalize(quaternion);
        }

        public static System.Windows.Media.Media3D.Quaternion ToQuat_wpf(this Quaternion quaternion)
        {
            return new System.Windows.Media.Media3D.Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
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

            return Convert.ToByte(retVal);
        }

        private static int GetNumDecimals(float value)
        {
            return GetNumDecimals_ToString(value.ToString(System.Globalization.CultureInfo.InvariantCulture));      // I think this forces decimal to always be a '.' ?
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

        private static string ToStringSignificantDigits_PossibleScientific(float value, int significantDigits)
        {
            return ToStringSignificantDigits_PossibleScientific_ToString(
                value.ToString(System.Globalization.CultureInfo.InvariantCulture),      // I think this forces decimal to always be a '.' ?
                value.ToString(),
                significantDigits);
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

        private static string ToStringSignificantDigits_Standard(float value, int significantDigits, bool useN)
        {
            return ToStringSignificantDigits_Standard(Convert.ToDecimal(value), significantDigits, useN);
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
