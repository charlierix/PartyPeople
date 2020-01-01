using System;
using System.Collections.Generic;
using System.Text;

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

        public int Length
        {
            get
            {
                return Math.Abs(this.Stop - this.Start) + 1;
            }
        }

        /// <summary>
        /// This will set one of the output x,y,z to index3D based on this.Axis
        /// </summary>
        public void Set3DIndex(ref int x, ref int y, ref int z, int index3D)
        {
            switch (this.Axis)
            {
                case Axis.X:
                    x = index3D;
                    break;

                case Axis.Y:
                    y = index3D;
                    break;

                case Axis.Z:
                    z = index3D;
                    break;

                default:
                    throw new ApplicationException("Unknown Axis: " + this.Axis.ToString());
            }
        }
        public void Set2DIndex(ref int x, ref int y, int index2D)
        {
            switch (this.Axis)
            {
                case Axis.X:
                    x = index2D;
                    break;

                case Axis.Y:
                    y = index2D;
                    break;

                case Axis.Z:
                    throw new ApplicationException("Didn't expect Z axis");

                default:
                    throw new ApplicationException("Unknown Axis: " + this.Axis.ToString());
            }
        }
        public int GetValueForOffset(int value2D)
        {
            if (this.IsPos)
            {
                return value2D;
            }
            else
            {
                return this.Start - value2D;        // using start, because it's negative, so start is the larger value
            }
        }

        public IEnumerable<int> Iterate()
        {
            for (int cntr = Start; IsPos ? cntr <= Stop : cntr >= Stop; cntr += Increment)
            {
                yield return cntr;
            }
        }

        public bool IsBetween(int test)
        {
            if (this.IsPos)
            {
                return test >= this.Start && test <= this.Stop;
            }
            else
            {
                return test >= this.Stop && test <= this.Start;
            }
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
                throw new ArgumentException("steps must be positive: " + steps.ToString());
            }
            else if (Math1D.IsNearValue(start, stop))
            {
                throw new ArgumentException("start and stop can't be the same value: " + start.ToString());
            }

            this.Axis = axis;
            this.Start = start;
            this.Stop = stop;

            this.IsPos = this.Stop > this.Start;
            this.Increment = (stop - start) / steps;
        }
        /// <summary>
        /// This overload sets up the struct to only have one value.  When you call Iterate(), it returns that one value, then stops
        /// </summary>
        public AxisForDouble(Axis axis, double value)
        {
            this.Axis = axis;
            this.Start = value;
            this.Stop = value;

            this.IsPos = true;
            this.Increment = 100;       // this way iterate will only return one value
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
            switch (this.Axis)
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
                    throw new ApplicationException("Unknown Axis: " + this.Axis.ToString());
            }
        }

        public IEnumerable<double> Iterate()
        {
            double retVal = this.Start;

            while ((this.IsPos ? retVal < this.Stop : retVal > this.Stop) || Math1D.IsNearValue(retVal, this.Stop))
            {
                yield return retVal;
                retVal += this.Increment;
            }
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
            this.X = x;
            this.Y = y;
            this.Offset1D = offset1D;
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
            this.X = x;
            this.Y = y;
            this.Z = z;
            this.Offset1D = offset1D;
        }

        public readonly int X;
        public readonly int Y;
        public readonly int Z;
        public readonly int Offset1D;
    }

    #endregion
}
