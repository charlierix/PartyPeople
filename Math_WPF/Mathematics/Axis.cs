using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.Mathematics
{
    #region enum: Axis

    public enum Axis
    {
        X,
        Y,
        Z,
        //W,
        //d5,
        //d6,
        //d7,
        //etc (or some other common way of identifying higher dimensions)
    }

    #endregion

    #region struct: AxisFor

    /// <summary>
    /// This helps with running for loops against an axis
    /// </summary>
    public partial struct AxisFor
    {
        public AxisFor(Axis axis, int start, int stop)
        {
            this.Axis = axis;
            this.Start = start;
            this.Stop = stop;

            this.IsPos = this.Stop > this.Start;
            this.Increment = this.IsPos ? 1 : -1;
        }

        public readonly Axis Axis;
        public readonly int Start;
        public readonly int Stop;
        public readonly int Increment;
        public readonly bool IsPos;

        public int Length => Math.Abs(Stop - Start) + 1;

        /// <summary>
        /// This will set one of the output x,y,z to value3D based on this.Axis
        /// </summary>
        public void Set3DValue<T>(ref T x, ref T y, ref T z, T value3D)
        {
            switch (Axis)
            {
                case Axis.X:
                    x = value3D;
                    break;

                case Axis.Y:
                    y = value3D;
                    break;

                case Axis.Z:
                    z = value3D;
                    break;

                default:
                    throw new ApplicationException($"Unknown Axis: {Axis}");
            }
        }
        public void Set2DValue<T>(ref T x, ref T y, T value2D)
        {
            switch (Axis)
            {
                case Axis.X:
                    x = value2D;
                    break;

                case Axis.Y:
                    y = value2D;
                    break;

                case Axis.Z:
                    throw new ApplicationException("Didn't expect Z axis");

                default:
                    throw new ApplicationException($"Unknown Axis: {Axis}");
            }
        }

        public int GetValueForOffset(int value2D)
        {
            if (IsPos)
                return value2D;

            else
                return Start - value2D;        // using start, because it's negative, so start is the larger value
        }

        //NOTE: there are wpf specific overloads in the other file
        public int GetValue(VectorInt3 vector)
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

        public IEnumerable<int> Iterate()
        {
            for (int i = Start; IsPos ? i <= Stop : i >= Stop; i += Increment)
            {
                yield return i;
            }
        }

        public bool IsBetween(int test)
        {
            if (IsPos)
                return test >= Start && test <= Stop;

            else
                return test >= Stop && test <= Start;
        }

        public override string ToString()
        {
            string by = Math.Abs(Increment) == 1 ?
                "" :
                $" by {Increment}";

            return string.Format("{0}: {1} to {2}{3}", Axis, Start, Stop, by);
        }
    }

    #endregion
    #region struct: AxisForDouble

    /// <summary>
    /// This helps with running for loops against an axis
    /// </summary>
    public struct AxisForDouble
    {
        /// <summary>
        /// This overload will walk from start to stop, across steps+1 number of times (
        /// </summary>
        /// <remarks>
        /// Iterate() will return start up to and including stop
        /// </remarks>
        public AxisForDouble(Axis axis, double start, double stop, int steps)
        {
            if (steps <= 0)
            {
                throw new ArgumentException($"steps must be positive: {steps}");
            }
            else if (Math1D.IsNearValue(start, stop))
            {
                throw new ArgumentException($"start and stop can't be the same value: {start}");
            }

            Axis = axis;
            Start = start;
            Stop = stop;

            IsPos = Stop > Start;
            Increment = (stop - start) / steps;
        }
        /// <summary>
        /// This overload sets up the struct to only have one value.  When you call Iterate(), it returns that one value, then stops
        /// </summary>
        public AxisForDouble(Axis axis, double value)
        {
            Axis = axis;
            Start = value;
            Stop = value;

            IsPos = true;
            Increment = 100;       // this way iterate will only return one value
        }

        public readonly Axis Axis;
        public readonly double Start;
        public readonly double Stop;
        public readonly double Increment;
        public readonly bool IsPos;

        /// <summary>
        /// This will set one of the output x,y,z to value2D based on this.Axis
        /// </summary>
        public void SetCorrespondingValue(ref double x, ref double y, ref double z, double value)
        {
            switch (Axis)
            {
                case Axis.X:
                    x = value;
                    break;

                case Axis.Y:
                    y = value;
                    break;

                case Axis.Z:
                    z = value;
                    break;

                default:
                    throw new ApplicationException($"Unknown Axis: {Axis}");
            }
        }

        public IEnumerable<double> Iterate()
        {
            double retVal = Start;

            while ((IsPos ? retVal < Stop : retVal > Stop) || Math1D.IsNearValue(retVal, Stop))
            {
                yield return retVal;
                retVal += Increment;
            }
        }

        public override string ToString()
        {
            string by = Math.Abs(Increment).IsNearValue(1) ?
                "" :
                $" by {Increment.ToStringSignificantDigits(2)}";

            return string.Format("{0}: {1} to {2}{3}", Axis, Start.ToStringSignificantDigits(2), Stop.ToStringSignificantDigits(2), by);
        }
    }

    #endregion

    #region struct: Mapping_2D_1D

    /// <summary>
    /// This is a mapping between 2D and 1D (good for bitmaps, or other rectangle grids that are physically stored as 1D arrays)
    /// </summary>
    public struct Mapping_2D_1D
    {
        public Mapping_2D_1D(int x, int y, int offset1D)
        {
            X = x;
            Y = y;
            Offset1D = offset1D;
        }

        public readonly int X;
        public readonly int Y;
        public readonly int Offset1D;
    }

    #endregion
    #region struct: Mapping_3D_1D

    /// <summary>
    /// This is a mapping between 3D and 1D
    /// </summary>
    public struct Mapping_3D_1D
    {
        public Mapping_3D_1D(int x, int y, int z, int offset1D)
        {
            X = x;
            Y = y;
            Z = z;
            Offset1D = offset1D;
        }

        public readonly int X;
        public readonly int Y;
        public readonly int Z;
        public readonly int Offset1D;
    }

    #endregion
}
