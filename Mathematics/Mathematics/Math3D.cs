using Game.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.Math_WPF.Mathematics
{
    public static partial class Math3D
    {
        #region enum: RayCastReturn

        public enum RayCastReturn
        {
            AllPoints,
            ClosestToRayOrigin,
            ClosestToRay,
            AlongRayDirection,
        }

        #endregion
        #region enum: LocationOnLineSegment

        public enum LocationOnLineSegment
        {
            Start,
            Middle,
            Stop,
        }

        #endregion

        #region Declaration Section

        public const double NEARZERO = UtilityMath.NEARZERO;

        #endregion

        #region Private Methods

        /// <summary>
        /// This returns a phi from 0 to pi based on an input from -1 to 1
        /// </summary>
        /// <remarks>
        /// NOTE: The input is linear (even chance of any value from -1 to 1), but the output is scaled to give an even chance of a Z
        /// on a sphere:
        /// 
        /// z is cos of phi, which isn't linear.  So the probability is higher that more will be at the poles.  Which means if I want
        /// a linear probability of z, I need to feed the cosine something that will flatten it into a line.  The curve that will do that
        /// is arccos (which basically rotates the cosine wave 90 degrees).  This means that it is undefined for any x outside the range
        /// of -1 to 1.  So I have to shift the random statement to go between -1 to 1, run it through the curve, then shift the result
        /// to go between 0 and pi
        /// </remarks>
        private static double GetPhiForRandom(double num_negone_posone)
        {
            //double phi = rand.NextDouble(-1, 1);		// value from -1 to 1
            //phi = -Math.Asin(phi) / (Math.PI * .5d);		// another value from -1 to 1
            //phi = (1d + phi) * Math.PI * .5d;		// from 0 to pi

            return Math.PI / 2d - Math.Asin(num_negone_posone);
        }
        /// <summary>
        /// This is a complimentary function to GetPhiForRandom.  It's used to figure out the range for random to get a desired phi
        /// </summary>
        private static double GetRandomForPhi(double expectedRadians)
        {
            return -Math.Sin(expectedRadians - (Math.PI / 2));
        }

        /// <summary>
        /// This returns a number between LeftOuter--LeftInner or RightInner--RightOuter
        /// </summary>
        private static double GetRandomValue_Hole(Random rand, double outerLeft, double holeLeft, double holeRight, double outerRight)
        {
            double leftLength = holeLeft - outerLeft;
            double rightLength = outerRight - holeRight;

            if (leftLength.IsNearZero() && rightLength.IsNearZero())
            {
                return rand.NextBool() ? holeLeft : holeRight;
            }
            else if (leftLength < 0 && rightLength < 0)
            {
                throw new ArgumentException("The hole is bigger than the outer");
            }
            else if (leftLength <= 0)
            {
                return rand.NextDouble(holeRight, outerRight);
            }
            else if (rightLength <= 0)
            {
                return rand.NextDouble(outerLeft, holeLeft);
            }

            double randValue = rand.NextDouble(leftLength + rightLength);

            if (randValue < leftLength)
            {
                return outerLeft + randValue;
            }
            else
            {
                return holeRight + (randValue - leftLength);
            }
        }

        //****************************************************************************
        //
        //  Purpose:
        //
        //    TETRAHEDRON_CIRCUMSPHERE computes the circumsphere of a tetrahedron.
        //
        //  Discussion:
        //
        //    The circumsphere, or circumscribed sphere, of a tetrahedron is the 
        //    sphere that passes through the four vertices.  The circumsphere is not
        //    necessarily the smallest sphere that contains the tetrahedron.
        //
        //    Surprisingly, the diameter of the sphere can be found by solving
        //    a 3 by 3 linear system.  This is because the vectors P2 - P1,
        //    P3 - P1 and P4 - P1 are secants of the sphere, and each forms a
        //    right triangle with the diameter through P1.  Hence, the dot product of
        //    P2 - P1 with that diameter is equal to the square of the length
        //    of P2 - P1, and similarly for P3 - P1 and P4 - P1.  This determines
        //    the diameter vector originating at P1, and hence the radius and
        //    center.
        //
        //  Licensing:
        //
        //    This code is distributed under the GNU LGPL license. 
        //
        //  Modified:
        //
        //    10 August 2005
        //
        //  Author:
        //
        //    John Burkardt
        //    http://people.sc.fsu.edu/~jburkardt/cpp_src/tetrahedron_properties/tetrahedron_properties.html
        //
        //  Reference:
        //
        //    Adrian Bowyer, John Woodwark,
        //    A Programmer's Geometry,
        //    Butterworths, 1983.
        //
        //  Parameters:
        //
        //    Input, double TETRA[3*4], the vertices of the tetrahedron.
        //
        //    Output, double &R, PC[3], the coordinates of the center of the
        //    circumscribed sphere, and its radius.  If the linear system is
        //    singular, then R = -1, PC[] = 0.
        //void tetrahedron_circumsphere ( double tetra[3*4], double &r, double pc[3] )
        private static void tetrahedron_circumsphere(double[] tetra, out double r, out double[] pc)
        {
            pc = new double[3];

            double[] a = new double[3 * 4]; //double a[3*4];
            int info;
            //
            //  Set up the linear system.
            //
            a[0 + 0 * 3] = tetra[0 + 1 * 3] - tetra[0 + 0 * 3];
            a[0 + 1 * 3] = tetra[1 + 1 * 3] - tetra[1 + 0 * 3];
            a[0 + 2 * 3] = tetra[2 + 1 * 3] - tetra[2 + 0 * 3];
            a[0 + 3 * 3] = Math.Pow(tetra[0 + 1 * 3] - tetra[0 + 0 * 3], 2)
                     + Math.Pow(tetra[1 + 1 * 3] - tetra[1 + 0 * 3], 2)
                     + Math.Pow(tetra[2 + 1 * 3] - tetra[2 + 0 * 3], 2);

            a[1 + 0 * 3] = tetra[0 + 2 * 3] - tetra[0 + 0 * 3];
            a[1 + 1 * 3] = tetra[1 + 2 * 3] - tetra[1 + 0 * 3];
            a[1 + 2 * 3] = tetra[2 + 2 * 3] - tetra[2 + 0 * 3];
            a[1 + 3 * 3] = Math.Pow(tetra[0 + 2 * 3] - tetra[0 + 0 * 3], 2)
                     + Math.Pow(tetra[1 + 2 * 3] - tetra[1 + 0 * 3], 2)
                     + Math.Pow(tetra[2 + 2 * 3] - tetra[2 + 0 * 3], 2);

            a[2 + 0 * 3] = tetra[0 + 3 * 3] - tetra[0 + 0 * 3];
            a[2 + 1 * 3] = tetra[1 + 3 * 3] - tetra[1 + 0 * 3];
            a[2 + 2 * 3] = tetra[2 + 3 * 3] - tetra[2 + 0 * 3];
            a[2 + 3 * 3] = Math.Pow(tetra[0 + 3 * 3] - tetra[0 + 0 * 3], 2)
                     + Math.Pow(tetra[1 + 3 * 3] - tetra[1 + 0 * 3], 2)
                     + Math.Pow(tetra[2 + 3 * 3] - tetra[2 + 0 * 3], 2);
            //
            //  Solve the linear system.
            //
            info = r8mat_solve(3, 1, a);
            //
            //  If the system was singular, return a consolation prize.
            //
            if (info != 0)
            {
                r = -1.0;
                r8vec_zero(3, pc);
                return;
            }
            //
            //  Compute the radius and center.
            //
            r = 0.5 * Math.Sqrt
              (a[0 + 3 * 3] * a[0 + 3 * 3]
              + a[1 + 3 * 3] * a[1 + 3 * 3]
              + a[2 + 3 * 3] * a[2 + 3 * 3]);

            pc[0] = tetra[0 + 0 * 3] + 0.5 * a[0 + 3 * 3];
            pc[1] = tetra[1 + 0 * 3] + 0.5 * a[1 + 3 * 3];
            pc[2] = tetra[2 + 0 * 3] + 0.5 * a[2 + 3 * 3];

            return;
        }
        //****************************************************************************
        //
        //  Purpose:
        //
        //    R8MAT_SOLVE uses Gauss-Jordan elimination to solve an N by N linear system.
        //
        //  Discussion: 							    
        //
        //    An R8MAT is a doubly dimensioned array of R8 values,  stored as a vector 
        //    in column-major order.
        //
        //    Entry A(I,J) is stored as A[I+J*N]
        //
        //  Licensing:
        //
        //    This code is distributed under the GNU LGPL license. 
        //
        //  Modified:
        //
        //    29 August 2003
        //
        //  Author:
        //
        //    John Burkardt
        //    http://people.sc.fsu.edu/~jburkardt/cpp_src/tetrahedron_properties/tetrahedron_properties.html
        //
        //  Parameters:
        //
        //    Input, int N, the order of the matrix.
        //
        //    Input, int RHS_NUM, the number of right hand sides.  RHS_NUM
        //    must be at least 0.
        //
        //    Input/output, double A[N*(N+RHS_NUM)], contains in rows and columns 1
        //    to N the coefficient matrix, and in columns N+1 through
        //    N+RHS_NUM, the right hand sides.  On output, the coefficient matrix
        //    area has been destroyed, while the right hand sides have
        //    been overwritten with the corresponding solutions.
        //
        //    Output, int R8MAT_SOLVE, singularity flag.
        //    0, the matrix was not singular, the solutions were computed;
        //    J, factorization failed on step J, and the solutions could not
        //    be computed.
        private static int r8mat_solve(int n, int rhs_num, double[] a)
        {
            double apivot;
            double factor;
            int i;
            int ipivot;
            int j;
            int k;
            double temp;

            for (j = 0; j < n; j++)
            {
                //
                //  Choose a pivot row.
                //
                ipivot = j;
                apivot = a[j + j * n];

                for (i = j; i < n; i++)
                {
                    if (Math.Abs(apivot) < Math.Abs(a[i + j * n]))
                    {
                        apivot = a[i + j * n];
                        ipivot = i;
                    }
                }

                if (apivot == 0.0)
                {
                    return j;
                }
                //
                //  Interchange.
                //
                for (i = 0; i < n + rhs_num; i++)
                {
                    temp = a[ipivot + i * n];
                    a[ipivot + i * n] = a[j + i * n];
                    a[j + i * n] = temp;
                }
                //
                //  A(J,J) becomes 1.
                //
                a[j + j * n] = 1.0;
                for (k = j; k < n + rhs_num; k++)
                {
                    a[j + k * n] = a[j + k * n] / apivot;
                }
                //
                //  A(I,J) becomes 0.
                //
                for (i = 0; i < n; i++)
                {
                    if (i != j)
                    {
                        factor = a[i + j * n];
                        a[i + j * n] = 0.0;
                        for (k = j; k < n + rhs_num; k++)
                        {
                            a[i + k * n] = a[i + k * n] - factor * a[j + k * n];
                        }
                    }
                }
            }

            return 0;
        }
        //****************************************************************************
        //
        //  Purpose:
        //
        //    R8VEC_ZERO zeroes an R8VEC.
        //
        //  Discussion:
        //
        //    An R8VEC is a vector of R8's.
        //
        //  Licensing:
        //
        //    This code is distributed under the GNU LGPL license. 
        //
        //  Modified:
        //
        //    03 July 2005
        //
        //  Author:
        //
        //    John Burkardt
        //    http://people.sc.fsu.edu/~jburkardt/cpp_src/tetrahedron_properties/tetrahedron_properties.html
        //
        //  Parameters:
        //
        //    Input, int N, the number of entries in the vector.
        //
        //    Output, double A[N], a vector of zeroes.
        //
        private static void r8vec_zero(int n, double[] a)
        {
            int i;

            for (i = 0; i < n; i++)
            {
                a[i] = 0.0;
            }
            return;
        }

        #endregion
    }
}
