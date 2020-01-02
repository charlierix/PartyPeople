using Game.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.Mathematics
{
    public static partial class Polytopes
    {
        /// <summary>
        /// Creates a regular tetrahedron
        /// </summary>
        public static Tetrahedron_wpf GetTetrahedron(double radius, Transform3D transform = null)
        {
            double one = radius;
            double oneThird = (radius / 3d);
            double sqrt23 = Math.Sqrt(2d / 3d) * radius;
            double sqrt29 = Math.Sqrt(2d / 9d) * radius;
            double sqrt89 = Math.Sqrt(8d / 9d) * radius;

            // Points
            Point3D[] points = new Point3D[]
            {
                new Point3D(sqrt89, 0, -oneThird),        // 0
                new Point3D(-sqrt29, sqrt23, -oneThird),        // 1
                new Point3D(-sqrt29, -sqrt23, -oneThird),        // 2
                new Point3D(0, 0, one),        // 3
            };

            if (transform != null)
            {
                points = points.
                    Select(o => transform.Transform(o)).
                    ToArray();
            }

            return new Tetrahedron_wpf(0, 1, 2, 3, points);
        }

        public static TriangleIndexed_wpf[] GetIcosahedron(double radius, int numRecursions = 0)
        {
            TriangleIndexed_wpf[] retVal = GetIcosahedron_Initial(radius);

            for (int cntr = 0; cntr < numRecursions; cntr++)
            {
                retVal = GetIcosahedron_Recurse(retVal, radius);
                //retVal = GetIcosahedron_Recurse(retVal, radius / Convert.ToDouble(cntr + 2));     // makes spikes
                //retVal = GetIcosahedron_Recurse(retVal, radius * Convert.ToDouble(cntr + 2));     // makes in facing spikes
            }

            return retVal;
        }
        /// <summary>
        /// This overload lets you choose a different radius for each level
        /// </summary>
        /// <remarks>
        /// It's easy to get pretty silly results.  The best result is when radius[0] is some value, and all the rest are the some smaller value.  That
        /// will create spikes
        /// </remarks>
        public static TriangleIndexed_wpf[] GetIcosahedron(double[] radius)
        {
            TriangleIndexed_wpf[] retVal = GetIcosahedron_Initial(radius[0]);

            for (int cntr = 1; cntr < radius.Length; cntr++)
            {
                retVal = GetIcosahedron_Recurse(retVal, radius[cntr]);
            }

            return retVal;
        }

        public static Rhombicuboctahedron_wpf GetRhombicuboctahedron(double sizeX, double sizeY, double sizeZ, Transform3D transform = null)
        {
            #region Points

            double hX = sizeX / 2d;
            double hY = sizeY / 2d;
            double hZ = sizeZ / 2d;

            double sqrt2_1 = Math.Sqrt(2) + 1d;
            double sX = (sizeX / sqrt2_1) / 2d;     // sX is half the width of one of the faces (the faces form an octogon)
            double sY = (sizeY / sqrt2_1) / 2d;
            double sZ = (sizeZ / sqrt2_1) / 2d;

            // Points
            Point3D[] points = new Point3D[]
            {
                // Top 4
                new Point3D(sX, sY, hZ),        // 0
                new Point3D(sX, -sY, hZ),       // 1
                new Point3D(-sX, -sY, hZ),      // 2
                new Point3D(-sX, sY, hZ),      // 3

                // Top 8
                new Point3D(hX, sY, sZ),        // 4
                new Point3D(hX, -sY, sZ),       // 5
                new Point3D(sX, -hY, sZ),       // 6
                new Point3D(-sX, -hY, sZ),      // 7
                new Point3D(-hX, -sY, sZ),      // 8
                new Point3D(-hX, sY, sZ),       // 9
                new Point3D(-sX, hY, sZ),       // 10
                new Point3D(sX, hY, sZ),        // 11

                // Bottom 8
                new Point3D(hX, sY, -sZ),       // 12
                new Point3D(hX, -sY, -sZ),      // 13
                new Point3D(sX, -hY, -sZ),      // 14
                new Point3D(-sX, -hY, -sZ),     // 15
                new Point3D(-hX, -sY, -sZ),     // 16
                new Point3D(-hX, sY, -sZ),      // 17
                new Point3D(-sX, hY, -sZ),      // 18
                new Point3D(sX, hY, -sZ),       // 19

                // Bottom 4
                new Point3D(sX, sY, -hZ),       // 20
                new Point3D(sX, -sY, -hZ),      // 21
                new Point3D(-sX, -sY, -hZ),     // 22
                new Point3D(-sX, sY, -hZ),     // 23
            };

            if (transform != null)
            {
                points = points.Select(o => transform.Transform(o)).ToArray();
            }

            #endregion

            int[][] squarePolys_Orth = new int[][]
            {
                new int[] { 0, 3, 2, 1 },       // Top
                new int[] { 4, 5, 13, 12 },     // Right
                new int[] { 6, 7, 15, 14 },     // Front
                new int[] { 8, 9, 17, 16 },     // Left
                new int[] { 10, 11, 19, 18 },       // Back
                new int[] { 20, 21, 22, 23 },       // Bottom
            };

            int[][] squarePolys_Diag = new int[][]
            {
                // Top 4 angled
                new int[] {0, 1, 5, 4 },
                new int[] { 1, 2, 7, 6 },
                new int[] { 2, 3, 9, 8 },
                new int[] { 0, 11, 10, 3 },

                // Middle 4 angled
                new int[] { 4, 12, 19, 11 },
                new int[] { 5, 6, 14, 13 },
                new int[] { 7, 8, 16, 15 },
                new int[] { 9, 10, 18, 17 },

                // Bottom 4 angled
                new int[] { 12, 13, 21, 20 },
                new int[] { 14, 15, 22, 21 },
                new int[] { 16, 17, 23, 22 },
                new int[] { 18, 19, 20, 23 },
            };

            TriangleIndexed_wpf[] triangles = new[]
            {
                // Top 4
                new TriangleIndexed_wpf(0, 4, 11, points),
                new TriangleIndexed_wpf(1, 6, 5, points),
                new TriangleIndexed_wpf(2, 8, 7, points),
                new TriangleIndexed_wpf(3, 10, 9, points),

                // Bottom 4
                new TriangleIndexed_wpf(12, 20, 19, points),
                new TriangleIndexed_wpf(13, 14, 21, points),
                new TriangleIndexed_wpf(15, 16, 22, points),
                new TriangleIndexed_wpf(17, 18, 23, points),
            };

            return new Rhombicuboctahedron_wpf(squarePolys_Orth, squarePolys_Diag, triangles, points);
        }

        public static Icosidodecahedron_wpf GetIcosidodecahedron(double radius, Transform3D transform = null)
        {
            //NOTE: Don't confuse this with an icosahedron.  That is the more common object (made of equilateral triangles).
            //This object is made of pentagons and triangles.  I'm just making this because it looks cool

            #region Points

            double t = (1d + Math.Sqrt(5d)) / 2d;
            double t2 = t / 2d;
            double t3 = (1d + t) / 2d;

            Point3D[] points = new Point3D[]
            {
                //(0,0,±φ)
                new Point3D(0, 0, t),       // 0
                new Point3D(0, 0, -t),      // 1

                //(0,±φ,0)
                new Point3D(0, t, 0),       // 2
                new Point3D(0, -t, 0),      // 3

                //(±φ,0,0)
                new Point3D(t, 0, 0),       // 4
                new Point3D(-t, 0, 0),      // 5

                //(±1/2, ±φ/2, ±(1+φ)/2)
                new Point3D(.5, t2, t3),        // 6
                new Point3D(.5, t2, -t3),       // 7
                new Point3D(.5, -t2, t3),       // 8
                new Point3D(.5, -t2, -t3),      // 9
                new Point3D(-.5, t2, t3),       // 10
                new Point3D(-.5, t2, -t3),      // 11
                new Point3D(-.5, -t2, t3),      // 12
                new Point3D(-.5, -t2, -t3),     // 13

                //(±φ/2, ±(1+φ)/2, ±1/2)
                new Point3D(t2, t3, .5),        // 14
                new Point3D(t2, t3, -.5),       // 15
                new Point3D(t2, -t3, .5),       // 16
                new Point3D(t2, -t3, -.5),      // 17
                new Point3D(-t2, t3, .5),       // 18
                new Point3D(-t2, t3, -.5),      // 19
                new Point3D(-t2, -t3, .5),      // 20
                new Point3D(-t2, -t3, -.5),     // 21

                //(±(1+φ)/2, ±1/2, ±φ/2)
                new Point3D(t3, .5, t2),        // 22
                new Point3D(t3, .5, -t2),       // 23
                new Point3D(t3, -.5, t2),       // 24
                new Point3D(t3, -.5, -t2),      // 25
                new Point3D(-t3, .5, t2),       // 26
                new Point3D(-t3, .5, -t2),      // 27
                new Point3D(-t3, -.5, t2),      // 28
                new Point3D(-t3, -.5, -t2),     // 29
            };

            double maxLength = points[6].ToVector().Length;     // this represents the longest vector
            double ratio = radius / maxLength;
            points = points.Select(o => (o.ToVector() * ratio).ToPoint()).ToArray();

            if (transform != null)
            {
                points = points.Select(o => transform.Transform(o)).ToArray();
            }

            #endregion

            int[][] pentagonPolys = new int[][]
            {
                new int [] { 0, 10, 26, 28, 12 },
                new int [] { 26, 18, 19, 27, 5 },
                new int [] { 5, 29, 21, 20, 28 },
                new int [] { 10, 6, 14, 2, 18 },
                new int [] { 3, 16, 8, 12, 20 },
                new int [] { 0, 8, 24, 22, 6 },
                new int [] { 9, 17, 3, 21, 13 },
                new int [] { 27, 11, 1, 13, 29 },
                new int [] { 4, 24, 16, 17, 25 },
                new int [] { 1, 7, 23, 25, 9 },
                new int [] { 4, 23, 15, 14, 22 },
                new int [] { 2, 15, 7, 11, 19 },
            };

            TriangleIndexed_wpf[] triangles = new[]
            {
                new TriangleIndexed_wpf(0, 12, 8, points),
                new TriangleIndexed_wpf(0, 6, 10, points),
                new TriangleIndexed_wpf(10, 18, 26, points),
                new TriangleIndexed_wpf(5, 28, 26, points),
                new TriangleIndexed_wpf(12, 28, 20, points),
                new TriangleIndexed_wpf(3, 20, 21, points),
                new TriangleIndexed_wpf(8, 16, 24, points),
                new TriangleIndexed_wpf(3, 17, 16, points),
                new TriangleIndexed_wpf(9, 25, 17, points),
                new TriangleIndexed_wpf(4, 25, 23, points),
                new TriangleIndexed_wpf(4, 22, 24, points),
                new TriangleIndexed_wpf(13, 21, 29, points),
                new TriangleIndexed_wpf(1, 9, 13, points),
                new TriangleIndexed_wpf(1, 11, 7, points),
                new TriangleIndexed_wpf(11, 27, 19, points),
                new TriangleIndexed_wpf(5, 27, 29, points),
                new TriangleIndexed_wpf(6, 22, 14, points),
                new TriangleIndexed_wpf(2, 14, 15, points),
                new TriangleIndexed_wpf(2, 19, 18, points),
                new TriangleIndexed_wpf(7, 15, 23, points),
            };

            return new Icosidodecahedron_wpf(pentagonPolys, triangles, points);
        }

        public static Dodecahedron_wpf GetDodecahedron(double radius, Transform3D transform = null)
        {
            // This is 12 pentagons

            #region Points

            double t = (1d + Math.Sqrt(5d)) / 2d;
            double t1 = 1d / t;

            Point3D[] points = new Point3D[]
            {
                //(±1, ±1, ±1)
                new Point3D(1, 1, 1),       // 0
                new Point3D(1, 1, -1),      // 1
                new Point3D(1, -1, 1),      // 2
                new Point3D(1, -1, -1),     // 3
                new Point3D(-1, 1, 1),      // 4
                new Point3D(-1, 1, -1),     // 5
                new Point3D(-1, -1, 1),     // 6
                new Point3D(-1, -1, -1),        // 7

                //(0, ±1/φ, ±φ)
                new Point3D(0, t1, t),      // 8
                new Point3D(0, t1, -t),     // 9
                new Point3D(0, -t1, t),     // 10
                new Point3D(0, -t1, -t),        // 11

                //(±1/φ, ±φ, 0)
                new Point3D(t1, t, 0),      // 12
                new Point3D(t1, -t, 0),     // 13
                new Point3D(-t1, t, 0),     // 14
                new Point3D(-t1, -t, 0),        // 15

                //(±φ, 0, ±1/φ)
                new Point3D(t, 0, t1),      // 16
                new Point3D(t, 0, -t1),     // 17
                new Point3D(-t, 0, t1),     // 18
                new Point3D(-t, 0, -t1),        // 19
            };

            double maxLength = points[8].ToVector().Length;     // this represents the longest vector
            double ratio = radius / maxLength;
            points = points.Select(o => (o.ToVector() * ratio).ToPoint()).ToArray();

            if (transform != null)
            {
                points = points.Select(o => transform.Transform(o)).ToArray();
            }

            #endregion

            int[][] pentagonPolys = new int[][]
            {
                new int [] { 2, 10, 6, 15, 13 },
                new int [] { 0, 8, 10, 2, 16 },
                new int [] { 0, 12, 14, 4, 8 },
                new int [] { 1, 9, 5, 14, 12 },
                new int [] { 1, 17, 3, 11, 9 },
                new int [] { 2, 13, 3, 17, 16 },
                new int [] { 3, 13, 15, 7, 11 },
                new int [] { 6, 18, 19, 7, 15 },
                new int [] { 4, 18, 6, 10, 8 },
                new int [] { 4, 14, 5, 19, 18 },
                new int [] { 5, 9, 11, 7, 19 },
                new int [] { 0, 16, 17, 1, 12 },
            };

            return new Dodecahedron_wpf(pentagonPolys, points);
        }

        /// <summary>
        /// This can be thought of as a dodecahedron with each pentagon turned into a pyramid
        /// </summary>
        /// <param name="baseRadius">
        /// If this is populated, then this is the radius of the dodecahedron (radius will be the radius of the pyramid tips).
        /// If baseRadius is less than radius, then you will get spikes.  If it's greater, you will get divots
        /// </param>
        public static TriangleIndexed_wpf[] GetPentakisDodecahedron(double radius, double? baseRadius = null)
        {
            // Get a dodecahedron that will be the foundation of the return
            Dodecahedron_wpf dodec = Polytopes.GetDodecahedron(baseRadius ?? radius);

            int offset = dodec.AllPoints.Length;

            List<Point3D> newPoints = new List<Point3D>();
            List<Tuple<int, int, int>> newTriangles = new List<Tuple<int, int, int>>();

            // Go through each pentagon, and create a 5 triangle pyramid
            foreach (int[] pentagon in dodec.PentagonPolys)
            {
                int tipIndex = offset + newPoints.Count;
                newPoints.Add(PentakisDodecahedron_Pyramid(pentagon, dodec.AllPoints, radius));

                for (int inner = 0; inner < pentagon.Length - 1; inner++)
                {
                    newTriangles.Add(Tuple.Create(pentagon[inner], pentagon[inner + 1], tipIndex));
                }

                newTriangles.Add(Tuple.Create(pentagon[pentagon.Length - 1], pentagon[0], tipIndex));
            }

            // Build the final triangles
            Point3D[] allPoints = UtilityCore.ArrayAdd(dodec.AllPoints, newPoints.ToArray());

            return newTriangles.Select(o => new TriangleIndexed_wpf(o.Item1, o.Item2, o.Item3, allPoints)).ToArray();
        }

        public static TruncatedIcosahedron_wpf GetTruncatedIcosahedron(double radius, Transform3D transform = null)
        {
            #region Points

            double t = (1d + Math.Sqrt(5d)) / 2d;
            double t2 = t * 2d;
            double t3 = t * 3d;
            double tA = 1d + (2d * t);
            double tB = 2d + t;

            // Length compare:
            //  t3  4.8541019662496847
            //  tA  4.23606797749979
            //  tB  3.6180339887498949
            //  t2  3.23606797749979
            //  2   2
            //  t   1.6180339887498949
            //  1   1
            //  0   0

            Point3D[] points = new Point3D[]
            {
                //------------------------------------------------- X axis
                //(±3φ, 0, ±1)
                new Point3D(t3, 0, 1),      // 0
                new Point3D(-t3, 0, 1),
                new Point3D(t3, 0, -1),
                new Point3D(-t3, 0, -1),        // 3

                //(±(1+2φ), ±φ, ±2)
                new Point3D(tA, t, 2),      // 4
                new Point3D(tA, -t, 2),
                new Point3D(-tA, t, 2),
                new Point3D(-tA, -t, 2),
                new Point3D(tA, t, -2),
                new Point3D(tA, -t, -2),
                new Point3D(-tA, t, -2),
                new Point3D(-tA, -t, -2),       // 11

                //(±(2+φ), ±2φ, ±1)
                new Point3D(tB, t2, 1),     // 12
                new Point3D(tB, t2, -1),
                new Point3D(tB, -t2, 1),
                new Point3D(tB, -t2, -1),
                new Point3D(-tB, t2, 1),
                new Point3D(-tB, t2, -1),
                new Point3D(-tB, -t2, 1),
                new Point3D(-tB, -t2, -1),      // 19

                //------------------------------------------------- Y axis
                //(±1, ±3φ, 0)
                new Point3D(1, t3, 0),      // 20
                new Point3D(1, -t3, 0),
                new Point3D(-1, t3, 0),
                new Point3D(-1, -t3, 0),        // 23

                //(±2, ±(1+2φ), ±φ)
                new Point3D(2, tA, t),      // 24
                new Point3D(2, tA, -t),
                new Point3D(2, -tA, t),
                new Point3D(2, -tA, -t),
                new Point3D(-2, tA, t),
                new Point3D(-2, tA, -t),
                new Point3D(-2, -tA, t),
                new Point3D(-2, -tA, -t),       // 31

                //(±1, ±(2+φ), ±2φ)
                new Point3D(1, tB, t2),     // 32
                new Point3D(1, tB, -t2),
                new Point3D(1, -tB, t2),
                new Point3D(1, -tB, -t2),
                new Point3D(-1, tB, t2),
                new Point3D(-1, tB, -t2),
                new Point3D(-1, -tB, t2),
                new Point3D(-1, -tB, -t2),      // 39

                //------------------------------------------------- Z axis
                //(0, ±1, ±3φ)
                new Point3D(0, 1, t3),      // 40
                new Point3D(0, 1, -t3),
                new Point3D(0, -1, t3),
                new Point3D(0, -1, -t3),        // 43

                //(±φ, ±2, ±(1+2φ))
                new Point3D(t, 2, tA),      // 44
                new Point3D(-t, 2, tA),
                new Point3D(t, 2, -tA),
                new Point3D(-t, 2, -tA),
                new Point3D(t, -2, tA),
                new Point3D(-t, -2, tA),
                new Point3D(t, -2, -tA),
                new Point3D(-t, -2, -tA),       // 51

                //(±2φ, ±1, ±(2+φ))
                new Point3D(t2, 1, tB),     // 52
                new Point3D(t2, 1, -tB),
                new Point3D(t2, -1, tB),
                new Point3D(t2, -1, -tB),
                new Point3D(-t2, 1, tB),
                new Point3D(-t2, 1, -tB),
                new Point3D(-t2, -1, tB),
                new Point3D(-t2, -1, -tB),      // 59
            };

            double maxLength = points[0].ToVector().Length;
            double ratio = radius / maxLength;
            points = points.Select(o => (o.ToVector() * ratio).ToPoint()).ToArray();

            if (transform != null)
            {
                points = points.Select(o => transform.Transform(o)).ToArray();
            }

            #endregion

            // Pentagons
            int[][] pentagonPolys = new int[][]
            {
                new int [] { 40, 44, 32, 36, 45 },
                new int [] { 42, 49, 38, 34, 48 },
                new int [] { 41, 47, 37, 33, 46 },
                new int [] { 43, 50, 35, 39, 51 },
                new int [] { 1, 7, 58, 56, 6 },
                new int [] { 0, 4, 52, 54, 5 },
                new int [] { 3, 10, 57, 59, 11 },
                new int [] { 2, 9, 55, 53, 8 },
                new int [] { 18, 19, 31, 23, 30 },
                new int [] { 14, 26, 21, 27, 15 },
                new int [] { 12, 13, 25, 20, 24 },
                new int [] { 16, 28, 22, 29, 17 },
            };

            // Hexagons
            int[][] hexagonPolys = new int[][]
            {
                new int [] { 40, 45, 56, 58, 49, 42 },
                new int [] { 40, 42, 48, 54, 52, 44 },
                new int [] { 41, 43, 51, 59, 57, 47 },
                new int [] { 41, 46, 53, 55, 50, 43 },
                new int [] { 1, 6, 16, 17, 10, 3 },
                new int [] { 1, 3, 11, 19, 18, 7 },
                new int [] { 0, 2, 8, 13, 12, 4 },
                new int [] { 0, 5, 14, 15, 9, 2 },
                new int [] { 34, 26, 14, 5, 54, 48 },
                new int [] { 32, 44, 52, 4, 12, 24 },
                new int [] { 38, 49, 58, 7, 18, 30 },
                new int [] { 33, 25, 13, 8, 53, 46 },
                new int [] { 35, 50, 55, 9, 15, 27 },
                new int [] { 36, 28, 16, 6, 56, 45 },
                new int [] { 39, 31, 19, 11, 59, 51 },
                new int [] { 37, 47, 57, 10, 17, 29 },
                new int [] { 20, 25, 33, 37, 29, 22 },
                new int [] { 20, 22, 28, 36, 32, 24 },
                new int [] { 21, 26, 34, 38, 30, 23 },
                new int [] { 21, 23, 31, 39, 35, 27 },
            };

            return new TruncatedIcosahedron_wpf(pentagonPolys, hexagonPolys, points);
        }

        public static TruncatedIcosidodecahedron_wpf GetTruncatedIcosidodecahedron(double radius, Transform3D transform = null)
        {
            //TODO: Currently, the points are hardcoded.  All the polygons are regular.  Take in a ratio for the length of side of the decagons.
            //  0 would make the decagons disappar, and the squares and hexagons would be it
            //  1 would make the squares disappear and the hexagons would become triangles
            //
            //The squares are the cornerstones.  When calculating points, figure out what the centers of the various polygons are.  Then adjust the
            //sizes of the squares.  From that, find the rest of the points using the decagons.  No need to find points for the hexagons, they have
            //no unique points

            #region Points

            double t = (1d + Math.Sqrt(5d)) / 2d;       // φ
            double tS = t * t;      // φ^2
            double tI1 = 1d / t;        // 1/φ
            double tI2 = 2d / t;        // 2/φ
            double t2 = 2d * t;     // 2φ
            double tA = 1d + (2d * t);      // 1+2φ
            double tB = 2d + t;     // 2+φ
            double tC = 3d + t;     // 3+φ
            double tN1 = -1d + (3d * t);        // -1+3φ
            double tN2 = -1d + (2d * t);        // -1+2φ

            Point3D[] points = new Point3D[]
            {
                //(±1/φ, ±1/φ, ±(3+φ))
                new Point3D(tI1, tI1, tC),
                new Point3D(tI1, tI1, -tC),
                new Point3D(tI1, -tI1, tC),
                new Point3D(tI1, -tI1, -tC),
                new Point3D(-tI1, tI1, tC),
                new Point3D(-tI1, tI1, -tC),
                new Point3D(-tI1, -tI1, tC),
                new Point3D(-tI1, -tI1, -tC),

                //(±2/φ, ±φ, ±(1+2φ))
                new Point3D(tI2, t, tA),
                new Point3D(tI2, t, -tA),
                new Point3D(tI2, -t, tA),
                new Point3D(tI2, -t, -tA),
                new Point3D(-tI2, t, tA),
                new Point3D(-tI2, t, -tA),
                new Point3D(-tI2, -t, tA),
                new Point3D(-tI2, -t, -tA),

                //(±1/φ, ±φ^2, ±(-1+3φ))
                new Point3D(tI1, tS, tN1),
                new Point3D(tI1, tS, -tN1),
                new Point3D(tI1, -tS, tN1),
                new Point3D(tI1, -tS, -tN1),
                new Point3D(-tI1, tS, tN1),
                new Point3D(-tI1, tS, -tN1),
                new Point3D(-tI1, -tS, tN1),
                new Point3D(-tI1, -tS, -tN1),

                //(±(-1+2φ), ±2, ±(2+φ))
                new Point3D(tN2, 2, tB),
                new Point3D(tN2, 2, -tB),
                new Point3D(tN2, -2, tB),
                new Point3D(tN2, -2, -tB),
                new Point3D(-tN2, 2, tB),
                new Point3D(-tN2, 2, -tB),
                new Point3D(-tN2, -2, tB),
                new Point3D(-tN2, -2, -tB),

                //(±φ, ±3, ±2φ),
                new Point3D(t, 3, t2),
                new Point3D(t, 3, -t2),
                new Point3D(t, -3, t2),
                new Point3D(t, -3, -t2),
                new Point3D(-t, 3, t2),
                new Point3D(-t, 3, -t2),
                new Point3D(-t, -3, t2),
                new Point3D(-t, -3, -t2),
            };

            points = points.Select(o => new Point3D[]
            {
                o,      // orig
                new Point3D(o.Y, o.Z, o.X),      // shift left
                new Point3D(o.Z, o.X, o.Y)       // shift left twice
            }).
            SelectMany(o => o).
            ToArray();

            double maxLength = points[0].ToVector().Length;
            double ratio = radius / maxLength;
            points = points.Select(o => (o.ToVector() * ratio).ToPoint()).ToArray();

            if (transform != null)
            {
                points = points.Select(o => transform.Transform(o)).ToArray();
            }

            #endregion

            int[][] decagonPolys = new int[][]
            {
                new int [] { 0, 6, 30, 78, 110, 62, 50, 98, 72, 24 },
                new int [] { 14, 38, 86, 100, 52, 64, 112, 92, 44, 20 },
                new int [] { 3, 27, 75, 104, 56, 68, 116, 81, 33, 9 },
                new int [] { 15, 21, 45, 93, 119, 71, 59, 107, 87, 39 },
                new int [] { 2, 8, 32, 80, 109, 61, 49, 97, 74, 26 },
                new int [] { 1, 7, 31, 79, 108, 60, 48, 96, 73, 25 },
                new int [] { 12, 36, 84, 101, 53, 65, 113, 90, 42, 18 },
                new int [] { 4, 28, 76, 102, 54, 66, 114, 82, 34, 10 },
                new int [] { 16, 22, 46, 94, 117, 69, 57, 105, 88, 40 },
                new int [] { 17, 23, 47, 95, 118, 70, 58, 106, 89, 41 },
                new int [] { 5, 29, 77, 103, 55, 67, 115, 83, 35, 11 },
                new int [] { 13, 37, 85, 99, 51, 63, 111, 91, 43, 19 },
            };

            int[][] hexagonPolys = new int[][]
            {
                new int [] { 6, 18, 42, 66, 54, 30 },
                new int [] { 82, 114, 90, 113, 89, 106 },
                new int [] { 5, 17, 41, 65, 53, 29 },
                new int [] { 77, 101, 84, 108, 79, 103 },
                new int [] { 7, 19, 43, 67, 55, 31 },
                new int [] { 83, 115, 91, 111, 87, 107 },
                new int [] { 3, 15, 39, 63, 51, 27 },
                new int [] { 0, 24, 48, 60, 36, 12 },
                new int [] { 72, 98, 74, 97, 73, 96 },
                new int [] { 1, 25, 49, 61, 37, 13 },
                new int [] { 75, 99, 85, 109, 80, 104 },
                new int [] { 76, 100, 86, 110, 78, 102 },
                new int [] { 2, 26, 50, 62, 38, 14 },
                new int [] { 8, 20, 44, 68, 56, 32 },
                new int [] { 81, 116, 92, 112, 88, 105 },
                new int [] { 9, 33, 57, 69, 45, 21 },
                new int [] { 11, 35, 59, 71, 47, 23 },
                new int [] { 93, 117, 94, 118, 95, 119 },
                new int [] { 10, 34, 58, 70, 46, 22 },
                new int [] { 4, 16, 40, 64, 52, 28 },
            };

            int[][] squarePolys = new int[][]
            {
                new int [] { 0, 12, 18, 6 },
                new int [] { 36, 60, 108, 84 },
                new int [] { 31, 55, 103, 79 },
                new int [] { 43, 91, 115, 67 },
                new int [] { 39, 87, 111, 63 },
                new int [] { 3, 9, 21, 15 },
                new int [] { 24, 72, 96, 48 },
                new int [] { 25, 73, 97, 49 },
                new int [] { 1, 13, 19, 7 },
                new int [] { 37, 61, 109, 85 },
                new int [] { 27, 51, 99, 75 },
                new int [] { 33, 81, 105, 57 },
                new int [] { 42, 90, 114, 66 },
                new int [] { 41, 89, 113, 65 },
                new int [] { 5, 11, 23, 17 },
                new int [] { 35, 83, 107, 59 },
                new int [] { 45, 69, 117, 93 },
                new int [] { 29, 53, 101, 77 },
                new int [] { 30, 54, 102, 78 },
                new int [] { 28, 52, 100, 76 },
                new int [] { 38, 62, 110, 86 },
                new int [] { 26, 74, 98, 50 },
                new int [] { 2, 14, 20, 8 },
                new int [] { 32, 56, 104, 80 },
                new int [] { 34, 82, 106, 58 },
                new int [] { 46, 70, 118, 94 },
                new int [] { 47, 71, 119, 95 },
                new int [] { 4, 10, 22, 16 },
                new int [] { 40, 88, 112, 64 },
                new int [] { 44, 92, 116, 68 },
            };

            return new TruncatedIcosidodecahedron_wpf(decagonPolys, hexagonPolys, squarePolys, points);
        }

        #region Private Methods

        private static TriangleIndexed_wpf[] GetIcosahedron_Initial(double radius)
        {
            // Create 12 vertices of a icosahedron
            double t = (1d + Math.Sqrt(5d)) / 2d;

            Point3D[] points = new Point3D[12];

            points[0] = ((new Vector3D(-1, t, 0)).ToUnit() * radius).ToPoint();
            points[1] = ((new Vector3D(1, t, 0)).ToUnit() * radius).ToPoint();
            points[2] = ((new Vector3D(-1, -t, 0)).ToUnit() * radius).ToPoint();
            points[3] = ((new Vector3D(1, -t, 0)).ToUnit() * radius).ToPoint();

            points[4] = ((new Vector3D(0, -1, t)).ToUnit() * radius).ToPoint();
            points[5] = ((new Vector3D(0, 1, t)).ToUnit() * radius).ToPoint();
            points[6] = ((new Vector3D(0, -1, -t)).ToUnit() * radius).ToPoint();
            points[7] = ((new Vector3D(0, 1, -t)).ToUnit() * radius).ToPoint();

            points[8] = ((new Vector3D(t, 0, -1)).ToUnit() * radius).ToPoint();
            points[9] = ((new Vector3D(t, 0, 1)).ToUnit() * radius).ToPoint();
            points[10] = ((new Vector3D(-t, 0, -1)).ToUnit() * radius).ToPoint();
            points[11] = ((new Vector3D(-t, 0, 1)).ToUnit() * radius).ToPoint();


            // create 20 triangles of the icosahedron
            List<TriangleIndexed_wpf> retVal = new List<TriangleIndexed_wpf>();

            // 5 faces around point 0
            retVal.Add(new TriangleIndexed_wpf(0, 11, 5, points));
            retVal.Add(new TriangleIndexed_wpf(0, 5, 1, points));
            retVal.Add(new TriangleIndexed_wpf(0, 1, 7, points));
            retVal.Add(new TriangleIndexed_wpf(0, 7, 10, points));
            retVal.Add(new TriangleIndexed_wpf(0, 10, 11, points));

            // 5 adjacent faces
            retVal.Add(new TriangleIndexed_wpf(1, 5, 9, points));
            retVal.Add(new TriangleIndexed_wpf(5, 11, 4, points));
            retVal.Add(new TriangleIndexed_wpf(11, 10, 2, points));
            retVal.Add(new TriangleIndexed_wpf(10, 7, 6, points));
            retVal.Add(new TriangleIndexed_wpf(7, 1, 8, points));

            // 5 faces around point 3
            retVal.Add(new TriangleIndexed_wpf(3, 9, 4, points));
            retVal.Add(new TriangleIndexed_wpf(3, 4, 2, points));
            retVal.Add(new TriangleIndexed_wpf(3, 2, 6, points));
            retVal.Add(new TriangleIndexed_wpf(3, 6, 8, points));
            retVal.Add(new TriangleIndexed_wpf(3, 8, 9, points));

            // 5 adjacent faces
            retVal.Add(new TriangleIndexed_wpf(4, 9, 5, points));
            retVal.Add(new TriangleIndexed_wpf(2, 4, 11, points));
            retVal.Add(new TriangleIndexed_wpf(6, 2, 10, points));
            retVal.Add(new TriangleIndexed_wpf(8, 6, 7, points));
            retVal.Add(new TriangleIndexed_wpf(9, 8, 1, points));

            return retVal.ToArray();
        }
        private static TriangleIndexed_wpf[] GetIcosahedron_Recurse(TriangleIndexed_wpf[] hull, double radius)
        {
            Point3D[] parentPoints = hull[0].AllPoints;

            SortedList<Tuple<int, int>, Tuple<int, Point3D>> childPoints = new SortedList<Tuple<int, int>, Tuple<int, Point3D>>();
            List<Tuple<int, int, int>> childFaces = new List<Tuple<int, int, int>>();

            // Cut each parent triangle into 4
            foreach (TriangleIndexed_wpf face in hull)
            {
                // Get the middle of each edge (the point between the two verticies, then extended to touch the sphere)
                int m01 = GetIcosahedron_MidPoint(face.Index0, face.Index1, parentPoints, radius, childPoints);
                int m12 = GetIcosahedron_MidPoint(face.Index1, face.Index2, parentPoints, radius, childPoints);
                int m20 = GetIcosahedron_MidPoint(face.Index2, face.Index0, parentPoints, radius, childPoints);

                // Turn those into triangles
                childFaces.Add(Tuple.Create(face.Index0, m01, m20));
                childFaces.Add(Tuple.Create(face.Index1, m12, m01));
                childFaces.Add(Tuple.Create(face.Index2, m20, m12));
                childFaces.Add(Tuple.Create(m01, m12, m20));
            }

            // Combine the points
            Point3D[] childPointsFinal = childPoints.Values.OrderBy(o => o.Item1).Select(o => o.Item2).ToArray();
            Point3D[] allNewPoints = UtilityCore.ArrayAdd(parentPoints, childPointsFinal);

            // Build the triangles
            return childFaces.Select(o => new TriangleIndexed_wpf(o.Item1, o.Item2, o.Item3, allNewPoints)).ToArray();
        }
        private static int GetIcosahedron_MidPoint(int index0, int index1, Point3D[] origPoints, double radius, SortedList<Tuple<int, int>, Tuple<int, Point3D>> newPoints)
        {
            Tuple<int, int> key = index0 < index1 ? Tuple.Create(index0, index1) : Tuple.Create(index1, index0);

            Tuple<int, Point3D> point;
            if (!newPoints.TryGetValue(key, out point))
            {
                // Get the average point between the two vertices, then push out to the sphere
                Point3D midPoint = (((origPoints[index0].ToVector() + origPoints[index1].ToVector()) / 2d).ToUnit() * radius).ToPoint();

                // Store this
                //NOTE: Adding origPoints.Length, so that the index will be accurate when the two lists are added together
                point = Tuple.Create(origPoints.Length + newPoints.Count, midPoint);
                newPoints.Add(key, point);
            }

            return point.Item1;
        }

        private static Point3D PentakisDodecahedron_Pyramid(int[] poly, Point3D[] points, double radius)
        {
            // Convert the indices into points
            Point3D[] polyPoints = poly.Select(o => points[o]).ToArray();

            // Find the center of the pentagon
            Point3D centerPoint = Math3D.GetCenter(polyPoints);

            // Find the center point's distance from the origin
            double centerPointDist = centerPoint.ToVector().Length;

            if (Math1D.IsNearValue(centerPointDist, radius))
            {
                // The center point is already at the desired radius.  Nothing left to do
                return centerPoint;
            }

            Vector3D normal = Math2D.GetPolygonNormal(polyPoints, PolygonNormalLength.Unit);

            // Project up or down the desired distance
            return centerPoint + (normal * (radius - centerPointDist));
        }

        #endregion
    }

    #region class: Rhombicuboctahedron_wpf

    public class Rhombicuboctahedron_wpf
    {
        public Rhombicuboctahedron_wpf(int[][] squarePolys_Orth, int[][] squarePolys_Diag, TriangleIndexed_wpf[] triangles, Point3D[] allPoints)
        {
            this.Squares_Orth = Math2D.GetTrianglesFromConvexPoly(squarePolys_Orth, allPoints);
            this.Squares_Diag = Math2D.GetTrianglesFromConvexPoly(squarePolys_Diag, allPoints);
            this.Triangles = triangles;

            this.SquarePolys_Orth = squarePolys_Orth;
            this.SquarePolys_Diag = squarePolys_Diag;

            this.AllPoints = allPoints;

            this.AllTriangles = UtilityCore.Iterate(
                this.Squares_Orth.SelectMany(o => o),
                this.Squares_Diag.SelectMany(o => o),
                triangles
                ).ToArray();
        }

        // These are subsets of triangles (one square will have two triangles)
        public readonly TriangleIndexed_wpf[][] Squares_Orth;       // these actually become rectangles when stretched
        public readonly TriangleIndexed_wpf[][] Squares_Diag;       // these actually become trapazoids when stretched
        public readonly TriangleIndexed_wpf[] Triangles;

        public readonly int[][] SquarePolys_Orth;
        public readonly int[][] SquarePolys_Diag;

        public readonly Point3D[] AllPoints;

        // These are all the triangles that make up this hull
        public readonly TriangleIndexed_wpf[] AllTriangles;

        public (int, int)[] GetUniqueLines()
        {
            return Icosidodecahedron_wpf.GetUniqueLines(UtilityCore.Iterate(this.SquarePolys_Orth, this.SquarePolys_Diag));
        }
    }

    #endregion
    #region class: Icosidodecahedron_wpf

    public class Icosidodecahedron_wpf
    {
        public Icosidodecahedron_wpf(int[][] pentagonPolys, TriangleIndexed_wpf[] triangles, Point3D[] allPoints)
        {
            this.Pentagons = Math2D.GetTrianglesFromConvexPoly(pentagonPolys, allPoints);
            this.Triangles = triangles;

            this.PentagonPolys = pentagonPolys;

            this.AllPoints = allPoints;

            this.AllTriangles = UtilityCore.Iterate(this.Pentagons.SelectMany(o => o), triangles).ToArray();
        }

        public readonly TriangleIndexed_wpf[][] Pentagons;
        public readonly TriangleIndexed_wpf[] Triangles;

        public readonly int[][] PentagonPolys;

        public readonly Point3D[] AllPoints;

        public readonly TriangleIndexed_wpf[] AllTriangles;

        public (int, int)[] GetUniqueLines()
        {
            return GetUniqueLines(this.PentagonPolys);
        }

        public static (int, int)[] PolyToTuple(int[] poly)
        {
            List<(int, int)> retVal = new List<(int, int)>();

            for (int cntr = 0; cntr < poly.Length - 1; cntr++)
            {
                retVal.Add((poly[cntr], poly[cntr + 1]));
            }

            retVal.Add((poly[poly.Length - 1], poly[0]));

            return retVal.ToArray();
        }
        public static (int, int)[] GetUniqueLines(IEnumerable<int[]> polys)
        {
            return polys.
                Select(o => PolyToTuple(o)).        // convert this poly into tuple segments
                SelectMany(o => o).     // flatten the polys into a single list
                Select(o => o.Item1 < o.Item2 ? o : (o.Item2, o.Item1)).        // make sure that item1 is smallest
                Distinct().     // dedupe
                ToArray();
        }
    }

    #endregion
    #region class: TruncatedIcosidodecahedron_wpf

    public class TruncatedIcosidodecahedron_wpf
    {
        public TruncatedIcosidodecahedron_wpf(int[][] decagonPolys, int[][] hexagonPolys, int[][] squarePolys, Point3D[] allPoints)
        {
            this.Decagons = Math2D.GetTrianglesFromConvexPoly(decagonPolys, allPoints);
            this.Hexagons = Math2D.GetTrianglesFromConvexPoly(hexagonPolys, allPoints);
            this.Squares = Math2D.GetTrianglesFromConvexPoly(squarePolys, allPoints);

            this.DecagonPolys = decagonPolys;
            this.HexagonPolys = hexagonPolys;
            this.SquarePolys = squarePolys;

            this.AllPoints = allPoints;

            this.AllTriangles = UtilityCore.Iterate(this.Decagons, this.Hexagons, this.Squares).SelectMany(o => o).ToArray();
        }

        public readonly TriangleIndexed_wpf[][] Decagons;       // 10 sided
        public readonly TriangleIndexed_wpf[][] Hexagons;
        public readonly TriangleIndexed_wpf[][] Squares;

        public readonly int[][] DecagonPolys;
        public readonly int[][] HexagonPolys;
        public readonly int[][] SquarePolys;

        public readonly Point3D[] AllPoints;

        public readonly TriangleIndexed_wpf[] AllTriangles;

        public (int, int)[] GetUniqueLines()
        {
            return Icosidodecahedron_wpf.GetUniqueLines(UtilityCore.Iterate(this.DecagonPolys, this.HexagonPolys, this.SquarePolys));
        }
    }

    #endregion
    #region class: Dodecahedron_wpf

    public class Dodecahedron_wpf
    {
        public Dodecahedron_wpf(int[][] pentagonPolys, Point3D[] allPoints)
        {
            this.Pentagons = Math2D.GetTrianglesFromConvexPoly(pentagonPolys, allPoints);

            this.PentagonPolys = pentagonPolys;

            this.AllPoints = allPoints;

            this.AllTriangles = this.Pentagons.SelectMany(o => o).ToArray();
        }

        public readonly TriangleIndexed_wpf[][] Pentagons;

        public readonly int[][] PentagonPolys;

        public readonly Point3D[] AllPoints;

        public readonly TriangleIndexed_wpf[] AllTriangles;

        public (int, int)[] GetUniqueLines()
        {
            return Icosidodecahedron_wpf.GetUniqueLines(this.PentagonPolys);
        }
    }

    #endregion
    #region class: TruncatedIcosahedron_wpf

    /// <summary>
    /// This is hexagons and pentagons (a soccer ball, carbon 60 buckyball)
    /// </summary>
    public class TruncatedIcosahedron_wpf
    {
        public TruncatedIcosahedron_wpf(int[][] pentagonPolys, int[][] hexagonPolys, Point3D[] allPoints)
        {
            this.Pentagons = Math2D.GetTrianglesFromConvexPoly(pentagonPolys, allPoints);
            this.Hexagons = Math2D.GetTrianglesFromConvexPoly(hexagonPolys, allPoints);

            this.PentagonPolys = pentagonPolys;
            this.HexagonPolys = hexagonPolys;

            this.AllPoints = allPoints;

            this.AllTriangles = UtilityCore.Iterate(this.Pentagons, this.Hexagons).SelectMany(o => o).ToArray();
        }

        public readonly TriangleIndexed_wpf[][] Pentagons;
        public readonly TriangleIndexed_wpf[][] Hexagons;

        public readonly int[][] PentagonPolys;
        public readonly int[][] HexagonPolys;

        public readonly Point3D[] AllPoints;

        public readonly TriangleIndexed_wpf[] AllTriangles;

        public (int, int)[] GetUniqueLines()
        {
            return Icosidodecahedron_wpf.GetUniqueLines(UtilityCore.Iterate(this.PentagonPolys, this.HexagonPolys));
        }
    }

    #endregion
}
