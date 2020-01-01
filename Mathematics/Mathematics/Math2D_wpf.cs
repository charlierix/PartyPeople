using Game.Core;
using Game.Math_WPF.Mathematics.Clipper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.Mathematics
{
    public static partial class Math2D
    {
        #region class: SutherlandHodgman_wpf

        /// <remarks>
        /// Put this here:
        /// http://rosettacode.org/wiki/Sutherland-Hodgman_polygon_clipping#C.23
        /// </remarks>
        private static class SutherlandHodgman_wpf
        {
            /// <summary>
            /// This clips the subject polygon against the clip polygon (gets the intersection of the two polygons)
            /// </summary>
            /// <remarks>
            /// Based on the psuedocode from:
            /// http://en.wikipedia.org/wiki/Sutherland%E2%80%93Hodgman
            /// </remarks>
            /// <param name="subjectPoly">Can be concave or convex</param>
            /// <param name="clipPoly">Must be convex</param>
            /// <returns>The intersection of the two polygons (or null)</returns>
            public static Point[] GetIntersectedPolygon(Point[] subjectPoly, Point[] clipPoly)
            {
                if (subjectPoly.Length < 3 || clipPoly.Length < 3)
                {
                    throw new ArgumentException(string.Format("The polygons passed in must have at least 3 points: subject={0}, clip={1}", subjectPoly.Length.ToString(), clipPoly.Length.ToString()));
                }

                List<Point> outputList = subjectPoly.ToList();

                // Make sure it's clockwise
                if (!Math2D.IsClockwise(subjectPoly))
                {
                    outputList.Reverse();
                }

                // Walk around the clip polygon clockwise
                foreach (Edge2D_wpf clipEdge in Math2D.IterateEdges(clipPoly, true))
                {
                    List<Point> inputList = outputList.ToList();		// clone it
                    outputList.Clear();

                    if (inputList.Count == 0)
                    {
                        // Sometimes when the polygons don't intersect, this list goes to zero.  Jump out to avoid an index out of range exception
                        break;
                    }

                    Point S = inputList[inputList.Count - 1];

                    foreach (Point E in inputList)
                    {
                        if (IsInside(clipEdge, E))
                        {
                            if (!IsInside(clipEdge, S))
                            {
                                Point? point = Math2D.GetIntersection_Line_Line(S, E, clipEdge.Point0, clipEdge.Point1.Value);
                                if (point == null)
                                {
                                    //throw new ApplicationException("Line segments don't intersect");		// may be colinear, or may be a bug
                                    return null;
                                }
                                else
                                {
                                    outputList.Add(point.Value);
                                }
                            }

                            outputList.Add(E);
                        }
                        else if (IsInside(clipEdge, S))
                        {
                            Point? point = Math2D.GetIntersection_Line_Line(S, E, clipEdge.Point0, clipEdge.Point1.Value);
                            if (point == null)
                            {
                                //throw new ApplicationException("Line segments don't intersect");		// may be colinear, or may be a bug
                                return null;        // this is hitting on a case where the subject and clip share an edge, but don't really intersect (just touch).  Plus, subject had 3 points that were colinear (plus a fourth that made it a proper polygon)
                            }
                            else
                            {
                                outputList.Add(point.Value);
                            }
                        }

                        S = E;
                    }
                }

                // Dedupe
                outputList = Math2D.DedupePoints(outputList);

                if (outputList.Count < 3)       // only one or two points is just the two polygons touching, not an intersection that creates a new polygon
                {
                    return null;
                }
                else
                {
                    return outputList.ToArray();
                }
            }

            #region Private Methods

            private static bool IsInside(Edge2D_wpf edge, Point test)
            {
                bool? isLeft = Math2D.IsLeftOf(edge, test);
                if (isLeft == null)
                {
                    // Colinear points should be considered inside
                    return true;
                }

                return !isLeft.Value;
            }

            #endregion
        }

        #endregion
        #region class: VoronoiUtil_wpf

        private static class VoronoiUtil_wpf
        {
            #region class: ControlPointStats

            private class ControlPointStats
            {
                #region Constructor

                public ControlPointStats(int index, double radius, Tuple<int, double>[] distanceToUnboundEdges)
                {
                    this.Index = index;
                    this.Radius = radius;
                    this.DistanceToUnboundEdges = distanceToUnboundEdges;
                }

                #endregion

                public readonly int Index;

                /// <summary>
                /// The radius of the control point
                /// </summary>
                public readonly double Radius;

                /// <summary>
                /// This only holds distances to the unbound edges (rays, lines)
                /// </summary>
                public readonly Tuple<int, double>[] DistanceToUnboundEdges;
            }

            #endregion
            #region class: NewCell

            private class NewCell
            {
                public NewCell(int controlPoint, int[] edges)
                {
                    this.ControlPoint = controlPoint;
                    this.Edges = edges;
                }

                public readonly int ControlPoint;
                public readonly int[] Edges;
            }

            #endregion
            #region class: VarBucket

            /// <summary>
            /// There are a lot of loose variables to pass to RebuildCell, so putting them into a class
            /// </summary>
            private class RebuildVars
            {
                #region Constructor

                public RebuildVars(VoronoiResult2D_wpf voronoi, ControlPointStats[] unboundControlPoints, int[] outsideEdgePoints, Point[] circle, double circleRadius)
                {
                    this.Voronoi = voronoi;
                    this.UnboundControlPoints = unboundControlPoints;
                    this.OutsideEdgePoints = outsideEdgePoints;
                    this.Circle = circle;
                    this.CircleRadius = circleRadius;
                }

                #endregion

                #region Public Properties

                public readonly VoronoiResult2D_wpf Voronoi;

                public readonly ControlPointStats[] UnboundControlPoints;
                public readonly int[] OutsideEdgePoints;

                public readonly Point[] Circle;
                public readonly double CircleRadius;

                public List<Point> NewEdgePoints = new List<Point>();
                public SortedList<int, int> Map_EdgePoints = new SortedList<int, int>();        // Key=old list, Value=new list

                public List<Tuple<int, int>> NewEdges = new List<Tuple<int, int>>();        // the new edges can only be line segments
                public SortedList<int, int> Map_Edges = new SortedList<int, int>();     // Key=old list, Value=new list

                #endregion

                #region Public Methods

                /// <summary>
                /// This is meant for points that copy over without being moved
                /// </summary>
                public int AddEdgePoint(int oldIndex)
                {
                    // See if this was already added
                    if (this.Map_EdgePoints.ContainsKey(oldIndex))
                    {
                        return this.Map_EdgePoints[oldIndex];
                    }

                    // It's new
                    this.NewEdgePoints.Add(this.Voronoi.EdgePoints[oldIndex]);

                    int retVal = this.NewEdgePoints.Count - 1;

                    this.Map_EdgePoints.Add(oldIndex, retVal);

                    return retVal;
                }
                /// <summary>
                /// This is for new points.  It gets duped checked with other new points (but not with the old points)
                /// </summary>
                public int AddEdgePoint(Point newPoint)
                {
                    // See if this is the same as an existing new point
                    foreach (int index in Enumerable.Range(0, this.NewEdgePoints.Count).Except(this.Map_EdgePoints.Values))
                    {
                        if (Math2D.IsNearValue(this.NewEdgePoints[index], newPoint))
                        {
                            return index;
                        }
                    }

                    // Add it new
                    this.NewEdgePoints.Add(newPoint);
                    return this.NewEdgePoints.Count - 1;
                }

                /// <summary>
                /// This is meant for edges that copy over without being changed
                /// </summary>
                public int AddEdge(int oldIndex)
                {
                    Edge2D_wpf edge = this.Voronoi.Edges[oldIndex];

                    if (edge.EdgeType != EdgeType.Segment)
                    {
                        throw new ArgumentException("This method is only meant for line segments: " + edge.EdgeType.ToString());
                    }

                    if (this.Map_Edges.ContainsKey(oldIndex))
                    {
                        // This edge has already been added
                        return this.Map_Edges[oldIndex];
                    }

                    // Make sure the points are there
                    int index0 = AddEdgePoint(edge.Index0);
                    int index1 = AddEdgePoint(edge.Index1.Value);

                    // Store it
                    this.NewEdges.Add(Tuple.Create(index0, index1));

                    int retVal = this.NewEdges.Count - 1;       // Return the location to this edge

                    this.Map_Edges.Add(oldIndex, retVal);

                    return retVal;
                }
                /// <summary>
                /// This is meant for edges that are created new.  It is up to the caller to call AddEdgePoint
                /// </summary>
                public int AddEdge(int index0, int index1)
                {
                    // Always store the lower index first, so that new edges can be compared
                    Tuple<int, int> edge = Tuple.Create(Math.Min(index0, index1), Math.Max(index0, index1));

                    // See if this is the same as an existing new edge
                    foreach (int index in Enumerable.Range(0, this.NewEdges.Count).Except(this.Map_Edges.Values))
                    {
                        //if (this.NewEdges[index] == edge)     // == doesn't work, but .Equals does
                        if (this.NewEdges[index].Equals(edge))
                        {
                            return index;
                        }
                    }

                    // It's new
                    this.NewEdges.Add(edge);
                    return this.NewEdges.Count - 1;
                }

                #endregion
            }

            #endregion

            public static VoronoiResult2D_wpf GetVoronoi(Point[] points, bool returnEdgesByPoint)
            {
                //TODO: Dedupe the points that are handed to the worker (but leave the dupes in the final return, it won't hurt anything


                //  Convert points to the internal objects (avoiding linq for speed reasons)
                Voronoi2D.Vector_ex[] vectors = new Voronoi2D.Vector_ex[points.Length];
                for (int cntr = 0; cntr < points.Length; cntr++)
                {
                    vectors[cntr] = new Voronoi2D.Vector_ex(new double[] { points[cntr].X, points[cntr].Y });
                }




                //  Do it
                var results = Voronoi2D.ComputeVoronoiGraph(vectors);
                //results = Voronoi2D.FilterVG(results, NEARZERO);      //  I don't trust this method, I'll make my own

                #region Convert edge points

                //  He doesn't include the points of infinite edges in results.Vertizes, so extract them first
                List<Point> edgePointList = new List<Point>();
                foreach (var edge in results.Edges)
                {
                    if (edge.IsInfinite)
                    {
                        edgePointList.Add(new Point(edge.FixedPoint[0], edge.FixedPoint[1]));
                    }
                }

                //  Convert the regular vertices
                edgePointList.AddRange(results.Vertizes.Select(o => new Point(o[0], o[1])));




                //throw new ApplicationException("Test that distinct works with points");
                //throw new ApplicationException("Distinct is too simple.  Do an IsNear compare");



                //Point[] edgePoints = edgePointList.Distinct().ToArray();
                Point[] edgePoints = edgePointList.ToArray();

                #endregion

                #region Convert edges

                var edgeList = new List<Edge2D_wpf>();
                foreach (var edge in results.Edges)
                {
                    if (edge.IsInfinite)
                    {
                        edgeList.Add(new Edge2D_wpf(EdgeType.Line, IndexOf(edge.FixedPoint, edgePoints), new Vector(edge.DirectionVector[0], edge.DirectionVector[1]), edgePoints));
                    }
                    else if (edge.VVertexB == Voronoi2D.VVInfinite)
                    {
                        //  Ray from A
                        edgeList.Add(new Edge2D_wpf(EdgeType.Ray, IndexOf(edge.VVertexA, edgePoints), new Vector(edge.DirectionVector[0], edge.DirectionVector[1]), edgePoints));
                    }
                    else if (edge.VVertexA == Voronoi2D.VVInfinite)
                    {
                        //  Ray from B
                        edgeList.Add(new Edge2D_wpf(EdgeType.Ray, IndexOf(edge.VVertexB, edgePoints), new Vector(edge.DirectionVector[0], edge.DirectionVector[1]), edgePoints));
                    }
                    else
                    {
                        //  Segment
                        edgeList.Add(new Edge2D_wpf(IndexOf(edge.VVertexA, edgePoints), IndexOf(edge.VVertexB, edgePoints), edgePoints));
                    }
                }


                //throw new ApplicationException("Need to dedupe the edge list as well (probably more involved than just calling distinct)");


                Edge2D_wpf[] edges = edgeList.ToArray();

                #endregion

                int[][] edgesByPoint = null;
                if (returnEdgesByPoint)
                {
                    edgesByPoint = GetEdgesByPoint(points, edges);
                }

                return new VoronoiResult2D_wpf(points, edgePoints, edges, edgesByPoint);
            }

            /// <summary>
            /// This assumes that the points are spread around in a circular disc.  It calculates how big of a radius to cap the rays, and returns
            /// a result that has only line segments that don't go beyond that radius
            /// </summary>
            public static VoronoiResult2D_wpf CapCircle(VoronoiResult2D_wpf voronoi)
            {
                if (voronoi.EdgesByControlPoint == null)
                {
                    throw new ArgumentException("The voronoi graph passed in must have EdgesByControlPoint populated");
                }

                if (voronoi.ControlPoints.Length == 0)
                {
                    return voronoi;
                }
                else if (voronoi.ControlPoints.Length == 1)
                {
                    // There is just a point.  Return the circle
                    return CapCircle_One(voronoi);
                }
                else if (voronoi.ControlPoints.Length == 2)
                {
                    // There are two points and a line.  Turn that line into two large squares, and intersect with the circle
                    return CapCircle_Two(voronoi);
                }

                // Get the points that have rays or lines (intead of fully surrounded by segments)
                ControlPointStats[] unboundPoints = GetUnboundControlPoints(voronoi);

                double maxRadius = GetMaxRadius(unboundPoints);

                int[] outsidePoints = GetOutsideEdgePoints(voronoi, maxRadius);

                // Get a circle
                Point[] circle = GetCircle(maxRadius, unboundPoints.Length * 3.5);

                RebuildVars vars = new RebuildVars(voronoi, unboundPoints, outsidePoints, circle, maxRadius);

                NewCell[] newCells = new NewCell[voronoi.ControlPoints.Length];

                for (int cntr = 0; cntr < voronoi.ControlPoints.Length; cntr++)
                {
                    newCells[cntr] = RebuildCell(cntr, vars);
                }

                return BuildResult(vars, newCells);
            }

            #region Private Methods - initial

            private static int IndexOf(Voronoi2D.Vector_ex vector, Point[] points)
            {
                const double NEARZERO = .0000001d;

                //  Try exact match
                for (int cntr = 0; cntr < points.Length; cntr++)
                {
                    if (vector[0] == points[cntr].X && vector[1] == points[cntr].Y)
                    {
                        return cntr;
                    }
                }

                //  Try close match
                for (int cntr = 0; cntr < points.Length; cntr++)
                {
                    if (vector[0] >= points[cntr].X - NEARZERO && vector[0] <= points[cntr].X + NEARZERO &&
                        vector[1] >= points[cntr].Y - NEARZERO && vector[1] <= points[cntr].Y + NEARZERO)
                    {
                        return cntr;
                    }
                }

                throw new ArgumentException("The vector isn't in the list of points");
                //return -1;
            }

            private static int[][] GetEdgesByPoint(Point[] points, Edge2D_wpf[] edges)
            {
                //  See which edges are neighbors
                int[][] touchingEdges = Edge2D_wpf.GetTouchingEdges(edges);

                int[][] retVal = new int[points.Length][];

                for (int cntr = 0; cntr < points.Length; cntr++)
                {
                    retVal[cntr] = GetEdgesByPoint_Point(points[cntr], edges, touchingEdges);
                }

                return retVal;
            }
            private static int[] GetEdgesByPoint_Point(Point point, Edge2D_wpf[] edges, int[][] touchingEdges)
            {
                //  Get the distance to each edge
                int closestIndex = GetEdgesByPoint_FindFirst(point, edges);
                if (closestIndex < 0)
                {
                    //  This will only happen if there is one point, there will be no edges
                    return new int[0];
                }

                List<int> retVal = new List<int>();
                retVal.Add(closestIndex);

                if (edges[closestIndex].EdgeType == EdgeType.Line)
                {
                    //  The only time there is a line is when there are exactly two points and one edge between them
                    return retVal.ToArray();
                }

                List<int> rays = new List<int>();
                if (edges[closestIndex].EdgeType == EdgeType.Ray)
                {
                    rays.Add(closestIndex);
                }

                while (true)
                {
                    //  Find the next closest
                    closestIndex = GetEdgesByPoint_FindNext(point, closestIndex, edges, touchingEdges, retVal, rays.Count > 0);
                    if (closestIndex < 0)
                    {
                        //  All surrounding edges have been found
                        break;
                    }

                    retVal.Add(closestIndex);

                    //  If hit ray, walk right until hit existing or hit ray
                    if (edges[closestIndex].EdgeType == EdgeType.Ray)
                    {
                        rays.Add(closestIndex);
                        if (rays.Count == 2)
                        {
                            //  There are either 0 rays or 2, so second means finished
                            break;
                        }

                        //  Set it up to start over
                        closestIndex = retVal[0];
                    }
                }

                //  Exit Function
                if (rays.Count > 0)
                {
                    return GetEdgesByPoint_Reorder(retVal.ToArray(), rays, touchingEdges);
                }
                else
                {
                    return retVal.ToArray();
                }
            }
            private static int GetEdgesByPoint_FindFirst(Point point, Edge2D_wpf[] edges)
            {
                //  For the first edge, find the one that's closest (no need to worry about the vertex of three rays being closer than
                //  an edge, the edge will always be closer than the vertex - at least for the first edge)

                int retVal = -1;
                double smallestDist = double.MaxValue;

                for (int cntr = 0; cntr < edges.Length; cntr++)
                {
                    double distance = GetClosestDistance_Edge_Point(edges[cntr], point);

                    if (distance < smallestDist)
                    {
                        retVal = cntr;
                        smallestDist = distance;
                    }
                }

                return retVal;
            }
            private static int GetEdgesByPoint_FindNext(Point point, int current, Edge2D_wpf[] edges, int[][] touchingEdges, List<int> returnList, bool hasRays)
            {
                foreach (int neighborIndex in touchingEdges[current])
                {
                    if (!hasRays && returnList.Count > 2 && neighborIndex == returnList[0])
                    {
                        //  This cell is all segments, and they've all been found (if I let this method keep running, it will still find the next closest
                        //  edge, but it will be for another cell
                        return -1;
                    }

                    if (returnList.Contains(neighborIndex))
                    {
                        continue;
                    }

                    //  Find the common point between these two edges, and get the two rays coming out of that point
                    var edgeRays = Edge2D_wpf.GetRays(edges[current], edges[neighborIndex]);
                    Vector bary = ToBarycentric(edgeRays.Item1, edgeRays.Item1 + edgeRays.Item2, edgeRays.Item1 + edgeRays.Item3, point);

                    //  If x and y are positive, then the point sits inside the triangle defined by edgeRays
                    if (bary.X >= 0 && bary.Y >= 0)
                    {
                        //NOTE: When first starting, there could be two edges that meet this condition (one on each side of current - when current
                        //is a segment).  But just arbitrarily pick the first one, and that will dictate which direction the edges are walked
                        return neighborIndex;
                    }
                }

                //return -1;
                throw new ApplicationException("Didn't find the next edge (this should only happen when there are dupe points)");
            }
            private static int[] GetEdgesByPoint_Reorder(int[] returnList, List<int> rays, int[][] touchingEdges)
            {
                if (returnList.Length < 3)
                {
                    //  Only 1 or 2 points, not enough to reorder
                    return returnList;
                }

                //  See where in the list the first ray starts
                int first = Array.IndexOf(returnList, rays[0]);
                int last = Array.IndexOf(returnList, rays[1]);

                if (first == 0 && last == returnList.Length - 1)
                {
                    //  It's already in order
                    return returnList;
                }

                if (Math.Abs(first - last) > 1)
                {
                    //  It's jumbled up.  Choose a ray, and rewalk
                    return GetEdgesByPoint_ReorderJumbled(returnList, rays[0], rays[1], touchingEdges);
                }

                int[] retVal = new int[returnList.Length];

                //  The rays are in the middle, shift them so that the last ray is the new start
                int remainLength = returnList.Length - last;
                Array.Copy(returnList, last, retVal, 0, remainLength);      //  last to end
                Array.Copy(returnList, 0, retVal, remainLength, first + 1);     //  zero to first

                return retVal;
            }
            private static int[] GetEdgesByPoint_ReorderJumbled(int[] returnList, int ray0, int ray1, int[][] touchingEdges)
            {
                List<int> retVal = new List<int>();

                retVal.Add(ray0);

                int current = ray0;

                for (int cntr = 0; cntr < returnList.Length - 2; cntr++)
                {
                    //  Find the one that's connected to the current edge
                    current = GetEdgesByPoint_ReorderJumbled_Next(returnList, retVal, touchingEdges[current]);

                    retVal.Add(current);
                }

                retVal.Add(ray1);

                return retVal.ToArray();
            }
            private static int GetEdgesByPoint_ReorderJumbled_Next(int[] oldReturn, List<int> newReturn, int[] touching)
            {
                foreach (int old in oldReturn)
                {
                    if (newReturn.Contains(old))
                    {
                        continue;
                    }

                    if (touching.Contains(old))
                    {
                        return old;
                    }
                }

                throw new ApplicationException("Didn't find a result");
            }

            private static double[][] GetMinDistances(Point[] points, Edge2D_wpf[] edges)
            {
                double[][] retVal = new double[points.Length][];

                for (int p = 0; p < points.Length; p++)
                {
                    retVal[p] = new double[edges.Length];

                    for (int e = 0; e < edges.Length; e++)
                    {
                        retVal[p][e] = GetClosestDistance_Edge_Point(edges[e], points[p]);
                    }
                }

                return retVal;
            }

            #endregion
            #region Private Methods - cap

            private static VoronoiResult2D_wpf CapCircle_One(VoronoiResult2D_wpf voronoi)
            {
                // Get the distance from the point to the origin
                double radius;
                if (Math2D.IsNearZero(voronoi.ControlPoints[0]))
                {
                    radius = 1d;
                }
                else
                {
                    double offset = voronoi.ControlPoints[0].ToVector().Length;
                    radius = offset * 2d;
                }

                // Get a circle
                Point[] circle = GetCircle(radius, 7);

                RebuildVars vars = new RebuildVars(voronoi, new ControlPointStats[0], new int[0], circle, radius);

                NewCell cell = new NewCell(0, CapCircle_OneTwo_Edges(vars, circle));

                return BuildResult(vars, new NewCell[] { cell });
            }
            private static VoronoiResult2D_wpf CapCircle_Two(VoronoiResult2D_wpf voronoi)
            {
                if (voronoi.Edges[0].EdgeType != EdgeType.Line)
                {
                    throw new ArgumentException("The edge type for two control points is supposed to be a line");
                }

                // Get the distance from a point to the line
                Point centerPoint = Math2D.GetNearestPoint_Edge_Point(voronoi.Edges[0], voronoi.ControlPoints[0]);        // just need the first control point.  The second is just a mirror image
                Vector centerToPoint0 = voronoi.ControlPoints[0] - centerPoint;

                double distance = centerToPoint0.Length;

                // Get radius and make a circle
                double radius = Math.Max(voronoi.ControlPoints[0].ToVector().Length + distance, voronoi.ControlPoints[1].ToVector().Length + distance);
                Point[] circle = GetCircle(radius, 9);

                // Create a box out of points

                Vector horizontal = centerToPoint0 * radius * 10d;
                Vector vertical = voronoi.Edges[0].Direction.Value * radius * 10d;
                Point top = centerPoint + vertical;
                Point bottom = centerPoint - vertical;

                Point[] cellPoints0 = new Point[4];
                cellPoints0[0] = top;       // topleft
                cellPoints0[1] = top + horizontal;        // topright
                cellPoints0[2] = bottom + horizontal;       // bottomright
                cellPoints0[3] = bottom;        // bottomleft

                Point[] cellPoints1 = new Point[4];
                cellPoints1[0] = top;        // topright
                cellPoints1[1] = top - horizontal;     // topleft
                cellPoints1[2] = bottom - horizontal;       // bottomleft
                cellPoints1[3] = bottom;      // bottomright

                // Clip box points with circle
                Point[] clippedPoints0 = Math2D.GetIntersection_Polygon_Polygon(cellPoints0, circle);
                Point[] clippedPoints1 = Math2D.GetIntersection_Polygon_Polygon(cellPoints1, circle);

                RebuildVars vars = new RebuildVars(voronoi, new ControlPointStats[0], new int[0], circle, radius);

                NewCell cell0 = new NewCell(0, CapCircle_OneTwo_Edges(vars, clippedPoints0));
                NewCell cell1 = new NewCell(1, CapCircle_OneTwo_Edges(vars, clippedPoints1));

                return BuildResult(vars, new NewCell[] { cell0, cell1 });
            }

            private static int[] CapCircle_OneTwo_Edges(RebuildVars vars, Point[] polygon)
            {
                //NOTE: This could be optimized a bit, but the gains are tiny, and this method shouldn't be called very often

                List<int> retVal = new List<int>();

                int pointIndex0, pointIndex1;

                for (int cntr = 0; cntr < polygon.Length - 1; cntr++)
                {
                    // Add the points
                    pointIndex0 = vars.AddEdgePoint(polygon[cntr]);
                    pointIndex1 = vars.AddEdgePoint(polygon[cntr + 1]);

                    // Add the edge
                    retVal.Add(vars.AddEdge(pointIndex0, pointIndex1));
                }

                // Add the points
                pointIndex0 = vars.AddEdgePoint(polygon[polygon.Length - 1]);
                pointIndex1 = vars.AddEdgePoint(polygon[0]);

                // Add the edge
                retVal.Add(vars.AddEdge(pointIndex0, pointIndex1));

                return retVal.ToArray();
            }

            /// <summary>
            /// This returns control points that are surrounded by cells that have a ray or line in the edges
            /// </summary>
            private static ControlPointStats[] GetUnboundControlPoints(VoronoiResult2D_wpf voronoi)
            {
                // Figure out which edges are rays or lines
                List<int> edgeIndices = new List<int>();

                for (int cntr = 0; cntr < voronoi.Edges.Length; cntr++)
                {
                    if (voronoi.Edges[cntr].EdgeType != EdgeType.Segment)
                    {
                        edgeIndices.Add(cntr);
                    }
                }

                List<int> pointIndices = new List<int>();

                // Now figure out which control points use those edges
                for (int cntr = 0; cntr < voronoi.EdgesByControlPoint.Length; cntr++)
                {
                    if (voronoi.EdgesByControlPoint[cntr].Any(o => edgeIndices.Contains(o)))
                    {
                        pointIndices.Add(cntr);
                    }
                }

                return GetUnboundControlPoints_Stats(voronoi, pointIndices.ToArray());
            }
            private static ControlPointStats[] GetUnboundControlPoints_Stats(VoronoiResult2D_wpf voronoi, int[] unboundPoints)
            {
                ControlPointStats[] retVal = new ControlPointStats[unboundPoints.Length];

                for (int cntr = 0; cntr < retVal.Length; cntr++)
                {
                    int index = unboundPoints[cntr];
                    Point point = voronoi.ControlPoints[index];

                    List<Tuple<int, double>> edgeStats = new List<Tuple<int, double>>();

                    foreach (int edgeIndex in voronoi.EdgesByControlPoint[index])
                    {
                        if (voronoi.Edges[edgeIndex].EdgeType == EdgeType.Segment)
                        {
                            continue;
                        }

                        double distanceToEdge = Math2D.GetClosestDistance_Line_Point(voronoi.Edges[edgeIndex].Point0, voronoi.Edges[edgeIndex].Direction.Value, point);      // even if it's a ray, treat it like a line (because there could be a long skinny neighboring cell with segments, so the ray starts way out there

                        edgeStats.Add(Tuple.Create(edgeIndex, distanceToEdge));
                    }

                    retVal[cntr] = new ControlPointStats(index, point.ToVector3D().Length, edgeStats.ToArray());
                }

                return retVal;
            }

            /// <summary>
            /// This returns the indices of edge points that sit outside or the radius passed in
            /// </summary>
            private static int[] GetOutsideEdgePoints(VoronoiResult2D_wpf voronoi, double radius)
            {
                List<int> retVal = new List<int>();

                double radiusSquared = radius * radius;

                for (int cntr = 0; cntr < voronoi.EdgePoints.Length; cntr++)
                {
                    if (voronoi.EdgePoints[cntr].ToVector3D().LengthSquared > radiusSquared)
                    {
                        retVal.Add(cntr);
                    }
                }

                return retVal.ToArray();
            }

            private static double GetMaxRadius(ControlPointStats[] unboundPoints)
            {
                double retVal = 0;

                foreach (ControlPointStats point in unboundPoints)
                {
                    //double localMax = point.Radius + point.DistanceToUnboundEdges.Max(o => o.Item2);
                    //double localMax = point.Radius + point.DistanceToUnboundEdges.Average(o => o.Item2);
                    double localMax = point.Radius + (point.DistanceToUnboundEdges.Min(o => o.Item2) * .75d);
                    if (localMax > retVal)
                    {
                        retVal = localMax;
                    }
                }

                return retVal;
            }

            private static Point[] GetCircle(double radius, double numPoints)
            {
                Point[] points = Math2D.GetCircle_Cached(numPoints < 7 ? 7 : Convert.ToInt32(Math.Round(numPoints)));

                Point[] retVal = new Point[points.Length];

                for (int cntr = 0; cntr < points.Length; cntr++)
                {
                    retVal[cntr] = new Point(points[cntr].X * radius, points[cntr].Y * radius);
                }

                return retVal;
            }

            private static NewCell RebuildCell(int index, RebuildVars vars)
            {
                // See if this is surrounded by any unbound edges
                var unbound = vars.UnboundControlPoints.FirstOrDefault(o => o.Index == index);
                if (unbound != null)
                {
                    return RebuildCell_Clip(index, vars);
                }

                Edge2D_wpf[] edges = vars.Voronoi.Edges;

                // See if any of the segments are outside the radius
                if (vars.Voronoi.EdgesByControlPoint[index].Any(o =>
                    vars.OutsideEdgePoints.Contains(vars.Voronoi.Edges[o].Index0) ||        // edge.index0 is outside
                    (vars.Voronoi.Edges[o].Index1 != null && vars.OutsideEdgePoints.Contains(vars.Voronoi.Edges[o].Index1.Value))))     // edge.index1 is outside
                {
                    return RebuildCell_Clip(index, vars);
                }

                // This can be copied as is
                return RebuildCell_Copy(index, vars);
            }
            private static NewCell RebuildCell_Clip(int index, RebuildVars vars)
            {
                // Convert the list of edges into a list of points
                Edge2D_wpf[] cellEdges = vars.Voronoi.EdgesByControlPoint[index].Select(o => vars.Voronoi.Edges[o]).ToArray();
                Point[] cellPoints = Edge2D_wpf.GetPolygon(cellEdges, vars.CircleRadius * 3d);

                // Clip them
                Point[] clippedPoints = Math2D.GetIntersection_Polygon_Polygon(cellPoints, vars.Circle);

                List<int> newEdges = new List<int>();

                for (int cntr = 0; cntr < clippedPoints.Length - 1; cntr++)
                {
                    // Find the edge that has clipped[cntr] and clipped[cntr+1], otherwise create a new edge
                    newEdges.Add(RebuildCell_GetNewEdgeIndex(clippedPoints[cntr], clippedPoints[cntr + 1], vars.Voronoi.Edges, vars.Voronoi.EdgesByControlPoint[index], vars));
                }

                newEdges.Add(RebuildCell_GetNewEdgeIndex(clippedPoints[clippedPoints.Length - 1], clippedPoints[0], vars.Voronoi.Edges, vars.Voronoi.EdgesByControlPoint[index], vars));

                return new NewCell(index, newEdges.ToArray());
            }
            private static NewCell RebuildCell_Copy(int index, RebuildVars vars)
            {
                int[] edges = new int[vars.Voronoi.EdgesByControlPoint[index].Length];

                for (int cntr = 0; cntr < edges.Length; cntr++)
                {
                    edges[cntr] = vars.AddEdge(vars.Voronoi.EdgesByControlPoint[index][cntr]);
                }

                return new NewCell(index, edges);
            }

            private static int RebuildCell_GetNewEdgeIndex(Point point0, Point point1, Edge2D_wpf[] allEdges, int[] cellEdges, RebuildVars vars)
            {
                int? pointIndex0 = null, pointIndex1 = null;

                foreach (int edgeIndex in cellEdges)
                {
                    bool isSegment = allEdges[edgeIndex].EdgeType == EdgeType.Segment;

                    bool match0 = false, match1 = false;

                    // Point 0
                    if (Math2D.IsNearValue(point0, allEdges[edgeIndex].Point0))
                    {
                        match0 = true;
                        pointIndex0 = allEdges[edgeIndex].Index0;
                    }
                    else if (isSegment && Math2D.IsNearValue(point0, allEdges[edgeIndex].Point1.Value))
                    {
                        match0 = true;
                        pointIndex0 = allEdges[edgeIndex].Index1.Value;
                    }

                    // Point 1
                    if (Math2D.IsNearValue(point1, allEdges[edgeIndex].Point0))
                    {
                        match1 = true;
                        pointIndex1 = allEdges[edgeIndex].Index0;
                    }
                    else if (isSegment && Math2D.IsNearValue(point1, allEdges[edgeIndex].Point1.Value))
                    {
                        match1 = true;
                        pointIndex1 = allEdges[edgeIndex].Index1.Value;
                    }

                    if (match0 && match1)
                    {
                        // This edge has both of these points
                        return vars.AddEdge(edgeIndex);
                    }
                }

                // If execution gets here, then one or both of the points are unique
                //NOTE: pointIndex0 and 1 point to the old point indices before this, but will point to the new indices after this
                if (pointIndex0 == null)
                {
                    pointIndex0 = vars.AddEdgePoint(point0);
                }
                else
                {
                    pointIndex0 = vars.AddEdgePoint(pointIndex0.Value);
                }

                if (pointIndex1 == null)
                {
                    pointIndex1 = vars.AddEdgePoint(point1);
                }
                else
                {
                    pointIndex1 = vars.AddEdgePoint(pointIndex1.Value);
                }

                // Now that vars knows about the points, add a new edge
                return vars.AddEdge(pointIndex0.Value, pointIndex1.Value);
            }

            private static VoronoiResult2D_wpf BuildResult(RebuildVars vars, NewCell[] newCells)
            {
                if (vars.Voronoi.ControlPoints.Length != newCells.Length)
                {
                    throw new ArgumentException("vars.Voronoi.ControlPoints must be the same length as newCells");
                }

                // Edge Points
                Point[] edgePoints = vars.NewEdgePoints.ToArray();

                // Edges
                Edge2D_wpf[] edges = new Edge2D_wpf[vars.NewEdges.Count];

                for (int cntr = 0; cntr < edges.Length; cntr++)
                {
                    edges[cntr] = new Edge2D_wpf(vars.NewEdges[cntr].Item1, vars.NewEdges[cntr].Item2, edgePoints);
                }

                // Edges by Control Point
                int[][] edgesByControlPoint = new int[newCells.Length][];

                for (int cntr = 0; cntr < newCells.Length; cntr++)
                {
#if DEBUG
                    if (newCells[cntr].ControlPoint != cntr)
                    {
                        throw new ArgumentException("The cell is for the wrong control point");
                    }
#endif

                    edgesByControlPoint[cntr] = newCells[cntr].Edges;
                }

                return new VoronoiResult2D_wpf(vars.Voronoi.ControlPoints.ToArray(), edgePoints, edges, edgesByControlPoint);
            }

            #endregion
        }

        #endregion
        #region class: QuickHull2D_wpf

        /// <remarks>
        /// Got this here (ported it from java):
        /// http://www.ahristov.com/tutorial/geometry-games/convex-hull.html
        /// </remarks>
        internal static class QuickHull2D_wpf
        {
            public static QuickHull2DResult_wpf GetConvexHull(Point3D[] points)
            {
                Transform3D transformTo2D = null;
                Transform3D transformTo3D = null;

                // If there are less than three points, just return everything
                if (points.Length == 0)
                {
                    return new QuickHull2DResult_wpf(new Point[0], new int[0], null, null);
                }
                else if (points.Length == 1)
                {
                    return new QuickHull2DResult_wpf(new Point[] { new Point(points[0].X, points[0].Y) }, new int[] { 0 }, new TranslateTransform3D(0, 0, -points[0].Z), new TranslateTransform3D(0, 0, points[0].Z));
                }
                else if (points.Length == 2)
                {
                    Point[] transformedPoints = GetRotatedPoints(out transformTo2D, out transformTo3D, points[0], points[1]);
                    return new QuickHull2DResult_wpf(transformedPoints, new int[] { 0, 1 }, transformTo2D, transformTo3D);
                }

                Point[] points2D = null;
                if (points.All(o => Math1D.IsNearValue(o.Z, points[0].Z)))
                {
                    // They are already in the xy plane
                    points2D = points.Select(o => new Point(o.X, o.Y)).ToArray();

                    if (!Math1D.IsNearZero(points[0].Z))        // if it's null, leave the transform null
                    {
                        // There is no Z, so just directly convert the points (leave transform null)
                        transformTo2D = new TranslateTransform3D(0, 0, -points[0].Z);
                        transformTo3D = new TranslateTransform3D(0, 0, points[0].Z);
                    }
                }
                else
                {
                    // Rotate the points so that Z drops out (and make sure they are all coplanar)
                    points2D = GetRotatedPoints(out transformTo2D, out transformTo3D, points, true);
                }

                if (points2D == null)
                {
                    return null;
                }

                // Call quickhull
                QuickHull2DResult_wpf retVal = GetConvexHull(points2D);
                return new QuickHull2DResult_wpf(retVal.Points, retVal.PerimiterLines, transformTo2D, transformTo3D);
            }
            public static QuickHull2DResult_wpf GetConvexHull(Point[] points)
            {
                if (points.Length < 3)
                {
                    return new QuickHull2DResult_wpf(points, Enumerable.Range(0, points.Length).ToArray(), null, null);		// return all the points
                }

                List<int> retVal = new List<int>();
                List<int> remainingPoints = Enumerable.Range(0, points.Length).ToList();

                #region Find two most extreme points

                int minIndex = -1;
                int maxIndex = -1;
                double minX = double.MaxValue;
                double maxX = double.MinValue;
                for (int cntr = 0; cntr < points.Length; cntr++)
                {
                    if (points[cntr].X < minX)
                    {
                        minX = points[cntr].X;
                        minIndex = cntr;
                    }

                    if (points[cntr].X > maxX)
                    {
                        maxX = points[cntr].X;
                        maxIndex = cntr;
                    }
                }

                #endregion

                #region Move points to return list

                retVal.Add(minIndex);
                retVal.Add(maxIndex);

                if (maxIndex > minIndex)
                {
                    remainingPoints.RemoveAt(maxIndex);		// need to remove the later index first so it doesn't shift
                    remainingPoints.RemoveAt(minIndex);
                }
                else
                {
                    remainingPoints.RemoveAt(minIndex);
                    remainingPoints.RemoveAt(maxIndex);
                }

                #endregion

                #region Divide the list left and right of the line

                List<int> leftSet = new List<int>();
                List<int> rightSet = new List<int>();

                for (int cntr = 0; cntr < remainingPoints.Count; cntr++)
                {
                    if (IsRightOfLine(minIndex, maxIndex, remainingPoints[cntr], points))
                    {
                        rightSet.Add(remainingPoints[cntr]);
                    }
                    else
                    {
                        leftSet.Add(remainingPoints[cntr]);
                    }
                }

                #endregion

                // Process these sets recursively, adding to retVal
                HullSet(minIndex, maxIndex, rightSet, retVal, points);
                HullSet(maxIndex, minIndex, leftSet, retVal, points);

                return new QuickHull2DResult_wpf(points, retVal.ToArray(), null, null);
            }

            #region Private Methods

            private static void HullSet(int lineStart, int lineStop, List<int> set, List<int> hull, Point[] allPoints)
            {
                int insertPosition = hull.IndexOf(lineStop);

                if (set.Count == 0)
                {
                    return;
                }
                else if (set.Count == 1)
                {
                    hull.Insert(insertPosition, set[0]);
                    set.RemoveAt(0);
                    return;
                }

                #region Find most distant point

                double maxDistance = double.MinValue;
                int farIndexIndex = -1;
                for (int cntr = 0; cntr < set.Count; cntr++)
                {
                    int point = set[cntr];
                    double distance = GetDistanceFromLineSquared(allPoints[lineStart], allPoints[lineStop], allPoints[point]);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        farIndexIndex = cntr;
                    }
                }

                // Move the point to the hull
                int farIndex = set[farIndexIndex];
                set.RemoveAt(farIndexIndex);
                hull.Insert(insertPosition, farIndex);

                #endregion

                #region Find everything left of (Start, Far)

                List<int> leftSet_Start_Far = new List<int>();
                for (int cntr = 0; cntr < set.Count; cntr++)
                {
                    int pointIndex = set[cntr];
                    if (IsRightOfLine(lineStart, farIndex, pointIndex, allPoints))
                    {
                        leftSet_Start_Far.Add(pointIndex);
                    }
                }

                #endregion

                #region Find everything right of (Far, Stop)

                List<int> leftSet_Far_Stop = new List<int>();
                for (int cntr = 0; cntr < set.Count; cntr++)
                {
                    int pointIndex = set[cntr];
                    if (IsRightOfLine(farIndex, lineStop, pointIndex, allPoints))
                    {
                        leftSet_Far_Stop.Add(pointIndex);
                    }
                }

                #endregion

                // Recurse
                //NOTE: The set passed in was split into these two sets
                HullSet(lineStart, farIndex, leftSet_Start_Far, hull, allPoints);
                HullSet(farIndex, lineStop, leftSet_Far_Stop, hull, allPoints);
            }

            internal static bool IsRightOfLine(int lineStart, int lineStop, int testPoint, Point[] allPoints)
            {
                double cp1 = ((allPoints[lineStop].X - allPoints[lineStart].X) * (allPoints[testPoint].Y - allPoints[lineStart].Y)) -
                                      ((allPoints[lineStop].Y - allPoints[lineStart].Y) * (allPoints[testPoint].X - allPoints[lineStart].X));

                return cp1 > 0;
                //return (cp1 > 0) ? 1 : -1;
            }
            internal static bool IsRightOfLine(int lineStart, int lineStop, Point testPoint, Point[] allPoints)
            {
                double cp1 = ((allPoints[lineStop].X - allPoints[lineStart].X) * (testPoint.Y - allPoints[lineStart].Y)) -
                                      ((allPoints[lineStop].Y - allPoints[lineStart].Y) * (testPoint.X - allPoints[lineStart].X));

                return cp1 > 0;
                //return (cp1 > 0) ? 1 : -1;
            }
            internal static bool IsRightOfLine(Point lineStart, Point lineStop, Point testPoint)
            {
                double cp1 = ((lineStop.X - lineStart.X) * (testPoint.Y - lineStart.Y)) -
                                      ((lineStop.Y - lineStart.Y) * (testPoint.X - lineStart.X));

                return cp1 > 0;
                //return (cp1 > 0) ? 1 : -1;
            }

            private static double GetDistanceFromLineSquared(Point lineStart, Point lineStop, Point testPoint)
            {
                Point3D pointOnLine = new Point3D(lineStart.X, lineStart.Y, 0d);
                Vector3D lineDirection = new Point3D(lineStop.X, lineStop.Y, 0d) - pointOnLine;
                Point3D point = new Point3D(testPoint.X, testPoint.Y, 0d);

                Point3D nearestPoint = Math3D.GetClosestPoint_Line_Point(pointOnLine, lineDirection, point);

                return (point - nearestPoint).LengthSquared;
            }

            #endregion
        }

        #endregion
        #region class: PointsSingleton_wpf

        private class PointsSingleton_wpf
        {
            #region Declaration Section

            private static readonly object _lockStatic = new object();
            private readonly object _lockInstance;

            /// <summary>
            /// The static constructor makes sure that this instance is created only once.  The outside users of this class
            /// call the static property Instance to get this one instance copy.  (then they can use the rest of the instance
            /// methods)
            /// </summary>
            private static PointsSingleton_wpf _instance;

            private SortedList<int, Point[]> _points;

            #endregion

            #region Constructor / Instance Property

            /// <summary>
            /// Static constructor.  Called only once before the first time you use my static properties/methods.
            /// </summary>
            static PointsSingleton_wpf()
            {
                lock (_lockStatic)
                {
                    // If the instance version of this class hasn't been instantiated yet, then do so
                    if (_instance == null)
                    {
                        _instance = new PointsSingleton_wpf();
                    }
                }
            }
            /// <summary>
            /// Instance constructor.  This is called only once by one of the calls from my static constructor.
            /// </summary>
            private PointsSingleton_wpf()
            {
                _lockInstance = new object();

                _points = new SortedList<int, Point[]>();
            }

            /// <summary>
            /// This is how you get at my instance.  The act of calling this property guarantees that the static constructor gets called
            /// exactly once (per process?)
            /// </summary>
            public static PointsSingleton_wpf Instance
            {
                get
                {
                    // There is no need to check the static lock, because _instance is only set one time, and that is guaranteed to be
                    // finished before this function gets called
                    return _instance;
                }
            }

            #endregion

            #region Public Methods

            public Point[] GetPoints(int numSides)
            {
                lock (_lockInstance)
                {
                    if (!_points.ContainsKey(numSides))
                    {
                        double deltaTheta = 2d * Math.PI / numSides;
                        double theta = 0d;

                        Point[] points = new Point[numSides];		// these define a unit circle

                        for (int cntr = 0; cntr < numSides; cntr++)
                        {
                            points[cntr] = new Point(Math.Cos(theta), Math.Sin(theta));
                            theta += deltaTheta;
                        }

                        _points.Add(numSides, points);
                    }

                    return _points[numSides];
                }
            }

            #endregion
        }

        #endregion
        #region class: TriangulateConcave_wpf

        // Got this here:
        // http://wiki.unity3d.com/index.php?title=Triangulator
        private static class TriangulateConcave_wpf
        {
            public static Tuple<int, int, int>[] Triangulate(Point[] points)
            {
                int n = points.Length;
                if (n < 3)
                {
                    return new Tuple<int, int, int>[0];
                }

                int[] V = new int[n];
                if (Area_Signed(points) > 0)
                {
                    for (int v = 0; v < n; v++)
                    {
                        V[v] = v;
                    }
                }
                else
                {
                    for (int v = 0; v < n; v++)
                    {
                        V[v] = (n - 1) - v;
                    }
                }

                List<int> retVal = new List<int>();

                int nv = n;
                int count = 2 * nv;
                for (int m = 0, v = nv - 1; nv > 2;)
                {
                    if ((count--) <= 0)
                    {
                        return ConvertToTriples(retVal.ToArray());
                    }

                    int u = v;
                    if (nv <= u)
                    {
                        u = 0;
                    }

                    v = u + 1;
                    if (nv <= v)
                    {
                        v = 0;
                    }

                    int w = v + 1;
                    if (nv <= w)
                    {
                        w = 0;
                    }

                    if (Snip(u, v, w, nv, V, points))
                    {
                        int a = V[u];
                        int b = V[v];
                        int c = V[w];

                        retVal.Add(a);
                        retVal.Add(b);
                        retVal.Add(c);

                        m++;
                        for (int s = v, t = v + 1; t < nv; s++, t++)
                        {
                            V[s] = V[t];
                        }

                        nv--;
                        count = 2 * nv;
                    }
                }

                retVal.Reverse();
                return ConvertToTriples(retVal.ToArray());
            }

            #region Private Methods

            /// <summary>
            /// This returns the area of the polygon (may be negative if normal faces down)
            /// </summary>
            private static double Area_Signed(Point[] points)
            {
                int n = points.Length;

                double retVal = 0d;

                for (int p = n - 1, q = 0; q < n; p = q++)
                {
                    retVal += points[p].X * points[q].Y - points[q].X * points[p].Y;
                }

                return (retVal / 2d);
            }

            //TODO: Rename as a question (IsSnipping?)
            private static bool Snip(int u, int v, int w, int n, int[] V, Point[] points)
            {
                int p;
                Point A = points[V[u]];
                Point B = points[V[v]];
                Point C = points[V[w]];

                if (Math3D.NEARZERO > (((B.X - A.X) * (C.Y - A.Y)) - ((B.Y - A.Y) * (C.X - A.X))))
                    return false;

                for (p = 0; p < n; p++)
                {
                    if ((p == u) || (p == v) || (p == w))
                    {
                        continue;
                    }

                    Point P = points[V[p]];

                    if (InsideTriangle(A, B, C, P))
                    {
                        return false;
                    }
                }

                return true;
            }

            //TODO: Replace with Math2D.IsInsidePolygon
            private static bool InsideTriangle(Point A, Point B, Point C, Point P)
            {
                double ax, ay, bx, by, cx, cy, apx, apy, bpx, bpy, cpx, cpy;
                double cCROSSap, bCROSScp, aCROSSbp;

                ax = C.X - B.X; ay = C.Y - B.Y;
                bx = A.X - C.X; by = A.Y - C.Y;
                cx = B.X - A.X; cy = B.Y - A.Y;
                apx = P.X - A.X; apy = P.Y - A.Y;
                bpx = P.X - B.X; bpy = P.Y - B.Y;
                cpx = P.X - C.X; cpy = P.Y - C.Y;

                aCROSSbp = ax * bpy - ay * bpx;
                cCROSSap = cx * apy - cy * apx;
                bCROSScp = bx * cpy - by * cpx;

                return ((aCROSSbp >= 0d) && (bCROSScp >= 0d) && (cCROSSap >= 0d));
            }

            private static Tuple<int, int, int>[] ConvertToTriples(int[] indices)
            {
                if (indices.Length % 3 != 0)
                {
                    throw new ApplicationException("Return list should be divisible by three: " + indices.Length.ToString());
                }

                Tuple<int, int, int>[] retVal = new Tuple<int, int, int>[indices.Length / 3];

                for (int cntr = 0; cntr < retVal.Length; cntr++)
                {
                    int index = cntr * 3;

                    retVal[cntr] = Tuple.Create(indices[index], indices[index + 1], indices[index + 2]);
                }

                return retVal;
            }

            #endregion
        }

        #endregion
        #region class: AveragePlane_wpf

        private static class AveragePlane_wpf
        {
            /// <remarks>
            /// I spent a couple days reading articles, trying to get a better solution.  Pages of math, or lots of cryptic code
            /// 
            /// There is a way to get an exact answer.  It becomes a system of linear equations (or something).
            /// 
            /// The problem is, a lot of the solutions assume that z is a function of x and y (so no asymptotes along z).  Then they take
            /// shortcuts with distance from the line to be distance along the z axis, instead of the distance perp to the plane.
            /// 
            /// This method takes the easy way out, and chooses a bunch of random triangles, calculates the normal for those, then averages
            /// those normals together.  This is accurate enough if the points aren't too far from coplanar.
            /// 
            /// Here are some pages that I looked at:
            /// 
            /// Ransac is a method that throws away outlier samples, and averages the rest together
            /// http://www.timzaman.com/?p=190
            ///
            /// This is c++, but he was casting a float array to a char array, then just had if(charArray).  And of course no comments
            /// http://codesuppository.blogspot.com/2006/03/best-fit-plane.html
            /// http://codesuppository.blogspot.com/2009/06/holy-crap-my-veyr-own-charles.html
            /// https://code.google.com/p/codesuppository/source/browse/trunk/app/CodeSuppository/
            /// http://www.geometrictools.com/
            /// </remarks>
            public static ITriangle_wpf GetAveragePlane(Point3D[] points, bool matchPolyNormalDirection = false)
            {
                if (points.Length < 3)
                {
                    return null;
                }

                // The plane will go through this center point
                Point3D center = Math3D.GetCenter(points);

                // Get a bunch of sample up vectors
                Vector3D[] upVectors = GetAveragePlane_UpVectors(points, matchPolyNormalDirection);
                if (upVectors.Length == 0)
                {
                    return null;
                }

                // Average them together to get a single normal
                Vector3D avgNormal = Math3D.GetAverage(upVectors);

                // Exit Function
                return Math3D.GetPlane(center, avgNormal);
            }

            #region Private Methods

            /// <summary>
            /// This chooses a bunch of random triangles out of the points passed in, and returns their normals (all pointing the
            /// same direction)
            /// </summary>
            /// <remarks>
            /// This limits the number of returned vectors to 100.  Here is a small table of the number of triangles based
            /// on the number of points (it's roughly count^3)
            /// 
            /// 3 - 1
            /// 4 - 4
            /// 5 - 10
            /// 6 - 20
            /// 7 - 35
            /// 8 - 56
            /// 9 - 84
            /// 10 - 120
            /// 11 - 165
            /// 12 - 220
            /// 13 - 286
            /// 14 - 364
            /// 15 - 455
            /// 16 - 560
            /// 17 - 680
            /// 18 - 816
            /// 19 - 969
            /// 20 - 1140
            /// 21 - 1330
            /// 22 - 1540
            /// </remarks>
            /// <param name="matchPolyNormalDirection">
            /// True = They will point in the direction of the polygon's normal (only makes sense if the points represent a polygon, and that polygon is convex)
            /// False = The direction of the vectors returned is arbitrary (they will all point in the same direction, but it's random which direction that will be)
            /// </param>
            private static Vector3D[] GetAveragePlane_UpVectors(Point3D[] points, bool matchPolyNormalDirection = false)
            {
                if (points.Length < 3)
                {
                    return new Vector3D[0];
                }

                Vector3D[] retVal;
                if (points.Length < 15)      //see the table in the remarks.  Even though 13 makes 364 triangles, it would be inneficient to randomly choose triangles, and throw out already attempted ones (I was looking for at least a 1:4 ratio - didn't do performance testing, just feels right)
                {
                    // Do them all
                    retVal = GetAveragePlane_UpVectors_All(points);
                }
                else
                {
                    // Randomly choose 100 points
                    retVal = GetAveragePlane_UpVectors_Sample(points, 100);

                    if (retVal.Length == 0)
                    {
                        #region lots of colinear

                        // The only way to get here is if the infinite loop detectors hit, which means there are a lot of colinear points (or identical
                        // points).  Before completely giving up, use the normal of any triangle formed by the points
                        try
                        {
                            Point3D[] nonDupedInitial = GetNonDupedInitialPoints(points);       // GetPolygonNormal throws an exception if the first two points are the same

                            Vector3D normal = Math2D.GetPolygonNormal(nonDupedInitial, PolygonNormalLength.DoesntMatter);

                            retVal = new[] { normal };
                        }
                        catch (Exception) { }       // the method throws exceptions if it can't get an answer

                        #endregion
                    }
                }

                if (retVal.Length == 0)
                {
                    return retVal;
                }

                // Make sure they are all pointing in the same direction
                GetAveragePlane_SameDirection(retVal, points, matchPolyNormalDirection);

                return retVal.ToArray();
            }

            private static Vector3D[] GetAveragePlane_UpVectors_All(Point3D[] points)
            {
                List<Vector3D> retVal = new List<Vector3D>();

                for (int a = 0; a < points.Length - 2; a++)
                {
                    for (int b = a + 1; b < points.Length - 1; b++)
                    {
                        for (int c = b + 1; c < points.Length; c++)
                        {
                            GetAveragePlane_UpVectors_Vector(retVal, a, b, c, points);
                        }
                    }
                }

                return retVal.ToArray();
            }
            private static Vector3D[] GetAveragePlane_UpVectors_Sample(Point3D[] points, int count)
            {
                List<Vector3D> retVal = new List<Vector3D>();
                SortedList<Tuple<int, int, int>, byte> used = new SortedList<Tuple<int, int, int>, byte>();     // the value doesn't mean anything, I just wanted to keep the keys sorted

                Random rand = StaticRandom.GetRandomForThread();
                int pointsLen = points.Length;      // not sure if there is a cost to hitting the length property, but this method would hit it a lot

                int infiniteLoopCntr1 = 0;
                int[] indices = new int[3];

                while (retVal.Count < count && infiniteLoopCntr1 < 40)
                {
                    int infiniteLoopCntr2 = 0;
                    Tuple<int, int, int> triangle = null;
                    while (infiniteLoopCntr2 < 1000)
                    {
                        infiniteLoopCntr2++;

                        // Choose 3 random points
                        indices[0] = rand.Next(pointsLen);
                        indices[1] = rand.Next(pointsLen);
                        indices[2] = rand.Next(pointsLen);

                        // Make sure they are unique
                        if (indices[0] == indices[1] || indices[0] == indices[2])
                        {
                            continue;
                        }

                        // Generate the key (the inidividual indices need to be sorted so that { 1,2,3 | 1,3,2 | 2,1,3 | 2,3,1 | 3,1,2 | 3,2,1 } would all be considered the same key)
                        Array.Sort(indices);
                        triangle = Tuple.Create(indices[0], indices[1], indices[2]);

                        // Make sure this hasn't been tried before
                        if (used.ContainsKey(triangle))
                        {
                            continue;
                        }

                        used.Add(triangle, 0);

                        // triangle is valid and not attempted yet, break out of this inner loop
                        break;
                    }

                    // Get the normal of triangle
                    if (GetAveragePlane_UpVectors_Vector(retVal, triangle.Item1, triangle.Item2, triangle.Item3, points))
                    {
                        infiniteLoopCntr1 = 0;
                    }
                    else
                    {
                        // The points are colinear.  If there are too many in a row, just fail
                        infiniteLoopCntr1++;
                    }
                }

                return retVal.ToArray();
            }

            private static bool GetAveragePlane_UpVectors_Vector(List<Vector3D> returnVectors, int index1, int index2, int index3, Point3D[] points)
            {
                Vector3D cross = Vector3D.CrossProduct(points[index2] - points[index1], points[index3] - points[index1]);
                if (!Math3D.IsInvalid(cross) && !Math3D.IsNearZero(cross))        // there may be colinear points
                {
                    returnVectors.Add(cross);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            private static void GetAveragePlane_SameDirection(Vector3D[] vectors, Point3D[] points, bool matchPolyNormalDirection)
            {
                // Get the vector to compare with
                int start;
                Vector3D match;
                if (matchPolyNormalDirection)
                {
                    start = 0;
                    match = Math2D.GetPolygonNormal(points, PolygonNormalLength.DoesntMatter);
                }
                else
                {
                    start = 1;
                    match = vectors[0];
                }

                // Make sure all the vectors match that direction
                for (int cntr = start; cntr < vectors.Length; cntr++)
                {
                    if (Vector3D.DotProduct(vectors[cntr], match) < 0)
                    {
                        vectors[cntr] = vectors[cntr] * -1d;
                    }
                }
            }

            /// <summary>
            /// This is an ugly, hardcoded method that makes sure the first two points aren't identical
            /// </summary>
            private static Point3D[] GetNonDupedInitialPoints(Point3D[] points)
            {
                if (points.Length == 0)
                {
                    return new Point3D[0];
                }

                int startIndex = 1;
                while (startIndex < points.Length)
                {
                    if (!Math3D.IsNearValue(points[0], points[startIndex]))
                    {
                        // Found a non identical point at startIndex
                        break;
                    }

                    startIndex++;
                }

                if (startIndex >= points.Length)
                {
                    // All the points in the array are the same, just return the first point
                    return new Point3D[] { points[0] };
                }
                else if (startIndex == 1)
                {
                    // There is nothing to cut out, just return what was passed in
                    return points;
                }

                // Cut out the points that are the same as the first
                List<Point3D> retVal = new List<Point3D>();
                retVal.Add(points[0]);
                retVal.AddRange(points.Skip(startIndex));

                return retVal.ToArray();
            }

            #endregion
        }

        #endregion
        #region class: Delaunay_wpf

        private static class Delaunay_wpf
        {
            public static TriangleIndexed_wpf[] GetDelaunayTriangulation(Point[] points, Point3D[] points3D)
            {
                const int MAXPOINTS = 32767;

                if (points.Length != points3D.Length)
                {
                    throw new ApplicationException("points and points3D must be the same size: " + points.Length.ToString() + ", " + points3D.Length.ToString());
                }
                else if (points.Length < 3)
                {
                    throw new ApplicationException("Must have at least 3 points: " + points.Length.ToString());
                }
                else if (points.Length > MAXPOINTS)
                {
                    // The original code was used to process autocad meshes which were limited to 32767.  There's nothing stopping this implementation
                    // from supporting more points, except the size of the arrays (I took a stab at rewriting it to use lists, but it's a tangled mess of code, and
                    // I don't care THAT much :)
                    throw new ApplicationException("This method only supports up to " + MAXPOINTS.ToString("N0") + " points (" + points.Length.ToString("N0") + " passed in)");
                }

                int count = points.Length;

                int i, j, k, numTriangles, numEd, thinTriangleCount = 0, dupeCount = 0;
                bool status;

                // Point coordinates
                double[] ptx = new double[MAXPOINTS + 3];
                double[] pty = new double[MAXPOINTS + 3];

                // Triangle definitions
                int[] pt1 = new int[MAXPOINTS * 2 + 1];
                int[] pt2 = new int[MAXPOINTS * 2 + 1];
                int[] pt3 = new int[MAXPOINTS * 2 + 1];

                // Circumscribed circle
                double[] cex = new double[MAXPOINTS * 2 + 1];
                double[] cey = new double[MAXPOINTS * 2 + 1];
                double[] rad = new double[MAXPOINTS * 2 + 1];
                double xmin, ymin, xmax, ymax, dx, dy, xmid, ymid;
                int[] ed1 = new int[MAXPOINTS * 2 + 1];
                int[] ed2 = new int[MAXPOINTS * 2 + 1];

                #region Load ptx, pty from points (distinct)

                k = 0;
                for (i = 0; i < count; i++)		//NOTE: When a dupe is found, both i and count are decremented
                {
                    ptx[i] = points[k].X;
                    pty[i] = points[k].Y;

                    // Look for dupes
                    for (j = 0; j < i; j++)
                    {
                        if ((ptx[i] == ptx[j]) && (pty[i] == pty[j]))
                        {
                            //i--; count--; dupeCount++;
                            throw new ApplicationException(string.Format("Found duplicate points: {0}, {1} - {2}, {3}", ptx[i], pty[i], ptx[j], pty[j]));
                        }
                    }

                    k++;
                }

                //if (status2 > 0)
                //{
                //    ed.WriteMessage("\nIgnored {0} point(s) with same coordinates.", status2);
                //}

                #endregion

                #region Supertriangle

                xmin = ptx[0]; xmax = xmin;
                ymin = pty[0]; ymax = ymin;
                for (i = 0; i < count; i++)
                {
                    if (ptx[i] < xmin) xmin = ptx[i];
                    if (ptx[i] > xmax) xmax = ptx[i];
                    if (pty[i] < xmin) ymin = pty[i];
                    if (pty[i] > xmin) ymax = pty[i];
                }
                dx = xmax - xmin; dy = ymax - ymin;
                xmid = (xmin + xmax) / 2; ymid = (ymin + ymax) / 2;
                i = count;
                ptx[i] = xmid - (90 * (dx + dy)) - 100;
                pty[i] = ymid - (50 * (dx + dy)) - 100;
                pt1[0] = i;
                i++;
                ptx[i] = xmid + (90 * (dx + dy)) + 100;
                pty[i] = ymid - (50 * (dx + dy)) - 100;
                pt2[0] = i;
                i++;
                ptx[i] = xmid;
                pty[i] = ymid + 100 * (dx + dy + 1);
                pt3[0] = i;
                numTriangles = 1;
                Circum(
                  ptx[pt1[0]], pty[pt1[0]], ptx[pt2[0]],
                  pty[pt2[0]], ptx[pt3[0]], pty[pt3[0]],
                  ref cex[0], ref cey[0], ref rad[0]
                );

                #endregion

                #region main loop

                for (i = 0; i < count; i++)
                {
                    numEd = 0;
                    xmin = ptx[i]; ymin = pty[i];
                    j = 0;
                    while (j < numTriangles)
                    {
                        dx = cex[j] - xmin; dy = cey[j] - ymin;
                        if (((dx * dx) + (dy * dy)) < rad[j])
                        {
                            ed1[numEd] = pt1[j]; ed2[numEd] = pt2[j];
                            numEd++;
                            ed1[numEd] = pt2[j]; ed2[numEd] = pt3[j];
                            numEd++;
                            ed1[numEd] = pt3[j]; ed2[numEd] = pt1[j];
                            numEd++;
                            numTriangles--;
                            pt1[j] = pt1[numTriangles];
                            pt2[j] = pt2[numTriangles];
                            pt3[j] = pt3[numTriangles];
                            cex[j] = cex[numTriangles];
                            cey[j] = cey[numTriangles];
                            rad[j] = rad[numTriangles];
                            j--;
                        }
                        j++;
                    }

                    for (j = 0; j < numEd - 1; j++)
                    {
                        for (k = j + 1; k < numEd; k++)
                        {
                            if ((ed1[j] == ed2[k]) && (ed2[j] == ed1[k]))
                            {
                                ed1[j] = -1; ed2[j] = -1; ed1[k] = -1; ed2[k] = -1;
                            }
                        }
                    }

                    for (j = 0; j < numEd; j++)
                    {
                        if ((ed1[j] >= 0) && (ed2[j] >= 0))
                        {
                            pt1[numTriangles] = ed1[j]; pt2[numTriangles] = ed2[j]; pt3[numTriangles] = i;
                            status = Circum(
                                ptx[pt1[numTriangles]], pty[pt1[numTriangles]], ptx[pt2[numTriangles]],
                                pty[pt2[numTriangles]], ptx[pt3[numTriangles]], pty[pt3[numTriangles]],
                                ref cex[numTriangles], ref cey[numTriangles], ref rad[numTriangles]
                              );

                            if (!status)
                            {
                                // Found a thin triangle
                                thinTriangleCount++;
                            }
                            numTriangles++;
                        }
                    }
                }

                #endregion

                #region removal of outer triangles

                i = 0;
                while (i < numTriangles)
                {
                    if ((pt1[i] >= count) || (pt2[i] >= count) || (pt3[i] >= count))
                    {
                        numTriangles--;
                        pt1[i] = pt1[numTriangles];
                        pt2[i] = pt2[numTriangles];
                        pt3[i] = pt3[numTriangles];
                        cex[i] = cex[numTriangles];
                        cey[i] = cey[numTriangles];
                        rad[i] = rad[numTriangles];
                        i--;
                    }
                    i++;
                }

                #endregion

                //if (status1 > 0)
                //{
                //    //ed.WriteMessage("\nWarning! {0} thin triangle(s) found!" + " Wrong result possible!", status1);
                //}
                //Application.UpdateScreen();

                #region Build Return

                TriangleIndexed_wpf[] retVal = new TriangleIndexed_wpf[numTriangles];

                for (int cntr = 0; cntr < retVal.Length; cntr++)
                {
                    retVal[cntr] = new TriangleIndexed_wpf(pt1[cntr], pt2[cntr], pt3[cntr], points3D);
                }

                #endregion

                return retVal;
            }

            public static Tuple<int, int>[] ThrowOutThinTriangles(TriangleIndexed_wpf[] triangles, double skipThinRatio)
            {
                // Figure out which edges to throw out
                Tuple<int, int>[] removes = TriangleIndexedLinked_wpf.ConvertToLinked(triangles, true, false).      // need to convert to linked so that shouldRemove knows if the edges are interior or exterior
                    Select(o => ShouldRemoveThinOuter(o, skipThinRatio)).
                    Where(o => o != null).
                    Select(o => o.Item1 < o.Item2 ? o : Tuple.Create(o.Item2, o.Item1)).        // make sure that item1 is less than item2 so that the tuples will match later
                    ToArray();

                if (removes.Length == 0)
                {
                    // There is nothing to remove, so just return everything
                    return TriangleIndexed_wpf.GetUniqueLines(triangles);
                }

                // Build the initial list of links
                List<Tuple<int, int>> retVal = TriangleIndexed_wpf.GetUniqueLines(triangles).
                    Select(o => o.Item1 < o.Item2 ? o : Tuple.Create(o.Item2, o.Item1)).        // GetUniqueLines already makes Item1 less than Item2, but that code could change in the future
                    ToList();

                // Remove the undesirable edges
                foreach (var edge in removes)
                {
                    retVal.Remove(edge);        // this works, because I made sure that Item1 is less than Item2
                }

                return retVal.ToArray();
            }

            #region Private Methods

            private static bool Circum(double x1, double y1, double x2, double y2, double x3, double y3, ref double xc, ref double yc, ref double r)
            {
                // Calculation of circumscribed circle coordinates and squared radius

                const double eps = 1e-6;
                const double big = 1e12;
                bool result = true;
                double m1, m2, mx1, mx2, my1, my2, dx, dy;

                if ((Math.Abs(y1 - y2) < eps) && (Math.Abs(y2 - y3) < eps))
                {
                    result = false;
                    xc = x1; yc = y1; r = big;
                }
                else
                {
                    if (Math.Abs(y2 - y1) < eps)
                    {
                        m2 = -(x3 - x2) / (y3 - y2);
                        mx2 = (x2 + x3) / 2;
                        my2 = (y2 + y3) / 2;
                        xc = (x2 + x1) / 2;
                        yc = m2 * (xc - mx2) + my2;
                    }
                    else if (Math.Abs(y3 - y2) < eps)
                    {
                        m1 = -(x2 - x1) / (y2 - y1);
                        mx1 = (x1 + x2) / 2;
                        my1 = (y1 + y2) / 2;
                        xc = (x3 + x2) / 2;
                        yc = m1 * (xc - mx1) + my1;
                    }
                    else
                    {
                        m1 = -(x2 - x1) / (y2 - y1);
                        m2 = -(x3 - x2) / (y3 - y2);
                        if (Math.Abs(m1 - m2) < eps)
                        {
                            result = false;
                            xc = x1;
                            yc = y1;
                            r = big;
                        }
                        else
                        {
                            mx1 = (x1 + x2) / 2;
                            mx2 = (x2 + x3) / 2;
                            my1 = (y1 + y2) / 2;
                            my2 = (y2 + y3) / 2;
                            xc = (m1 * mx1 - m2 * mx2 + my2 - my1) / (m1 - m2);
                            yc = m1 * (xc - mx1) + my1;
                        }
                    }
                }

                dx = x2 - xc;
                dy = y2 - yc;
                r = dx * dx + dy * dy;

                return result;
            }

            private static Tuple<int, int> ShouldRemoveThinOuter(TriangleIndexedLinked_wpf triangle, double ratio)
            {
                // See which edges are interior and exterior facing
                var nullNonNullEdges = GetNullNonNullEdges(triangle);

                if (nullNonNullEdges.Item1.Count == 0)
                {
                    // This is an interior triangle
                    return null;
                }

                // Get the lengths, and put them in a common structure so the lists can be combined
                var nulls = nullNonNullEdges.Item1.Select(o => Tuple.Create(o, triangle.GetEdgeLength(o), true));
                var nonNulls = nullNonNullEdges.Item2.Select(o => Tuple.Create(o, triangle.GetEdgeLength(o), false));

                var all = UtilityCore.Iterate(nulls, nonNulls).
                    OrderBy(o => o.Item2).      // put the longest at the end of the list
                    ToArray();

                if (!all[2].Item3)
                {
                    // The longest segment is an interior facing segment, so it can't be removed
                    return null;
                }

                // The closer the ratio gets to one, the thinner the triangle is
                if (all[2].Item2 / (all[0].Item2 + all[1].Item2) >= ratio)
                {
                    // This can be removed.  Return the corresponding indexes
                    return Tuple.Create(triangle.GetIndex(all[2].Item1, true), triangle.GetIndex(all[2].Item1, false));
                }
                else
                {
                    return null;
                }
            }

            private static Tuple<List<TriangleEdge>, List<TriangleEdge>> GetNullNonNullEdges(TriangleIndexedLinked_wpf triangle)
            {
                List<TriangleEdge> nullEdges = new List<TriangleEdge>();
                List<TriangleEdge> nonNullEdges = new List<TriangleEdge>();

                if (triangle.Neighbor_01 == null)
                {
                    nullEdges.Add(TriangleEdge.Edge_01);
                }
                else
                {
                    nonNullEdges.Add(TriangleEdge.Edge_01);
                }

                if (triangle.Neighbor_12 == null)
                {
                    nullEdges.Add(TriangleEdge.Edge_12);
                }
                else
                {
                    nonNullEdges.Add(TriangleEdge.Edge_12);
                }

                if (triangle.Neighbor_20 == null)
                {
                    nullEdges.Add(TriangleEdge.Edge_20);
                }
                else
                {
                    nonNullEdges.Add(TriangleEdge.Edge_20);
                }

                return Tuple.Create(nullEdges, nonNullEdges);
            }

            private static double GetEdgeRatio(ITriangle_wpf triangle, TriangleEdge edge)
            {
                double edgeLength = triangle.GetEdgeLength(edge);
                double otherLength;

                switch (edge)
                {
                    case TriangleEdge.Edge_01:
                        otherLength = triangle.GetEdgeLength(TriangleEdge.Edge_12) + triangle.GetEdgeLength(TriangleEdge.Edge_20);
                        break;

                    case TriangleEdge.Edge_12:
                        otherLength = triangle.GetEdgeLength(TriangleEdge.Edge_01) + triangle.GetEdgeLength(TriangleEdge.Edge_20);
                        break;

                    case TriangleEdge.Edge_20:
                        otherLength = triangle.GetEdgeLength(TriangleEdge.Edge_01) + triangle.GetEdgeLength(TriangleEdge.Edge_12);
                        break;

                    default:
                        throw new ApplicationException("Unknown TriangleEdge: " + edge.ToString());
                }

                return edgeLength / otherLength;
            }

            #endregion
        }

        #endregion
        #region class:
        #endregion

        #region simple

        public static bool IsNearZero(Vector testVect)
        {
            return Math.Abs(testVect.X) <= NEARZERO && Math.Abs(testVect.Y) <= NEARZERO;
        }
        public static bool IsNearZero(Point testPoint)
        {
            return Math.Abs(testPoint.X) <= NEARZERO && Math.Abs(testPoint.Y) <= NEARZERO;
        }
        public static bool IsNearValue(Vector testVect, Vector compareTo)
        {
            return testVect.X >= compareTo.X - NEARZERO && testVect.X <= compareTo.X + NEARZERO &&
                        testVect.Y >= compareTo.Y - NEARZERO && testVect.Y <= compareTo.Y + NEARZERO;
        }
        public static bool IsNearValue(Point testPoint, Point compareTo)
        {
            return testPoint.X >= compareTo.X - NEARZERO && testPoint.X <= compareTo.X + NEARZERO &&
                        testPoint.Y >= compareTo.Y - NEARZERO && testPoint.Y <= compareTo.Y + NEARZERO;
        }

        public static bool IsInvalid(Vector testVect)
        {
            return Math1D.IsInvalid(testVect.X) || Math1D.IsInvalid(testVect.Y);
        }
        public static bool IsInvalid(Point testVect)
        {
            return Math1D.IsInvalid(testVect.X) || Math1D.IsInvalid(testVect.Y);
        }

        #endregion

        #region misc

        /// <summary>
        /// This returns the center of position of the points
        /// </summary>
        public static Point GetCenter(IEnumerable<Point> points)
        {
            if (points == null)
            {
                return new Point(0, 0);
            }

            double x = 0d;
            double y = 0d;

            int length = 0;

            foreach (Point point in points)
            {
                x += point.X;
                y += point.Y;

                length++;
            }

            if (length == 0)
            {
                return new Point(0, 0);
            }

            double oneOverLen = 1d / Convert.ToDouble(length);

            return new Point(x * oneOverLen, y * oneOverLen);
        }
        /// <summary>
        /// This returns the center of mass of the points
        /// </summary>
        public static Point GetCenter(Tuple<Point, double>[] pointsMasses)
        {
            if (pointsMasses == null || pointsMasses.Length == 0)
            {
                return new Point(0, 0);
            }

            double totalMass = pointsMasses.Sum(o => o.Item2);
            if (Math1D.IsNearZero(totalMass))
            {
                return GetCenter(pointsMasses.Select(o => o.Item1).ToArray());
            }

            double x = 0d;
            double y = 0d;

            foreach (var pointMass in pointsMasses)
            {
                x += pointMass.Item1.X * pointMass.Item2;
                y += pointMass.Item1.Y * pointMass.Item2;
            }

            double totalMassInverse = 1d / totalMass;

            return new Point(x * totalMassInverse, y * totalMassInverse);
        }

        /// <summary>
        /// This is identical to GetCenter.  (with points, that is thought of as the center.  With vectors, that's thought of as the
        /// average - even though it's the same logic)
        /// </summary>
        public static Vector GetAverage(IEnumerable<Vector> vectors)
        {
            if (vectors == null)
            {
                return new Vector(0, 0);
            }

            double x = 0d;
            double y = 0d;

            int length = 0;

            foreach (Vector vector in vectors)
            {
                x += vector.X;
                y += vector.Y;

                length++;
            }

            if (length == 0)
            {
                return new Vector(0, 0);
            }

            double oneOverLen = 1d / Convert.ToDouble(length);

            return new Vector(x * oneOverLen, y * oneOverLen);
        }

        public static Vector GetSum(IEnumerable<Vector> vectors)
        {
            if (vectors == null)
            {
                return new Vector(0, 0);
            }

            double x = 0d;
            double y = 0d;

            foreach (Vector vector in vectors)
            {
                x += vector.X;
                y += vector.Y;
            }

            return new Vector(x, y);
        }

        public static Point[] GetUnique(IEnumerable<Point> points)
        {
            List<Point> retVal = new List<Point>();

            foreach (Point point in points)
            {
                if (!retVal.Any(o => Math2D.IsNearValue(o, point)))
                {
                    retVal.Add(point);
                }
            }

            return retVal.ToArray();
        }
        public static Vector[] GetUnique(IEnumerable<Vector> vectors)
        {
            List<Vector> retVal = new List<Vector>();

            foreach (Vector vector in vectors)
            {
                if (!retVal.Any(o => Math2D.IsNearValue(o, vector)))
                {
                    retVal.Add(vector);
                }
            }

            return retVal.ToArray();
        }

        /// <summary>
        /// This gets the area of any polygon as long as edges don't cross over (like a 4 point creating a bow tie)
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://www.wikihow.com/Calculate-the-Area-of-a-Polygon
        /// http://www.mathopenref.com/coordpolygonarea.html
        /// 
        /// What a crazy algorithm.  It even works with negative coordinates
        /// </remarks>
        public static double GetAreaPolygon(Point[] polygon)
        {
            double sum = 0d;

            for (int cntr = 0; cntr < polygon.Length - 1; cntr++)
            {
                sum += (polygon[cntr].X * polygon[cntr + 1].Y) - (polygon[cntr].Y * polygon[cntr + 1].X);
            }

            int last = polygon.Length - 1;
            sum += (polygon[last].X * polygon[0].Y) - (polygon[last].Y * polygon[0].X);

            return Math.Abs(sum) / 2d;
        }
        public static double GetAreaPolygon(Point3D[] polygon)
        {
            Vector3D normal = GetPolygonNormal(polygon, PolygonNormalLength.DoesntMatter);
            return GetAreaPolygon(polygon, normal);
        }
        public static double GetAreaPolygon(Point3D[] polygon, Vector3D normal)
        {
            // Rotate into 2D
            RotateTransform3D rotateTo2D = new RotateTransform3D(new QuaternionRotation3D(Math3D.GetRotation(normal, new Vector3D(0, 0, 1))));

            Point[] poly2D = new Point[polygon.Length];

            for (int cntr = 0; cntr < polygon.Length; cntr++)
            {
                Point3D rotated = rotateTo2D.Transform(polygon[cntr]);
                poly2D[cntr] = new Point(rotated.X, rotated.Y);
            }

            // Get area using transformed 2D points
            return Math2D.GetAreaPolygon(poly2D);
        }

        public static Vector3D GetPolygonNormal(Point[] polygon, PolygonNormalLength returnLength)
        {
            //NOTE: Even though this is a copy of the 3D overload's code, it would be inneficient to convert all points to 3D just to call
            //that overload (especially if they request PolygonNormalLength.PolygonArea)

            if (polygon.Length < 3)
            {
                throw new ArgumentException("The polygon passed in must have at least 3 points");
            }

            Vector3D direction1 = polygon[1].ToPoint3D() - polygon[0].ToPoint3D();
            if (Math3D.IsNearZero(direction1))
            {
                throw new ApplicationException("First two points are identical");
            }

            Vector3D? retVal = null;

            // Can't just blindly use the first three points, they could be colinear
            for (int cntr = 1; cntr < polygon.Length - 1; cntr++)
            {
                Vector3D direction2 = polygon[cntr + 1].ToPoint3D() - polygon[cntr].ToPoint3D();

                retVal = Vector3D.CrossProduct(direction1, direction2);

                if (!Math3D.IsInvalid(retVal.Value) && !Math3D.IsNearZero(retVal.Value))        // it will be invalid or zero if the vectors are colinear
                {
                    break;
                }
            }

            if (retVal == null)
            {
                throw new ArgumentException("The points in the polygon are colinear");
            }

            switch (returnLength)
            {
                case PolygonNormalLength.DoesntMatter:
                    break;

                case PolygonNormalLength.Unit:
                    retVal.Value.Normalize();
                    break;

                case PolygonNormalLength.PolygonArea:
                    retVal.Value.Normalize();
                    retVal = retVal.Value * GetAreaPolygon(polygon);
                    break;

                default:
                    throw new ApplicationException("Unknown PolygonNormalLength: " + returnLength.ToString());
            }

            return retVal.Value;
        }
        public static Vector3D GetPolygonNormal(Point3D[] polygon, PolygonNormalLength returnLength)
        {
            if (polygon.Length < 3)
            {
                throw new ArgumentException("The polygon passed in must have at least 3 points");
            }

            Vector3D direction1 = polygon[1] - polygon[0];
            if (Math3D.IsNearZero(direction1))
            {
                throw new ApplicationException("First two points are identical");
            }

            Vector3D? retVal = null;

            // Can't just blindly use the first three points, they could be colinear
            for (int cntr = 1; cntr < polygon.Length - 1; cntr++)
            {
                Vector3D direction2 = polygon[cntr + 1] - polygon[cntr];

                retVal = Vector3D.CrossProduct(direction1, direction2);

                if (!Math3D.IsInvalid(retVal.Value) && !Math3D.IsNearZero(retVal.Value))        // it will be invalid or zero if the vectors are colinear
                {
                    break;
                }
            }

            if (retVal == null)
            {
                throw new ArgumentException("The points in the polygon are colinear");
            }

            switch (returnLength)
            {
                case PolygonNormalLength.DoesntMatter:
                    break;

                case PolygonNormalLength.Unit:
                    retVal = retVal.Value.ToUnit();     // can't just call .Normalize, because it's a nullable, so readonly
                    break;

                case PolygonNormalLength.PolygonArea:
                    retVal = retVal.Value.ToUnit();
                    retVal = retVal.Value * GetAreaPolygon(polygon, retVal.Value);
                    break;

                default:
                    throw new ApplicationException("Unknown PolygonNormalLength: " + returnLength.ToString());
            }

            return retVal.Value;
        }

        /// <summary>
        /// Got this here:
        /// http://social.msdn.microsoft.com/Forums/windows/en-US/95055cdc-60f8-4c22-8270-ab5f9870270a/determine-if-the-point-is-in-the-polygon-c
        /// 
        /// Explanation here:
        /// http://conceptual-misfire.awardspace.com/point_in_polygon.htm
        /// </summary>
        /// <param name="includeEdgeHits">
        /// null=Edge hits aren't handled specially (some edge hits may return true, some may return false)
        /// true=Edge hits return true
        /// false=Edge hits return false
        /// </param>
        public static bool IsInsidePolygon(Point[] polygon, Point testPoint, bool? includeEdgeHits)
        {
            if (includeEdgeHits != null && IsOnEdgeOfPolygon(polygon, testPoint))
            {
                // The point is sitting on the edge.  Return what they asked for
                return includeEdgeHits.Value;
            }

            if (polygon.Length < 3)
            {
                return false;
            }

            Point p1, p2;
            bool inside = false;

            Point oldPoint = new Point(polygon[polygon.Length - 1].X, polygon[polygon.Length - 1].Y);

            for (int i = 0; i < polygon.Length; i++)
            {
                Point newPoint = new Point(polygon[i].X, polygon[i].Y);

                if (newPoint.X > oldPoint.X)
                {
                    p1 = oldPoint;
                    p2 = newPoint;
                }
                else
                {
                    p1 = newPoint;
                    p2 = oldPoint;
                }

                if ((newPoint.X < testPoint.X) == (testPoint.X <= oldPoint.X) &&
                    (testPoint.Y - p1.Y) * (p2.X - p1.X) <
                    (p2.Y - p1.Y) * (testPoint.X - p1.X))
                {
                    inside = !inside;
                }

                oldPoint = newPoint;
            }

            return inside;
        }
        public static bool IsOnEdgeOfPolygon(Point[] polygon, Point testPoint)
        {
            foreach (Edge2D_wpf edge in Math2D.IterateEdges(polygon, null))
            {
                double distance = (Math2D.GetNearestPoint_Edge_Point(edge, testPoint) - testPoint).Length;

                if (Math1D.IsNearZero(distance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// This is a copy of Math3D's version
        /// </summary>
        public static Vector ToBarycentric(Point p0, Point p1, Point p2, Point testPoint)
        {
            // Compute vectors        
            Vector v0 = p2 - p0;
            Vector v1 = p1 - p0;
            Vector v2 = testPoint - p0;

            // Compute dot products
            double dot00 = Vector.Multiply(v0, v0);
            double dot01 = Vector.Multiply(v0, v1);
            double dot02 = Vector.Multiply(v0, v2);
            double dot11 = Vector.Multiply(v1, v1);
            double dot12 = Vector.Multiply(v1, v2);

            // Compute barycentric coordinates
            double invDenom = 1d / (dot00 * dot11 - dot01 * dot01);
            double u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            double v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            return new Vector(u, v);
        }

        public static double GetAspectRatio(Size size)
        {
            return size.Width / size.Height;
        }

        public static TransformsToFrom2D_wpf GetTransformTo2D(Point3D[] polygon)
        {
            if (polygon.Length < 3)
            {
                throw new ArgumentException("The polygon must have at least three points: " + polygon.Length.ToString());
            }

            return GetTransformTo2D(new Triangle_wpf(polygon[0], polygon[1], polygon[2]));
        }
        public static TransformsToFrom2D_wpf GetTransformTo2D(ITriangle_wpf triangle)
        {
            Vector3D zUp = new Vector3D(0, 0, 1);

            if (Math.Abs(Vector3D.DotProduct(triangle.NormalUnit, zUp)).IsNearValue(1))
            {
                // It's already 2D
                return new TransformsToFrom2D_wpf()
                {
                    From3D_To2D = new TranslateTransform3D(0, 0, -triangle.Point0.Z),
                    From2D_BackTo3D = new TranslateTransform3D(0, 0, triangle.Point0.Z),
                };
            }

            // Don't bother with a double vector, just rotate the normal
            Quaternion rotation = Math3D.GetRotation(triangle.NormalUnit, zUp);

            Transform3DGroup transformTo2D = new Transform3DGroup();
            transformTo2D.Children.Add(new RotateTransform3D(new QuaternionRotation3D(rotation)));

            // Need to rotate the point so that it's parallel to the XY plane, then subtract off it's Z
            Point3D rotatedXYPlane = transformTo2D.Transform(triangle[0]);
            transformTo2D.Children.Add(new TranslateTransform3D(0, 0, -rotatedXYPlane.Z));

            Transform3DGroup transformTo3D = new Transform3DGroup();
            transformTo3D.Children.Add(new TranslateTransform3D(0, 0, rotatedXYPlane.Z));
            transformTo3D.Children.Add(new RotateTransform3D(new QuaternionRotation3D(rotation.ToReverse())));

            return new TransformsToFrom2D_wpf()
            {
                From3D_To2D = transformTo2D,
                From2D_BackTo3D = transformTo3D,
            };
        }

        public static List<Point> DedupePoints(List<Point> points)
        {
            List<Point> retVal = new List<Point>();

            if (points.Count == 0)
            {
                return retVal;
            }

            retVal.Add(points[0]);

            for (int cntr = 1; cntr < points.Count; cntr++)
            {
                if (!IsNearValue(points[cntr], retVal[retVal.Count - 1]))       // don't need to dedupe against all existing points, just the prev one
                {
                    retVal.Add(points[cntr]);
                }
            }

            return retVal;
        }

        /// <summary>
        /// This iterates through the edges of the polygon.  The Edge2D.EdgeType will always be Segment
        /// </summary>
        /// <param name="isClockwise">
        /// null: doesn't matter
        /// true: walk clockwise
        /// false: walk counter clockwise
        /// </param>
        public static IEnumerable<Edge2D_wpf> IterateEdges(Point[] polygon, bool? isClockwise)
        {
            bool traverseAsIs = true;
            if (isClockwise != null)
            {
                traverseAsIs = IsClockwise(polygon) == isClockwise.Value;
            }

            if (traverseAsIs)
            {
                #region Already proper direction

                for (int cntr = 0; cntr < polygon.Length - 1; cntr++)
                {
                    yield return new Edge2D_wpf(cntr, cntr + 1, polygon);
                }

                yield return new Edge2D_wpf(polygon.Length - 1, 0, polygon);

                #endregion
            }
            else
            {
                #region Reverse

                for (int cntr = polygon.Length - 1; cntr > 0; cntr--)
                {
                    yield return new Edge2D_wpf(cntr, cntr - 1, polygon);
                }

                yield return new Edge2D_wpf(0, polygon.Length - 1, polygon);

                #endregion
            }
        }

        public static Tuple<Point, Point> GetAABB(IEnumerable<Point> points)
        {
            bool foundOne = false;
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (Point point in points)
            {
                foundOne = true;        // it's too expensive to look at points.Count()

                if (point.X < minX)
                {
                    minX = point.X;
                }

                if (point.Y < minY)
                {
                    minY = point.Y;
                }

                if (point.X > maxX)
                {
                    maxX = point.X;
                }

                if (point.Y > maxY)
                {
                    maxY = point.Y;
                }
            }

            if (!foundOne)
            {
                // There were no points passed in
                //TODO: May want an exception
                return Tuple.Create(new Point(0, 0), new Point(0, 0));
            }

            return Tuple.Create(new Point(minX, minY), new Point(maxX, maxY));
        }
        public static Tuple<Vector, Vector> GetAABB(IEnumerable<Vector> points)
        {
            //NOTE: Copied for speed

            bool foundOne = false;
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (Vector point in points)
            {
                foundOne = true;        // it's too expensive to look at points.Count()

                if (point.X < minX)
                {
                    minX = point.X;
                }

                if (point.Y < minY)
                {
                    minY = point.Y;
                }

                if (point.X > maxX)
                {
                    maxX = point.X;
                }

                if (point.Y > maxY)
                {
                    maxY = point.Y;
                }
            }

            if (!foundOne)
            {
                // There were no points passed in
                //TODO: May want an exception
                return Tuple.Create(new Vector(0, 0), new Vector(0, 0));
            }

            return Tuple.Create(new Vector(minX, minY), new Vector(maxX, maxY));
        }
        public static Rect GetAABB(IEnumerable<Rect> rectangles)
        {
            //NOTE: Copied for speed

            bool foundOne = false;
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (Rect rect in rectangles)
            {
                foundOne = true;        // it's too expensive to look at points.Count()

                if (rect.X < minX)
                {
                    minX = rect.X;
                }

                if (rect.Y < minY)
                {
                    minY = rect.Y;
                }

                if (rect.Right > maxX)
                {
                    maxX = rect.Right;
                }

                if (rect.Bottom > maxY)
                {
                    maxY = rect.Bottom;
                }
            }

            if (!foundOne)
            {
                // There were no rectangles passed in
                //TODO: May want an exception
                return new Rect(0, 0, 0, 0);
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        public static Point LERP(Point a, Point b, double percent)
        {
            return new Point(
                a.X + (b.X - a.X) * percent,
                a.Y + (b.Y - a.Y) * percent);
        }
        public static Vector LERP(Vector a, Vector b, double percent)
        {
            return new Vector(
                a.X + (b.X - a.X) * percent,
                a.Y + (b.Y - a.Y) * percent);
        }

        /// <summary>
        /// This is the same as calling the other overload, and not ensuring coplanar.  Just removing the hassle of ignoring
        /// the out transforms.
        /// </summary>
        public static Point[] GetRotatedPoints(Point3D[] points)
        {
            Transform3D dummy1, dummy2;
            return GetRotatedPoints(out dummy1, out dummy2, points, false);
        }
        /// <summary>
        /// This overload assumes that there are at least 3 points, or it will just return null
        /// </summary>
        /// <remarks>
        /// If the input points aren't perfectly coplanar, then the out transforms aren't terribly useful (assuming ensureCoplanarInput is false)
        /// 
        /// Also, don't go too nuts with the input points variance from the plane, or the returned 2D points may not form the same shaped
        /// polygon that the input 3D points did (assuming the points passed in are for a polygon, it could just be a point cloud)
        /// </remarks>
        /// <param name="ensureCoplanarInput">
        /// True = Once the plane is calculated, any points no lying on that plane will cause this method to return null.
        /// False = The points are assumed to be not perfectly coplanar, and a slightly more expensive (but far more forgiving) method is called.
        /// </param>
        public static Point[] GetRotatedPoints(out Transform3D transformTo2D, out Transform3D transformTo3D, Point3D[] points, bool ensureCoplanarInput = false)
        {
            ITriangle_wpf plane;
            if (ensureCoplanarInput)
            {
                plane = GetPlane_Strict(points);
            }
            else
            {
                plane = GetPlane_Average(points, true);
            }

            if (plane == null)
            {
                transformTo2D = null;
                transformTo3D = null;
                return null;
            }

            // Figure out a transform that will make Z drop out
            var transform = GetTransformTo2D(plane);
            transformTo2D = transform.From3D_To2D;
            transformTo3D = transform.From2D_BackTo3D;

            // Transform them
            Point[] retVal = new Point[points.Length];
            for (int cntr = 0; cntr < points.Length; cntr++)
            {
                Point3D transformed = transformTo2D.Transform(points[cntr]);
                retVal[cntr] = new Point(transformed.X, transformed.Y);
            }

            return retVal;
        }
        /// <summary>
        /// This overload handles exactly two points
        /// </summary>
        public static Point[] GetRotatedPoints(out Transform3D transformTo2D, out Transform3D transformTo3D, Point3D point1, Point3D point2)
        {
            // Get rotation
            Quaternion rotation = Quaternion.Identity;
            if (!Math3D.IsNearValue(point1, point2))
            {
                Vector3D line1 = point2 - point1;		// this line is not along the z plane
                Vector3D line2 = new Point3D(point2.X, point2.Y, point1.Z) - point1;		// this line uses point1's z so is in the z plane

                rotation = Math3D.GetRotation(line1, line2);
            }

            // To 2D
            Transform3DGroup group = new Transform3DGroup();
            group.Children.Add(new RotateTransform3D(new QuaternionRotation3D(rotation)));
            group.Children.Add(new TranslateTransform3D(0, 0, -point1.Z));
            transformTo2D = group;

            // To 3D
            group = new Transform3DGroup();
            group.Children.Add(new TranslateTransform3D(0, 0, point1.Z));
            group.Children.Add(new RotateTransform3D(new QuaternionRotation3D(rotation.ToReverse())));
            transformTo3D = group;

            // Transform the points
            Point[] retVal = new Point[2];
            Point3D transformedPoint = transformTo2D.Transform(point1);
            retVal[0] = new Point(transformedPoint.X, transformedPoint.Y);

            transformedPoint = transformTo2D.Transform(point2);
            retVal[1] = new Point(transformedPoint.X, transformedPoint.Y);

            return retVal;
        }

        /// <summary>
        /// This takes a set of points that are coplanar, or pretty close to coplanar, and returns the plane that they sit on
        /// </summary>
        public static ITriangle_wpf GetPlane_Average(Point3D[] points, bool matchPolyNormalDirection = false)
        {
            return AveragePlane_wpf.GetAveragePlane(points, matchPolyNormalDirection);
        }
        /// <summary>
        /// This returns the plane that these points sit on.  It also makes sure that all the points lie in the same plane as the returned triangle
        /// (some of the points may still be colinear or the same, but at least they are on the same plane)
        /// </summary>
        /// <remarks>
        /// NOTE: GetPolygonNormal has a lot of similarity with this method, but is simpler, cheaper
        /// </remarks>
        public static ITriangle_wpf GetPlane_Strict(Point3D[] points)
        {
            Vector3D? line1 = null;
            Vector3D? line1Unit = null;

            ITriangle_wpf retVal = null;

            for (int cntr = 1; cntr < points.Length; cntr++)
            {
                if (Math3D.IsNearValue(points[0], points[cntr]))
                {
                    // These points are sitting on top of each other
                    continue;
                }

                Vector3D line = points[cntr] - points[0];

                if (line1 == null)
                {
                    // Found the first line
                    line1 = line;
                    line1Unit = line.ToUnit();
                    continue;
                }

                if (retVal == null)
                {
                    if (!Math1D.IsNearValue(Math.Abs(Vector3D.DotProduct(line1Unit.Value, line.ToUnit())), 1d))
                    {
                        // These two lines aren't colinear.  Found the second line
                        retVal = new Triangle_wpf(points[0], points[0] + line1.Value, points[cntr]);
                    }

                    continue;
                }

                //if (!Math1D.IsNearZero(Vector3D.DotProduct(retVal.NormalUnit, line.ToUnit())))
                if (Math.Abs(Vector3D.DotProduct(retVal.NormalUnit, line.ToUnit())) > (Math3D.NEARZERO * 1000))        // this was being a bit too strict.  Loosening it a little
                {
                    // This point isn't coplanar with the triangle
                    return null;
                }
            }

            return retVal;
        }

        public static Tuple<int, int, double>[] GetDistancesBetween(Point[] positions)
        {
            List<Tuple<int, int, double>> retVal = new List<Tuple<int, int, double>>();

            for (int outer = 0; outer < positions.Length - 1; outer++)
            {
                for (int inner = outer + 1; inner < positions.Length; inner++)
                {
                    double distance = (positions[outer] - positions[inner]).Length;
                    retVal.Add(Tuple.Create(outer, inner, distance));
                }
            }

            return retVal.ToArray();
        }

        public static Point[] ApplyBallOfSprings(Point[] positions, Tuple<int, int, double>[] desiredDistances, int numIterations)
        {
            VectorND[] pos = positions.
                Select(o => new VectorND(o.X, o.Y)).
                ToArray();

            VectorND[] retVal = MathND.ApplyBallOfSprings(pos, desiredDistances, numIterations);

            return retVal.
                Select(o => new Point(o[0], o[1])).
                ToArray();
        }

        /// <summary>
        /// This returns grid cells
        /// </summary>
        /// <param name="size">The width and height of the total square</param>
        /// <param name="numCellsXY">The number of horizontal and vertical cells (2 would be a 2x2, 3 would be 3x3, etc)</param>
        public static (Rect rect, Point center)[] GetCells_WithinSquare(double totalSize, int numCellsXY, double margin = 0, Point? center = null, bool invertY = false)
        {
            if (numCellsXY <= 0)
            {
                return new (Rect, Point)[0];
            }

            double cellSize = (totalSize - (margin * (numCellsXY - 1))) / numCellsXY;

            if (invertY)
            {
                return GetCells_InvertY(cellSize, numCellsXY, numCellsXY, margin, center);
            }
            else
            {
                return GetCells(cellSize, numCellsXY, numCellsXY, margin, center);
            }
        }
        /// <summary>
        /// This overload handles MxN cells.  Instead of taking in the final desired size, it takes in the size of a cell
        /// and multiplies to get the final size
        /// </summary>
        public static (Rect rect, Point center)[] GetCells(double cellSize, int numCellsX, int numCellsY, double margin = 0, Point? center = null)
        {
            if (numCellsX <= 0 || numCellsY <= 0)
            {
                return new (Rect, Point)[0];
            }

            (Rect, Point)[] retVal = new (Rect, Point)[numCellsX * numCellsY];

            double offsetX = (cellSize * numCellsX) / -2;
            double offsetY = (cellSize * numCellsY) / -2;

            offsetX += (margin * (numCellsX - 1)) / -2;
            offsetY += (margin * (numCellsY - 1)) / -2;

            if (center != null)
            {
                offsetX += center.Value.X;
                offsetY += center.Value.Y;
            }

            double halfCellSize = cellSize / 2;

            for (int y = 0; y < numCellsY; y++)
            {
                int yIndex = y * numCellsX;

                for (int x = 0; x < numCellsX; x++)
                {
                    Point cellTopLeft = new Point(
                        offsetX + (cellSize * x) + (margin * x),
                        offsetY + (cellSize * y) + (margin * y));

                    Point cellCenter = new Point(cellTopLeft.X + halfCellSize, cellTopLeft.Y + halfCellSize);

                    retVal[yIndex + x] =
                    (
                        new Rect(cellTopLeft.X, cellTopLeft.Y, cellSize, cellSize),
                        cellCenter
                    );
                }
            }

            return retVal;
        }
        /// <summary>
        /// This overload makes the Y values go down instead of up
        /// </summary>
        /// <remarks>
        /// Standard 2D graphics have 0,0 at the top left and then x and y go out from there (the screen is in the fourth quadrant)
        /// 
        /// But 3D scenes have +Y go up instead of down.  So if you want to put these tiles into 3D, it would be easier to use this
        /// inverted overload (because in this overload, Y starts high and then goes negative)
        /// 
        /// Standard shown in 3D:
        /// 8   9   A   B
        /// 4   5   6   7
        /// 0   1   2   3
        /// 
        /// InvertY shown in 3D:
        /// 0   1   2   3
        /// 4   5   6   7
        /// 8   9   A   B
        /// </remarks>
        public static (Rect rect, Point center)[] GetCells_InvertY(double cellSize, int numCellsX, int numCellsY, double margin = 0, Point? center = null)
        {
            if (numCellsX <= 0 || numCellsY <= 0)
            {
                return new (Rect, Point)[0];
            }

            (Rect, Point)[] retVal = new (Rect, Point)[numCellsX * numCellsY];

            double offsetX = (cellSize * numCellsX) / -2;
            double offsetY = (cellSize * numCellsY) / 2;

            offsetX += (margin * (numCellsX - 1)) / -2;
            offsetY += (margin * (numCellsY - 1)) / 2;

            if (center != null)
            {
                offsetX += center.Value.X;
                offsetY += center.Value.Y;
            }

            double halfCellSize = cellSize / 2;

            for (int y = 0; y < numCellsY; y++)
            {
                int yIndex = y * numCellsX;

                for (int x = 0; x < numCellsX; x++)
                {
                    Point cellTopLeft = new Point(
                        offsetX + (cellSize * x) + (margin * x),
                        offsetY - (cellSize * y) - (margin * y) - cellSize);

                    Point cellCenter = new Point(cellTopLeft.X + halfCellSize, cellTopLeft.Y + halfCellSize);

                    retVal[yIndex + x] =
                    (
                        new Rect(cellTopLeft.X, cellTopLeft.Y, cellSize, cellSize),
                        cellCenter
                    );
                }
            }

            return retVal;
        }

        /// <summary>
        /// Use this to figure out how to call GetCells, GetCells_InvertY with an arbitrary number of cells.  This packs them into
        /// roughly a square
        /// </summary>
        public static VectorInt GetCellColumnsRows(int count)
        {
            int rows = Math.Sqrt(count).ToInt_Floor();

            int columns = count / rows;
            if (count % rows != 0)
            {
                columns++;
            }

            return new VectorInt(columns, rows);
        }

        #endregion

        #region Hulls/Graphs/Triangulation

        /// <summary>
        /// Voronoi algorithm takes a bunch of points, and returns polygons where all points inside of a polygon are closer to the contained
        /// point (that was passed in) than any others
        /// </summary>
        public static VoronoiResult2D_wpf GetVoronoi(Point[] points, bool returnEdgesByPoint)
        {
            return VoronoiUtil_wpf.GetVoronoi(points, returnEdgesByPoint);
        }
        public static VoronoiResult2D_wpf CapVoronoiCircle(VoronoiResult2D_wpf voronoi)
        {
            return VoronoiUtil_wpf.CapCircle(voronoi);
        }

        /// <summary>
        /// This finds triangles out of the points (a basic description of the Delaunay is that it tries to avoid thin triangles)
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://through-the-interface.typepad.com/through_the_interface/2009/04/triangulating-an-autocad-polyface-mesh-from-a-set-of-points-using-net.html
        /// 
        /// search for "delaunay triangulation" to read more
        /// </remarks>
        /// <param name="points">These are the points that will be processed</param>
        /// <param name="points3D">These are the same points, but could be in a different 3D plane (the 2D points are these points rotated onto the xy plane).  These points will be used in the return triangles</param>
        public static TriangleIndexed_wpf[] GetDelaunayTriangulation(Point[] points, Point3D[] points3D)
        {
            return Delaunay_wpf.GetDelaunayTriangulation(points, points3D);
        }
        /// <summary>
        /// This overload just returns the links from point to point
        /// </summary>
        /// <param name="throwOutThinOuterTriangles">This method generates long thin triangles along the outside that seem to only be there to make the polygon convex</param>
        /// <param name="skipThinRatio">If throwOutThinOuterTriangles is true, then this is the ratio that the edges need to divide to for the triangle to be considered thin (the ratio approaches 1.  The closer to 1, the more strict)</param>
        public static Tuple<int, int>[] GetDelaunayTriangulation(Point[] points, bool throwOutThinOuterTriangles = false, double skipThinRatio = .9)
        {
            TriangleIndexed_wpf[] triangles = Delaunay_wpf.GetDelaunayTriangulation(points, points.Select(o => o.ToPoint3D()).ToArray());

            if (throwOutThinOuterTriangles)
            {
                return Delaunay_wpf.ThrowOutThinTriangles(triangles, skipThinRatio);
            }
            else
            {
                return TriangleIndexed_wpf.GetUniqueLines(triangles);
            }
        }

        /// <summary>
        /// This returns a convex hull of lines that uses the outermost points (uses the quickhull algorithm)
        /// </summary>
        public static QuickHull2DResult_wpf GetConvexHull(Point3D[] points)
        {
            return QuickHull2D_wpf.GetConvexHull(points);
        }
        /// <summary>
        /// This returns a convex hull of lines that uses the outermost points (uses the quickhull algorithm)
        /// </summary>
        public static QuickHull2DResult_wpf GetConvexHull(Point[] points)
        {
            return QuickHull2D_wpf.GetConvexHull(points);
        }

        public static TriangleIndexed_wpf[] GetTrianglesFromConvexPoly(Point[] polygon, double z)
        {
            return GetTrianglesFromConvexPoly(polygon.Select(o => o.ToPoint3D(z)).ToArray());
        }
        public static TriangleIndexed_wpf[] GetTrianglesFromConvexPoly(Point3D[] polygon)
        {
            return GetTrianglesFromConvexPoly(Enumerable.Range(0, polygon.Length).ToArray(), polygon);
        }
        public static TriangleIndexed_wpf[] GetTrianglesFromConvexPoly(int[] polygon, Point3D[] allPoints)
        {
            //TODO: Support concave
            //http://www.siggraph.org/education/materials/HyperGraph/scanline/outprims/polygon1.htm
            //http://www.codeproject.com/Articles/8238/Polygon-Triangulation-in-C

            //TODO: Support with holes

            if (polygon.Length < 3)
            {
                throw new ArgumentException("There must be at least 3 points to make a triangle: " + polygon.Length.ToString());
            }

            var retVal = new List<TriangleIndexed_wpf>();

            // Start with 0,1,2
            retVal.Add(new TriangleIndexed_wpf(polygon[0], polygon[1], polygon[2], allPoints));

            int lowerIndex = 2;
            int upperIndex = polygon.Length - 1;
            int lastUsedIndex = 0;
            bool shouldBumpLower = true;

            // Do the rest of the triangles
            while (lowerIndex < upperIndex)
            {
                retVal.Add(new TriangleIndexed_wpf(polygon[lowerIndex], polygon[upperIndex], polygon[lastUsedIndex], allPoints));

                if (shouldBumpLower)
                {
                    lastUsedIndex = lowerIndex;
                    lowerIndex++;
                }
                else
                {
                    lastUsedIndex = upperIndex;
                    upperIndex--;
                }

                shouldBumpLower = !shouldBumpLower;
            }

            return retVal.ToArray();
        }
        public static TriangleIndexed_wpf[][] GetTrianglesFromConvexPoly(int[][] polygon, Point3D[] allPoints)
        {
            TriangleIndexed_wpf[][] retVal = new TriangleIndexed_wpf[polygon.Length][];

            for (int cntr = 0; cntr < retVal.Length; cntr++)
            {
                retVal[cntr] = GetTrianglesFromConvexPoly(polygon[cntr], allPoints);
            }

            return retVal;
        }

        public static Tuple<int, int, int>[] GetTrianglesFromConcavePoly(Point3D[] points)
        {
            return TriangulateConcave_wpf.Triangulate(GetRotatedPoints(points));
        }
        public static Tuple<int, int, int>[] GetTrianglesFromConcavePoly(Point[] points)
        {
            return TriangulateConcave_wpf.Triangulate(points);
        }
        public static TriangleIndexed_wpf[] GetTrianglesFromConcavePoly3D(Point3D[] points)
        {
            return GetTrianglesFromConcavePoly(points).
                Select(o => new TriangleIndexed_wpf(o.Item1, o.Item2, o.Item3, points)).
                ToArray();
        }

        /// <summary>
        /// This returns points around a unit circle.  The result is cached in a singleton, so any future request for the same number
        /// of sides is fast
        /// </summary>
        public static Point[] GetCircle_Cached(int numSides)
        {
            return PointsSingleton_wpf.Instance.GetPoints(numSides);
        }

        #endregion

        #region intersections

        /// <summary>
        /// Returns the intersection of the two lines (line segments are passed in, but they are treated like infinite lines)
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://stackoverflow.com/questions/14480124/how-do-i-detect-triangle-and-rectangle-intersection
        /// </remarks>
        public static Point? GetIntersection_Line_Line(Point line1From, Point line1To, Point line2From, Point line2To)
        {
            Vector direction1 = line1To - line1From;
            Vector direction2 = line2To - line2From;
            double dotPerp = (direction1.X * direction2.Y) - (direction1.Y * direction2.X);

            // If it's 0, it means the lines are parallel so have infinite intersection points
            if (Math1D.IsNearZero(dotPerp))
            {
                return null;
            }

            Vector c = line2From - line1From;
            double t = (c.X * direction2.Y - c.Y * direction2.X) / dotPerp;
            //if (t < 0 || t > 1)
            //{
            //    return null;		// lies outside the line segment
            //}

            //double u = (c.X * direction1.Y - c.Y * direction1.X) / dotPerp;
            //if (u < 0 || u > 1)
            //{
            //    return null;		// lies outside the line segment
            //}

            // Return the intersection point
            return line1From + (t * direction1);
        }
        /// <summary>
        /// Returns the intersection of the two line segments
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://stackoverflow.com/questions/14480124/how-do-i-detect-triangle-and-rectangle-intersection
        /// </remarks>
        public static Point? GetIntersection_LineSegment_LineSegment(Point line1From, Point line1To, Point line2From, Point line2To)
        {
            Vector direction1 = line1To - line1From;
            Vector direction2 = line2To - line2From;
            double dotPerp = (direction1.X * direction2.Y) - (direction1.Y * direction2.X);

            // If it's 0, it means the lines are parallel so have infinite intersection points
            if (Math1D.IsNearZero(dotPerp))
            {
                return null;
            }

            Vector c = line2From - line1From;
            double t = (c.X * direction2.Y - c.Y * direction2.X) / dotPerp;
            if (t < 0 || t > 1)
            {
                return null;		// lies outside the line segment
            }

            double u = (c.X * direction1.Y - c.Y * direction1.X) / dotPerp;
            if (u < 0 || u > 1)
            {
                return null;		// lies outside the line segment
            }

            // Return the intersection point
            return line1From + (t * direction1);
        }

        /// <summary>
        /// This clips the subject polygon against the clip polygon (gets the intersection of the two polygons)
        /// </summary>
        /// <remarks>
        /// Based on the psuedocode from:
        /// http://en.wikipedia.org/wiki/Sutherland%E2%80%93Hodgman
        /// </remarks>
        /// <param name="subjectPoly">Can be concave or convex</param>
        /// <param name="clipPoly">Must be convex</param>
        /// <returns>The intersection of the two polygons (or null)</returns>
        public static Point[] GetIntersection_Polygon_Polygon(Point[] subjectPoly, Point[] clipPoly)
        {
            return SutherlandHodgman_wpf.GetIntersectedPolygon(subjectPoly, clipPoly);
        }
        /// <summary>
        /// This overload takes a 2D polygon and triangle (but in 3D coords)
        /// NOTE: polygon and triangle are expected to be coplanar
        /// </summary>
        /// <param name="polygon">Can be concave or convex</param>
        public static Point3D[] GetIntersection_Polygon_Triangle(Point3D[] polygon, ITriangle_wpf triangle)
        {
            // triangle and polygon should be in the same plane.  Find a transform that will cause the Z to drop out
            Quaternion rotation = Math3D.GetRotation(triangle.Normal, new Vector3D(0, 0, 1));

            RotateTransform3D rotateTo2D = new RotateTransform3D(new QuaternionRotation3D(rotation));

            // Transform the points to the 2D plane (leaving the triangle as Point3D to get the z offset)
            Point3D[] triangleRotated = new Point3D[] { rotateTo2D.Transform(triangle.Point0), rotateTo2D.Transform(triangle.Point1), rotateTo2D.Transform(triangle.Point2) };
            Point[] polygonRotated = polygon.Select(o => rotateTo2D.Transform(o)).Select(o => new Point(o.X, o.Y)).ToArray();

            Point[] retVal = SutherlandHodgman_wpf.GetIntersectedPolygon(polygonRotated, triangleRotated.Select(o => new Point(o.X, o.Y)).ToArray());
            if (retVal == null || retVal.Length == 0)
            {
                return null;
            }

            // Transform clipped back into 3D
            RotateTransform3D rotateTo3D = new RotateTransform3D(new QuaternionRotation3D(rotation.ToReverse()));

            return retVal.Select(o => rotateTo3D.Transform(o.ToPoint3D(triangleRotated[0].Z))).ToArray();
        }
        /// <summary>
        /// This overload takes two 2D polygons (but in 3D coords)
        /// NOTE: Both polygons are expected to be coplanar
        /// </summary>
        /// <param name="subjectPoly">Can be concave or convex</param>
        /// <param name="clipPoly">Must be convex</param>
        public static Point3D[] GetIntersection_Polygon_Polygon(Point3D[] subjectPoly, Point3D[] clipPoly)
        {
            // triangle and polygon should be in the same plane.  Find a transform that will cause the Z to drop out
            Quaternion rotation = Math3D.GetRotation(GetPolygonNormal(clipPoly, PolygonNormalLength.DoesntMatter), new Vector3D(0, 0, 1));

            RotateTransform3D rotateTo2D = new RotateTransform3D(new QuaternionRotation3D(rotation));

            // Transform the points to the 2D plane
            Point3D[] clipRotated = clipPoly.Select(o => rotateTo2D.Transform(o)).ToArray();     // leaving the clip as Point3D to get the z offset
            Point[] subjectRotated = subjectPoly.Select(o => rotateTo2D.Transform(o)).Select(o => new Point(o.X, o.Y)).ToArray();

            Point[] retVal = SutherlandHodgman_wpf.GetIntersectedPolygon(subjectRotated, clipRotated.Select(o => new Point(o.X, o.Y)).ToArray());
            if (retVal == null || retVal.Length == 0)
            {
                return null;
            }

            // Transform clipped back into 3D
            RotateTransform3D rotateTo3D = new RotateTransform3D(new QuaternionRotation3D(rotation.ToReverse()));

            return retVal.Select(o => rotateTo3D.Transform(o.ToPoint3D(clipRotated[0].Z))).ToArray();
        }

        public static Point? GetIntersection_LineSegment_Circle(Point lineStart, Point lineEnd, Point circleCenter, double radius)
        {
            double? percent = GetIntersection_LineSegment_Circle_percent(lineStart, lineEnd, circleCenter, radius);
            if (percent == null)
            {
                return null;
            }
            else
            {
                return lineStart + ((lineEnd - lineStart) * percent.Value);
            }
        }
        /// <summary>
        /// Got this here:
        /// http://stackoverflow.com/questions/1073336/circle-line-collision-detection
        /// </summary>
        public static double? GetIntersection_LineSegment_Circle_percent(Point lineStart, Point lineEnd, Point circleCenter, double radius)
        {
            Vector lineDir = lineEnd - lineStart;

            Point C = circleCenter;
            double r = radius;
            Point E = lineStart;
            Vector d = lineDir;
            Vector f = E - C;

            Vector3D d3D = new Vector3D(d.X, d.Y, 0);
            Vector3D f3D = new Vector3D(f.X, f.Y, 0);

            double a = Vector3D.DotProduct(d3D, d3D);
            double b = 2d * Vector3D.DotProduct(f3D, d3D);
            double c = Vector3D.DotProduct(f3D, f3D) - (r * r);

            double discriminant = (b * b) - (4 * a * c);
            if (discriminant < 0d)
            {
                // no intersection
                return null;
            }
            else
            {
                // ray didn't totally miss circle, so there is a solution to the equation.
                discriminant = Math.Sqrt(discriminant);

                // either solution may be on or off the ray so need to test both
                double t1 = (-b + discriminant) / (2d * a);
                double t2 = (-b - discriminant) / (2d * a);

                if (t1 >= 0d && t1 <= 1d)
                {
                    // t1 solution on is ON THE RAY.
                    return t1;
                }
                else if (Math1D.IsNearZero(t1))
                {
                    return 0d;
                }
                else if (Math1D.IsNearValue(t1, 1d))
                {
                    return 1d;
                }
                else
                {
                    // t1 solution "out of range" of ray
                    //return null;
                }

                if (t2 >= 0d && t2 <= 1d)
                {
                    // t2 solution on is ON THE RAY.
                    return t2;
                }
                else if (Math1D.IsNearZero(t2))
                {
                    return 0d;
                }
                else if (Math1D.IsNearValue(t2, 1d))
                {
                    return 1d;
                }
                else
                {
                    // t2 solution "out of range" of ray
                }
            }

            return null;
        }

        /// <summary>
        /// This takes in an arbitrary number of polygons (it's ok for them to be concave), and returns polygons
        /// that are the union
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://sourceforge.net/projects/polyclipping/
        /// </remarks>
        public static Polygon2D_wpf[] GetUnion_Polygons(Point[][] polygons)
        {
            if (polygons == null || polygons.Length == 0)
            {
                return new Polygon2D_wpf[0];
            }

            double scale = GetUnion_Polygons_GetScale(polygons);
            var convertedPolys = GetUnion_Polygons_ConvertInput(polygons, scale);

            Clipper.Clipper clipper = new Clipper.Clipper();
            clipper.AddPolygons(convertedPolys, PolyType.ptSubject);        // when doing union, I don't think it matters what is subject and what is union
            //clipper.ForceSimple = true;       // not sure if this is helpful or not

            // Here is a page describing PolyFillType (nonzero is what you intuitively think of for a union)
            // http://www.angusj.com/delphi/clipper/documentation/Docs/Units/ClipperLib/Types/PolyFillType.htm

            PolyTree solution = new PolyTree();
            if (!clipper.Execute(ClipType.ctUnion, solution, PolyFillType.pftNonZero, PolyFillType.pftNonZero))
            {
                return new Polygon2D_wpf[0];
            }

            return GetUnion_Polygons_ConvertOutput(solution, 1d / scale);
        }

        /// <summary>
        /// This takes a bunch of polygons, and figures out which ones are inside of others
        /// WARNING: The polygons passed in cannot intersect each other, if you don't know, call GetUnion_Polygons instead.  It's more general purpose (but slower)
        /// </summary>
        /// <remarks>
        /// If there are polygons inside of holes, they are treated as holes of the outermost polygon
        /// </remarks>
        public static Tuple<int, int[]>[] GetPolygonIslands(Point[][] polygons)
        {
            // Find the polygons that are inside of others
            SortedList<int, List<int>> containers = GetPolygonIslands_FindHoles(polygons);

            // The initial pass finds parent-child, but doesn't detect grandchild and deeper.  Merge grandchildren so that containers.Keys is
            // only root polygons
            GetPolygonIslands_Roots(containers);

            // Build the return
            List<Tuple<int, int[]>> retVal = new List<Tuple<int, int[]>>();

            foreach (int key in containers.Keys)
            {
                retVal.Add(Tuple.Create(key, containers[key].Distinct().ToArray()));
            }

            int[] mentioned = UtilityCore.Iterate(containers.Keys, containers.Values.SelectMany(o => o)).ToArray();
            int[] notMentioned = Enumerable.Range(0, polygons.Length).Where(o => !mentioned.Contains(o)).ToArray();

            retVal.AddRange(notMentioned.Select(o => Tuple.Create(o, new int[0])));     // any polygons that aren't in containers are standalone

            return retVal.ToArray();
        }

        public static Point GetNearestPoint_Edge_Point(Edge2D_wpf edge, Point point)
        {
            switch (edge.EdgeType)
            {
                case EdgeType.Line:
                    return GetNearestPoint_Line_Point(edge.Point0, edge.Direction.Value, point);

                case EdgeType.Ray:
                    return GetNearestPoint_Ray_Point(edge.Point0, edge.Direction.Value, point);

                case EdgeType.Segment:
                    return GetNearestPoint_LineSegment_Point(edge.Point0, edge.Point1.Value, point);

                default:
                    throw new ApplicationException("Unknown EdgeType: " + edge.EdgeType.ToString());
            }
        }
        public static double GetClosestDistance_Edge_Point(Edge2D_wpf edge, Point point)
        {
            switch (edge.EdgeType)
            {
                case EdgeType.Line:
                    return GetClosestDistance_Line_Point(edge.Point0, edge.Direction.Value, point);

                case EdgeType.Ray:
                    return GetClosestDistance_Ray_Point(edge.Point0, edge.Direction.Value, point);

                case EdgeType.Segment:
                    return GetClosestDistance_LineSegment_Point(edge.Point0, edge.Point1.Value, point);

                default:
                    throw new ApplicationException("Unknown EdgeType: " + edge.EdgeType.ToString());
            }
        }

        /// <summary>
        /// Returns the nearest point between a point and line segment
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://stackoverflow.com/questions/849211/shortest-distance-between-a-point-and-a-line-segment
        /// </remarks>
        public static Point GetNearestPoint_LineSegment_Point(Point start, Point stop, Point test)
        {
            double segmentLenSqr = (stop - start).LengthSquared;
            if (Math1D.IsNearZero(segmentLenSqr))
            {
                return start;       //  line segment is just a point, so return that point
            }

            // Consider the line extending the segment, parameterized as start + t (stop - start).
            // We find projection of test point onto the line. 
            // It falls where t = [(test-start) . (stop-start)] / |stop-start|^2
            double t = Vector.Multiply(test - start, stop - start) / segmentLenSqr;
            if (t < 0d)
            {
                return start;       // Beyond the start of the segment
            }
            else if (t > 1d)
            {
                return stop;  // Beyond the stop of the segment
            }

            return start + t * (stop - start);  // Projection falls on the segment
        }
        public static double GetClosestDistance_LineSegment_Point(Point start, Point stop, Point test)
        {
            Point nearest = GetNearestPoint_LineSegment_Point(start, stop, test);
            return (test - nearest).Length;
        }

        public static Point GetNearestPoint_Ray_Point(Point start, Vector direction, Point test)
        {
            if (Math1D.IsNearZero(direction.LengthSquared))
            {
                return start;       //  the dirction is zero, so just return the start of the ray
            }

            // Consider the line extending the ray, parameterized as start + t (direction).
            // We find projection of test point onto the line. 
            // It falls where t = [(test-start) . direction]
            double t = Vector.Multiply(test - start, direction);
            if (t < 0d)
            {
                return start;       // Beyond the start of the ray
            }

            return start + t * (direction);  // Projection falls on the ray
        }
        public static double GetClosestDistance_Ray_Point(Point start, Vector direction, Point test)
        {
            Point nearest = GetNearestPoint_Ray_Point(start, direction, test);
            return (test - nearest).Length;
        }

        public static Point GetNearestPoint_Line_Point(Point pointOnLine, Vector direction, Point test)
        {
            if (Math1D.IsNearZero(direction.LengthSquared))
            {
                return pointOnLine;       //  the dirction is zero, so just return the point (this isn't tecnically correct, but the user should never pass in a zero length direction)
            }

            // Find projection of test point onto the line. 
            // It falls where t = [(test-start) . direction]
            double t = Vector.Multiply(test - pointOnLine, direction);

            return pointOnLine + t * (direction);
        }
        public static double GetClosestDistance_Line_Point(Point pointOnLine, Vector direction, Point test)
        {
            Point nearest = GetNearestPoint_Line_Point(pointOnLine, direction, test);
            return (test - nearest).Length;
        }

        #endregion

        #region Private Methods

        private static bool IsClockwise(Point[] polygon)
        {
            for (int cntr = 2; cntr < polygon.Length; cntr++)
            {
                bool? isLeft = IsLeftOf(new Edge2D_wpf(polygon[0], polygon[1]), polygon[cntr]);
                if (isLeft != null)		// some of the points may be colinear.  That's ok as long as the overall is a polygon
                {
                    return !isLeft.Value;
                }
            }

            throw new ArgumentException("All the points in the polygon are colinear");
        }

        /// <summary>
        /// Tells if the test point lies on the left side of the edge line
        /// </summary>
        private static bool? IsLeftOf(Edge2D_wpf edge, Point test)
        {
            Vector tmp1 = edge.Point1Ext - edge.Point0;
            Vector tmp2 = test - edge.Point1Ext;

            double x = (tmp1.X * tmp2.Y) - (tmp1.Y * tmp2.X);		// dot product of perpendicular?

            if (x < 0)
            {
                return false;
            }
            else if (x > 0)
            {
                return true;
            }
            else
            {
                // Colinear points;
                return null;
            }
        }

        private static double GetUnion_Polygons_GetScale(Point[][] polygons)
        {
            // I was going to go with massively large scale to use most of the range of int64, but there was a comment that says he
            // caps at +- 1.5 billion

            //var aabb = Math2D.GetAABB(polygons.SelectMany(o => o));
            //double max = Math3D.Max(aabb.Item1.X, aabb.Item1.Y, aabb.Item2.X, aabb.Item2.Y);


            //TODO: Don't scale if aabb is larger than some value
            //TODO: Scale a lot if there are a lot of points less than .001


            return 10000d;
        }
        private static List<List<IntPoint>> GetUnion_Polygons_ConvertInput(Point[][] polygons, double scale)
        {
            List<List<IntPoint>> retVal = new List<List<IntPoint>>();

            // The union method flakes out if the polygons have different windings (clockwise vs counter clockwise)
            Vector3D normal = Math2D.GetPolygonNormal(polygons[0], PolygonNormalLength.DoesntMatter);

            for (int cntr = 0; cntr < polygons.Length; cntr++)
            {
                Point[] points = polygons[cntr];

                if (cntr > 0)       // no need to compare the first poly with itself
                {
                    Vector3D normal2 = Math2D.GetPolygonNormal(points, PolygonNormalLength.DoesntMatter);

                    if (Vector3D.DotProduct(normal, normal2) < 0)
                    {
                        // This is wound the wrong direction.  Reverse it
                        points = points.Reverse().ToArray();
                    }
                }

                // Convert into the custom poly format
                retVal.Add(points.Select(o => new IntPoint(Convert.ToInt64(o.X * scale), Convert.ToInt64(o.Y * scale))).ToList());
            }

            return retVal;
        }
        /// <summary>
        /// Convert into an array of polygons
        /// </summary>
        /// <remarks>
        /// The polytree will nest deeply if solids are inside of holes.  But Polygon2D would treat the solids inside of holes as their own
        /// independent isntances
        /// 
        /// http://www.angusj.com/delphi/clipper/documentation/Docs/Units/ClipperLib/Classes/PolyTree/_Body.htm
        /// </remarks>
        private static Polygon2D_wpf[] GetUnion_Polygons_ConvertOutput(PolyTree solution, double scaleInverse)
        {
            List<Polygon2D_wpf> retVal = new List<Polygon2D_wpf>();

            // Walk the tree, and get all parents (need to look at contour count so that the root gets skipped)
            foreach (PolyNode parent in ((PolyNode)solution).Descendants(o => o.Childs).Where(o => !o.IsHole && o.Contour.Count > 0))
            {
                // Convert the parent polygon
                Point[] points = parent.Contour.Select(o => new Point(o.X * scaleInverse, o.Y * scaleInverse)).ToArray();

                if (parent.Childs.Count == 0)
                {
                    // No holes
                    retVal.Add(new Polygon2D_wpf(points));
                }
                else
                {
                    List<Point[]> holes = new List<Point[]>();

                    foreach (PolyNode child in parent.Childs)
                    {
                        if (!child.IsHole)
                        {
                            throw new ApplicationException("Expected the child of a non hole to be a hole");
                        }

                        // Convert the hole polygon
                        holes.Add(child.Contour.Select(o => new Point(o.X * scaleInverse, o.Y * scaleInverse)).ToArray());
                    }

                    // Store with holes
                    retVal.Add(new Polygon2D_wpf(points, holes.ToArray()));
                }
            }

            return retVal.ToArray();
        }

        private static SortedList<int, List<int>> GetPolygonIslands_FindHoles(Point[][] polygons)
        {
            SortedList<int, List<int>> retVal = new SortedList<int, List<int>>();

            for (int outer = 0; outer < polygons.Length; outer++)
            {
                for (int inner = 0; inner < polygons.Length; inner++)
                {
                    if (outer == inner)
                    {
                        continue;
                    }

                    if (retVal.ContainsKey(outer) && retVal[outer].Contains(inner))
                    {
                        // This outer ate a hole that contained other holes.  inner is one of those grandchild holes
                        continue;
                    }

                    // There shouldn't be any intersections of polygons, so just need to see if one of the points is inside
                    //if (polygons2D[inner].All(o => Math2D.IsInsidePolygon(polygons2D[outer], o, null)))
                    if (Math2D.IsInsidePolygon(polygons[outer], polygons[inner][0], null))
                    {
                        if (!retVal.ContainsKey(outer))
                        {
                            retVal.Add(outer, new List<int>());
                        }

                        retVal[outer].Add(inner);

                        if (retVal.ContainsKey(inner))
                        {
                            retVal[outer].AddRange(retVal[inner]);      // don't worry about dupes.  The children will be deduped later
                            retVal.Remove(inner);
                        }
                    }
                }
            }

            return retVal;
        }
        private static void GetPolygonIslands_Roots(SortedList<int, List<int>> containers)
        {
            bool hadMerge = false;

            do
            {
                hadMerge = false;

                // Do a pass through all the keys, looking for a merge
                foreach (int key in containers.Keys.ToArray())      // may not need toarray here, but it feels cleaner (because a merge will call remove)
                {
                    foreach (int child in containers[key].ToArray())        // convert to array so that the merging won't affect the while loop
                    {
                        if (containers.ContainsKey(child))
                        {
                            containers[key].AddRange(containers[child]);        // don't worry about dupes, that will be handled later
                            containers.Remove(child);

                            hadMerge = true;
                        }
                    }

                    // If a key has done a merge, then need to go back to the outer loop and scan again
                    if (hadMerge)
                    {
                        break;
                    }
                }
            } while (hadMerge);
        }

        #endregion
    }

    #region class: QuickHull2DResult_wpf

    public class QuickHull2DResult_wpf
    {
        public QuickHull2DResult_wpf(Point[] points, int[] perimiterLines, Transform3D transformTo2D, Transform3D transformTo3D)
        {
            Points = points;
            PerimiterLines = perimiterLines;
            TransformTo2D = transformTo2D;
            TransformTo3D = transformTo3D;
        }

        public readonly Point[] Points;
        public readonly int[] PerimiterLines;
        private readonly Transform3D TransformTo2D;
        private readonly Transform3D TransformTo3D;

        public bool IsInside(Point point)
        {
            for (int cntr = 0; cntr < this.Points.Length - 1; cntr++)
            {
                if (!Math2D.QuickHull2D_wpf.IsRightOfLine(cntr, cntr + 1, point, this.Points))
                {
                    return false;
                }
            }

            if (!Math2D.QuickHull2D_wpf.IsRightOfLine(this.Points.Length - 1, 0, point, this.Points))
            {
                return false;
            }

            return true;
        }
        public Point? GetTransformedPoint(Point3D point)
        {
            // Use the transform to rotate/translate the point to the z plane
            Point3D transformed = point;
            if (this.TransformTo2D != null)
            {
                transformed = this.TransformTo2D.Transform(point);
            }

            // Only return a value if it's now in the z plane, which will only work if the point passed in is in the same plane as this.Points
            if (Math1D.IsNearZero(transformed.Z))
            {
                return new Point(transformed.X, transformed.Y);
            }
            else
            {
                return null;
            }
        }
        public Point3D GetTransformedPoint(Point point)
        {
            Point3D retVal = point.ToPoint3D();
            if (this.TransformTo3D != null)
            {
                retVal = this.TransformTo3D.Transform(retVal);
            }

            return retVal;
        }
    }

    #endregion
    #region class: VoronoiResult2D_wpf

    public class VoronoiResult2D_wpf
    {
        #region Constructor

        public VoronoiResult2D_wpf(Point[] controlPoints, Point[] edgePoints, Edge2D_wpf[] edges, int[][] edgesByControlPoint)
        {
            this.ControlPoints = controlPoints;
            this.EdgePoints = edgePoints;
            this.Edges = edges;
            this.EdgesByControlPoint = edgesByControlPoint;
        }

        #endregion

        /// <summary>
        /// These are the original points passed in
        /// </summary>
        public readonly Point[] ControlPoints;

        /// <summary>
        /// These are the points that make up the edges
        /// </summary>
        public readonly Point[] EdgePoints;
        /// <summary>
        /// These are the lines
        /// </summary>
        public readonly Edge2D_wpf[] Edges;

        /// <summary>
        /// This tells which set of edges are for each control point
        /// </summary>
        /// <remarks>
        /// EdgesByControlPoint[control points index][i]
        /// </remarks>
        public readonly int[][] EdgesByControlPoint;

        /// <summary>
        /// Tells whether the cell is a closed polygon, or contains rays
        /// </summary>
        public bool IsClosed(int controlPointIndex)
        {
            return this.EdgesByControlPoint[controlPointIndex].
                All(o => this.Edges[o].EdgeType == EdgeType.Segment);
        }

        public Point[] GetPolygon(int controlPointIndex, double rayLength)
        {
            Edge2D_wpf[] edges = this.EdgesByControlPoint[controlPointIndex].
                Select(o => this.Edges[o]).
                ToArray();

            return Edge2D_wpf.GetPolygon(edges, rayLength);
        }

        /// <summary>
        /// This returns which control points are neighbors for each control point
        /// </summary>
        /// <remarks>
        /// return[control point index][neighbor control point indices]
        /// </remarks>
        public int[][] GetNeighbors()
        {
            // Not all nodes may share an edge. ex: 8.  But edge points will get shared
            int[][] edgePointsByControlPoint = this.EdgesByControlPoint.
                Select(o =>
                    o.SelectMany(p => UtilityCore.Iterate<int>(this.Edges[p].Index0, this.Edges[p].Index1)).
                    Distinct().
                    ToArray()).
                ToArray();

            int[][] retVal = new int[this.ControlPoints.Length][];

            for (int outer = 0; outer < this.ControlPoints.Length; outer++)
            {
                List<int> neighbors = new List<int>();

                for (int inner = 0; inner < this.ControlPoints.Length; inner++)
                {
                    if (inner == outer)
                    {
                        continue;
                    }

                    if (UtilityCore.SharesItem(edgePointsByControlPoint[outer], edgePointsByControlPoint[inner]))
                    {
                        neighbors.Add(inner);
                    }
                }

                retVal[outer] = neighbors.ToArray();
            }

            return retVal;
        }

        /// <summary>
        /// This returns the neighbor information as a list of pairs
        /// </summary>
        public Tuple<int, int>[] GetNeighborLinks()
        {
            return this.GetNeighbors().
                SelectMany((o, i) => o.Select(p => Tuple.Create(Math.Min(i, p), Math.Max(i, p)))).
                Distinct().
                ToArray();
        }
    }

    #endregion

    #region class: Edge2D_wpf

    /// <summary>
    /// This represents a line in 2D (either a line segment, ray, or infinite line)
    /// </summary>
    /// <remarks>
    /// I decided to take an array of points, and store indexes into that array.  That makes it easier to compare points across
    /// multiple edges to see which ones are using the same points (int comparisons are exact, doubles are iffy)
    /// </remarks>
    public class Edge2D_wpf
    {
        #region Constructor

        public Edge2D_wpf(Point point0, Point point1)
        {
            this.EdgeType = EdgeType.Segment;
            this.Index0 = 0;
            this.Index1 = 1;
            this.Direction = null;
            this.AllEdgePoints = new Point[] { point0, point1 };
        }
        public Edge2D_wpf(int index0, int index1, Point[] allEdgePoints)
        {
            this.EdgeType = EdgeType.Segment;
            this.Index0 = index0;
            this.Index1 = index1;
            this.Direction = null;
            this.AllEdgePoints = allEdgePoints;
        }
        public Edge2D_wpf(EdgeType edgeType, int index0, Vector direction, Point[] allEdgePoints)
        {
            if (edgeType == EdgeType.Segment)
            {
                throw new ArgumentException("This overload requires edge type to be Ray or Line, not Segment");
            }

            this.EdgeType = edgeType;
            this.Index0 = index0;
            this.Index1 = null;
            this.Direction = direction;
            this.AllEdgePoints = allEdgePoints;
        }

        #endregion

        /// <summary>
        /// This tells what type of line this edge represents
        /// </summary>
        /// <remarks>
        /// Segment:
        ///     Index0, Index1 will be populated
        /// 
        /// Ray:
        ///     Index0, Direction will be populated
        ///     
        /// Line:
        ///     Index0, Direction will be populated, but to get the full line, use the opposite of direction as well
        /// </remarks>
        public readonly EdgeType EdgeType;

        public readonly int Index0;
        public readonly int? Index1;

        public readonly Vector? Direction;
        /// <summary>
        /// This either returns Direction (if the edge is a ray or line), or it returns Point1 - Point0
        /// (this is helpful if you just want to treat the edge like a segment)
        /// </summary>
        public Vector DirectionExt
        {
            get
            {
                if (this.Direction != null)
                {
                    return this.Direction.Value;
                }
                else
                {
                    return this.Point1.Value - this.Point0;
                }
            }
        }

        public Point Point0
        {
            get
            {
                return this.AllEdgePoints[this.Index0];
            }
        }
        public Point? Point1
        {
            get
            {
                if (this.Index1 == null)
                {
                    return null;
                }

                return this.AllEdgePoints[this.Index1.Value];
            }
        }
        /// <summary>
        /// This either returns Point1 (if the edge is a segment), or it returns Point0 + Direction
        /// (this is helpful if you just want to always treat the edge like a segment)
        /// </summary>
        public Point Point1Ext
        {
            get
            {
                if (this.Point1 != null)
                {
                    return this.Point1.Value;
                }
                else
                {
                    return this.Point0 + this.Direction.Value;
                }
            }
        }

        public readonly Point[] AllEdgePoints;

        #region Public Methods

        /// <summary>
        /// This is the same as Point1Ext get;, but lets the user pass in how long the extension should be
        /// </summary>
        public Point GetPoint1Ext(double extensionLength)
        {
            if (this.Point1 != null)
            {
                return this.Point1.Value;
            }
            else
            {
                return this.Point0 + (this.Direction.Value.ToUnit(true) * extensionLength);
            }
        }

        /// <summary>
        /// This tells which edges are touching each of the other edges
        /// NOTE: This only looks at the ints.  It doesn't project lines
        /// </summary>
        public static int[][] GetTouchingEdges(Edge2D_wpf[] edges)
        {
            int[][] retVal = new int[edges.Length][];

            for (int outer = 0; outer < edges.Length; outer++)
            {
                List<int> touching = new List<int>();

                for (int inner = 0; inner < edges.Length; inner++)
                {
                    if (outer == inner)
                    {
                        continue;
                    }

                    if (IsTouching(edges[outer], edges[inner]))
                    {
                        touching.Add(inner);
                    }
                }

                retVal[outer] = touching.ToArray();
            }

            return retVal;
        }

        /// <summary>
        /// This returns whether the two edges touch
        /// NOTE: It only compares ints.  It doesn't check if lines cross each other
        /// </summary>
        public static bool IsTouching(Edge2D_wpf edge0, Edge2D_wpf edge1)
        {
            return GetCommonIndex(edge0, edge1) >= 0;
        }
        public static int GetCommonIndex(Edge2D_wpf edge0, Edge2D_wpf edge1)
        {
            //  All edge types have an index 0, so get that comparison out of the way
            if (edge0.Index0 == edge1.Index0)
            {
                return edge0.Index0;
            }

            if (edge0.EdgeType == EdgeType.Segment)
            {
                //  Extra check, since edge0 is a segment
                if (edge0.Index1.Value == edge1.Index0)
                {
                    return edge0.Index1.Value;
                }

                //  If edge1 is also a segment, then compare its endpoint to edge0's points
                if (edge1.EdgeType == EdgeType.Segment)
                {
                    if (edge1.Index1.Value == edge0.Index0)
                    {
                        return edge1.Index1.Value;
                    }
                    else if (edge1.Index1.Value == edge0.Index1.Value)
                    {
                        return edge1.Index1.Value;
                    }
                }
            }
            else if (edge1.EdgeType == EdgeType.Segment)
            {
                //  Edge1 is a segment, but edge0 isn't, so just need the single compare
                if (edge1.Index1.Value == edge0.Index0)
                {
                    return edge1.Index1.Value;
                }
            }

            //  No more compares needed (this method doesn't bother with projecting rays/lines to see if they intersect, that's left up to the caller if they need it)
            return -1;
        }

        /// <summary>
        /// This returns the point in common between the two edges, and vectors that represent rays coming out of
        /// that point
        /// </summary>
        /// <remarks>
        /// This will throw an exception if the edges don't share a common point
        /// 
        /// It doesn't matter if the edges are segments or rays (will bomb if either is a line)
        /// </remarks>
        public static Tuple<Point, Vector, Vector> GetRays(Edge2D_wpf edge0, Edge2D_wpf edge1)
        {
            if (edge0.EdgeType == EdgeType.Line || edge1.EdgeType == EdgeType.Line)
            {
                throw new ArgumentException("This method doesn't allow lines, only segments and rays");
            }

            int common = GetCommonIndex(edge0, edge1);
            if (common < 0)
            {
                throw new ArgumentException("The edges passed in don't share a common point");
            }

            return Tuple.Create(
                edge0.AllEdgePoints[common],
                GetDirectionFromPoint(edge0, common),
                GetDirectionFromPoint(edge1, common));
        }

        /// <summary>
        /// This returns the direction from the point at index passed in to the other point (rays only go one direction, but
        /// segments can go either, lines throw an exception)
        /// </summary>
        public static Vector GetDirectionFromPoint(Edge2D_wpf edge, int index)
        {
            switch (edge.EdgeType)
            {
                case EdgeType.Line:
                    throw new ArgumentException("This method doesn't make sense for lines");        //  because lines can go two directions

                case EdgeType.Ray:
                    #region Ray

                    if (edge.Index0 != index)
                    {
                        throw new ArgumentException("The index passed in doesn't belong to this edge");
                    }

                    return edge.Direction.Value;

                #endregion

                case EdgeType.Segment:
                    #region Segment

                    if (edge.Index0 == index)
                    {
                        return edge.Point1.Value - edge.Point0;
                    }
                    else if (edge.Index1.Value == index)
                    {
                        return edge.Point0 - edge.Point1.Value;
                    }
                    else
                    {
                        throw new ArgumentException("The index passed in doesn't belong to this edge");
                    }

                #endregion

                default:
                    throw new ApplicationException("Unknown EdgeType: " + edge.EdgeType.ToString());
            }
        }

        /// <summary>
        /// This takes a set of edges and returns points in order
        /// NOTE: edges[0] must share a point with edges[1], etc (these should have come from Voronoi2DResult.EdgesByControlPoint, which puts rays on the outside)
        /// WARNING: This method is a bit fragile.  If the edges come from someplace other than Voronoi2DResult.EdgesByControlPoint, test it well
        /// </summary>
        /// <param name="rayLength">If an edge is a ray, it will be projected this far to be a line segment</param>
        public static Point[] GetPolygon(Edge2D_wpf[] edges, double rayLength)
        {
            if (edges == null || edges.Length < 2)
            {
                throw new ArgumentException("Need at least two edges to make a polygon");
            }
#if DEBUG
            else if (edges.Any(o => o.EdgeType == EdgeType.Line))
            {
                throw new ArgumentException("This method doesn't support lines");
            }
#endif

            List<Point> retVal = new List<Point>();
            int commonIndex;

            for (int cntr = 0; cntr < edges.Length - 1; cntr++)
            {
                #region cntr, cntr+1

                // Add the point from cntr that isn't shared with cntr + 1
                commonIndex = GetCommonIndex(edges[cntr], edges[cntr + 1]);
                if (commonIndex < 0)
                {
                    // While in this main loop, there can't be any breaks
                    throw new ApplicationException("Didn't find common point between edges");
                }
                else
                {
                    retVal.Add(GetPolygon_Point(edges[cntr], rayLength, edges[cntr].Index0 != commonIndex));
                }

                #endregion
            }

            #region Last edge

            commonIndex = GetCommonIndex(edges[0], edges[edges.Length - 1]);

            if (commonIndex < 0 || edges.Length == 2)       // When the length is 2, it's a V.  Come in here to force the middle point to be written
            {
                // These edges define an open polygon - looks like a U.  They could be segments or rays, doesn't matter.  Since it's the last
                // edge that's being written, it shares a common point with the second to last edge (or an exception would have been thrown
                // above).  So just use that as the one not to write
                commonIndex = GetCommonIndex(edges[edges.Length - 2], edges[edges.Length - 1]);

                // But write the common one first, because it would get skipped
                retVal.Add(GetPolygon_Point(edges[edges.Length - 1], rayLength, edges[edges.Length - 1].Index0 == commonIndex));
            }

            retVal.Add(GetPolygon_Point(edges[edges.Length - 1], rayLength, edges[edges.Length - 1].Index0 != commonIndex));

            #endregion

            return retVal.ToArray();
        }
        private static Point GetPolygon_Point(Edge2D_wpf edge, double rayLength, bool useZero)
        {
            if (useZero)
            {
                return edge.Point0;
            }

            switch (edge.EdgeType)
            {
                case EdgeType.Segment:
                    return edge.Point1.Value;

                case EdgeType.Ray:
                    return edge.Point0 + (edge.Direction.Value.ToUnit(true) * rayLength);

                default:
                    throw new ApplicationException("Unexpected EdgeType: " + edge.EdgeType.ToString());     // lines aren't supported
            }
        }

        /// <summary>
        /// This takes a set of edges that either form a polygon, or are a set of rays
        /// NOTE: edges[0] must share a point with edges[1], etc (these should have come from Voronoi2DResult.EdgesByControlPoint, which puts rays on the outside)
        /// WARNING: This method is a bit fragile.  If the edges come from someplace other than Voronoi2DResult.EdgesByControlPoint, test it well
        /// </summary>
        /// <remarks>
        /// This is sort of a relaxed version of GetPolygon
        /// 
        /// NOTE: This method takes advantage of the fact that the edges always form a convex polygon
        /// </remarks>
        public static bool IsInside(Edge2D_wpf[] edges, Point point)
        {
            if (edges == null || edges.Length < 2)
            {
                throw new ArgumentException("This method requires at least two edges");
            }
#if DEBUG
            else if (edges.Any(o => o.EdgeType == EdgeType.Line))
            {
                throw new ArgumentException("This method doesn't support lines");
            }
#endif

            // Figure out the winding
            bool includeRight = IsInside_Winding(edges);

            int common;
            Point from, to;

            for (int cntr = 0; cntr < edges.Length - 1; cntr++)
            {
                #region cntr, cntr+1

                // Can't just blindly use p0 then p1, need to get the common point, then always go from other to common
                common = GetCommonIndex(edges[cntr], edges[cntr + 1]);

                from = IsInside_OtherPoint(edges[cntr], common);
                to = edges[cntr].AllEdgePoints[common];

                if (Math2D.QuickHull2D_wpf.IsRightOfLine(from, to, point) != includeRight)
                {
                    return false;
                }

                #endregion
            }

            #region Last Edge

            // Compare the previous two, rather than looping back to edges[0] (since this could be an open poly, the prev two edges will always touch)
            common = GetCommonIndex(edges[edges.Length - 1], edges[edges.Length - 2]);

            // Within the loop, from is other.  But in this section to is other
            from = edges[edges.Length - 1].AllEdgePoints[common];
            to = IsInside_OtherPoint(edges[edges.Length - 1], common);

            if (Math2D.QuickHull2D_wpf.IsRightOfLine(from, to, point) != includeRight)
            {
                return false;
            }

            #endregion

            return true;
        }
        private static bool IsInside_Winding(Edge2D_wpf[] edges)
        {
            int common = GetCommonIndex(edges[0], edges[1]);

            Point left = IsInside_OtherPoint(edges[0], common);
            Point middle = edges[0].AllEdgePoints[common];
            Point right = IsInside_OtherPoint(edges[1], common);

            return Math2D.QuickHull2D_wpf.IsRightOfLine(left, middle, right);
        }
        /// <summary>
        /// Returns the point that isn't commmon
        /// </summary>
        private static Point IsInside_OtherPoint(Edge2D_wpf edge, int common)
        {
            if (edge.Index0 == common)
            {
                return edge.Point1Ext;
            }
            else
            {
                return edge.Point0;
            }
        }

        /// <summary>
        /// This is useful when looking at lists of edges in the quick watch
        /// </summary>
        public override string ToString()
        {
            const string DELIM = "       |       ";

            StringBuilder retVal = new StringBuilder(100);

            retVal.Append(this.EdgeType.ToString());
            retVal.Append(DELIM);

            switch (this.EdgeType)
            {
                case EdgeType.Segment:
                    retVal.Append(string.Format("{0} - {1}{2}({3}) ({4})",
                        this.Index0,
                        this.Index1,
                        DELIM,
                        this.Point0.ToString(2),
                        this.Point1.Value.ToString(2)));
                    break;

                case EdgeType.Ray:
                    retVal.Append(string.Format("{0}{1}({2}) --> ({3})",
                        this.Index0,
                        DELIM,
                        this.Point0.ToString(2),
                        this.Direction.Value.ToString(2)));
                    break;

                case EdgeType.Line:
                    retVal.Append(string.Format("{0}{1}({2}) <---> ({3})",
                        this.Index0,
                        DELIM,
                        this.Point0.ToString(2),
                        this.Direction.Value.ToString(2)));
                    break;

                default:
                    retVal.Append("Unknown EdgeType");
                    break;
            }

            return retVal.ToString();
        }

        #endregion
    }

    #endregion

    #region class: Polygon2D_wpf

    public class Polygon2D_wpf
    {
        public Polygon2D_wpf(Point[] polygon)
            : this(polygon, new Point[0][]) { }
        public Polygon2D_wpf(Point[] polygon, Point[][] holes)
        {
            this.Polygon = polygon;
            this.Holes = holes;
        }

        /// <summary>
        /// This is the outer shell that represents a polygon
        /// </summary>
        public readonly Point[] Polygon;
        /// <summary>
        /// This is an array of polygons that are inside the outer polygon
        /// </summary>
        public readonly Point[][] Holes;
    }

    #endregion

    #region class: TransformsToFrom2D

    /// <summary>
    /// This holds a pair of transforms to take coplanar 3D points into 2D, then back from that 2D plane to the 3D's plane
    /// </summary>
    public class TransformsToFrom2D_wpf
    {
        /// <summary>
        /// Transform from 3D to 2D
        /// </summary>
        public Transform3D From3D_To2D { get; set; }
        /// <summary>
        /// How to get a 2D back to 3D
        /// </summary>
        public Transform3D From2D_BackTo3D { get; set; }
    }

    #endregion
}
