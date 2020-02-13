using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.WPF
{
    //TODO: Don't cache a bezier.  As requests for hues come in, calculate at that point.  If there are close enough nearby points, replace that section with a bezier
    /// <summary>
    /// If you keep saturation and value constant, then choose colors for different hues, they will appear lighter
    /// and darker (yellows/greens get bright, blues/purples get dark)
    /// 
    /// This takes a sample color, converts to gray, then returns the closest match for the requested hue
    /// </summary>
    /// <remarks>
    /// If you only need a couple, just use the static method.  But if you will choose many random colors based on
    /// a source color, create an instance of this class, which internally caches a bezier
    /// </remarks>
    public class EquivalentColor
    {
        #region Declaration Section

        //NOTE: When mapping between HSV and XYZ: (this was the convention used by ColorManipulationsWindow while writing these functions)
        // X = V
        // Y = S
        // Z = H

        private readonly ColorHSV _sourceColor;

        private readonly BezierSegment3D_wpf[] _bezier;

        #endregion

        /// <summary>
        /// This creates an instance that will return colors that are close to the match color
        /// </summary>
        public EquivalentColor(ColorHSV colorToMatch)
        {
            // factors of 360: 1, 2, 3, 4, 5, 6, 8, 9, 10, 12, 15, 18, 20, 24, 30, 36, 40, 45, 60, 72, 90, 120, 180, 360
            const int COUNT = 90;

            _sourceColor = colorToMatch;

            // Get a spread of other hues
            var samples = Enumerable.Range(0, COUNT + 1).
                Select(o => o * (360 / COUNT)).
                AsParallel().
                Select(o => GetEquivalent(_sourceColor, o)).
                OrderBy(o => o.H).      // parallel may have scrambled the order
                ToArray();

            Point3D[] samplePoints = samples.
                Select(o => new Point3D(o.V, o.S, o.H)).
                ToArray();

            _bezier = BezierUtil.GetBezierSegments(samplePoints, .125);
        }
        public ColorHSV GetEquivalent(double requestHue)
        {
            double hueCapped = UtilityWPF.GetHueCapped(requestHue);
            double percent = hueCapped / 360d;

            Point3D point = BezierUtil.GetPoint(percent, _bezier);

            return new ColorHSV(hueCapped, point.Y, point.X);
        }

        public static Point3D[] GetDebugSamples(ColorHSV colorToMatch, int count)
        {
            var samples = Enumerable.Range(0, count + 1).
                Select(o => o * (360 / count)).
                AsParallel().
                Select(o => GetEquivalent(colorToMatch, o)).
                OrderBy(o => o.H).      // parallel may have scrambled the order
                ToArray();

            Point3D[] samplePoints = samples.
                Select(o => new Point3D(o.V, o.S, o.H)).
                ToArray();

            return samplePoints;
        }

        /// <summary>
        /// This returns a color that is the hue passed in, but close to the color to match against
        /// </summary>
        public static ColorHSV GetEquivalent(ColorHSV colorToMatch, double requestHue)
        {
            byte gray = colorToMatch.ToRGB().ToGray().R;

            // Get the rectangle to search in
            var rect = GetSearchRect(requestHue, gray, colorToMatch);

            // Get the closest match from within that rectangle
            var best = SearchForBest(requestHue, gray, colorToMatch, rect);

            if (best == null)
            {
                // This should never happen.  But if it does, just return something instead of throwing an exception
                return new ColorHSV(requestHue, colorToMatch.S, colorToMatch.V);
            }
            else
            {
                return new ColorHSV(requestHue, best.Value.s, best.Value.v);
            }
        }

        #region Private Methods

        private static RectInt GetSearchRect(double hue, byte gray, ColorHSV sourceColor)
        {
            byte sourceGray = UtilityWPF.HSVtoRGB(hue, sourceColor.S, sourceColor.V).ToGray().R;

            int sourceV = sourceColor.V.ToInt_Round();
            int sourceS = sourceColor.S.ToInt_Round();

            if (sourceGray == gray)
            {
                return new RectInt(sourceV, sourceS, 1, 1);
            }

            // Send out feelers.  All the way up down left right and see which directions crossed over the boundry
            bool isUp = !IsSameSide(gray, sourceGray, hue, 100, sourceColor.V);
            bool isDown = !IsSameSide(gray, sourceGray, hue, 0, sourceColor.V);
            bool isLeft = !IsSameSide(gray, sourceGray, hue, sourceColor.S, 0);
            bool isRight = !IsSameSide(gray, sourceGray, hue, sourceColor.S, 100);

            if ((isUp && isDown) || (isLeft && isRight) || (!isUp && !isDown && !isLeft && !isRight))
            {
                // Should never happen, just brute force the whole square
                return new RectInt(0, 0, 100, 100);
            }

            // Walk along the yes lines until there is a match, or it's crossed over, then stop at that line

            if ((isUp || isDown) && (isLeft || isRight))
            {
                // Two edges to walk
                AxisFor horz = isLeft ?
                    new AxisFor(Axis.X, sourceColor.V.ToInt_Round(), 0) :
                    new AxisFor(Axis.X, sourceColor.V.ToInt_Round(), 100);

                AxisFor vert = isDown ?
                    new AxisFor(Axis.Y, sourceColor.S.ToInt_Round(), 0) :
                    new AxisFor(Axis.Y, sourceColor.S.ToInt_Round(), 100);

                VectorInt corner = Find_TwoAxiis(horz, vert, hue, sourceGray, gray);

                var aabb = Math2D.GetAABB(new[] { corner, new VectorInt(sourceV, sourceS) });

                return new RectInt(aabb.min.X, aabb.min.Y, aabb.max.X - aabb.min.X, aabb.max.Y - aabb.min.Y);
            }
            else
            {
                // Only one edge to walk.  Walk the main one until there's a match or threshold change
                AxisFor main =
                    isLeft ? new AxisFor(Axis.X, sourceColor.V.ToInt_Round(), 0) :
                    isRight ? new AxisFor(Axis.X, sourceColor.V.ToInt_Round(), 100) :
                    isDown ? new AxisFor(Axis.Y, sourceColor.S.ToInt_Round(), 0) :
                    isUp ? new AxisFor(Axis.Y, sourceColor.S.ToInt_Round(), 100) :
                    throw new ApplicationException("Should have exactly one direction to go");

                int end = Find_OneAxis(main, hue, sourceColor.V, sourceColor.S, sourceGray, gray);

                // Now create an axis perpendicular to this
                return GetBox_OneAxis(main, end, hue, sourceColor.V, sourceColor.S, sourceGray, gray);
            }
        }

        private static VectorInt Find_TwoAxiis(AxisFor horz, AxisFor vert, double hue, byte sourceGray, byte gray, int extra = 2)
        {
            VectorInt retVal = new VectorInt(horz.Start, vert.Start);

            var enumHz = horz.Iterate().GetEnumerator();
            var enumVt = vert.Iterate().GetEnumerator();

            enumHz.MoveNext();      // the first time through is Start, so prime them to do the item after start
            enumVt.MoveNext();

            bool stoppedH = false;
            bool stoppedV = false;

            // Walk one step at a time along the sides of the square until one of the edges finds the curve
            while (true)
            {
                bool foundH = false;
                if (!stoppedH && enumHz.MoveNext())
                {
                    retVal.X = enumHz.Current;
                    foundH = !IsSameSide(gray, sourceGray, hue, vert.Start, retVal.X);
                }
                else
                {
                    stoppedH = true;
                }

                bool foundV = false;
                if (!stoppedV && enumVt.MoveNext())
                {
                    retVal.Y = enumVt.Current;
                    foundV = !IsSameSide(gray, sourceGray, hue, retVal.Y, horz.Start);
                }
                else
                {
                    stoppedV = true;
                }

                if (foundH || foundV)
                {
                    break;
                }

                if (stoppedH && stoppedV)       // this should never happen
                {
                    break;
                }
            }

            return new VectorInt(UtilityMath.Clamp(retVal.X + (horz.Increment * extra), 0, 100), UtilityMath.Clamp(retVal.Y + (vert.Increment * extra), 0, 100));
        }
        private static int Find_OneAxis(AxisFor axis, double hue, double val, double sat, byte sourceGray, byte gray, int extra = 2)
        {
            int x = val.ToInt_Round();
            int y = sat.ToInt_Round();

            foreach (int item in axis.Iterate())
            {
                axis.Set2DIndex(ref x, ref y, item);

                if (!IsSameSide(gray, sourceGray, hue, y, x))
                {
                    return item + (axis.Increment * extra);
                }
            }

            // The curve wasn't found.  This should never happen
            //return axis.Stop;
            throw new ApplicationException("Didn't find curve");
        }

        private static RectInt GetBox_OneAxis(AxisFor axis, int axisStop, double hue, double val, double sat, byte sourceGray, byte gray)
        {
            int fromX = val.ToInt_Round();
            int fromY = sat.ToInt_Round();

            int direction = GetBox_OneAxis_Direction(axis, axisStop, hue, fromX, fromY, sourceGray, gray);

            var corners = new List<VectorInt>();

            corners.Add(new VectorInt(fromX, fromY));

            switch (axis.Axis)
            {
                case Axis.X:
                    int distX = Math.Abs(fromX - axisStop);

                    if (direction <= 0)
                    {
                        corners.Add(new VectorInt(fromX, fromY - distX));
                        corners.Add(new VectorInt(axisStop, fromY - distX));
                    }

                    if (direction >= 0)
                    {
                        corners.Add(new VectorInt(fromX, fromY + distX));
                        corners.Add(new VectorInt(axisStop, fromY + distX));
                    }
                    break;

                case Axis.Y:
                    int distY = Math.Abs(fromY - axisStop);

                    if (direction <= 0)
                    {
                        corners.Add(new VectorInt(fromX - distY, fromY));
                        corners.Add(new VectorInt(fromX - distY, axisStop));
                    }

                    if (direction >= 0)
                    {
                        corners.Add(new VectorInt(fromX + distY, fromY));
                        corners.Add(new VectorInt(fromX + distY, axisStop));
                    }

                    break;

                case Axis.Z:
                    throw new ApplicationException("Didn't expect Z axis");

                default:
                    throw new ApplicationException($"Unknown Axis: {axis.Axis}");
            }

            var aabb = Math2D.GetAABB(corners);

            aabb =
            (
                new VectorInt(UtilityMath.Clamp(aabb.min.X, 0, 100), UtilityMath.Clamp(aabb.min.Y, 0, 100)),
                new VectorInt(UtilityMath.Clamp(aabb.max.X, 0, 100), UtilityMath.Clamp(aabb.max.Y, 0, 100))
            );

            return new RectInt(aabb.min.X, aabb.min.Y, aabb.max.X - aabb.min.X, aabb.max.Y - aabb.min.Y);
        }
        private static int GetBox_OneAxis_Direction(AxisFor axis, int axisStop, double hue, int fromX, int fromY, byte sourceGray, byte gray)
        {
            // After this, fromX and fromY will be toX and toY.  It's difficult to name these meaningfully
            axis.Set2DIndex(ref fromX, ref fromY, axisStop);

            // Get posistions of feelers
            AxisFor perpAxis = new AxisFor(axis.Axis == Axis.X ? Axis.Y : Axis.X, 66, 88);      // the ints don't matter, just using this for Set2DIndex

            int negX = fromX;
            int negY = fromY;
            perpAxis.Set2DIndex(ref negX, ref negY, 0);

            int posX = fromX;
            int posY = fromY;
            perpAxis.Set2DIndex(ref posX, ref posY, 100);

            // Handle cases where fromX,fromY is sitting on the edge of the square
            if (negX == fromX && negY == fromY)
            {
                return 1;
            }
            else if (posX == fromX && posY == fromY)
            {
                return -1;
            }

            // See which crossed over
            bool isNeg = !IsSameSide(gray, sourceGray, hue, negY, negX);
            bool isPos = !IsSameSide(gray, sourceGray, hue, posY, posX);

            if (isNeg && !isPos)
            {
                return -1;      // The curve is in box within the negative side
            }
            else if (isPos && !isNeg)
            {
                return 1;
            }
            else
            {
                return 0;       // undetermined, search both sides
            }
        }

        private static (double s, double v, Color color)? SearchForBest(double hue, byte gray, ColorHSV sourceColor, RectInt rectangle)
        {
            return AllGridPoints(rectangle.Top, rectangle.Bottom, rectangle.Left, rectangle.Right).     // x is value, y is saturation
                AsParallel().
                Select(o =>
                {
                    Color c = UtilityWPF.HSVtoRGB(hue, o.s, o.v);
                    byte g = c.ToGray().R;

                    return new
                    {
                        o.s,
                        o.v,
                        color = c,
                        grayDistance = Math.Abs(gray - g),      // wrong distance.  Go by distance from request point
                        pointDistance = Math2D.LengthSquared(o.v, o.s, sourceColor.V, sourceColor.S),
                    };
                }).
                OrderBy(o => o.grayDistance).
                ThenBy(o => o.pointDistance).
                Select(o => ((double)o.s, (double)o.v, o.color)).
                First();
        }

        private static bool IsSameSide(byte target, byte current, double h, double s, double v)
        {
            byte test = UtilityWPF.HSVtoRGB(h, s, v).ToGray().R;

            return (current >= target && test >= target) ||
                (current <= target && test <= target);
        }

        private static IEnumerable<(int s, int v)> AllGridPoints(int fromS, int toS, int fromV, int toV)
        {
            for (int s = fromS; s <= toS; s++)
            {
                for (int v = fromV; v <= toV; v++)
                {
                    yield return (s, v);
                }
            }
        }

        #endregion
    }
}
