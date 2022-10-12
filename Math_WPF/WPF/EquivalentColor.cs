using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.WPF
{
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
        #region class: RawResult

        private class RawResult
        {
            public RawResult(int hue, Point result)
            {
                Hue = hue;
                Result = result;
            }

            public int Hue { get; }
            public Point Result { get; }

            public override string ToString()
            {
                return $"H={Hue}, S={Result.Y.ToStringSignificantDigits(1)}, V={Result.X.ToStringSignificantDigits(1)}";
            }
        }

        #endregion
        #region class: Interval

        private class Interval
        {
            public Interval(IEnumerable<RawResult> raw)
            {
                // Make sure they are sorted
                Raw = raw.
                    OrderBy(o => o.Hue).
                    ToArray();

                From = Raw[0].Hue;
                To = Raw[^1].Hue;

                //TODO: Build a tree for faster lookups (need to reference accord.net)
                //But only when Raw.Length > some_threshold
                //
                //On second thought, see if the tree can be added to.  When creating a new interval from existing intervals
                //and raws, pass in the current trees and decide whether to add to one or start over (the overhead of creation
                //is pretty high, so it's only worth it if the tree will be used quite a bit after creation)

                //NOTE: Euclidean class's overload that takes in scalars just returns: Math.Abs(x - y);  (so no need to worry about square root)
                //_tree = VPTree_Custom<double, RawResult>.FromData(raw.Select(o => o.Hue).ToArray(), raw.ToArray(), new Euclidean(), new Euclidean(), false);
            }

            public int From { get; }
            public int To { get; }

            public RawResult[] Raw { get; }

            //private VPTree<double, RawResult> _tree;

            public bool Contains(double hue)
            {
                return hue >= From && hue <= To;
            }

            public ColorHSV GetColor(double hue)
            {
                if (!Contains(hue))
                {
                    throw new ArgumentOutOfRangeException($"The hue passed in is outside this interval: hue={hue} from={From} to={To}");
                }

                //NOTE: This first draft is very unoptimized, it's just written to get all thoughts working
                //TODO: Get this from a tree

                RawResult exact = Raw.FirstOrDefault(o => hue.IsNearValue(o.Hue));
                if (exact != null)
                {
                    return ToHSV(hue, exact.Result);
                }

                var distances = Raw.
                    Select(o => new
                    {
                        o.Hue,
                        o.Result,
                        dist = Math.Abs(o.Hue - hue),
                    }).
                    OrderBy(o => o.dist).
                    ToArray();

                var left = distances.
                    Where(o => o.Hue < hue).
                    FirstOrDefault();

                var right = distances.
                    Where(o => o.Hue > hue).
                    FirstOrDefault();

                if (left == null || right == null)
                {
                    throw new ApplicationException("The hue is in range, but didn't find left and right");
                }

                double percent = (double)(hue - left.Hue) / (double)(right.Hue - left.Hue);

                Point lerp = Math2D.LERP(left.Result, right.Result, percent);

                return ToHSV(hue, lerp);
            }

            public override string ToString()
            {
                return $"{From} - {To} | count={Raw.Length}";
            }
        }

        #endregion

        #region Declaration Section

        //NOTE: When mapping between HSV and XYZ: (this was the convention used by ColorManipulationsWindow while writing these functions)
        // X = V
        // Y = S
        // Z = H

        private readonly object _lock = new object();

        private readonly ColorHSV _sourceColor;

        private readonly int _maxDistance;

        private readonly List<RawResult> _rawResults = new List<RawResult>();
        private readonly List<Interval> _intervals = new List<Interval>();

        #endregion

        /// <summary>
        /// This creates an instance that will return colors that are close to the match color
        /// </summary>
        public EquivalentColor(ColorHSV colorToMatch, int maxDistance = 5)
        {
            _sourceColor = colorToMatch;
            _maxDistance = maxDistance;

            // Immediately calculate the value for hue=0 and store at 0 and 360.  That way the endpoints will always be there, which
            // simplify logic (I ran a test of lots of random colors and 0 and 360 results are identical)
            ColorHSV result0 = GetEquivalent(_sourceColor, 0);
            _rawResults.Add(new RawResult(0, new Point(result0.V, result0.S)));
            _rawResults.Add(new RawResult(360, new Point(result0.V, result0.S)));       //NOTE: UtilityWPF.GetHueCapped will return 0 when 360 is requested.  So this entry will be an upper bound, but will still be an end cap of an interval
        }

        /// <summary>
        /// This is the instance version.  It caches results of previous calls, interpolates the answer if the requested hue
        /// is between two other requests and is close enough to them
        /// </summary>
        public ColorHSV GetEquivalent(double requestHue)
        {
            int hueCapped = UtilityMath.Clamp(UtilityWPF.GetHueCapped(requestHue).ToInt_Round(), 0, 360);

            lock (_lock)
            {
                // See if a previously cached interval instance can answer this
                var interval = _intervals.FirstOrDefault(o => o.Contains(hueCapped));
                if (interval != null)
                {
                    return interval.GetColor(hueCapped);
                }

                // Make a new one
                ColorHSV newColor = GetEquivalent(_sourceColor, hueCapped);
                RawResult newEntry = new RawResult(hueCapped, new Point(newColor.V, newColor.S));

                // Store this new entry
                StoreNewEntry(newEntry);

                return newColor;
            }
        }

        /// <summary>
        /// This returns a color that is the hue passed in, but close to the color to match against
        /// </summary>
        public static ColorHSV GetEquivalent(ColorHSV colorToMatch, double requestHue)
        {
            int hueCapped = UtilityMath.Clamp(UtilityWPF.GetHueCapped(requestHue).ToInt_Round(), 0, 360);

            byte gray = colorToMatch.ToRGB().ToGray().R;

            // Get the rectangle to search in
            var rect = GetSearchRect(hueCapped, gray, colorToMatch);

            // Get the closest match from within that rectangle
            var best = SearchForBest(hueCapped, gray, colorToMatch, rect);

            if (best == null)
            {
                // This should never happen.  But if it does, just return something instead of throwing an exception
                return new ColorHSV(hueCapped, colorToMatch.S, colorToMatch.V);
            }
            else
            {
                return new ColorHSV(hueCapped, best.Value.s, best.Value.v);
            }
        }

        #region Private Methods

        private void StoreNewEntry(RawResult newEntry)
        {
            // Find neighbors
            var nearRaw = _rawResults.
                Select((o, i) => new
                {
                    index = i,
                    raw = o,
                    dist = Math.Abs(newEntry.Hue - o.Hue),
                }).
                Where(o => o.dist <= _maxDistance).
                OrderByDescending(o => o.index).        // descending so they can be removed without affecting index of others
                ToArray();

            var nearInterval = _intervals.
                Select((o, i) => new
                {
                    index = i,
                    interval = o,
                    dist = Math.Min(Math.Abs(newEntry.Hue - o.From), Math.Abs(newEntry.Hue - o.To)),
                }).
                Where(o => o.dist <= _maxDistance).
                OrderByDescending(o => o.index).
                ToArray();

            if (nearRaw.Length == 0 && nearInterval.Length == 0)
            {
                // There are no others close enough, store this as a loose point
                _rawResults.Add(newEntry);
            }
            else
            {
                // There are neighbors.  Remove them from the lists and build an interval out of them
                var allNearRaw = new List<RawResult>();
                allNearRaw.Add(newEntry);

                foreach (var raw in nearRaw)
                {
                    _rawResults.RemoveAt(raw.index);        // they are sorted descending so removing won't change the index
                    allNearRaw.Add(raw.raw);
                }

                foreach (var interval2 in nearInterval)
                {
                    _intervals.RemoveAt(interval2.index);
                    allNearRaw.AddRange(interval2.interval.Raw);
                }

                _intervals.Add(new Interval(allNearRaw));
            }
        }

        private static RectInt2 GetSearchRect(double hue, byte gray, ColorHSV sourceColor)
        {
            byte sourceGray = UtilityWPF.HSVtoRGB(hue, sourceColor.S, sourceColor.V).ToGray().R;

            int sourceV = sourceColor.V.ToInt_Round();
            int sourceS = sourceColor.S.ToInt_Round();

            if (sourceGray == gray)
            {
                return new RectInt2(sourceV, sourceS, 1, 1);
            }

            // Send out feelers.  All the way up down left right and see which directions crossed over the boundry
            bool isUp = !IsSameSide(gray, sourceGray, hue, 100, sourceColor.V);
            bool isDown = !IsSameSide(gray, sourceGray, hue, 0, sourceColor.V);
            bool isLeft = !IsSameSide(gray, sourceGray, hue, sourceColor.S, 0);
            bool isRight = !IsSameSide(gray, sourceGray, hue, sourceColor.S, 100);

            if ((isUp && isDown) || (isLeft && isRight) || (!isUp && !isDown && !isLeft && !isRight))
            {
                // Should never happen, just brute force the whole square
                return new RectInt2(0, 0, 100, 100);
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

                VectorInt2 corner = Find_TwoAxiis(horz, vert, hue, sourceGray, gray);

                var aabb = Math2D.GetAABB(new[] { corner, new VectorInt2(sourceV, sourceS) });

                return new RectInt2(aabb.min.X, aabb.min.Y, aabb.max.X - aabb.min.X, aabb.max.Y - aabb.min.Y);
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

        private static VectorInt2 Find_TwoAxiis(AxisFor horz, AxisFor vert, double hue, byte sourceGray, byte gray, int extra = 2)
        {
            VectorInt2 retVal = new VectorInt2(horz.Start, vert.Start);

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

            return new VectorInt2(UtilityMath.Clamp(retVal.X + (horz.Increment * extra), 0, 100), UtilityMath.Clamp(retVal.Y + (vert.Increment * extra), 0, 100));
        }
        private static int Find_OneAxis(AxisFor axis, double hue, double val, double sat, byte sourceGray, byte gray, int extra = 2)
        {
            int x = val.ToInt_Round();
            int y = sat.ToInt_Round();

            foreach (int item in axis.Iterate())
            {
                axis.Set2DValue(ref x, ref y, item);

                if (!IsSameSide(gray, sourceGray, hue, y, x))
                {
                    return item + (axis.Increment * extra);
                }
            }

            // The curve wasn't found.  This should never happen
            //return axis.Stop;
            throw new ApplicationException("Didn't find curve");
        }

        private static RectInt2 GetBox_OneAxis(AxisFor axis, int axisStop, double hue, double val, double sat, byte sourceGray, byte gray)
        {
            int fromX = val.ToInt_Round();
            int fromY = sat.ToInt_Round();

            int direction = GetBox_OneAxis_Direction(axis, axisStop, hue, fromX, fromY, sourceGray, gray);

            var corners = new List<VectorInt2>();

            corners.Add(new VectorInt2(fromX, fromY));

            switch (axis.Axis)
            {
                case Axis.X:
                    int distX = Math.Abs(fromX - axisStop);

                    if (direction <= 0)
                    {
                        corners.Add(new VectorInt2(fromX, fromY - distX));
                        corners.Add(new VectorInt2(axisStop, fromY - distX));
                    }

                    if (direction >= 0)
                    {
                        corners.Add(new VectorInt2(fromX, fromY + distX));
                        corners.Add(new VectorInt2(axisStop, fromY + distX));
                    }
                    break;

                case Axis.Y:
                    int distY = Math.Abs(fromY - axisStop);

                    if (direction <= 0)
                    {
                        corners.Add(new VectorInt2(fromX - distY, fromY));
                        corners.Add(new VectorInt2(fromX - distY, axisStop));
                    }

                    if (direction >= 0)
                    {
                        corners.Add(new VectorInt2(fromX + distY, fromY));
                        corners.Add(new VectorInt2(fromX + distY, axisStop));
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
                new VectorInt2(UtilityMath.Clamp(aabb.min.X, 0, 100), UtilityMath.Clamp(aabb.min.Y, 0, 100)),
                new VectorInt2(UtilityMath.Clamp(aabb.max.X, 0, 100), UtilityMath.Clamp(aabb.max.Y, 0, 100))
            );

            return new RectInt2(aabb.min.X, aabb.min.Y, aabb.max.X - aabb.min.X, aabb.max.Y - aabb.min.Y);
        }
        private static int GetBox_OneAxis_Direction(AxisFor axis, int axisStop, double hue, int fromX, int fromY, byte sourceGray, byte gray)
        {
            // After this, fromX and fromY will be toX and toY.  It's difficult to name these meaningfully
            axis.Set2DValue(ref fromX, ref fromY, axisStop);

            // Get posistions of feelers
            AxisFor perpAxis = new AxisFor(axis.Axis == Axis.X ? Axis.Y : Axis.X, 66, 88);      // the ints don't matter, just using this for Set2DIndex

            int negX = fromX;
            int negY = fromY;
            perpAxis.Set2DValue(ref negX, ref negY, 0);

            int posX = fromX;
            int posY = fromY;
            perpAxis.Set2DValue(ref posX, ref posY, 100);

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

        private static (double s, double v, Color color)? SearchForBest(double hue, byte gray, ColorHSV sourceColor, RectInt2 rectangle)
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

        private static ColorHSV ToHSV(double hue, Point point)
        {
            return new ColorHSV(hue, point.Y, point.X);
        }

        #endregion
    }
}
