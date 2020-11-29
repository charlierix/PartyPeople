using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.Mathematics
{
    // System.Numerics and wpf have several of the same named types.  Putting wpf in its own file

    //TODO: As system.numerics is getting embedded, add _wpf to the end of functions that return a wpf type.  ex: ToVector() becomes ToVector_wpf()
    //Only do this once most of the relevant parts of the old game are copied over so references are known
    public static partial class Extenders
    {
        #region Vector

        public static bool IsNearZero(this Vector vector)
        {
            return vector.X.IsNearZero() &&
                        vector.Y.IsNearZero();
        }
        public static bool IsNearValue(this Vector vector, Vector compare)
        {
            return vector.X.IsNearValue(compare.X) &&
                        vector.Y.IsNearValue(compare.Y);
        }

        public static bool IsInvalid(this Vector vector)
        {
            return Math2D.IsInvalid(vector);
        }

        public static Point ToPoint(this Vector vector)
        {
            return new Point(vector.X, vector.Y);
        }

        public static Point3D ToPoint3D(this Vector vector)
        {
            return new Point3D(vector.X, vector.Y, 0d);
        }
        public static Point3D ToPoint3D(this Vector vector, double z)
        {
            return new Point3D(vector.X, vector.Y, z);
        }

        public static Vector3D ToVector3D(this Vector vector)
        {
            return new Vector3D(vector.X, vector.Y, 0d);
        }
        public static Vector3D ToVector3D(this Vector vector, double z)
        {
            return new Vector3D(vector.X, vector.Y, z);
        }

        public static VectorND ToVectorND(this Vector vector, int? dimensions = null)
        {
            if (dimensions == null)
            {
                return new VectorND(vector.X, vector.Y);
            }
            else
            {
                double[] arr = new double[dimensions.Value];

                for (int cntr = 0; cntr < Math.Min(2, dimensions.Value); cntr++)
                {
                    switch (cntr)
                    {
                        case 0: arr[cntr] = vector.X; break;
                        case 1: arr[cntr] = vector.Y; break;
                    }
                }

                return new VectorND(arr);
            }
        }

        public static string ToString(this Vector vector, bool extensionsVersion)
        {
            return vector.X.ToString() + ", " + vector.Y.ToString();
        }
        public static string ToString(this Vector vector, int significantDigits)
        {
            return vector.X.ToString("N" + significantDigits.ToString()) + ", " + vector.Y.ToString("N" + significantDigits.ToString());
        }

        public static string ToStringSignificantDigits(this Vector vector, int significantDigits, bool shouldRound = true)
        {
            return string.Format("{0}, {1}",
                vector.X.ToStringSignificantDigits(significantDigits, shouldRound),
                vector.Y.ToStringSignificantDigits(significantDigits, shouldRound));
        }

        /// <summary>
        /// I was getting tired of needing two statements to get a unit vector
        /// </summary>
        public static Vector ToUnit(this Vector vector, bool useNaNIfInvalid = false)
        {
            Vector retVal = vector;
            retVal.Normalize();

            if (!useNaNIfInvalid && Math2D.IsInvalid(retVal))
            {
                retVal = new Vector(0, 0);
            }

            return retVal;
        }

        #endregion

        #region Point

        public static bool IsNearZero(this Point point)
        {
            return point.X.IsNearZero() &&
                        point.Y.IsNearZero();
        }
        public static bool IsNearValue(this Point point, Point compare)
        {
            return point.X.IsNearValue(compare.X) &&
                        point.Y.IsNearValue(compare.Y);
        }

        public static bool IsInvalid(this Point point)
        {
            return Math2D.IsInvalid(point);
        }

        public static Vector ToVector(this Point point)
        {
            return new Vector(point.X, point.Y);
        }

        public static Vector3D ToVector3D(this Point point)
        {
            return new Vector3D(point.X, point.Y, 0d);
        }
        public static Vector3D ToVector3D(this Point point, double z)
        {
            return new Vector3D(point.X, point.Y, z);
        }

        public static Point3D ToPoint3D(this Point point)
        {
            return new Point3D(point.X, point.Y, 0d);
        }
        public static Point3D ToPoint3D(this Point point, double z)
        {
            return new Point3D(point.X, point.Y, z);
        }

        public static VectorND ToVectorND(this Point point, int? dimensions = null)
        {
            if (dimensions == null)
            {
                return new VectorND(point.X, point.Y);
            }
            else
            {
                double[] arr = new double[dimensions.Value];

                for (int cntr = 0; cntr < Math.Min(2, dimensions.Value); cntr++)
                {
                    switch (cntr)
                    {
                        case 0: arr[cntr] = point.X; break;
                        case 1: arr[cntr] = point.Y; break;
                    }
                }

                return new VectorND(arr);
            }
        }

        public static string ToString(this Point point, bool extensionsVersion)
        {
            return point.X.ToString() + ", " + point.Y.ToString();
        }
        public static string ToString(this Point point, int significantDigits)
        {
            return point.X.ToString("N" + significantDigits.ToString()) + ", " + point.Y.ToString("N" + significantDigits.ToString());
        }

        public static string ToStringSignificantDigits(this Point point, int significantDigits, bool shouldRound = true)
        {
            return string.Format("{0}, {1}",
                point.X.ToStringSignificantDigits(significantDigits, shouldRound),
                point.Y.ToStringSignificantDigits(significantDigits, shouldRound));
        }

        #endregion

        #region Vector3D

        public static bool IsZero(this Vector3D vector)
        {
            return vector.X == 0d && vector.Y == 0d && vector.Z == 0d;
        }

        public static bool IsNearZero(this Vector3D vector)
        {
            return vector.X.IsNearZero() &&
                        vector.Y.IsNearZero() &&
                        vector.Z.IsNearZero();
        }
        public static bool IsNearValue(this Vector3D vector, Vector3D compare)
        {
            return vector.X.IsNearValue(compare.X) &&
                        vector.Y.IsNearValue(compare.Y) &&
                        vector.Z.IsNearValue(compare.Z);
        }

        public static bool IsInvalid(this Vector3D vector)
        {
            return Math3D.IsInvalid(vector);
        }

        public static System.Numerics.Vector3 ToVector3(this Vector3D vector)
        {
            return new System.Numerics.Vector3((float)vector.X, (float)vector.Y, (float)vector.Z);
        }

        public static Point3D ToPoint(this Vector3D vector)
        {
            return new Point3D(vector.X, vector.Y, vector.Z);
        }

        public static Vector ToVector2D(this Vector3D vector)
        {
            return new Vector(vector.X, vector.Y);
        }
        public static Point ToPoint2D(this Vector3D vector)
        {
            return new Point(vector.X, vector.Y);
        }

        public static Size3D ToSize(this Vector3D vector)
        {
            return new Size3D(Math.Abs(vector.X), Math.Abs(vector.Y), Math.Abs(vector.Z));
        }

        public static VectorND ToVectorND(this Vector3D vector, int? dimensions = null)
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

        public static double[] ToArray(this Vector3D vector)
        {
            return new[] { vector.X, vector.Y, vector.Z };
        }

        public static string ToString(this Vector3D vector, bool extensionsVersion)
        {
            return vector.X.ToString() + ", " + vector.Y.ToString() + ", " + vector.Z.ToString();
        }
        public static string ToString(this Vector3D vector, int significantDigits)
        {
            return vector.X.ToString("N" + significantDigits.ToString()) + ", " + vector.Y.ToString("N" + significantDigits.ToString()) + ", " + vector.Z.ToString("N" + significantDigits.ToString());
        }

        public static string ToStringSignificantDigits(this Vector3D vector, int significantDigits, bool shouldRound = true)
        {
            return string.Format("{0}, {1}, {2}",
                vector.X.ToStringSignificantDigits(significantDigits, shouldRound),
                vector.Y.ToStringSignificantDigits(significantDigits, shouldRound),
                vector.Z.ToStringSignificantDigits(significantDigits, shouldRound));
        }

        /// <summary>
        /// Rotates the vector around the angle in degrees
        /// </summary>
        public static Vector3D GetRotatedVector(this Vector3D vector, Vector3D axis, double angle)
        {
            Matrix3D matrix = new Matrix3D();
            matrix.Rotate(new Quaternion(axis, angle));

            MatrixTransform3D transform = new MatrixTransform3D(matrix);

            return transform.Transform(vector);
        }

        /// <summary>
        /// Returns the portion of this vector that lies along the other vector
        /// NOTE: The return will be the same direction as alongVector, but the length from zero to this vector's full length
        /// </summary>
        /// <remarks>
        /// Lookup "vector projection" to see the difference between this and dot product
        /// http://en.wikipedia.org/wiki/Vector_projection
        /// </remarks>
        public static Vector3D GetProjectedVector(this Vector3D vector, Vector3D alongVector, bool eitherDirection = true)
        {
            // c = (a dot unit(b)) * unit(b)

            if (Math3D.IsNearZero(vector) || Math3D.IsNearZero(alongVector))
            {
                return new Vector3D(0, 0, 0);
            }

            Vector3D alongVectorUnit = alongVector;
            alongVectorUnit.Normalize();

            double length = Vector3D.DotProduct(vector, alongVectorUnit);

            if (!eitherDirection && length < 0)
            {
                // It's in the oppositie direction, and that isn't allowed
                return new Vector3D(0, 0, 0);
            }

            return alongVectorUnit * length;
        }
        public static Vector3D GetProjectedVector(this Vector3D vector, ITriangle_wpf alongPlane)
        {
            // Get a line that is parallel to the plane, but along the direction of the vector
            Vector3D alongLine = Vector3D.CrossProduct(alongPlane.Normal, Vector3D.CrossProduct(vector, alongPlane.Normal));

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
        public static Vector3D ToUnit(this Vector3D vector, bool useNaNIfInvalid = false)
        {
            Vector3D retVal = vector;
            retVal.Normalize();

            if (!useNaNIfInvalid && Math3D.IsInvalid(retVal))
            {
                retVal = new Vector3D(0, 0, 0);
            }

            return retVal;
        }

        public static double Coord(this Vector3D vector, Axis axis)
        {
            switch (axis)
            {
                case Axis.X:
                    return vector.X;

                case Axis.Y:
                    return vector.Y;

                case Axis.Z:
                    return vector.Z;

                default:
                    throw new ApplicationException("Unknown Axis: " + axis.ToString());
            }
        }

        #endregion

        #region Point3D

        public static Point3D MultiplyBy(this Point3D point, double scalar)
        {
            return new Point3D(point.X * scalar, point.Y * scalar, point.Z * scalar);
        }
        public static Point3D DivideBy(this Point3D point, double scalar)
        {
            return new Point3D(point.X / scalar, point.Y / scalar, point.Z / scalar);
        }

        public static bool IsInvalid(this Point3D point)
        {
            return Math3D.IsInvalid(point);
        }

        public static bool IsNearZero(this Point3D point)
        {
            return point.X.IsNearZero() &&
                        point.Y.IsNearZero() &&
                        point.Z.IsNearZero();
        }
        public static bool IsNearValue(this Point3D point, Point3D compare)
        {
            return point.X.IsNearValue(compare.X) &&
                        point.Y.IsNearValue(compare.Y) &&
                        point.Z.IsNearValue(compare.Z);
        }

        public static System.Numerics.Vector3 ToVector3(this Point3D point)
        {
            return new System.Numerics.Vector3((float)point.X, (float)point.Y, (float)point.Z);
        }

        public static Vector3D ToVector(this Point3D point)
        {
            return new Vector3D(point.X, point.Y, point.Z);
        }

        public static Point ToPoint2D(this Point3D point)
        {
            return new Point(point.X, point.Y);
        }
        public static Vector ToVector2D(this Point3D point)
        {
            return new Vector(point.X, point.Y);
        }

        public static VectorND ToVectorND(this Point3D point, int? dimensions = null)
        {
            if (dimensions == null)
            {
                return new VectorND(point.X, point.Y, point.Z);
            }
            else
            {
                double[] arr = new double[dimensions.Value];

                for (int cntr = 0; cntr < Math.Min(3, dimensions.Value); cntr++)
                {
                    switch (cntr)
                    {
                        case 0: arr[cntr] = point.X; break;
                        case 1: arr[cntr] = point.Y; break;
                        case 2: arr[cntr] = point.Z; break;
                    }
                }

                return new VectorND(arr);
            }
        }

        public static double[] ToArray(this Point3D point)
        {
            return new[] { point.X, point.Y, point.Z };
        }

        public static string ToString(this Point3D point, bool extensionsVersion)
        {
            return point.X.ToString() + ", " + point.Y.ToString() + ", " + point.Z.ToString();
        }
        public static string ToString(this Point3D point, int significantDigits)
        {
            return point.X.ToString("N" + significantDigits.ToString()) + ", " + point.Y.ToString("N" + significantDigits.ToString()) + ", " + point.Z.ToString("N" + significantDigits.ToString());
        }

        public static string ToStringSignificantDigits(this Point3D point, int significantDigits, bool shouldRound = true)
        {
            return string.Format("{0}, {1}, {2}",
                point.X.ToStringSignificantDigits(significantDigits, shouldRound),
                point.Y.ToStringSignificantDigits(significantDigits, shouldRound),
                point.Z.ToStringSignificantDigits(significantDigits, shouldRound));
        }

        /// <summary>
        /// I was getting tired of needing two statements to get a unit vector
        /// </summary>
        /// <param name="useNaNIfInvalid">
        /// True=Standard behavior.  By definition a unit vector always has length of one, so if the initial length is zero, then the length becomes NaN
        /// False=Vector just goes to zero
        /// </param>
        public static Point3D ToUnit(this Point3D point, bool useNaNIfInvalid = false)
        {
            return point.ToVector().ToUnit(useNaNIfInvalid).ToPoint();
        }

        public static double Coord(this Point3D point, Axis axis)
        {
            switch (axis)
            {
                case Axis.X:
                    return point.X;

                case Axis.Y:
                    return point.Y;

                case Axis.Z:
                    return point.Z;

                default:
                    throw new ApplicationException("Unknown Axis: " + axis.ToString());
            }
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
        //TODO: Test whether to use this matrix transform, or use: new RotateTransform3D(new QuaternionRotation3D(quaternion))
        public static Vector3D GetRotatedVector(this Quaternion quaternion, Vector3D vector)
        {
            Matrix3D matrix = new Matrix3D();
            matrix.Rotate(quaternion);

            MatrixTransform3D transform = new MatrixTransform3D(matrix);

            return transform.Transform(vector);
        }
        public static Point3D GetRotatedVector(this Quaternion quaternion, Point3D point)
        {
            Matrix3D matrix = new Matrix3D();
            matrix.Rotate(quaternion);

            MatrixTransform3D transform = new MatrixTransform3D(matrix);

            return transform.Transform(point);
        }
        public static DoubleVector_wpf GetRotatedVector(this Quaternion quaternion, DoubleVector_wpf doubleVector)
        {
            Matrix3D matrix = new Matrix3D();
            matrix.Rotate(quaternion);

            MatrixTransform3D transform = new MatrixTransform3D(matrix);

            return new DoubleVector_wpf(transform.Transform(doubleVector.Standard), transform.Transform(doubleVector.Orth));
        }

        public static void GetRotatedVector(this Quaternion quaternion, Vector3D[] vectors)
        {
            Matrix3D matrix = new Matrix3D();
            matrix.Rotate(quaternion);

            MatrixTransform3D transform = new MatrixTransform3D(matrix);

            transform.Transform(vectors);
        }
        public static void GetRotatedVector(this Quaternion quaternion, Point3D[] points)
        {
            Matrix3D matrix = new Matrix3D();
            matrix.Rotate(quaternion);

            MatrixTransform3D transform = new MatrixTransform3D(matrix);

            transform.Transform(points);
        }
        public static void GetRotatedVector(this Quaternion quaternion, DoubleVector_wpf[] doubleVectors)
        {
            Matrix3D matrix = new Matrix3D();
            matrix.Rotate(quaternion);

            MatrixTransform3D transform = new MatrixTransform3D(matrix);

            for (int cntr = 0; cntr < doubleVectors.Length; cntr++)
            {
                doubleVectors[cntr] = new DoubleVector_wpf(transform.Transform(doubleVectors[cntr].Standard), transform.Transform(doubleVectors[cntr].Orth));
            }
        }
        /// <summary>
        /// This overload will rotate arrays of different types.  Just pass null if you don't have any of that type
        /// </summary>
        public static void GetRotatedVector(this Quaternion quaternion, Vector3D[] vectors, Point3D[] points, DoubleVector_wpf[] doubleVectors)
        {
            Matrix3D matrix = new Matrix3D();
            matrix.Rotate(quaternion);

            MatrixTransform3D transform = new MatrixTransform3D(matrix);

            if (vectors != null)
            {
                transform.Transform(vectors);
            }

            if (points != null)
            {
                transform.Transform(points);
            }

            if (doubleVectors != null)
            {
                for (int cntr = 0; cntr < doubleVectors.Length; cntr++)
                {
                    doubleVectors[cntr] = new DoubleVector_wpf(transform.Transform(doubleVectors[cntr].Standard), transform.Transform(doubleVectors[cntr].Orth));
                }
            }
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
            return quaternion.ToUnit() * delta.ToUnit();
        }

        /// <summary>
        /// This returns a quaternion that will rotate in the opposite direction
        /// </summary>
        public static Quaternion ToReverse(this Quaternion quaternion)
        {
            #region OLD

            // From MSDN:
            //		Conjugate - Replaces a quaternion with its conjugate.
            //		Invert - Replaces the specified quaternion with its inverse
            //
            // Awesome explanation.  I'm assuming that conjugate is the inverse of a unit quaternion, and invert is the inverse of any quaternion (slower but safer)
            //
            // I poked around, and found the source for quaternion here:
            // http://reflector.webtropy.com/default.aspx/Dotnetfx_Vista_SP2/Dotnetfx_Vista_SP2/8@0@50727@4016/DEVDIV/depot/DevDiv/releases/Orcas/QFE/wpf/src/Core/CSharp/System/Windows/Media3D/Quaternion@cs/1/Quaternion@cs
            //
            // Here is the important part of the invert method:
            //		Conjugate();
            //		double norm2 = _x * _x + _y * _y + _z * _z + _w * _w;
            //		_x /= norm2;
            //		_y /= norm2;
            //		_z /= norm2;
            //		_w /= norm2;

            // I was about to do this, but I'm not sure if that would return what I think, so I'll just use the same axis and invert the angle
            //Quaternion retVal = quaternion;
            //if (!retVal.IsNormalized)
            //{
            //    retVal.Normalize();
            //}
            //retVal.Conjugate();
            //return retVal;

            #endregion

            if (quaternion.IsIdentity)
            {
                return Quaternion.Identity;
            }
            else
            {
                return new Quaternion(quaternion.Axis, -quaternion.Angle);
            }
        }

        public static Quaternion ToUnit(this Quaternion quaternion)
        {
            Quaternion retVal = quaternion;
            retVal.Normalize();
            return retVal;
        }

        public static System.Numerics.Quaternion ToQuat_numerics(this Quaternion quaternion)
        {
            return new System.Numerics.Quaternion((float)quaternion.X, (float)quaternion.Y, (float)quaternion.Z, (float)quaternion.W);
        }

        public static Vector3D ToWorld(this Quaternion quaternion, Vector3D directionLocal)
        {
            Matrix3D matrix = new Matrix3D();
            matrix.Rotate(quaternion);

            return matrix.Transform(directionLocal);

            //MatrixTransform3D transform = new MatrixTransform3D(matrix);

            //return transform.Transform(directionLocal);
        }
        public static Vector3D FromWorld(this Quaternion quaternion, Vector3D directionWorld)
        {
            Matrix3D matrix = new Matrix3D();
            matrix.Rotate(quaternion);
            matrix.Invert();

            return matrix.Transform(directionWorld);

            //MatrixTransform3D transform = new MatrixTransform3D(matrix);

            //return transform.Transform(directionWorld);
        }

        #endregion

        #region Size

        public static Size Add(this Size size, Size add)
        {
            return new Size(size.Width + add.Width, size.Height + add.Height);
        }
        public static Size Subtract(this Size size, Size subtract)
        {
            return new Size(size.Width - subtract.Width, size.Height - subtract.Height);
        }
        public static Size Multiply(this Size size, double mult)
        {
            return new Size(size.Width * mult, size.Height * mult);
        }
        public static Size Divide(this Size size, double divisor)
        {
            return new Size(size.Width / divisor, size.Height / divisor);
        }

        public static Size3D ToSize3D(this Size size, double z = 0)
        {
            return new Size3D(size.Width, size.Height, z);
        }

        #endregion

        #region Size3D

        public static Size ToSize2D(this Size3D size)
        {
            return new Size(size.X, size.Y);
        }

        public static Vector3D ToVector(this Size3D size)
        {
            return new Vector3D(size.X, size.Y, size.Z);
        }

        #endregion

        #region Rect

        public static double CenterX(this Rect rect)
        {
            return rect.X + (rect.Width / 2d);
        }
        public static double CenterY(this Rect rect)
        {
            return rect.Y + (rect.Height / 2d);
        }

        public static Point Center(this Rect rect)
        {
            return new Point(rect.CenterX(), rect.CenterY());
        }

        /// <summary>
        /// This returns a rectangle that is the new size, but still centered around the original's center point
        /// </summary>
        public static Rect ChangeSize(this Rect rect, double multiplier)
        {
            double halfWidth = rect.Width / 2;
            double halfHeight = rect.Height / 2;

            return new Rect(
                (rect.X + halfWidth) - (halfWidth * multiplier),
                (rect.Y + halfHeight) - (halfHeight * multiplier),
                rect.Width * multiplier,
                rect.Height * multiplier);
        }

        public static Rect3D ToRect3D(this Rect rect, double z = 0)
        {
            return new Rect3D(rect.Location.ToPoint3D(z), rect.Size.ToSize3D());
        }

        #endregion

        #region Rect3D

        public static double CenterX(this Rect3D rect)
        {
            return rect.X + (rect.SizeX / 2d);
        }
        public static double CenterY(this Rect3D rect)
        {
            return rect.Y + (rect.SizeY / 2d);
        }
        public static double CenterZ(this Rect3D rect)
        {
            return rect.Z + (rect.SizeZ / 2d);
        }

        /// <summary>
        /// Returns true if either rectangle is inside the other or touching
        /// </summary>
        public static bool OverlapsWith(this Rect3D thisRect, Rect3D rect)
        {
            return thisRect.IntersectsWith(rect) || thisRect.Contains(rect) || rect.Contains(thisRect);
        }

        public static double DiagonalLength(this Rect3D rect)
        {
            return new Vector3D(rect.SizeX, rect.SizeY, rect.SizeZ).Length;
        }

        /// <summary>
        /// This returns a rectangle that is the new size, but still centered around the original's center point
        /// </summary>
        public static Rect3D ChangeSize(this Rect3D rect, double multiplier)
        {
            return new Rect3D(
                rect.X - (rect.SizeX * multiplier / 2),
                rect.Y - (rect.SizeY * multiplier / 2),
                rect.Z - (rect.SizeZ * multiplier / 2),
                rect.SizeX * multiplier,
                rect.SizeY * multiplier,
                rect.SizeZ * multiplier);
        }

        public static Rect ToRect2D(this Rect3D rect)
        {
            return new Rect(rect.Location.ToPoint2D(), rect.Size.ToSize2D());
        }

        #endregion

        #region double[]

        public static Vector ToVector(this double[] values, bool enforceSize = true)
        {
            if (enforceSize)
            {
                if (values == null)
                {
                    throw new ArgumentNullException("values");
                }
                else if (values.Length != 2)
                {
                    throw new ArgumentOutOfRangeException("values", string.Format("This method requires the double array to be length 2.  len={0}", values.Length));
                }

                return new Vector(values[0], values[1]);
            }
            else
            {
                if (values == null)
                {
                    return new Vector();
                }

                return new Vector
                (
                    values.Length >= 1 ?
                        values[0] :
                        0,
                    values.Length >= 2 ?
                        values[1] :
                        0
                );
            }
        }
        public static Vector3D ToVector3D(this double[] values, bool enforceSize = true)
        {
            if (enforceSize)
            {
                if (values == null)
                {
                    throw new ArgumentNullException("values");
                }
                else if (values.Length != 3)
                {
                    throw new ArgumentOutOfRangeException("values", string.Format("This method requires the double array to be length 3.  len={0}", values.Length));
                }

                return new Vector3D(values[0], values[1], values[2]);
            }
            else
            {
                if (values == null)
                {
                    return new Vector3D();
                }

                return new Vector3D
                (
                    values.Length >= 1 ?
                        values[0] :
                        0,
                    values.Length >= 2 ?
                        values[1] :
                        0,
                    values.Length >= 3 ?
                        values[2] :
                        0
                );
            }
        }

        public static Point ToPoint(this double[] values, bool enforceSize = true)
        {
            if (enforceSize)
            {
                if (values == null)
                {
                    throw new ArgumentNullException("values");
                }
                else if (values.Length != 2)
                {
                    throw new ArgumentOutOfRangeException("values", string.Format("This method requires the double array to be length 2.  len={0}", values.Length));
                }

                return new Point(values[0], values[1]);
            }
            else
            {
                if (values == null)
                {
                    return new Point();
                }

                return new Point
                (
                    values.Length >= 1 ?
                        values[0] :
                        0,
                    values.Length >= 2 ?
                        values[1] :
                        0
                );
            }
        }
        public static Point3D ToPoint3D(this double[] values, bool enforceSize = true)
        {
            if (enforceSize)
            {
                if (values == null)
                {
                    throw new ArgumentNullException("values");
                }
                else if (values.Length != 3)
                {
                    throw new ArgumentOutOfRangeException("values", string.Format("This method requires the double array to be length 3.  len={0}", values.Length));
                }

                return new Point3D(values[0], values[1], values[2]);
            }
            else
            {
                if (values == null)
                {
                    return new Point3D();
                }

                return new Point3D
                (
                    values.Length >= 1 ?
                        values[0] :
                        0,
                    values.Length >= 2 ?
                        values[1] :
                        0,
                    values.Length >= 3 ?
                        values[2] :
                        0
                );
            }
        }

        #endregion
    }
}
