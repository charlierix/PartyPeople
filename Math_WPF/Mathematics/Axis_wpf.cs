using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.Mathematics
{
    #region struct: AxisFor

    public partial struct AxisFor
    {
        public double GetValue(Point3D point)
        {
            switch (this.Axis)
            {
                case Axis.X:
                    return point.X;

                case Axis.Y:
                    return point.Y;

                case Axis.Z:
                    return point.Z;

                default:
                    throw new ApplicationException($"Unknown Axis: {this.Axis}");
            }
        }

        public double GetValue(Vector3D vector)
        {
            switch (this.Axis)
            {
                case Axis.X:
                    return vector.X;

                case Axis.Y:
                    return vector.Y;

                case Axis.Z:
                    return vector.Z;

                default:
                    throw new ApplicationException($"Unknown Axis: {this.Axis}");
            }
        }

        /// <summary>
        /// This iterates over two axiis as points
        /// WARNING: It's up to the caller to make sure each of the two axiis is unique (one for X, one for Y, nothing for Z)
        /// </summary>
        public static IEnumerable<Point> Iterate(AxisForDouble axis1, AxisForDouble axis2)
        {
            if (axis1.Axis == Axis.Z || axis2.Axis == Axis.Z)
            {
                throw new ArgumentException("Z should never be passed into this 2D method");
            }

            foreach (double v1 in axis1.Iterate())
            {
                foreach (double v2 in axis2.Iterate())
                {
                    double x = 0, y = 0, dummy = 0;

                    axis1.SetCorrespondingValue(ref x, ref y, ref dummy, v1);
                    axis2.SetCorrespondingValue(ref x, ref y, ref dummy, v2);

                    yield return new Point(x, y);
                }
            }
        }
        /// <summary>
        /// This iterates over three axiis as points
        /// WARNING: It's up to the caller to make sure each of the three axiis is unique (one for X, one for Y, one for Z)
        /// </summary>
        public static IEnumerable<Point3D> Iterate(AxisForDouble axis1, AxisForDouble axis2, AxisForDouble axis3)
        {
            foreach (double v1 in axis1.Iterate())
            {
                foreach (double v2 in axis2.Iterate())
                {
                    foreach (double v3 in axis3.Iterate())
                    {
                        double x = 0, y = 0, z = 0;

                        axis1.SetCorrespondingValue(ref x, ref y, ref z, v1);
                        axis2.SetCorrespondingValue(ref x, ref y, ref z, v2);
                        axis3.SetCorrespondingValue(ref x, ref y, ref z, v3);

                        yield return new Point3D(x, y, z);
                    }
                }
            }
        }
    }

    #endregion
}
