﻿using Game.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.Mathematics
{
    public static partial class BezierUtil
    {
        #region struct: BezierMeshSamples_Distance_wpf

        //TODO: Shorten the name
        private struct BezierMeshSamples_Distance_wpf
        {
            public Point3D From { get; set; }
            public Point3D To { get; set; }

            public VectorInt2 FromIndex { get; set; }
            public VectorInt2 ToIndex { get; set; }

            public double DistSegment { get; set; }
            public double DistSum { get; set; }

            public bool IsHorizontal { get; set; }
        }

        #endregion
        #region record: NormalizedPosPointer

        private record NormalizedPosPointer
        {
            /// <summary>
            /// the index into the list that was passed in
            /// </summary>
            public int Desired_Index { get; init; }

            /// <summary>
            /// The total percent along the curve
            /// </summary>
            public double Total_Percent { get; init; }

            /// <summary>
            /// Index into the bezier array
            /// </summary>
            public int Segment_Index { get; init; }
            /// <summary>
            /// Percent along the bezier segment
            /// </summary>
            public double Segment_Local_Percent { get; init; }
        }

        #endregion
        #region record: CurvatureSample

        public record CurvatureSample
        {
            public int SegmentIndex { get; init; }
            public double Percent_Along_Segment { get; init; }

            public double Percent_Total { get; init; }

            public Point3D Point { get; init; }

            public double Dot { get; init; }
            public double Dist_From_NegOne { get; init; }

            /// <summary>
            /// inverted normalized distance from -1
            /// </summary>
            /// <remarks>
            /// dot of -1 means the angle is 180, so the left and right segment are colinear
            /// dot of 1 means the angle is 0, so the left and right segments are laying on top of each other (the most pinch possible)
            /// 
            /// that dot is inverted to make it easier to think about, so 0 is colinear and 1 is pinched
            /// 
            /// it is also normalized, so 1 is the hardest pinch of the curve.  This makes it easy to find the extremes of the current
            /// curve, but wouldn't be very useful for creating accurate stretch
            /// </remarks>
            public double Weight { get; init; }
        }

        #endregion
        #region record: AdjustmentDensity

        private record AdjustmentDensity
        {
            public int Index { get; init; }

            public double Density_Minus { get; init; }
            public double Density_Current { get; init; }
            public double Density_Plus { get; init; }
        }

        #endregion

        // Get points along the curve
        public static Point3D[] GetPoints(int count, Point3D from, Point3D control, Point3D to)
        {
            return GetPoints(count, new[] { from, control, to });
        }
        public static Point3D[] GetPoints(int count, Point3D from, Point3D fromControl, Point3D toControl, Point3D to)
        {
            return GetPoints(count, new[] { from, fromControl, toControl, to });
        }
        public static Point3D[] GetPoints(int count, Point3D from, Point3D[] controls, Point3D to)
        {
            return GetPoints(count, UtilityCore.Iterate<Point3D>(from, controls, to).ToArray());
        }
        public static Point3D[] GetPoints(int count, Point3D[] controlPoints)
        {
            #region asserts
#if DEBUG
            if (controlPoints.Length < 2)
            {
                throw new ArgumentException("There must be at least two points passed in: " + controlPoints.Length.ToString());
            }
#endif
            #endregion

            double countD = count - 1;

            Point3D[] retVal = new Point3D[count];

            retVal[0] = controlPoints[0];
            retVal[count - 1] = controlPoints[controlPoints.Length - 1];

            for (int cntr = 1; cntr < count - 1; cntr++)
            {
                retVal[cntr] = GetPoint(cntr / countD, controlPoints);
            }

            return retVal;
        }
        public static Point3D[] GetPoints(int count, BezierSegment3D_wpf segment)
        {
            return GetPoints(count, UtilityCore.Iterate<Point3D>(segment.EndPoint0, segment.ControlPoints, segment.EndPoint1).ToArray());
        }

        /// <summary>
        /// Returns points across several segment definitions (linked together into a single path).  count is the total number of sample points to return
        /// </summary>
        /// <remarks>
        /// This bunches the points up at pinched curves and has fewer points along straight lines
        /// 
        /// For the most part this works well, but sometimes there is a noticable change in point density when
        /// moving from one bezier segment to the next.  I think this is from segments having different lengths
        /// combined with how the control points of each segment are....???  Idk, something related to that
        /// 
        /// I went down a rabbit hole trying to fix it, ended up just redistributing how many of the total count
        /// each segment gets based on its length compared to total length.  Not perfect, but simple fix, fast
        /// to compute, generally good enough
        /// 
        /// -------- Ideas for improvement --------
        /// 1: bool allow_filler_points = false (quick, but may have a few more than the count that was passed in)
        /// Look at the segments before and after a bezier segment boundry.  If their relative lengths are larger than some threshold,
        /// add a filler point in the longer segment
        /// 
        /// 2: Need based population (slower execution, but should give a cleaner output)
        /// Privately call this function using 2/3 of the count
        /// Get the dot product for all those points
        /// Find the dot with the highest diff from -1 and replace the 3 points with 4 (recalculating dot products for those points)
        /// Repeat until count is reached
        /// </remarks>
        public static Point3D[] GetPoints(int count, BezierSegment3D_wpf[] segments)
        {
            double total_length = segments.Sum(o => o.Length_quick);

            var counts = segments.
                Select(o => (count * (o.Length_quick / total_length)).ToInt_Round()).     // get close to the final count for this segment, based on the ratio of this segment's length to the total path length
                ToArray();

            int current_count = counts.Sum();

            while (current_count != count)
            {
                var densities = GetSegmentDensities(segments, counts);

                if (current_count < count)
                {
                    AddCountToSegment(counts, densities);
                    current_count++;
                }
                else
                {
                    RemoveCountFromSegment(counts, densities);
                    current_count--;
                }
            }

            counts = EnsureCountsHaveEnds(counts, o => GetSegmentDensities(segments, o));

            return GetSamples(segments, counts);
        }

        private static AdjustmentDensity[] GetSegmentDensities(BezierSegment3D_wpf[] segments, int[] counts)
        {
            return Enumerable.Range(0, segments.Length).
                Select(o => new AdjustmentDensity()
                {
                    Index = o,
                    Density_Minus = (counts[o] - 1) / segments[o].Length_quick,
                    Density_Current = counts[o] / segments[o].Length_quick,
                    Density_Plus = (counts[o] + 1) / segments[o].Length_quick,
                }).
                ToArray();
        }

        /// <summary>
        /// Returns points across several segment definitions (linked together into a single path).  count is the total number of sample points to return
        /// </summary>
        /// <remarks>
        /// The points are evenly distributed (line segment length between two points is the same).  This looks good for
        /// sweeping curves, but pinched curves don't have enough points at the pinch point and look jagged
        /// </remarks>
        public static Point3D[] GetPoints_UniformDistribution(int count, BezierSegment3D_wpf[] segments)
        {
            Point3D[] retVal = new Point3D[count];

            retVal[0] = segments[0].EndPoint0;
            retVal[count - 1] = segments[segments.Length - 1].EndPoint1;        //NOTE: If the segment is a closed curve, this is the same point as retVal[0].  May want a boolean that tells whether the last point should be replicated

            var samples = Enumerable.Range(0, count - 2).       // for (int i = 1; i < count - 1; i++)
                Select(o => (double)(o + 1) / (count - 1));      // {double totalPercent = (double)i / (count - 1);

            foreach (var sample in ConvertToNormalizedPositions(samples, segments))
            {
                // Calculate the bezier point
                retVal[sample.Desired_Index + 1] = GetPoint(sample.Segment_Local_Percent, segments[sample.Segment_Index].Combined);
            }

            return retVal;
        }

        /// <summary>
        /// Returns points across sets of segment definition.  Each set is run through the other path overload.  So the endpoints
        /// of each set are guaranteed to be included in the return points (deduped)
        /// </summary>
        /// <param name="countPerPath">This is how many points per set (the total number of points will be countPerPath * segmentSets.Length)</param>
        public static Point3D[] GetPoints(int countPerPath, BezierSegment3D_wpf[][] segmentSets)
        {
            //TODO: Make an overload that takes in total count instead of per path

            // Get the points for each set of beziers
            List<Point3D[]> perPathPoints = new List<Point3D[]>();

            foreach (BezierSegment3D_wpf[] segments in segmentSets)
            {
                if (segments.Length == 1)
                {
                    perPathPoints.Add(GetPoints(countPerPath, segments[0]));
                }
                else
                {
                    perPathPoints.Add(GetPoints(countPerPath, segments));
                }
            }

            // Dedupe them
            List<Point3D> retVal = new List<Point3D>();

            retVal.AddRange(perPathPoints[0]);

            for (int cntr = 1; cntr < perPathPoints.Count; cntr++)
            {
                if (Math3D.IsNearValue(retVal[retVal.Count - 1], perPathPoints[cntr][0]))
                {
                    // First point dupes with the last
                    retVal.AddRange(perPathPoints[cntr].Skip(1));
                }
                else
                {
                    retVal.AddRange(perPathPoints[cntr]);
                }
            }

            if (Math3D.IsNearValue(retVal[0], retVal[retVal.Count - 1]))
            {
                retVal.RemoveAt(retVal.Count - 1);
            }

            return retVal.ToArray();
        }

        public static Point3D GetPoint(double percent, BezierSegment3D_wpf[] bezier)
        {
            //TODO: If the bezier is closed, make it circular
            if (percent < 0)
                return bezier[0].EndPoint0;

            double totalLength = bezier.Sum(o => o.Length_quick);

            double fromPercent = 0d;
            for (int cntr = 0; cntr < bezier.Length; cntr++)
            {
                double toPercent = fromPercent + (bezier[cntr].Length_quick / totalLength);

                if (percent >= fromPercent && percent <= toPercent)
                {
                    double localPercent = ((percent - fromPercent) * totalLength) / bezier[cntr].Length_quick;

                    return GetPoint(localPercent, bezier[cntr]);
                }

                fromPercent = toPercent;
            }

            return bezier[bezier.Length - 1].EndPoint1;
        }
        public static Point3D GetPoint(double percent, BezierSegment3D_wpf segment)
        {
            return GetPoint(percent, segment.Combined);
        }
        /// <summary>
        /// Get a single point along the curve
        /// </summary>
        /// <returns>
        /// Got this here:
        /// http://www.cubic.org/docs/bezier.htm
        /// </returns>
        public static Point3D GetPoint(double percent, Point3D[] controlPoints)
        {
            #region asserts
#if DEBUG
            if (controlPoints.Length < 2)
                throw new ArgumentException("There must be at least two points passed in: " + controlPoints.Length.ToString());
#endif
            #endregion

            Point3D[] prev = controlPoints;
            Point3D[] current = null;

            for (int outer = controlPoints.Length - 1; outer > 0; outer--)
            {
                current = new Point3D[outer];

                for (int inner = 0; inner < outer; inner++)
                {
                    current[inner] = Math3D.LERP(prev[inner], prev[inner + 1], percent);
                }

                prev = current;
            }

            return current[0];      // by the time execution gets here, the array only has one element
        }

        /// <summary>
        /// This is a helper method that creates a bezier definition that runs through a set of points
        /// </summary>
        /// <param name="ends">These are the end points that the beziers run through</param>
        /// <param name="along">This is how far out the control points should be pulled from the end points (it is a percent of that line segment's length)</param>
        /// <param name="isClosed">
        /// True: The assumption is that ends[0] and ends[len-1] aren't the same point.  This will add an extra segment to create a closed curve.
        /// False: This method compares ends[0] and ends[len-1].  If they are the same point, it makes a closed curve.  If they are different, it makes an open curve.
        /// </param>
        public static BezierSegment3D_wpf[] GetBezierSegments(Point3D[] ends, double along = .25, bool isClosed = false)
        {
            if (isClosed)
                return GetBezierSegments_Closed(ends, along);

            if (ends.Length > 2 && Math3D.IsNearValue(ends[0], ends[ends.Length - 1]))
            {
                Point3D[] endsClosed = new Point3D[ends.Length - 1];
                Array.Copy(ends, endsClosed, ends.Length - 1);
                return GetBezierSegments_Closed(endsClosed, along);       // remove the last point, which is redundant
            }
            else
            {
                return GetBezierSegments_Open(ends, along);
            }
        }

        public static (Point, Point) GetControlPoints_Middle(Point end1, Point end2, Point end3, double percentAlong12 = .25, double percentAlong23 = .25)
        {
            // Just use the 3D overload
            var retVal = GetControlPoints_Middle(end1.ToPoint3D(), end2.ToPoint3D(), end3.ToPoint3D(), percentAlong12, percentAlong23);

            // Convert the response back to 2D
            return (retVal.Item1.ToPoint2D(), retVal.Item2.ToPoint2D());
        }
        /// <summary>
        /// This is a helper method to generate control points
        /// </summary>
        /// <remarks>
        /// A bezier curve will always go through the end points.  It will use the control points to pull it off the direct
        /// line segment.
        /// 
        /// When two bezier segments are linked, the curve will be smooth if the two control points for the shared
        /// end point are in a line.
        /// 
        /// This method takes the three end points, and returns the two control points for the middle end point (end2)
        /// 
        /// The returned control points will be colinear with end2
        /// </remarks>
        /// <param name="percentAlong12">This is the percent of the 1-2 segment's length</param>
        /// <param name="percentAlong23">This is the percent of the 2-3 segment's length</param>
        /// <returns>
        /// Item1=control point for end2 for the 1-2 bezier segment (this is the last point in this.ControlPoints)
        /// Item2=control point for end2 for the 2-3 bezier segment (this is the first point in this.ControlPoints)
        /// </returns>
        public static (Point3D, Point3D) GetControlPoints_Middle(Point3D end1, Point3D end2, Point3D end3, double percentAlong12 = .25, double percentAlong23 = .25)
        {
            Vector3D dir21 = end1 - end2;
            Vector3D dir23 = end3 - end2;

            Vector3D? controlLine = GetControlPoints_Middle_ControlLine(dir21, dir23);
            if (controlLine == null)
            {
                // The directions are either on top of each other, or pointing directly away from each other, or
                // some of the end points are the same.
                //
                // Just return control points that are the same as the middle point.  This could be improved in the
                // future if certain cases look bad
                return (end2, end2);
            }

            Vector3D controlLineUnit;
            if (Vector3D.DotProduct(dir21, controlLine.Value) > 0)
            {
                // Control line is toward end 1
                controlLineUnit = controlLine.Value.ToUnit();
            }
            else
            {
                // Control line is toward end 3
                controlLineUnit = (-controlLine.Value).ToUnit();
            }

            Point3D control21 = end2 + (controlLineUnit * (dir21.Length * percentAlong12));
            Point3D control23 = end2 - (controlLineUnit * (dir23.Length * percentAlong23));

            return (control21, control23);
        }

        /// <summary>
        /// This creates a control point for end1.  It is along a line that is some angle from dir12.  The distance along
        /// that rotated direction is (end2-end1)*percentAlong12
        /// </summary>
        /// <param name="otherPoint">This is a third point that is coplanar to end1 and end2.  It is just used to figure out the rotation axis (axis will be orthogonal to the plane defined by the three points)</param>
        /// <param name="isAwayFromOther">Whether the control line should rotate away from otherPoint or toward it</param>
        /// <param name="angle">Angle in degrees</param>
        /// <param name="percentAlong12">This is the percent of the 1-2 segment's length</param>
        public static Point3D GetControlPoint_End(Point3D end1, Point3D end2, Point3D otherPoint, bool isAwayFromOther = true, double angle = 30, double percentAlong12 = .25)
        {
            // Figure out the axis
            Vector3D axis = isAwayFromOther ?
                Vector3D.CrossProduct(otherPoint - end1, end2 - end1) :
                Vector3D.CrossProduct(end2 - end1, otherPoint - end1);

            // Call the other overload
            return GetControlPoint_End(end1, end2, axis, angle, percentAlong12);
        }
        public static Point3D GetControlPoint_End(Point3D end1, Point3D end2, Vector3D axis, double angle = 20, double along12 = .25)
        {
            Vector3D dir12 = end2 - end1;

            Vector3D controlLine = dir12.GetRotatedVector(axis, angle).ToUnit();
            controlLine = controlLine * (dir12.Length * along12);

            return end1 + controlLine;
        }

        /// <summary>
        /// This takes in some horizontal beziers, then can generate a grid of points from that definition (using the beziers to infer
        /// points at regular intervals)
        /// </summary>
        /// <param name="horizontals">
        /// A set of beziers that define horizontals
        /// 
        /// Use GetBezierSegments() to create a horizontal line
        /// 
        /// The horizontals don't need to form a rectangle, they could form any kind of patch (they don't even need to be horizontal
        /// along X).  But they shouldn't cross over each other
        /// </param>
        /// <param name="horzCount">How many horizontal points to return</param>
        /// <param name="vertCount">How many vertical points to return</param>
        /// <param name="controlPointPercent">
        /// Where along the line segment to place the control point
        /// 
        /// .5 would be halfway.  Anything less than half will cause the curves to pinch.  Anything greater will exaggerate the curves
        /// </param>
        /// <returns>
        /// A grid of points
        /// 
        /// NOTE: This 1D arrays is vertical, which is backward from normal images (bitmaps are horizontal scan lines concatenated
        /// together.  These GetBezierMesh functions use vertical scan lines concatenated together)
        /// </returns>
        public static Point3D[] GetBezierMesh_Points(BezierSegment3D_wpf[][] horizontals, int horzCount, int vertCount, double controlPointPercent = .5)
        {
            if (horzCount < 2 || vertCount < 2)
                throw new ArgumentException($"horzCount and vertCount need to be at least 2.  horzCount={horzCount}, vertCount={vertCount}");

            return GetVerticalSamples(horizontals, horzCount, vertCount, controlPointPercent).
                SelectMany(o => o).
                ToArray();
        }
        public static Point3D[][] GetBezierMesh_Horizontals(BezierSegment3D_wpf[][] horizontals, int horzCount, int vertCount, double controlPointPercent = .5)
        {
            if (horzCount < 2 || vertCount < 2)
                throw new ArgumentException($"horzCount and vertCount need to be at least 2.  horzCount={horzCount}, vertCount={vertCount}");

            Point3D[][] verticals = GetVerticalSamples(horizontals, horzCount, vertCount, controlPointPercent);

            // Convert the verticals into horizontals
            return Enumerable.Range(0, vertCount).
                Select(v => Enumerable.Range(0, horzCount).
                    Select(h => verticals[h][v]).
                    ToArray()).
                ToArray();
        }
        /// <summary>
        /// This creates a continous mesh of triangles
        /// </summary>
        /// <remarks>
        /// NOTE: When there were only two triangles per square, the lighting reflection looked bad.  So an extra middle point per
        /// square is generated and each square has four triangles.  This means that there are more points returned than requested
        /// </remarks>
        public static ITriangleIndexed_wpf[] GetBezierMesh_Triangles(BezierSegment3D_wpf[][] horizontals, int horzCount, int vertCount, double controlPointPercent = .5)
        {
            if (horzCount < 2 || vertCount < 2)
                throw new ArgumentException($"horzCount and vertCount need to be at least 2.  horzCount={horzCount}, vertCount={vertCount}");

            int horzCount_centers = (horzCount * 2) - 1;
            int vertCount_centers = (vertCount * 2) - 1;

            var verticals = GetVerticalSamples(horizontals, horzCount_centers, vertCount_centers, controlPointPercent);

            //NOTE: There are more points here than the triangles will use.  But it keeps the math simpler to keep a square grid
            Point3D[] allPoints = verticals.
                SelectMany(o => o).
                ToArray();

            List<ITriangleIndexed_wpf> triangles = new List<ITriangleIndexed_wpf>();

            // Build triangles
            foreach (var vertex in IterateTrianglePoints(horzCount_centers, vertCount_centers))
            {
                triangles.Add(new TriangleIndexed_wpf(vertex.index0, vertex.index1, vertex.index2, allPoints));
            }

            return triangles.ToArray();
        }
        /// <summary>
        /// This creates a geometry that has the TextureCoordinates collection filled out
        /// </summary>
        /// <remarks>
        /// For best results, the dimensions of the mesh should be roughly the same as the image (and roughly square).  If not, large amounts
        /// of the edges of the mesh will stay transparent
        /// 
        /// NOTE: You must set "ImageBrush.ViewportUnits = BrushMappingMode.Absolute" to honor these texture mappings.  If you
        /// leave it with the default of BrushMappingMode.RelativeToBoundingBox, wpf does it's own thing and the edges of the image
        /// will likely fall off the geometry
        /// 
        /// TODO: Take in a zoom property
        /// </remarks>
        /// <param name="textureAspectRatio">
        /// Measure the image's width/height before calling this function
        /// 
        /// This way the image will map to the curve and preserve the aspect ratio.  This function also makes sure the entire image fits on
        /// the mesh.  Which means some of the geometry outside the image will be transparent
        /// </param>
        public static MeshGeometry3D GetBezierMesh_MeshGeometry3D(BezierSegment3D_wpf[][] horizontals, int horzCount, int vertCount, double controlPointPercent = .5, double textureAspectRatio = 1, bool invertY = true, double zoom = 1)
        {
            if (horzCount < 3 || vertCount < 3)
                throw new ArgumentException($"horzCount and vertCount need to be at least 3.  horzCount={horzCount}, vertCount={vertCount}");

            else if (horzCount % 2 != 1 || vertCount % 2 != 1)
                throw new ArgumentException($"horzCount and vertCount both need to be odd numbers (because the mesh's center needs to fall on an actual point).  horzCount={horzCount}, vertCount={vertCount}");

            int horzCount_centers = (horzCount * 2) - 1;
            int vertCount_centers = (vertCount * 2) - 1;

            var verticals = GetVerticalSamples(horizontals, horzCount_centers, vertCount_centers, controlPointPercent);

            //NOTE: There are more points here than the triangles will use.  But it keeps the math simpler to keep a square grid
            Point3D[] allPoints = verticals.
                SelectMany(o => o).
                ToArray();

            VectorInt2 center = new VectorInt2(horzCount_centers / 2, vertCount_centers / 2);

            var distances = GetDistancesFromPoint(center, allPoints, horzCount_centers, vertCount_centers);

            #region build transform

            double maxX = distances.lines.
                Where(o => o.IsHorizontal).
                Where(o => o.ToIndex.X == 0 || o.ToIndex.X == horzCount_centers - 1).
                Min(o => Math.Abs(o.DistSum));

            double maxY = distances.lines.
                Where(o => !o.IsHorizontal).
                Where(o => o.ToIndex.Y == 0 || o.ToIndex.Y == vertCount_centers - 1).
                Min(o => Math.Abs(o.DistSum));

            maxX *= 2;      // aspect ratio deals with width, and maxX and Y are only have width, half height.
            maxY *= 2;

            double aspect = maxX / maxY;

            double scaleX = 1d;
            double scaleY = 1d;
            if (aspect.IsNearValue(textureAspectRatio))
            {
                scaleX = 1 / maxX;
                scaleY = 1 / maxY;
            }
            else if (aspect > textureAspectRatio)
            {
                scaleX = 1 / (maxX / (aspect / textureAspectRatio));
                scaleY = 1 / maxY;
            }
            else
            {
                scaleX = 1 / maxX;
                scaleY = 1 / maxY / (aspect / textureAspectRatio);
            }

            scaleX /= zoom;
            scaleY /= zoom;

            TransformGroup textureTransform = new TransformGroup();
            textureTransform.Children.Add(new ScaleTransform(scaleX, scaleY));      // turn -maxX to maxX into -.5 to .5
            textureTransform.Children.Add(new TranslateTransform(.5, .5));      // distances.lengths is centered at (.5,.5).  TextureCoordinates needs 0 to 1

            #endregion

            MeshGeometry3D retVal = new MeshGeometry3D();

            for (int cntr = 0; cntr < allPoints.Length; cntr++)
            {
                retVal.Positions.Add(allPoints[cntr]);

                Point point = textureTransform.Transform(distances.lengths[cntr].ToPoint());
                if (invertY)
                    point = new Point(point.X, 1 - point.Y);

                retVal.TextureCoordinates.Add(point);
            }

            foreach (var vertex in IterateTrianglePoints(horzCount_centers, vertCount_centers))
            {
                retVal.TriangleIndices.Add(vertex.index0);
                retVal.TriangleIndices.Add(vertex.index1);
                retVal.TriangleIndices.Add(vertex.index2);
            }

            #region DEBUG DRAW

            //double LINE = .01;

            //var window = new Debug3DWindow();

            ////TODO: Come up with more distinct colors
            //var getColor = new Func<double, Color>(d =>
            //{
            //    if (d >= 0 && d <= 1)
            //    {
            //        return UtilityWPF.AlphaBlend(Colors.White, Colors.Black, d);
            //    }
            //    else
            //    {
            //        d = d < 0 ? -d : d - 1;
            //        return UtilityWPF.AlphaBlend(Colors.Red, Colors.Gray, UtilityCore.GetScaledValue_Capped(0, 1, 0, 2, d));
            //    }
            //});

            //foreach (var line in distances.lines)
            //{
            //    Point textureCoord = new Point(line.DistSum, line.DistSum);     // the lines are either horizontal or vertical (so there should be twice as many distances.lines as distances.lengths)
            //    textureCoord = textureTransform.Transform(textureCoord);
            //    Color color = getColor(line.IsHorizontal ? textureCoord.X : textureCoord.Y);

            //    window.AddLine(line.From, line.To, LINE, color);
            //}

            //window.AddText($"max X: {maxX.ToStringSignificantDigits(3)}");
            //window.AddText($"max Y: {maxY.ToStringSignificantDigits(3)}");
            //window.AddText("");
            //window.AddText($"scale X: {scaleX.ToStringSignificantDigits(3)}");
            //window.AddText($"scale Y: {scaleY.ToStringSignificantDigits(3)}");

            //window.Show();

            #endregion

            return retVal;
        }

        #region Private Methods

        private static BezierSegment3D_wpf[] GetBezierSegments_Closed(Point3D[] ends, double along = .25)
        {
            //NOTE: The difference between closed and open is closed has one more segment that loops back to zero (and a control point for point zero)

            // Precalculate the control points
            var controls = new (Point3D, Point3D)[ends.Length - 1];

            for (int i = 1; i < ends.Length; i++)
            {
                int lastIndex = i == ends.Length - 1 ? 0 : i + 1;

                var adjustedAlong = GetAdjustedRatios(ends[i - 1], ends[i], ends[lastIndex], along);

                controls[i - 1] = GetControlPoints_Middle(ends[i - 1], ends[i], ends[lastIndex], adjustedAlong.Item1, adjustedAlong.Item2);
            }

            var adjustedAlong2 = GetAdjustedRatios(ends[ends.Length - 1], ends[0], ends[1], along);
            var extraControl = GetControlPoints_Middle(ends[ends.Length - 1], ends[0], ends[1], adjustedAlong2.Item1, adjustedAlong2.Item2);      // loop back

            // Build the return segments
            BezierSegment3D_wpf[] retVal = new BezierSegment3D_wpf[ends.Length];

            for (int i = 0; i < ends.Length; i++)
            {
                Point3D? ctrl0 = i == 0 ? extraControl.Item2 : controls[i - 1].Item2;
                Point3D? ctrl1 = i == ends.Length - 1 ? extraControl.Item1 : controls[i].Item1;

                int lastIndex = i == ends.Length - 1 ? 0 : i + 1;

                retVal[i] = new BezierSegment3D_wpf(i, lastIndex, UtilityCore.Iterate<Point3D>(ctrl0, ctrl1).ToArray(), ends);
            }

            return retVal;
        }
        private static BezierSegment3D_wpf[] GetBezierSegments_Open(Point3D[] ends, double along = .25)
        {
            // Precalculate the control points
            var controls = new (Point3D, Point3D)[ends.Length - 2];

            for (int i = 1; i < ends.Length - 1; i++)
            {
                var adjustedAlong = GetAdjustedRatios(ends[i - 1], ends[i], ends[i + 1], along);

                controls[i - 1] = GetControlPoints_Middle(ends[i - 1], ends[i], ends[i + 1], adjustedAlong.Item1, adjustedAlong.Item2);
            }

            // Build the return segments
            BezierSegment3D_wpf[] retVal = new BezierSegment3D_wpf[ends.Length - 1];

            for (int i = 0; i < ends.Length - 1; i++)
            {
                Point3D? ctrl0 = i == 0 ? (Point3D?)null : controls[i - 1].Item2;
                Point3D? ctrl1 = i == ends.Length - 2 ? (Point3D?)null : controls[i].Item1;

                retVal[i] = new BezierSegment3D_wpf(i, i + 1, UtilityCore.Iterate<Point3D>(ctrl0, ctrl1).ToArray(), ends);
            }

            return retVal;
        }

        private static Vector3D? GetControlPoints_Middle_ControlLine(Vector3D dir21, Vector3D dir23)
        {
            // Get the angle between the two directions
            double angle = Vector3D.AngleBetween(dir21, dir23);
            if (Double.IsNaN(angle))
                return null;

            Vector3D axis = Vector3D.CrossProduct(dir21, dir23);
            if (axis.IsNearZero())
            {
                if (angle.IsNearValue(180))
                {
                    // The two lines are colinear.  Can't return null because the calling function will return arbitrary points which is wrong.  Come
                    // up with a random orth to one of the vectors so that the below portion of this function will choose accurate control points
                    axis = Math3D.GetArbitraryOrthogonal(dir21);
                }
                else
                {
                    return null;
                }
            }

            // Get the vector directly between the two directions
            Vector3D between = dir21.GetRotatedVector(axis, angle / 2d);

            // Now get the vector that is orthogonal to that between vector.  This is the line that
            // the control points will be along
            return Vector3D.CrossProduct(between, axis);        // length doesn't really matter for this.  It could also point in the exact opposite direction, and that wouldn't matter
        }

        //TODO: instead of a constant along percent, have an option to set along based on dot product:
        //  dot=0: use along
        //  dot=1: use along_min
        //
        // This will help really spiky segments to not be so loopy
        private static (double, double) GetAdjustedRatios(Point3D p1, Point3D p2, Point3D p3, double along)
        {
            Vector3D v12 = p2 - p1;
            Vector3D v23 = p3 - p2;

            double length12 = v12.Length;
            double length23 = v23.Length;

            v12 = v12 / length12;
            v23 = v23 / length23;

            // Adjust at extreme angles
            double dot = Vector3D.DotProduct(v12, v23);
            if (dot < -0.9)
                along = UtilityMath.GetScaledValue(along / 3, along, -1, -0.9, dot);        // pinched.  need to reduce so it doesn't get so loopy

            else if (dot > 0.25)
                along = UtilityMath.GetScaledValue(along, along * 2, 0.25, 1, dot);      // obtuse.  expanding so it becomes a smoother curve

            if (along > 0.5)
                along = 0.5;        // if length goes beyond midpoint, the final curve looks bad

            // The shorter segment gets the full amount, and the longer segment gets an adjusted amount

            if (length12.IsNearValue(length23))
                return (along, along);

            else if (length12 < length23)
                return (along, along * (length12 / length23));

            else
                return (along * (length23 / length12), along);
        }

        private static Point3D[][] GetVerticalSamples(BezierSegment3D_wpf[][] horizontals, int horzCount, int vertCount, double controlPointPercent)
        {
            // Get samples of the horizontals
            Point3D[][] horizontalPoints = horizontals.
                Select(o => GetPoints(horzCount, o)).
                ToArray();

            // Get samples of the verticals (these are the final points)
            return GetVerticalSamples(horizontalPoints, horzCount, vertCount, controlPointPercent);
        }
        private static Point3D[][] GetVerticalSamples(Point3D[][] horizontals, int horzCount, int vertCount, double controlPointPercent)
        {
            List<Point3D[]> retVal = new List<Point3D[]>();

            for (int h = 0; h < horzCount; h++)
            {
                // Get the points from each of the horizontal lines at this index
                Point3D[] samples = horizontals.
                    Select(o => o[h]).
                    ToArray();

                if (samples.Length == vertCount)
                {
                    // It would be rare that the number of vertical points requested is the same as the number of horizontal
                    // stripes passed in as control points.  It's more probable that the horizontal stripes are just a rough set
                    // of control points, and they are asking for a higher resolution of sample points within the mesh
                    retVal.Add(samples);
                }
                else
                {
                    // Turn those sample points into a vertical bezier
                    BezierSegment3D_wpf[] vertSegments = GetBezierSegments(samples, controlPointPercent);

                    Point3D[] vertLine = GetPoints(vertCount, vertSegments);

                    retVal.Add(vertLine);
                }
            }

            return retVal.ToArray();
        }

        private static (Vector[] lengths, BezierMeshSamples_Distance_wpf[] lines) GetDistancesFromPoint(VectorInt2 center, Point3D[] points, int horzCount, int vertCount)
        {
            Vector[] lengths = new Vector[points.Length];
            var lines = new List<BezierMeshSamples_Distance_wpf>();

            // Horizontal passes
            for (int y = 0; y < vertCount; y++)
            {
                lines.AddRange(DoPass_X_atY(new AxisFor(Axis.X, center.X, horzCount - 2), y, lengths, points, horzCount, vertCount));
                lines.AddRange(DoPass_X_atY(new AxisFor(Axis.X, center.X, 1), y, lengths, points, horzCount, vertCount));
            }

            // Vertical passes
            for (int x = 0; x < horzCount; x++)
            {
                lines.AddRange(DoPass_Y_atX(new AxisFor(Axis.Y, center.Y, vertCount - 2), x, lengths, points, horzCount, vertCount));
                lines.AddRange(DoPass_Y_atX(new AxisFor(Axis.Y, center.Y, 1), x, lengths, points, horzCount, vertCount));
            }

            return (lengths, lines.ToArray());
        }
        private static BezierMeshSamples_Distance_wpf[] DoPass_X_atY(AxisFor axisX, int y, Vector[] lengths, Point3D[] points, int horzCount, int vertCount)
        {
            var retVal = new List<BezierMeshSamples_Distance_wpf>();

            foreach (int x in axisX.Iterate())
            {
                int index0 = (x * vertCount) + y;
                int index1 = ((x + axisX.Increment) * vertCount) + y;

                Vector3D line = points[index1] - points[index0];

                double lineLen = line.Length * (axisX.IsPos ? 1 : -1);      // lines going left should have a negative length
                lengths[index1].X = lengths[index0].X + lineLen;

                retVal.Add(new BezierMeshSamples_Distance_wpf()
                {
                    From = points[index0],
                    To = points[index1],
                    FromIndex = new VectorInt2(x, y),
                    ToIndex = new VectorInt2(x + axisX.Increment, y),
                    DistSegment = lineLen,
                    DistSum = lengths[index1].X,
                    IsHorizontal = true,
                });
            }

            return retVal.ToArray();
        }
        private static BezierMeshSamples_Distance_wpf[] DoPass_Y_atX(AxisFor axisY, int x, Vector[] lengths, Point3D[] points, int horzCount, int vertCount)
        {
            var retVal = new List<BezierMeshSamples_Distance_wpf>();

            foreach (int y in axisY.Iterate())
            {
                int index0 = (x * vertCount) + y;
                int index1 = (x * vertCount) + y + axisY.Increment;

                Vector3D line = points[index1] - points[index0];

                double lineLen = line.Length * (axisY.IsPos ? 1 : -1);      // lines going toward 0 should have negative length
                lengths[index1].Y = lengths[index0].Y + lineLen;

                retVal.Add(new BezierMeshSamples_Distance_wpf()
                {
                    From = points[index0],
                    To = points[index1],
                    FromIndex = new VectorInt2(x, y),
                    ToIndex = new VectorInt2(x, y + axisY.Increment),
                    DistSegment = lineLen,
                    DistSum = lengths[index1].Y,
                    IsHorizontal = false,
                });
            }

            return retVal.ToArray();
        }

        private static double GetInputForOutput(double output, (double input, double output)[] percents)
        {
            if (output <= 0)
                return 0;
            else if (output >= 1)
                return 1;

            for (int i = 0; i < percents.Length; i++)
            {
                if (output > percents[i].output)
                    continue;

                return UtilityMath.GetScaledValue(percents[i - 1].input, percents[i].input, percents[i - 1].output, percents[i].output, output);
            }

            throw new ApplicationException($"Couldn't find input for output: {output}");
        }

        private static (double total_len, double[] cumulative_lengths) GetCumulativeLengths(BezierSegment3D_wpf[] segments)
        {
            // Get the total length of the curve
            double total_len = 0;
            double[] cumulative_lengths = new double[segments.Length + 1];

            for (int i = 1; i < segments.Length + 1; i++)
            {
                total_len += segments[i - 1].Length_quick;
                cumulative_lengths[i] = cumulative_lengths[i - 1] + segments[i - 1].Length_quick;
            }

            return (total_len, cumulative_lengths);
        }

        /// <summary>
        /// This walks through the list of desired percents and returns percents to use that when applied to the bezier
        /// will be that desired percent
        /// </summary>
        /// <remarks>
        /// This is a loop for optimization reasons, which requires the percents to be streamed in ascending order
        /// </remarks>
        private static IEnumerable<NormalizedPosPointer> ConvertToNormalizedPositions(IEnumerable<double> total_percents, BezierSegment3D_wpf[] segments)
        {
            var len = GetCumulativeLengths(segments);

            double prev_percent = 0;
            int segment_index = 0;
            int sample_index = 0;

            foreach (double percent in total_percents)
            {
                if (percent < prev_percent)
                    throw new ApplicationException($"The percents must be passed in ascending order.  current: {percent}, prev: {prev_percent}");

                prev_percent = percent;

                // Get the location along the entire path
                double portion_total_len = len.total_len * percent;

                // Advance to the appropriate segment
                while (len.cumulative_lengths[segment_index + 1] < portion_total_len)
                {
                    segment_index++;
                }

                // Get the percent of the current segment
                double local_len = portion_total_len - len.cumulative_lengths[segment_index];
                double local_percent_desired = local_len / segments[segment_index].Length_quick;
                double local_percent_actual = GetInputForOutput(local_percent_desired, segments[segment_index].Percents);

                yield return new NormalizedPosPointer()
                {
                    Desired_Index = sample_index,
                    Total_Percent = percent,
                    Segment_Index = segment_index,
                    Segment_Local_Percent = local_percent_actual,
                };

                sample_index++;
            }
        }
        private static IEnumerable<NormalizedPosPointer> ConvertToNormalizedPositions(IEnumerable<(int segment_index, double percent_along_segment)> local_percents, BezierSegment3D_wpf[] segments)
        {
            var len = GetCumulativeLengths(segments);

            int prev_index = 0;
            double prev_percent = 0;
            int sample_index = 0;

            foreach (var percent in local_percents)
            {
                if (percent.segment_index < prev_index || (percent.segment_index == prev_index && percent.percent_along_segment < prev_percent))
                    throw new ApplicationException($"The percents must be passed in ascending order.  current: {percent.segment_index} - {percent.percent_along_segment}, prev: {prev_index} - {prev_percent}");

                prev_index = percent.segment_index;
                prev_percent = percent.percent_along_segment;

                double from_len = len.cumulative_lengths[percent.segment_index];
                double to_len = len.cumulative_lengths[percent.segment_index + 1];      // there is one more entry in len.cumulative_lengths.  Element 0 is always 0

                double lerp_len = UtilityMath.LERP(from_len, to_len, percent.percent_along_segment);

                yield return new NormalizedPosPointer()
                {
                    Desired_Index = sample_index,
                    Total_Percent = lerp_len / len.total_len,
                    Segment_Index = percent.segment_index,
                    Segment_Local_Percent = percent.percent_along_segment,
                };

                sample_index++;
            }

        }

        public static CurvatureSample[] GetCurvatureHeatmap(BezierSegment3D_wpf[] beziers)
        {
            var heatmap = new List<CurvatureSample>();

            heatmap.Add(new CurvatureSample()        // having endpoints at 0 and 1 makes processing these easier
            {
                Dist_From_NegOne = 0,
                Dot = -1,
                Percent_Along_Segment = 0,
                Point = beziers[0].EndPoint0,
                SegmentIndex = 0,
                Percent_Total = 0,
            });

            for (int i = 0; i < beziers.Length; i++)
            {
                if (i > 0)
                {
                    // Stitch last segment of prev bezier segment with first of current
                    // NOTE: beziers[i - 1].Samples[^1].Point == beziers[i].Samples[0].Point == beziers[i - 1].EndPoint1 == beziers[i].EndPoint0
                    heatmap.Add(GetCurvatureHeatmap_Item(
                        beziers[i - 1].Samples[^2].Point,
                        beziers[i].EndPoint0,
                        beziers[i].Samples[1].Point,
                        beziers[i - 1].Lengths[^1],
                        beziers[i].Lengths[0],
                        i,
                        0));
                }

                // samples is 12
                // lengths is 11
                // heatmap is 10

                var samples = beziers[i].Samples;
                double[] lengths = beziers[i].Lengths;

                for (int j = 1; j < samples.Length - 1; j++)
                {
                    heatmap.Add(GetCurvatureHeatmap_Item(samples[j - 1].Point, samples[j].Point, samples[j + 1].Point, lengths[j - 1], lengths[j], i, samples[j].Percent_Along));
                }
            }

            heatmap.Add(new CurvatureSample()
            {
                Dist_From_NegOne = 0,
                Dot = -1,
                Percent_Along_Segment = 1,
                Point = beziers[^1].EndPoint1,
                SegmentIndex = beziers.Length - 1,
                Percent_Total = 1,
            });

            // Post Processing
            var retVal = GetCurvatureHeatmap_ApplyScore(heatmap.ToArray());
            retVal = GetCurvatureHeatmap_ApplyTotalPercent(retVal, beziers);

            return retVal;
        }
        private static CurvatureSample GetCurvatureHeatmap_Item(Point3D point_left, Point3D point_middle, Point3D point_right, double len_left, double len_right, int segment_index, double percent_middle)
        {
            Vector3D left = point_left - point_middle;
            Vector3D right = point_right - point_middle;

            double dot = Vector3D.DotProduct(left / len_left, right / len_right);
            double dist_from_negone = 1 + dot;

            return new CurvatureSample()
            {
                SegmentIndex = segment_index,
                Percent_Along_Segment = percent_middle,

                Percent_Total = -1,     // this will be populated in a post pass

                Point = point_middle,

                Dot = dot,
                Dist_From_NegOne = dist_from_negone,
            };
        }
        private static CurvatureSample[] GetCurvatureHeatmap_ApplyScore(CurvatureSample[] heatmap)
        {
            double min = heatmap.Min(o => o.Dist_From_NegOne);
            double max = heatmap.Max(o => o.Dist_From_NegOne);

            // For now, just flip it
            return heatmap.
                Select(o => o with
                {
                    Weight = UtilityMath.GetScaledValue(0, 1, max, min, o.Dist_From_NegOne),
                }).
                ToArray();
        }
        private static CurvatureSample[] GetCurvatureHeatmap_ApplyTotalPercent(CurvatureSample[] heatmap, BezierSegment3D_wpf[] beziers)
        {
            var local_percents = heatmap.
                Select(o => (o.SegmentIndex, o.Percent_Along_Segment)).
                ToArray();

            var total_percents = BezierUtil.ConvertToNormalizedPositions(local_percents, beziers).
                ToArray();

            return Enumerable.Range(0, heatmap.Length).
                Select(o => heatmap[o] with
                {
                    Percent_Total = total_percents[o].Total_Percent,
                }).
                ToArray();
        }

        private static void AddCountToSegment(int[] counts, AdjustmentDensity[] densities)
        {
            // In this case, add to the one with the lowest density
            var best = densities.
                OrderBy(o => o.Density_Current).
                ToArray();

            #region NOPE

            //var best = densities.
            //    OrderBy(o => o.Density_Plus).
            //    ToArray();

            //var projections = Enumerable.Range(0, densities.Length).
            //    Select(o => densities.
            //        Select(p => new
            //        {
            //            item = p,
            //            density = p.Index == o ?
            //                p.Density_Plus :
            //                p.Density_Current,
            //        }).
            //        OrderBy(p => p.density).
            //        ToArray()).
            //    Select((o,i) => new
            //    {
            //        index = i,
            //        count_prev = counts[i],
            //        count_new = counts[i] + 1,
            //        projected_densities = o,
            //        gap = o[^1].density - o[0].density,
            //    }).
            //    OrderBy(o => o.gap).
            //    ToArray();


            //var window = new Debug3DWindow();

            //var graphs = projections.
            //    Select(o => Debug3DWindow.GetGraph(o.projected_densities.Select(p => p.density).ToArray(), o.index.ToString())).
            //    ToArray();

            //window.AddGraphs(graphs, new Point3D(), 1);

            //window.Show();

            //counts[projections[0].index]++;

            #endregion

            counts[best[0].Index]++;
        }
        private static void RemoveCountFromSegment(int[] counts, AdjustmentDensity[] densities)
        {
            #region NOPE

            //var best = densities.
            //    OrderByDescending(o => o.Density_Current).
            //    ToArray();

            // In this case, use the one that after removing, it's still the highest density (it's the segment that will have the least impact of removal)
            //NO: need to use aspects of the projected query
            //var best = densities.
            //    OrderByDescending(o => o.Density_Minus).        
            //    ToArray();

            //counts[best[0].Index]--;



            //var projections = Enumerable.Range(0, densities.Length).
            //    Select(o => densities.
            //        Select(p => new
            //        {
            //            item = p,
            //            density = p.Index == o ?
            //                p.Density_Minus :
            //                p.Density_Current,
            //        }).
            //        OrderByDescending(p => p.density).
            //        ToArray()).
            //    Select((o, i) => new
            //    {
            //        index = i,
            //        count_prev = counts[i],
            //        count_new = counts[i] - 1,
            //        projected_densities = o,
            //        gap = o[0].density - o[^1].density,
            //    }).
            //    OrderBy(o => o.gap).
            //    ToArray();


            //var window = new Debug3DWindow();

            //var graphs = projections.
            //    Select(o => Debug3DWindow.GetGraph(o.projected_densities.Select(p => p.density).ToArray(), o.index.ToString())).
            //    ToArray();

            //window.AddGraphs(graphs, new Point3D(), 1);

            //window.Show();

            //counts[projections[0].index]--;

            #endregion

            // Remove the index that will have the least impact
            var projections = Enumerable.Range(0, densities.Length).
                Select(o => densities.
                    Select(p => new
                    {
                        item = p,
                        density = p.Index == o ?
                            p.Density_Minus :
                            p.Density_Current,
                    }).
                    OrderByDescending(p => p.density).
                    ToArray()).
                Select((o, i) => new
                {
                    index = i,
                    projected_densities = o,        // this is what the densities look like with that single index removed
                    lowest_density = o.Min(p => p.density),
                }).
                OrderByDescending(o => o.lowest_density).
                ToArray();

            counts[projections[0].index]--;
        }

        private static int[] EnsureCountsHaveEnds(int[] counts, Func<int[], AdjustmentDensity[]> getDensities)
        {
            int[] retVal = counts.ToArray();

            bool first = false;
            if (retVal[0] == 0)
            {
                RemoveCountFromSegment(retVal, getDensities(retVal));
                first = true;
            }

            // There's no need to add to the last, since in GetSamples, it adds one to all but the first segment
            //bool last = false;
            //if (retVal.Length > 0 && retVal[^1] == 0)
            //{
            //    RemoveCountFromSegment(retVal, getDensities(retVal));
            //    last = true;
            //}

            if (first)
                retVal[0]++;

            //if(last)
            //    retVal[^1]++;

            return retVal;
        }

        private static Point3D[] GetSamples(BezierSegment3D_wpf[] beziers, int[] counts)        //, bool is_closed)
        {
            var retVal = new List<Point3D>();

            for (int i = 0; i < beziers.Length; i++)
            {
                int count_adjusted = counts[i];
                bool take_first = true;

                if (i > 0)  // || is_closed)        // turns out the first point of the first segment is needed
                {
                    // The first point of i is the same as the last point of i-1.  If this is closed, then the last
                    // point of ^1 will be used as the first point of 0
                    count_adjusted++;
                    take_first = false;
                }

                if (count_adjusted == 0)        // this will happen if a segment is so short compared to other segments that it becomes a rounding error
                    continue;

                if (count_adjusted == 1)
                {
                    if (i == 0)
                        retVal.Add(beziers[i].EndPoint0);
                    else if (i == beziers.Length - 1)
                        retVal.Add(beziers[i].EndPoint1);
                    else
                        retVal.Add(Math3D.GetCenter(beziers[i].EndPoint0, beziers[i].EndPoint1));       // just take the average of the two

                    continue;
                }

                Point3D[] points = BezierUtil.GetPoints(count_adjusted, beziers[i]);

                if (take_first)
                    retVal.AddRange(points);
                else
                    retVal.AddRange(points.Skip(1));
            }

            return retVal.ToArray();
        }

        #endregion

        #region temp public - keeping around for tester windows

        public static Point3D[] GetPoints_PinchImproved(int count, BezierSegment3D_wpf[] segments, PathSnippet[] pinch_map)
        {
            Point3D[] retVal = new Point3D[count];

            retVal[0] = segments[0].EndPoint0;
            retVal[count - 1] = segments[segments.Length - 1].EndPoint1;        //NOTE: If the segment is a closed curve, this is the same point as retVal[0].  May want a boolean that tells whether the last point should be replicated

            var samples = Enumerable.Range(0, count - 2).       // for (int i = 1; i < count - 1; i++)
                Select(o => (double)(o + 1) / (count - 1)).     // {double totalPercent = (double)i / (count - 1);
                Select(o => ApplyPinchMap2(o, pinch_map));       // run this point through the pinch map (the map exaggerates how much total percent goes to pinch points, less goes to flat spots)

            foreach (var sample in ConvertToNormalizedPositions(samples, segments))
            {
                // Calculate the bezier point
                retVal[sample.Desired_Index + 1] = GetPoint(sample.Segment_Local_Percent, segments[sample.Segment_Index].Combined);
            }

            return retVal;
        }
        private static double ApplyPinchMap1(double percent, PathSnippet[] pinch_map)
        {
            if (percent <= 0)
                return 0;
            else if (percent >= 1)
                return 1;

            for (int i = 0; i < pinch_map.Length; i++)
            {
                // find the exact spot along the pinch map
                if (percent >= pinch_map[i].From_Percent_In && percent <= pinch_map[i].To_Percent_In)
                {
                    double local_percent = (percent - pinch_map[i].From_Percent_In) / (pinch_map[i].To_Percent_In - pinch_map[i].From_Percent_In);

                    // convert to the corresponding output
                    return UtilityMath.LERP(pinch_map[i].From_Percent_Out, pinch_map[i].To_Percent_Out, local_percent);
                }
            }

            throw new ApplicationException($"Couldn't find percent: {percent}");
        }
        private static double ApplyPinchMap2(double percent, PathSnippet[] pinch_map)
        {

            //NOTE: this method is correct, but uses the In/Out backward from how they are intended
            //TODO: the worker that builds these snippets needs to play with In instead of Out


            if (percent <= 0)
                return 0;
            else if (percent >= 1)
                return 1;

            for (int i = 0; i < pinch_map.Length; i++)
            {
                // find the exact spot along the pinch map
                if (percent >= pinch_map[i].From_Percent_Out && percent <= pinch_map[i].To_Percent_Out)
                {
                    double local_percent = (percent - pinch_map[i].From_Percent_Out) / (pinch_map[i].To_Percent_Out - pinch_map[i].From_Percent_Out);

                    // convert to the corresponding output
                    return UtilityMath.LERP(pinch_map[i].From_Percent_In, pinch_map[i].To_Percent_In, local_percent);
                }
            }

            throw new ApplicationException($"Couldn't find percent: {percent}");
        }

        public static Point3D[] GetPoints_PinchImproved2(int count, BezierSegment3D_wpf[] segments, BezierSegment3D_wpf stretch_map)
        {
            Point3D[] retVal = new Point3D[count];

            retVal[0] = segments[0].EndPoint0;
            retVal[count - 1] = segments[segments.Length - 1].EndPoint1;        //NOTE: If the segment is a closed curve, this is the same point as retVal[0].  May want a boolean that tells whether the last point should be replicated

            var samples = Enumerable.Range(0, count - 2).       // for (int i = 1; i < count - 1; i++)
                Select(o => (double)(o + 1) / (count - 1)).     // {double totalPercent = (double)i / (count - 1);
                Select(o => ApplyStretchMap(o, stretch_map));       // run this point through the pinch map (the map exaggerates how much total percent goes to pinch points, less goes to flat spots)

            foreach (var sample in ConvertToNormalizedPositions(samples, segments))
            {
                // Calculate the bezier point
                retVal[sample.Desired_Index + 1] = GetPoint(sample.Segment_Local_Percent, segments[sample.Segment_Index].Combined);
            }

            return retVal;
        }
        private static double ApplyStretchMap(double percent, BezierSegment3D_wpf stretch_map)
        {
            return GetPoint(percent, stretch_map.Combined).Y;
        }

        #endregion
    }

    #region class: BezierSegment3D_wpf

    public class BezierSegment3D_wpf       // wpf already has a BezierSegment
    {
        public record SampleInfo
        {
            public double Percent_Along { get; init; }
            public Point3D Point { get; init; }
        }

        #region Constructor

        public BezierSegment3D_wpf(Point3D end0, Point3D end1, Point3D[] controlPoints)
            : this(0, 1, controlPoints, new[] { end0, end1 }) { }

        public BezierSegment3D_wpf(int endIndex0, int endIndex1, Point3D[] controlPoints, Point3D[] allEndPoints, double sample_count_mult = 1)
        {
            EndIndex0 = endIndex0;
            EndIndex1 = endIndex1;
            ControlPoints = controlPoints;
            AllEndPoints = allEndPoints;

            Combined = UtilityCore.Iterate<Point3D>(EndPoint0, ControlPoints, EndPoint1).ToArray();

            var analyze = AnalyzeCurve(allEndPoints[endIndex0], allEndPoints[endIndex1], controlPoints, Combined, GetSampleCount(sample_count_mult));
            Length_quick = analyze.length_quick;
            Percents = analyze.percents;
            Lengths = analyze.lengths;
            Samples = analyze.samples.
                Select((o, i) => new SampleInfo()
                {
                    Percent_Along = (double)i / analyze.samples.Length,
                    Point = o,
                }).
                ToArray();
        }

        #endregion

        public readonly Point3D[] AllEndPoints;

        public readonly int EndIndex0;
        public readonly int EndIndex1;

        public Point3D EndPoint0 => AllEndPoints[EndIndex0];
        public Point3D EndPoint1 => AllEndPoints[EndIndex1];

        public readonly Point3D[] ControlPoints;

        /// <summary>
        /// This is { EndPoint0, ControlPoints, EndPoint1 }
        /// </summary>
        public readonly Point3D[] Combined;

        /// <summary>
        /// This is a rough approximation of the length of the bezier
        /// </summary>
        public readonly double Length_quick;

        /// <summary>
        /// A call to BezierUtil.GetPoint with an input percent will result in a point that is some percent along the
        /// actual curved path
        /// </summary>
        public readonly (double input, double output)[] Percents;

        public readonly double[] Lengths;

        public readonly SampleInfo[] Samples;

        #region Public Methods

        public BezierSegment3D_wpf ToReverse()
        {
            Point3D[] controlPoints = null;
            if (this.ControlPoints != null)
            {
                controlPoints = this.ControlPoints.Reverse().ToArray();
            }

            return new BezierSegment3D_wpf(this.EndIndex1, this.EndIndex0, controlPoints, this.AllEndPoints);      // no need to reverse AllEndPoints, just the indexes
        }

        #endregion

        #region Private Methods

        private static int GetSampleCount(double? mult)
        {
            const int DEFAULT = 12;

            return (DEFAULT * (mult ?? 1)).ToInt_Ceiling();
        }

        private static ((double input, double output)[] percents, double[] lengths, double length_quick, Point3D[] samples) AnalyzeCurve(Point3D end0, Point3D end1, Point3D[] controlPoints, Point3D[] combined, int count)
        {
            if (controlPoints == null || controlPoints.Length == 0)     // just a regular line segment
            {
                return
                (
                    new[]
                    {
                        (0d, 0d),
                        (1d, 1d),
                    },
                    new[] { (end1 - end0).Length },
                    (end1 - end0).Length,
                    new[] { end0, end1 }
                );
            }

            Point3D[] samples = BezierUtil.GetPoints(count, combined);

            double[] lengths = GetSegmentLengths(samples);

            double length_quick = lengths.Sum();

            var percents = GetInputOutputPercents(samples, lengths, length_quick);

            return (percents, lengths, length_quick, samples);
        }

        private static double[] GetSegmentLengths(Point3D[] samples)
        {
            var retVal = new double[samples.Length - 1];

            for (int i = 0; i < samples.Length - 1; i++)
            {
                retVal[i] = (samples[i + 1] - samples[i]).Length;
            }

            return retVal;
        }

        private static (double input, double output)[] GetInputOutputPercents(Point3D[] samples, double[] lengths, double length_quick)
        {
            var retVal = new (double, double)[samples.Length];

            retVal[0] = (0, 0);
            retVal[^1] = (1, 1);

            double input_inc = 1d / (samples.Length - 1);
            double sum_input = 0;
            double sum_output = 0;

            for (int i = 0; i < lengths.Length - 1; i++)        // no need to calculate the last one, it's always going to become (1,1)
            {
                sum_input += input_inc;
                sum_output += lengths[i];

                retVal[i + 1] = (sum_input, sum_output / length_quick);
            }

            return retVal;
        }

        #endregion
    }

    #endregion

    #region class: BezierMesh_wpf

    //TODO: This needs to use BezierSegment1D.  The x and y in the bezier objects are ignored, and is just wasted calculations

    /// <summary>
    /// This takes in a grid of points, and will then apply beziers to estimate locations between those control points
    /// </summary>
    public class BezierMesh_wpf
    {
        #region Constructor

        public BezierMesh_wpf(double[] axisX, double[] axisY, double[] valuesZ)
        {
            var result = ConvertToBezier(axisX, axisY, valuesZ);

            this.AxisX = result.Item1;
            this.Horizontal = result.Item2;

            this.AxisY = result.Item3;
            this.Vertical = result.Item4;
        }

        #endregion

        #region Public Properties

        public readonly double[] AxisX;
        public readonly double[] AxisY;

        public readonly BezierSegment3D_wpf[][] Horizontal;
        public readonly BezierSegment3D_wpf[][] Vertical;

        #endregion

        #region Public Methods

        public double EstimateValue(double x, double y)
        {
            var segX = FindSegment(x, this.AxisX);
            var segY = FindSegment(y, this.AxisY);

            if (new[] { segX.Item1, segX.Item2, segY.Item1, segY.Item2 }.Any(o => o < 0))
            {
                // Probably want to just assume linear.  Use the slope and length of the nearest segments
                throw new ApplicationException("TODO: Handle out of bounds requests");
            }

            Point3D lerp = BicubicInterpolation(
                this.Horizontal[segY.Item1][segX.Item1],
                this.Horizontal[segY.Item2][segX.Item1],
                this.Vertical[segX.Item1][segY.Item1],
                this.Vertical[segX.Item2][segY.Item1],
                segX.Item3,
                segY.Item3);

            return lerp.Z;
        }

        #endregion

        #region Private Methods

        private static Tuple<double[], BezierSegment3D_wpf[][], double[], BezierSegment3D_wpf[][]> ConvertToBezier(double[] axisX, double[] axisY, double[] valuesZ)
        {
            #region validate

            // X length
            if (axisX == null || axisX.Length < 2)
                throw new ArgumentException(string.Format("axisX must have at least 2 items: len={0}", axisX == null ? "null" : axisX.Length.ToString()));

            // Y length
            if (axisY == null || axisY.Length < 2)
                throw new ArgumentException(string.Format("axisY must have at least 2 items: len={0}", axisY == null ? "null" : axisY.Length.ToString()));

            // Z area
            if (valuesZ == null || valuesZ.Length != axisX.Length * axisY.Length)
                throw new ArgumentException(string.Format("valuesZ is invalid length: values={0}, axis1={1}, axis2={2}", valuesZ == null ? "null" : valuesZ.Length.ToString(), axisX.Length, axisY.Length));

            // X equality
            if (Enumerable.Range(0, axisX.Length - 1).Any(o => axisX[o].IsNearValue(axisX[o + 1])))
                throw new ArgumentException("Values can't be the same in x axis");

            // Y equality
            if (Enumerable.Range(0, axisY.Length - 1).Any(o => axisY[o].IsNearValue(axisY[o + 1])))
                throw new ArgumentException("Values can't be the same in y axis");

            #endregion

            bool isAccendingX = axisX[1] > axisX[0];
            bool isAccendingY = axisY[1] > axisY[0];

            #region validate

            // X ascending
            if (Enumerable.Range(0, axisX.Length - 1).Any(o => isAccendingX ? axisX[o + 1] < axisX[o] : axisX[o + 1] > axisX[o]))
                throw new ArgumentException("The values in axisX must all ascend or descend");

            // Y ascending
            if (Enumerable.Range(0, axisY.Length - 1).Any(o => isAccendingY ? axisY[o + 1] < axisY[o] : axisY[o + 1] > axisY[o]))
                throw new ArgumentException("The values in axisX must all ascend or descend");

            #endregion

            #region ensure ascending X

            if (!isAccendingX)
            {
                axisX = axisX.Reverse().ToArray();

                double[] newZ = new double[valuesZ.Length];

                for (int oldX = 0; oldX < axisX.Length; oldX++)
                {
                    int newX = axisX.Length - 1 - oldX;

                    for (int y = 0; y < axisY.Length; y++)
                    {
                        int yIndex = y * axisX.Length;
                        newZ[yIndex + newX] = valuesZ[yIndex + oldX];
                    }
                }

                valuesZ = newZ;
            }

            #endregion
            #region ensure ascending Y

            if (!isAccendingY)
            {
                axisY = axisY.Reverse().ToArray();

                double[] newZ = new double[valuesZ.Length];

                for (int oldY = 0; oldY < axisY.Length; oldY++)
                {
                    int newY = axisY.Length - 1 - oldY;

                    int yIndexOld = oldY * axisX.Length;
                    int yIndexNew = newY * axisX.Length;

                    for (int x = 0; x < axisX.Length; x++)
                    {
                        newZ[yIndexNew + x] = valuesZ[yIndexOld + x];
                    }
                }

                valuesZ = newZ;
            }

            #endregion

            BezierSegment3D_wpf[][] horizontal = new BezierSegment3D_wpf[axisY.Length][];     // there is a horizontal set for each y
            BezierSegment3D_wpf[][] vertical = new BezierSegment3D_wpf[axisX.Length][];

            //TODO: Make an option of this that figures out the percent to use:
            //if you pass in .25, that is the max.
            //  if both segments are equal length then it will be .25 for each
            //  if they are different lengths, the smaller will use .25.  the larger will use the ratio of lengths*.25
            //BezierSegmentDef.GetBezierSegments();

            #region horizontal

            for (int y = 0; y < axisY.Length; y++)
            {
                int yIndex = y * axisX.Length;

                Point3D[] ends = Enumerable.Range(0, axisX.Length).
                    Select(x => new Point3D(axisX[x], axisY[y], valuesZ[yIndex + x])).
                    ToArray();

                horizontal[y] = BezierUtil.GetBezierSegments(ends);
            }

            #endregion
            #region vertical

            for (int x = 0; x < axisX.Length; x++)
            {
                Point3D[] ends = Enumerable.Range(0, axisY.Length).
                    Select(y => new Point3D(axisX[x], axisY[y], valuesZ[(y * axisX.Length) + x])).
                    ToArray();

                vertical[x] = BezierUtil.GetBezierSegments(ends);
            }

            #endregion

            return Tuple.Create(axisX, horizontal, axisY, vertical);
        }

        private static Tuple<int, int, double> FindSegment(double value, double[] axis)
        {
            // Find the two points that straddle the value
            for (int cntr = 0; cntr < axis.Length - 1; cntr++)
            {
                if (value >= axis[cntr] && value <= axis[cntr + 1])
                {
                    double percent = (value - axis[cntr]) / (axis[cntr + 1] - axis[cntr]);
                    return Tuple.Create(cntr, cntr + 1, percent);
                }
            }

            // Out of bounds
            if (value < axis[0])
            {
                return Tuple.Create(-1, 0, 0d);
            }
            else //if (value > axis[axis.Length - 1])
            {
                return Tuple.Create(axis.Length - 1, -1, 0d);
            }
        }

        #endregion

        //TODO: Put this in Math3D
        public static Point3D BicubicInterpolation(BezierSegment3D_wpf top, BezierSegment3D_wpf bottom, BezierSegment3D_wpf left, BezierSegment3D_wpf right, double percentX, double percentY)
        {
            Point3D valueTop = BezierUtil.GetPoint(percentX, top);
            Point3D valueBottom = BezierUtil.GetPoint(percentX, bottom);

            Point3D valueLeft = BezierUtil.GetPoint(percentY, left);
            Point3D valueRight = BezierUtil.GetPoint(percentY, right);

            var points = new[]
                {
                    (valueTop, 1 - percentY),
                    (valueBottom, percentY),
                    (valueLeft, 1 - percentX),
                    (valueRight, percentX),
                };

            return Math3D.GetCenter(points);
        }
    }

    #endregion


    #region record: PathSnippet

    /// <summary>
    /// This represents a transform from gap_in to gap_out
    /// </summary>
    public record PathSnippet
    {
        public double From_Percent_In { get; init; }
        public double From_Percent_Out { get; init; }
        public double To_Percent_In { get; init; }
        public double To_Percent_Out { get; init; }
    }

    #endregion
    #region record: SnippetPos

    // This is just an easier structure to move around
    public record SnippetPos
    {
        public double From { get; init; }
        public double To { get; init; }
        public double Center { get; init; }
        public double Width => To - From;
    }

    #endregion
}
