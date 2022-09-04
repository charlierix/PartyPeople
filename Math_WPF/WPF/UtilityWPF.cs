using Game.Core;
using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.WPF
{
    public static class UtilityWPF
    {
        #region class: BuildTube

        private static class BuildTube
        {
            public static MeshGeometry3D Build(int numSides, List<TubeRingBase> rings, bool softSides, bool shouldCenterZ, Transform3D transform = null)
            {
                if (transform == null)
                {
                    transform = Transform3D.Identity;
                }

                // Do some validation/prep work
                double height, curZ;
                Initialize(out height, out curZ, numSides, rings, shouldCenterZ);

                MeshGeometry3D retVal = new MeshGeometry3D();

                int pointOffset = 0;

                // This is used when softSides is true.  This allows for a way to have a common normal between one ring's bottom and the next ring's top
                double[] rotateAnglesForPerp = null;

                TubeRingBase nextRing = rings.Count > 1 ? rings[1] : null;
                EndCap(ref pointOffset, ref rotateAnglesForPerp, retVal, numSides, null, rings[0], nextRing, transform, true, curZ, softSides);

                for (int cntr = 0; cntr < rings.Count - 1; cntr++)
                {
                    if (cntr > 0 && cntr < rings.Count - 1 && !(rings[cntr] is TubeRingRegularPolygon))
                    {
                        throw new ArgumentException("Only rings are allowed in the middle of the tube");
                    }

                    Middle(ref pointOffset, ref rotateAnglesForPerp, retVal, transform, numSides, rings[cntr], rings[cntr + 1], curZ, softSides);

                    curZ += rings[cntr + 1].DistFromPrevRing;
                }

                TubeRingBase prevRing = rings.Count > 1 ? rings[rings.Count - 2] : null;
                EndCap(ref pointOffset, ref rotateAnglesForPerp, retVal, numSides, prevRing, rings[rings.Count - 1], null, transform, false, curZ, softSides);

                //retVal.Freeze();
                return retVal;
            }

            #region Private Methods

            private static void Initialize(out double height, out double startZ, int numSides, List<TubeRingBase> rings, bool shouldCenterZ)
            {
                #region validate

                if (rings.Count == 1)
                {
                    TubeRingRegularPolygon ringCast = rings[0] as TubeRingRegularPolygon;
                    if (ringCast == null || !ringCast.IsClosedIfEndCap)
                    {
                        throw new ArgumentException("Only a single ring was passed in, so the only valid type is a closed ring: " + rings[0].GetType().ToString());
                    }
                }
                else if (rings.Count == 2)
                {
                    if (!rings.Any(o => o is TubeRingRegularPolygon))
                    {
                        // Say both are points - you'd have a line.  Domes must attach to a ring, not a point or another dome
                        throw new ArgumentException(string.Format("When only two rings definitions are passed in, at least one of them must be a ring:\r\n{0}\r\n{1}", rings[0].GetType().ToString(), rings[1].GetType().ToString()));
                    }
                }

                if (numSides < 3)
                {
                    throw new ArgumentException("numSides must be at least 3: " + numSides.ToString(), "numSides");
                }

                #endregion

                // Calculate total height
                height = TubeRingBase.GetTotalHeight(rings);

                // Figure out the starting Z
                startZ = 0d;
                if (shouldCenterZ)
                {
                    startZ = height * -.5d;      // starting in the negative
                }
            }

            private static Point[] GetPointsRegPoly(int numSides, TubeRingRegularPolygon ring)
            {
                // Multiply the returned unit circle by the ring's radius
                return Math2D.GetCircle_Cached(numSides).
                    Select(o => new Point(ring.RadiusX * o.X, ring.RadiusY * o.Y)).
                    ToArray();
            }

            private static void EndCap(ref int pointOffset, ref double[] rotateAnglesForPerp, MeshGeometry3D geometry, int numSides, TubeRingBase ringPrev, TubeRingBase ringCurrent, TubeRingBase ringNext, Transform3D transform, bool isFirst, double z, bool softSides)
            {
                if (ringCurrent is TubeRingDome)
                {
                    #region dome

                    Point[] domePointsTheta = EndCap_GetPoints(ringPrev, ringNext, numSides, isFirst);
                    double capHeight = EndCap_GetCapHeight(ringCurrent, ringNext, isFirst);
                    Transform3D domeTransform = EndCap_GetTransform(transform, isFirst, z, capHeight);
                    Transform3D domeTransformNormal = EndCap_GetNormalTransform(domeTransform);

                    if (softSides)
                    {
                        EndCap_DomeSoft(ref pointOffset, ref rotateAnglesForPerp, geometry, domePointsTheta, domeTransform, domeTransformNormal, (TubeRingDome)ringCurrent, capHeight, isFirst);
                    }
                    else
                    {
                        EndCap_DomeHard(ref pointOffset, ref rotateAnglesForPerp, geometry, domePointsTheta, domeTransform, domeTransformNormal, (TubeRingDome)ringCurrent, capHeight, isFirst);
                    }

                    #endregion
                }
                else if (ringCurrent is TubeRingPoint)
                {
                    #region point

                    Point[] conePointsTheta = EndCap_GetPoints(ringPrev, ringNext, numSides, isFirst);
                    double capHeight = EndCap_GetCapHeight(ringCurrent, ringNext, isFirst);
                    Transform3D coneTransform = EndCap_GetTransform(transform, isFirst, z, capHeight);

                    if (softSides)
                    {
                        Transform3D coneTransformNormal = EndCap_GetNormalTransform(coneTransform);     // only need the transform for soft, because it cheats and has a hardcode normal (hard calculates the normal for each triangle)

                        EndCap_ConeSoft(ref pointOffset, ref rotateAnglesForPerp, geometry, conePointsTheta, coneTransform, coneTransformNormal, (TubeRingPoint)ringCurrent, capHeight, isFirst);
                    }
                    else
                    {
                        EndCap_ConeHard(ref pointOffset, ref rotateAnglesForPerp, geometry, conePointsTheta, coneTransform, (TubeRingPoint)ringCurrent, capHeight, isFirst);
                    }

                    #endregion
                }
                else if (ringCurrent is TubeRingRegularPolygon)
                {
                    #region regular polygon

                    TubeRingRegularPolygon ringCurrentCast = (TubeRingRegularPolygon)ringCurrent;

                    if (ringCurrentCast.IsClosedIfEndCap)		// if it's open, there is nothing to do for the end cap
                    {
                        Point[] polyPointsTheta = GetPointsRegPoly(numSides, ringCurrentCast);
                        Transform3D polyTransform = EndCap_GetTransform(transform, isFirst, z);
                        Transform3D polyTransformNormal = EndCap_GetNormalTransform(polyTransform);

                        if (softSides)
                        {
                            EndCap_PlateSoft(ref pointOffset, ref rotateAnglesForPerp, geometry, polyPointsTheta, polyTransform, polyTransformNormal, ringCurrent, isFirst);
                        }
                        else
                        {
                            EndCap_PlateHard(ref pointOffset, ref rotateAnglesForPerp, geometry, polyPointsTheta, polyTransform, polyTransformNormal, ringCurrent, isFirst);
                        }
                    }

                    #endregion
                }
                else
                {
                    throw new ApplicationException("Unknown tube ring type: " + ringCurrent.GetType().ToString());
                }
            }
            private static double EndCap_GetCapHeight(TubeRingBase ringCurrent, TubeRingBase ringNext, bool isFirst)
            {
                if (isFirst)
                {
                    // ringCurrent.DistFromPrevRing is ignored (because there is no previous).  So the cap's height is the next ring's dist from prev
                    return ringNext.DistFromPrevRing;
                }
                else
                {
                    // This is the last, so dist from prev has meaning
                    return ringCurrent.DistFromPrevRing;
                }
            }
            private static Point[] EndCap_GetPoints(TubeRingBase ringPrev, TubeRingBase ringNext, int numSides, bool isFirst)
            {
                // Figure out which ring to pull from
                TubeRingBase ring = null;
                if (isFirst)
                {
                    ring = ringNext;
                }
                else
                {
                    ring = ringPrev;
                }

                // Get the points
                Point[] retVal = null;
                if (ring != null && ring is TubeRingRegularPolygon)
                {
                    retVal = GetPointsRegPoly(numSides, (TubeRingRegularPolygon)ring);
                }

                if (retVal == null)
                {
                    throw new ApplicationException("The points are null for dome/point.  Validation should have caught this before now");
                }

                return retVal;
            }
            private static Transform3D EndCap_GetTransform(Transform3D transform, bool isFirst, double z)
            {
                // This overload is for a flat plate

                Transform3DGroup retVal = new Transform3DGroup();

                if (isFirst)
                {
                    // This still needs to be flipped for a flat cap so the normals turn out right
                    retVal.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 180d)));
                    retVal.Children.Add(new TranslateTransform3D(0, 0, z));
                    retVal.Children.Add(transform);
                }
                else
                {
                    retVal.Children.Add(new TranslateTransform3D(0, 0, z));
                    retVal.Children.Add(transform);
                }

                return retVal;
            }
            private static Transform3D EndCap_GetTransform(Transform3D transform, bool isFirst, double z, double capHeight)
            {
                //This overload is for a cone/dome

                Transform3DGroup retVal = new Transform3DGroup();

                if (isFirst)
                {
                    // The dome/point methods are hard coded to go from 0 to capHeight, so rotate it so it will build from capHeight
                    // down to zero (offset by z)
                    retVal.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 180d)));
                    retVal.Children.Add(new TranslateTransform3D(0, 0, z + capHeight));
                    retVal.Children.Add(transform);
                }
                else
                {
                    retVal.Children.Add(new TranslateTransform3D(0, 0, z - capHeight));		// z is currently at the tip of the dome (it works for a flat cap, but a dome has height, so must back up to the last drawn object)
                    retVal.Children.Add(transform);
                }

                return retVal;
            }
            private static Transform3D EndCap_GetNormalTransform(Transform3D transform)
            {
                // Can't use all of the transform passed in for the normal, because translate portions will skew the normals funny
                Transform3DGroup retVal = new Transform3DGroup();
                if (transform is Transform3DGroup)
                {
                    foreach (var subTransform in ((Transform3DGroup)transform).Children)
                    {
                        if (!(subTransform is TranslateTransform3D))
                        {
                            retVal.Children.Add(subTransform);
                        }
                    }
                }
                else if (transform is TranslateTransform3D)
                {
                    retVal.Children.Add(Transform3D.Identity);
                }
                else
                {
                    retVal.Children.Add(transform);
                }

                return retVal;
            }

            private static void EndCap_DomeSoft(ref int pointOffset, ref double[] rotateAnglesForPerp, MeshGeometry3D geometry, Point[] pointsTheta, Transform3D transform, Transform3D normalTransform, TubeRingDome ring, double capHeight, bool isFirst)
            {
                // NOTE: There is one more than NumSegmentsPhi
                Point[] pointsPhi = ring.GetUnitPointsPhi(ring.NumSegmentsPhi);

                #region positions/normals

                //for (int phiCntr = 0; phiCntr < numSegmentsPhi; phiCntr++)		// The top point will be added after this loop
                for (int phiCntr = pointsPhi.Length - 1; phiCntr > 0; phiCntr--)
                {
                    if (!isFirst && ring.MergeNormalWithPrevIfSoft)
                    {
                        // Just reuse the points/normals from the previous ring
                        continue;
                    }

                    for (int thetaCntr = 0; thetaCntr < pointsTheta.Length; thetaCntr++)
                    {
                        // Phi points are going from bottom to equator.  
                        // pointsTheta are already the length they are supposed to be (not nessassarily a unit circle)

                        Point3D point = new Point3D(
                            pointsTheta[thetaCntr].X * pointsPhi[phiCntr].Y,
                            pointsTheta[thetaCntr].Y * pointsPhi[phiCntr].Y,
                            capHeight * pointsPhi[phiCntr].X);

                        geometry.Positions.Add(transform.Transform(point));

                        if (ring.MergeNormalWithPrevIfSoft)
                        {
                            //TODO:  Merge the normal with rotateAngleForPerp (see the GetCone method)
                            throw new ApplicationException("finish this");
                        }
                        else
                        {
                            geometry.Normals.Add(normalTransform.Transform(point).ToVector().ToUnit());		// the normal is the same as the point for a sphere (but no tranlate transform)
                        }
                    }
                }

                // This is north pole point
                geometry.Positions.Add(transform.Transform(new Point3D(0, 0, capHeight)));
                geometry.Normals.Add(normalTransform.Transform(new Vector3D(0, 0, capHeight < 0 ? -1 : 1)));        // they can enter a negative height (which would make a bowl)

                #endregion

                #region triangles - rings

                int zOffsetBottom = pointOffset;
                int zOffsetTop;

                for (int phiCntr = 0; phiCntr < ring.NumSegmentsPhi - 1; phiCntr++)		// The top cone will be added after this loop
                {
                    zOffsetTop = zOffsetBottom + pointsTheta.Length;

                    for (int thetaCntr = 0; thetaCntr < pointsTheta.Length - 1; thetaCntr++)
                    {
                        // Top/Left triangle
                        geometry.TriangleIndices.Add(zOffsetBottom + thetaCntr + 0);
                        geometry.TriangleIndices.Add(zOffsetTop + thetaCntr + 1);
                        geometry.TriangleIndices.Add(zOffsetTop + thetaCntr + 0);

                        // Bottom/Right triangle
                        geometry.TriangleIndices.Add(zOffsetBottom + thetaCntr + 0);
                        geometry.TriangleIndices.Add(zOffsetBottom + thetaCntr + 1);
                        geometry.TriangleIndices.Add(zOffsetTop + thetaCntr + 1);
                    }

                    // Connecting the last 2 points to the first 2
                    // Top/Left triangle
                    geometry.TriangleIndices.Add(zOffsetBottom + (pointsTheta.Length - 1) + 0);
                    geometry.TriangleIndices.Add(zOffsetTop);		// wrapping back around
                    geometry.TriangleIndices.Add(zOffsetTop + (pointsTheta.Length - 1) + 0);

                    // Bottom/Right triangle
                    geometry.TriangleIndices.Add(zOffsetBottom + (pointsTheta.Length - 1) + 0);
                    geometry.TriangleIndices.Add(zOffsetBottom);
                    geometry.TriangleIndices.Add(zOffsetTop);

                    // Prep for the next ring
                    zOffsetBottom = zOffsetTop;
                }

                #endregion
                #region triangles - cap

                int topIndex = geometry.Positions.Count - 1;

                for (int cntr = 0; cntr < pointsTheta.Length - 1; cntr++)
                {
                    geometry.TriangleIndices.Add(zOffsetBottom + cntr + 0);
                    geometry.TriangleIndices.Add(zOffsetBottom + cntr + 1);
                    geometry.TriangleIndices.Add(topIndex);
                }

                // The last triangle links back to zero
                geometry.TriangleIndices.Add(zOffsetBottom + pointsTheta.Length - 1 + 0);
                geometry.TriangleIndices.Add(zOffsetBottom + 0);
                geometry.TriangleIndices.Add(topIndex);


                //for (int cntr = 0; cntr < pointsTheta.Length - 1; cntr++)
                //{
                //    geometry.TriangleIndices.Add(zOffsetBottom + cntr + 0);
                //    geometry.TriangleIndices.Add(topIndex);
                //    geometry.TriangleIndices.Add(zOffsetBottom + cntr + 1);
                //}

                //// The last triangle links back to zero
                //geometry.TriangleIndices.Add(zOffsetBottom + pointsTheta.Length - 1 + 0);
                //geometry.TriangleIndices.Add(topIndex);
                //geometry.TriangleIndices.Add(zOffsetBottom + 0);


                #endregion

                pointOffset = geometry.Positions.Count;
            }
            private static void EndCap_DomeHard_FLAWED(ref int pointOffset, ref double[] rotateAnglesForPerp, MeshGeometry3D geometry, Point[] pointsTheta, Transform3D transform, Transform3D normalTransform, TubeRingDome ring, double capHeight, bool isFirst)
            {
                // NOTE: There is one more than NumSegmentsPhi
                Point[] pointsPhi = ring.GetUnitPointsPhi(ring.NumSegmentsPhi);

                Point3D point;
                Vector3D normal;
                int zOffset = pointOffset;

                #region Triangles - Rings

                for (int phiCntr = 0; phiCntr < ring.NumSegmentsPhi - 1; phiCntr++)		// The top cone will be added after this loop
                {
                    for (int thetaCntr = 0; thetaCntr < pointsTheta.Length - 1; thetaCntr++)
                    {
                        // Phi points are going from bottom to equator.             <---------- NO!!!
                        // Phi points are going from the equator to the top





                        // pointsTheta are already the length they are supposed to be (not nessassarily a unit circle)

                        #region top/left triangle

                        point = new Point3D(
                            pointsTheta[thetaCntr].X * pointsPhi[phiCntr].Y,
                            pointsTheta[thetaCntr].Y * pointsPhi[phiCntr].Y,
                            capHeight * pointsPhi[phiCntr].X);

                        geometry.Positions.Add(transform.Transform(point));

                        point = new Point3D(
                            pointsTheta[thetaCntr + 1].X * pointsPhi[phiCntr + 1].Y,
                            pointsTheta[thetaCntr + 1].Y * pointsPhi[phiCntr + 1].Y,
                            capHeight * pointsPhi[phiCntr + 1].X);

                        geometry.Positions.Add(transform.Transform(point));

                        point = new Point3D(
                            pointsTheta[thetaCntr].X * pointsPhi[phiCntr + 1].Y,
                            pointsTheta[thetaCntr].Y * pointsPhi[phiCntr + 1].Y,
                            capHeight * pointsPhi[phiCntr + 1].X);

                        geometry.Positions.Add(transform.Transform(point));

                        normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                        geometry.Normals.Add(normal);		// the normals point straight out of the face
                        geometry.Normals.Add(normal);
                        geometry.Normals.Add(normal);

                        geometry.TriangleIndices.Add(zOffset + 0);
                        geometry.TriangleIndices.Add(zOffset + 1);
                        geometry.TriangleIndices.Add(zOffset + 2);

                        #endregion

                        zOffset += 3;

                        #region bottom/right triangle

                        point = new Point3D(
                            pointsTheta[thetaCntr].X * pointsPhi[phiCntr].Y,
                            pointsTheta[thetaCntr].Y * pointsPhi[phiCntr].Y,
                            capHeight * pointsPhi[phiCntr].X);

                        geometry.Positions.Add(transform.Transform(point));

                        point = new Point3D(
                            pointsTheta[thetaCntr + 1].X * pointsPhi[phiCntr].Y,
                            pointsTheta[thetaCntr + 1].Y * pointsPhi[phiCntr].Y,
                            capHeight * pointsPhi[phiCntr].X);

                        geometry.Positions.Add(transform.Transform(point));

                        point = new Point3D(
                            pointsTheta[thetaCntr + 1].X * pointsPhi[phiCntr + 1].Y,
                            pointsTheta[thetaCntr + 1].Y * pointsPhi[phiCntr + 1].Y,
                            capHeight * pointsPhi[phiCntr + 1].X);

                        geometry.Positions.Add(transform.Transform(point));

                        normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                        geometry.Normals.Add(normal);		// the normals point straight out of the face
                        geometry.Normals.Add(normal);
                        geometry.Normals.Add(normal);

                        geometry.TriangleIndices.Add(zOffset + 0);
                        geometry.TriangleIndices.Add(zOffset + 1);
                        geometry.TriangleIndices.Add(zOffset + 2);

                        #endregion

                        zOffset += 3;
                    }

                    // Connecting the last 2 points to the first 2
                    #region top/left triangle

                    point = new Point3D(
                        pointsTheta[pointsTheta.Length - 1].X * pointsPhi[phiCntr].Y,
                        pointsTheta[pointsTheta.Length - 1].Y * pointsPhi[phiCntr].Y,
                        capHeight * pointsPhi[phiCntr].X);

                    geometry.Positions.Add(transform.Transform(point));

                    point = new Point3D(
                        pointsTheta[0].X * pointsPhi[phiCntr + 1].Y,        // wrapping theta back around
                        pointsTheta[0].Y * pointsPhi[phiCntr + 1].Y,
                        capHeight * pointsPhi[phiCntr + 1].X);

                    geometry.Positions.Add(transform.Transform(point));

                    point = new Point3D(
                        pointsTheta[pointsTheta.Length - 1].X * pointsPhi[phiCntr + 1].Y,
                        pointsTheta[pointsTheta.Length - 1].Y * pointsPhi[phiCntr + 1].Y,
                        capHeight * pointsPhi[phiCntr + 1].X);

                    geometry.Positions.Add(transform.Transform(point));

                    normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                    geometry.Normals.Add(normal);		// the normals point straight out of the face
                    geometry.Normals.Add(normal);
                    geometry.Normals.Add(normal);

                    geometry.TriangleIndices.Add(zOffset + 0);
                    geometry.TriangleIndices.Add(zOffset + 1);
                    geometry.TriangleIndices.Add(zOffset + 2);

                    #endregion

                    zOffset += 3;

                    #region bottom/right triangle

                    point = new Point3D(
                        pointsTheta[pointsTheta.Length - 1].X * pointsPhi[phiCntr].Y,
                        pointsTheta[pointsTheta.Length - 1].Y * pointsPhi[phiCntr].Y,
                        capHeight * pointsPhi[phiCntr].X);

                    geometry.Positions.Add(transform.Transform(point));

                    point = new Point3D(
                        pointsTheta[0].X * pointsPhi[phiCntr].Y,
                        pointsTheta[0].Y * pointsPhi[phiCntr].Y,
                        capHeight * pointsPhi[phiCntr].X);

                    geometry.Positions.Add(transform.Transform(point));

                    point = new Point3D(
                        pointsTheta[0].X * pointsPhi[phiCntr + 1].Y,
                        pointsTheta[0].Y * pointsPhi[phiCntr + 1].Y,
                        capHeight * pointsPhi[phiCntr + 1].X);

                    geometry.Positions.Add(transform.Transform(point));

                    normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                    geometry.Normals.Add(normal);		// the normals point straight out of the face
                    geometry.Normals.Add(normal);
                    geometry.Normals.Add(normal);

                    geometry.TriangleIndices.Add(zOffset + 0);
                    geometry.TriangleIndices.Add(zOffset + 1);
                    geometry.TriangleIndices.Add(zOffset + 2);

                    #endregion

                    zOffset += 3;
                }

                #endregion
                #region triangles - cap

                // This is basically the same idea as EndCap_ConeHard, except for the extra phi bits

                Point3D topPoint = transform.Transform(new Point3D(0, 0, capHeight));

                for (int thetaCntr = 0; thetaCntr < pointsTheta.Length - 1; thetaCntr++)
                {
                    point = new Point3D(
                        pointsTheta[thetaCntr].X * pointsPhi[pointsPhi.Length - 1].Y,
                        pointsTheta[thetaCntr].Y * pointsPhi[pointsPhi.Length - 1].Y,
                        capHeight * pointsPhi[pointsPhi.Length - 1].X);

                    geometry.Positions.Add(transform.Transform(point));

                    point = new Point3D(
                        pointsTheta[thetaCntr + 1].X * pointsPhi[pointsPhi.Length - 1].Y,
                        pointsTheta[thetaCntr + 1].Y * pointsPhi[pointsPhi.Length - 1].Y,
                        capHeight * pointsPhi[pointsPhi.Length - 1].X);

                    geometry.Positions.Add(transform.Transform(point));

                    geometry.Positions.Add(topPoint);

                    normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                    geometry.Normals.Add(normal);		// the normals point straight out of the face
                    geometry.Normals.Add(normal);
                    geometry.Normals.Add(normal);

                    geometry.TriangleIndices.Add(zOffset + 0);
                    geometry.TriangleIndices.Add(zOffset + 1);
                    geometry.TriangleIndices.Add(zOffset + 2);

                    zOffset += 3;
                }

                // The last triangle links back to zero
                point = new Point3D(
                    pointsTheta[pointsTheta.Length - 1].X * pointsPhi[pointsPhi.Length - 1].Y,
                    pointsTheta[pointsTheta.Length - 1].Y * pointsPhi[pointsPhi.Length - 1].Y,
                    capHeight * pointsPhi[pointsPhi.Length - 1].X);

                geometry.Positions.Add(transform.Transform(point));

                point = new Point3D(
                    pointsTheta[0].X * pointsPhi[pointsPhi.Length - 1].Y,
                    pointsTheta[0].Y * pointsPhi[pointsPhi.Length - 1].Y,
                    capHeight * pointsPhi[pointsPhi.Length - 1].X);

                geometry.Positions.Add(transform.Transform(point));

                geometry.Positions.Add(topPoint);

                normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                geometry.Normals.Add(normal);		// the normals point straight out of the face
                geometry.Normals.Add(normal);
                geometry.Normals.Add(normal);

                geometry.TriangleIndices.Add(zOffset + 0);
                geometry.TriangleIndices.Add(zOffset + 1);
                geometry.TriangleIndices.Add(zOffset + 2);

                zOffset += 3;

                #endregion

                pointOffset = geometry.Positions.Count;
            }
            private static void EndCap_DomeHard(ref int pointOffset, ref double[] rotateAnglesForPerp, MeshGeometry3D geometry, Point[] pointsTheta, Transform3D transform, Transform3D normalTransform, TubeRingDome ring, double capHeight, bool isFirst)
            {
                // NOTE: There is one more than NumSegmentsPhi
                Point[] pointsPhi = ring.GetUnitPointsPhi(ring.NumSegmentsPhi);

                Point3D point;
                Vector3D normal;
                int zOffset = pointOffset;

                #region Triangles - Rings

                for (int phiCntr = 1; phiCntr < pointsPhi.Length - 1; phiCntr++)		// The top cone will be added after this loop
                {
                    for (int thetaCntr = 0; thetaCntr < pointsTheta.Length - 1; thetaCntr++)
                    {
                        // Phi points are going from bottom to equator                          <----------- NO?????????
                        // Phi points are going from the equator to the top

                        // pointsTheta are already the length they are supposed to be (not nessassarily a unit circle)

                        #region top/left triangle

                        point = new Point3D(
                            pointsTheta[thetaCntr].X * pointsPhi[phiCntr].Y,
                            pointsTheta[thetaCntr].Y * pointsPhi[phiCntr].Y,
                            capHeight * pointsPhi[phiCntr].X);

                        geometry.Positions.Add(transform.Transform(point));

                        point = new Point3D(
                            pointsTheta[thetaCntr].X * pointsPhi[phiCntr + 1].Y,
                            pointsTheta[thetaCntr].Y * pointsPhi[phiCntr + 1].Y,
                            capHeight * pointsPhi[phiCntr + 1].X);

                        geometry.Positions.Add(transform.Transform(point));

                        point = new Point3D(
                            pointsTheta[thetaCntr + 1].X * pointsPhi[phiCntr + 1].Y,
                            pointsTheta[thetaCntr + 1].Y * pointsPhi[phiCntr + 1].Y,
                            capHeight * pointsPhi[phiCntr + 1].X);

                        geometry.Positions.Add(transform.Transform(point));

                        normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                        geometry.Normals.Add(normal);		// the normals point straight out of the face
                        geometry.Normals.Add(normal);
                        geometry.Normals.Add(normal);

                        geometry.TriangleIndices.Add(zOffset + 0);
                        geometry.TriangleIndices.Add(zOffset + 1);
                        geometry.TriangleIndices.Add(zOffset + 2);

                        #endregion

                        zOffset += 3;

                        #region bottom/right triangle

                        point = new Point3D(
                            pointsTheta[thetaCntr].X * pointsPhi[phiCntr].Y,
                            pointsTheta[thetaCntr].Y * pointsPhi[phiCntr].Y,
                            capHeight * pointsPhi[phiCntr].X);

                        geometry.Positions.Add(transform.Transform(point));

                        point = new Point3D(
                            pointsTheta[thetaCntr + 1].X * pointsPhi[phiCntr + 1].Y,
                            pointsTheta[thetaCntr + 1].Y * pointsPhi[phiCntr + 1].Y,
                            capHeight * pointsPhi[phiCntr + 1].X);

                        geometry.Positions.Add(transform.Transform(point));

                        point = new Point3D(
                            pointsTheta[thetaCntr + 1].X * pointsPhi[phiCntr].Y,
                            pointsTheta[thetaCntr + 1].Y * pointsPhi[phiCntr].Y,
                            capHeight * pointsPhi[phiCntr].X);

                        geometry.Positions.Add(transform.Transform(point));

                        normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                        geometry.Normals.Add(normal);		// the normals point straight out of the face
                        geometry.Normals.Add(normal);
                        geometry.Normals.Add(normal);

                        geometry.TriangleIndices.Add(zOffset + 0);
                        geometry.TriangleIndices.Add(zOffset + 1);
                        geometry.TriangleIndices.Add(zOffset + 2);

                        #endregion

                        zOffset += 3;
                    }

                    // Connecting the last 2 points to the first 2
                    #region top/left triangle

                    point = new Point3D(
                        pointsTheta[pointsTheta.Length - 1].X * pointsPhi[phiCntr].Y,
                        pointsTheta[pointsTheta.Length - 1].Y * pointsPhi[phiCntr].Y,
                        capHeight * pointsPhi[phiCntr].X);

                    geometry.Positions.Add(transform.Transform(point));

                    point = new Point3D(
                        pointsTheta[pointsTheta.Length - 1].X * pointsPhi[phiCntr + 1].Y,
                        pointsTheta[pointsTheta.Length - 1].Y * pointsPhi[phiCntr + 1].Y,
                        capHeight * pointsPhi[phiCntr + 1].X);

                    geometry.Positions.Add(transform.Transform(point));

                    point = new Point3D(
                        pointsTheta[0].X * pointsPhi[phiCntr + 1].Y,        // wrapping theta back around
                        pointsTheta[0].Y * pointsPhi[phiCntr + 1].Y,
                        capHeight * pointsPhi[phiCntr + 1].X);

                    geometry.Positions.Add(transform.Transform(point));

                    normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                    geometry.Normals.Add(normal);		// the normals point straight out of the face
                    geometry.Normals.Add(normal);
                    geometry.Normals.Add(normal);

                    geometry.TriangleIndices.Add(zOffset + 0);
                    geometry.TriangleIndices.Add(zOffset + 1);
                    geometry.TriangleIndices.Add(zOffset + 2);

                    #endregion

                    zOffset += 3;

                    #region bottom/right triangle

                    point = new Point3D(
                        pointsTheta[pointsTheta.Length - 1].X * pointsPhi[phiCntr].Y,
                        pointsTheta[pointsTheta.Length - 1].Y * pointsPhi[phiCntr].Y,
                        capHeight * pointsPhi[phiCntr].X);

                    geometry.Positions.Add(transform.Transform(point));

                    point = new Point3D(
                        pointsTheta[0].X * pointsPhi[phiCntr + 1].Y,
                        pointsTheta[0].Y * pointsPhi[phiCntr + 1].Y,
                        capHeight * pointsPhi[phiCntr + 1].X);

                    geometry.Positions.Add(transform.Transform(point));

                    point = new Point3D(
                        pointsTheta[0].X * pointsPhi[phiCntr].Y,
                        pointsTheta[0].Y * pointsPhi[phiCntr].Y,
                        capHeight * pointsPhi[phiCntr].X);

                    geometry.Positions.Add(transform.Transform(point));

                    normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                    geometry.Normals.Add(normal);		// the normals point straight out of the face
                    geometry.Normals.Add(normal);
                    geometry.Normals.Add(normal);

                    geometry.TriangleIndices.Add(zOffset + 0);
                    geometry.TriangleIndices.Add(zOffset + 1);
                    geometry.TriangleIndices.Add(zOffset + 2);

                    #endregion

                    zOffset += 3;
                }

                #endregion
                #region triangles - cap

                // This is basically the same idea as EndCap_ConeHard, except for the extra phi bits

                Point3D topPoint = transform.Transform(new Point3D(0, 0, capHeight));

                for (int thetaCntr = 0; thetaCntr < pointsTheta.Length - 1; thetaCntr++)
                {
                    point = new Point3D(
                        pointsTheta[thetaCntr].X * pointsPhi[1].Y,
                        pointsTheta[thetaCntr].Y * pointsPhi[1].Y,
                        capHeight * pointsPhi[1].X);

                    geometry.Positions.Add(transform.Transform(point));

                    point = new Point3D(
                        pointsTheta[thetaCntr + 1].X * pointsPhi[1].Y,
                        pointsTheta[thetaCntr + 1].Y * pointsPhi[1].Y,
                        capHeight * pointsPhi[1].X);

                    geometry.Positions.Add(transform.Transform(point));

                    geometry.Positions.Add(topPoint);

                    normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                    geometry.Normals.Add(normal);		// the normals point straight out of the face
                    geometry.Normals.Add(normal);
                    geometry.Normals.Add(normal);

                    geometry.TriangleIndices.Add(zOffset + 0);
                    geometry.TriangleIndices.Add(zOffset + 1);
                    geometry.TriangleIndices.Add(zOffset + 2);

                    zOffset += 3;
                }

                // The last triangle links back to zero
                point = new Point3D(
                    pointsTheta[pointsTheta.Length - 1].X * pointsPhi[1].Y,
                    pointsTheta[pointsTheta.Length - 1].Y * pointsPhi[1].Y,
                    capHeight * pointsPhi[1].X);

                geometry.Positions.Add(transform.Transform(point));

                point = new Point3D(
                    pointsTheta[0].X * pointsPhi[1].Y,
                    pointsTheta[0].Y * pointsPhi[1].Y,
                    capHeight * pointsPhi[1].X);

                geometry.Positions.Add(transform.Transform(point));

                geometry.Positions.Add(topPoint);

                normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                geometry.Normals.Add(normal);		// the normals point straight out of the face
                geometry.Normals.Add(normal);
                geometry.Normals.Add(normal);

                geometry.TriangleIndices.Add(zOffset + 0);
                geometry.TriangleIndices.Add(zOffset + 1);
                geometry.TriangleIndices.Add(zOffset + 2);

                zOffset += 3;

                #endregion

                pointOffset = geometry.Positions.Count;
            }

            private static void EndCap_ConeSoft(ref int pointOffset, ref double[] rotateAnglesForPerp, MeshGeometry3D geometry, Point[] pointsTheta, Transform3D transform, Transform3D normalTransform, TubeRingPoint ring, double capHeight, bool isFirst)
            {
                #region positions/normals

                if (isFirst || !ring.MergeNormalWithPrevIfSoft)
                {
                    for (int thetaCntr = 0; thetaCntr < pointsTheta.Length; thetaCntr++)
                    {
                        Point3D point = new Point3D(pointsTheta[thetaCntr].X, pointsTheta[thetaCntr].Y, 0d);
                        geometry.Positions.Add(transform.Transform(point));
                        geometry.Normals.Add(normalTransform.Transform(point).ToVector().ToUnit());		// the normal is the same as the point for a sphere (but no tranlate transform)
                    }
                }

                // Cone tip
                geometry.Positions.Add(transform.Transform(new Point3D(0, 0, capHeight)));
                geometry.Normals.Add(transform.Transform(new Vector3D(0, 0, capHeight < 0 ? -1 : 1)));      // they can pass in a negative cap height

                #endregion

                #region triangles

                int topIndex = geometry.Positions.Count - 1;

                for (int cntr = 0; cntr < pointsTheta.Length - 1; cntr++)
                {
                    geometry.TriangleIndices.Add(pointOffset + cntr + 0);
                    geometry.TriangleIndices.Add(pointOffset + cntr + 1);
                    geometry.TriangleIndices.Add(topIndex);
                }

                // The last triangle links back to zero
                geometry.TriangleIndices.Add(pointOffset + pointsTheta.Length - 1 + 0);
                geometry.TriangleIndices.Add(pointOffset + 0);
                geometry.TriangleIndices.Add(topIndex);

                #endregion

                pointOffset = geometry.Positions.Count;
            }
            private static void EndCap_ConeHard(ref int pointOffset, ref double[] rotateAnglesForPerp, MeshGeometry3D geometry, Point[] pointsTheta, Transform3D transform, TubeRingPoint ring, double capHeight, bool isFirst)
            {
                Point3D tipPosition = transform.Transform(new Point3D(0, 0, capHeight));

                int localOffset = 0;

                for (int cntr = 0; cntr < pointsTheta.Length - 1; cntr++)
                {
                    geometry.Positions.Add(transform.Transform(new Point3D(pointsTheta[cntr].X, pointsTheta[cntr].Y, 0d)));
                    geometry.Positions.Add(transform.Transform(new Point3D(pointsTheta[cntr + 1].X, pointsTheta[cntr + 1].Y, 0d)));
                    geometry.Positions.Add(tipPosition);

                    Vector3D normal = GetNormal(geometry.Positions[pointOffset + localOffset + 0], geometry.Positions[pointOffset + localOffset + 1], geometry.Positions[pointOffset + localOffset + 2]);
                    geometry.Normals.Add(normal);		// the normals point straight out of the face
                    geometry.Normals.Add(normal);
                    geometry.Normals.Add(normal);

                    geometry.TriangleIndices.Add(pointOffset + localOffset + 0);
                    geometry.TriangleIndices.Add(pointOffset + localOffset + 1);
                    geometry.TriangleIndices.Add(pointOffset + localOffset + 2);

                    localOffset += 3;
                }

                // The last triangle links back to zero
                geometry.Positions.Add(transform.Transform(new Point3D(pointsTheta[pointsTheta.Length - 1].X, pointsTheta[pointsTheta.Length - 1].Y, 0d)));
                geometry.Positions.Add(transform.Transform(new Point3D(pointsTheta[0].X, pointsTheta[0].Y, 0d)));
                geometry.Positions.Add(tipPosition);

                Vector3D normal2 = GetNormal(geometry.Positions[pointOffset + localOffset + 0], geometry.Positions[pointOffset + localOffset + 1], geometry.Positions[pointOffset + localOffset + 2]);
                geometry.Normals.Add(normal2);		// the normals point straight out of the face
                geometry.Normals.Add(normal2);
                geometry.Normals.Add(normal2);

                geometry.TriangleIndices.Add(pointOffset + localOffset + 0);
                geometry.TriangleIndices.Add(pointOffset + localOffset + 1);
                geometry.TriangleIndices.Add(pointOffset + localOffset + 2);

                // Update ref param
                pointOffset = geometry.Positions.Count;
            }

            private static void EndCap_PlateSoft(ref int pointOffset, ref double[] rotateAnglesForPerp, MeshGeometry3D geometry, Point[] pointsTheta, Transform3D transform, Transform3D normalTransform, TubeRingBase ring, bool isFirst)
            {
                #region positions/normals

                if (isFirst || !ring.MergeNormalWithPrevIfSoft)
                {
                    for (int thetaCntr = 0; thetaCntr < pointsTheta.Length; thetaCntr++)
                    {
                        Point3D point = new Point3D(pointsTheta[thetaCntr].X, pointsTheta[thetaCntr].Y, 0d);
                        geometry.Positions.Add(transform.Transform(point));

                        Vector3D normal;
                        if (ring.MergeNormalWithPrevIfSoft)
                        {
                            //normal = point.ToVector();		// this isn't right
                            throw new ApplicationException("finish this");
                        }
                        else
                        {
                            normal = new Vector3D(0, 0, 1);
                        }

                        geometry.Normals.Add(normalTransform.Transform(normal).ToUnit());
                    }
                }

                #endregion

                #region add the triangles

                // Start with 0,1,2
                geometry.TriangleIndices.Add(pointOffset + 0);
                geometry.TriangleIndices.Add(pointOffset + 1);
                geometry.TriangleIndices.Add(pointOffset + 2);

                int lowerIndex = 2;
                int upperIndex = pointsTheta.Length - 1;
                int lastUsedIndex = 0;
                bool shouldBumpLower = true;

                // Do the rest of the triangles
                while (lowerIndex < upperIndex)
                {
                    geometry.TriangleIndices.Add(pointOffset + lowerIndex);
                    geometry.TriangleIndices.Add(pointOffset + upperIndex);
                    geometry.TriangleIndices.Add(pointOffset + lastUsedIndex);

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

                #endregion

                pointOffset = geometry.Positions.Count;
            }
            private static void EndCap_PlateHard(ref int pointOffset, ref double[] rotateAnglesForPerp, MeshGeometry3D geometry, Point[] pointsTheta, Transform3D transform, Transform3D normalTransform, TubeRingBase ring, bool isFirst)
            {
                Vector3D normal = normalTransform.Transform(new Vector3D(0, 0, 1)).ToUnit();

                // Start with 0,1,2
                geometry.Positions.Add(transform.Transform(new Point3D(pointsTheta[0].X, pointsTheta[0].Y, 0d)));
                geometry.Positions.Add(transform.Transform(new Point3D(pointsTheta[1].X, pointsTheta[1].Y, 0d)));
                geometry.Positions.Add(transform.Transform(new Point3D(pointsTheta[2].X, pointsTheta[2].Y, 0d)));

                geometry.Normals.Add(normal);
                geometry.Normals.Add(normal);
                geometry.Normals.Add(normal);

                geometry.TriangleIndices.Add(pointOffset + 0);
                geometry.TriangleIndices.Add(pointOffset + 1);
                geometry.TriangleIndices.Add(pointOffset + 2);

                int lowerIndex = 2;
                int upperIndex = pointsTheta.Length - 1;
                int lastUsedIndex = 0;
                bool shouldBumpLower = true;

                int localOffset = 3;

                // Do the rest of the triangles
                while (lowerIndex < upperIndex)
                {
                    geometry.Positions.Add(transform.Transform(new Point3D(pointsTheta[lowerIndex].X, pointsTheta[lowerIndex].Y, 0d)));
                    geometry.Positions.Add(transform.Transform(new Point3D(pointsTheta[upperIndex].X, pointsTheta[upperIndex].Y, 0d)));
                    geometry.Positions.Add(transform.Transform(new Point3D(pointsTheta[lastUsedIndex].X, pointsTheta[lastUsedIndex].Y, 0d)));

                    geometry.Normals.Add(normal);
                    geometry.Normals.Add(normal);
                    geometry.Normals.Add(normal);

                    geometry.TriangleIndices.Add(pointOffset + localOffset + 0);
                    geometry.TriangleIndices.Add(pointOffset + localOffset + 1);
                    geometry.TriangleIndices.Add(pointOffset + localOffset + 2);

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

                    localOffset += 3;
                }

                // Update ref param
                pointOffset = geometry.Positions.Count;
            }

            private static void Middle(ref int pointOffset, ref double[] rotateAnglesForPerp, MeshGeometry3D geometry, Transform3D transform, int numSides, TubeRingBase ring1, TubeRingBase ring2, double curZ, bool softSides)
            {
                if (ring1 is TubeRingRegularPolygon && ring2 is TubeRingRegularPolygon)
                {
                    #region tube

                    if (softSides)
                    {
                        Middle_TubeSoft(ref pointOffset, ref rotateAnglesForPerp, geometry, transform, numSides, (TubeRingRegularPolygon)ring1, (TubeRingRegularPolygon)ring2, curZ);
                    }
                    else
                    {
                        Middle_TubeHard(ref pointOffset, ref rotateAnglesForPerp, geometry, transform, numSides, (TubeRingRegularPolygon)ring1, (TubeRingRegularPolygon)ring2, curZ);
                    }

                    #endregion
                }
                else
                {
                    // There are no other combos that need to show a visual right now (eventually, I'll have a definition for
                    // a non regular polygon - low in fiber)
                }
            }

            private static void Middle_TubeSoft(ref int pointOffset, ref double[] rotateAnglesForPerp, MeshGeometry3D geometry, Transform3D transform, int numSides, TubeRingRegularPolygon ring1, TubeRingRegularPolygon ring2, double curZ)
            {
                if (ring1.MergeNormalWithPrevIfSoft || ring2.MergeNormalWithPrevIfSoft)
                {
                    throw new ApplicationException("finish this");
                }

                Point[] points = Math2D.GetCircle_Cached(numSides);

                #region points/normals

                //TODO: Don't add the bottom ring's points, only the top

                // Ring 1
                for (int cntr = 0; cntr < numSides; cntr++)
                {
                    geometry.Positions.Add(transform.Transform(new Point3D(points[cntr].X * ring1.RadiusX, points[cntr].Y * ring1.RadiusY, curZ)));
                    geometry.Normals.Add(transform.Transform(new Vector3D(points[cntr].X * ring1.RadiusX, points[cntr].Y * ring1.RadiusY, 0d).ToUnit()));		// the normals point straight out of the side
                }

                // Ring 2
                for (int cntr = 0; cntr < numSides; cntr++)
                {
                    geometry.Positions.Add(transform.Transform(new Point3D(points[cntr].X * ring2.RadiusX, points[cntr].Y * ring2.RadiusY, curZ + ring2.DistFromPrevRing)));
                    geometry.Normals.Add(transform.Transform(new Vector3D(points[cntr].X * ring2.RadiusX, points[cntr].Y * ring2.RadiusY, 0d).ToUnit()));		// the normals point straight out of the side
                }

                #endregion

                #region triangles

                int zOffsetBottom = pointOffset;
                int zOffsetTop = zOffsetBottom + numSides;

                for (int cntr = 0; cntr < numSides - 1; cntr++)
                {
                    // Top/Left triangle
                    geometry.TriangleIndices.Add(zOffsetBottom + cntr + 0);
                    geometry.TriangleIndices.Add(zOffsetTop + cntr + 1);
                    geometry.TriangleIndices.Add(zOffsetTop + cntr + 0);

                    // Bottom/Right triangle
                    geometry.TriangleIndices.Add(zOffsetBottom + cntr + 0);
                    geometry.TriangleIndices.Add(zOffsetBottom + cntr + 1);
                    geometry.TriangleIndices.Add(zOffsetTop + cntr + 1);
                }

                // Connecting the last 2 points to the first 2
                // Top/Left triangle
                geometry.TriangleIndices.Add(zOffsetBottom + (numSides - 1) + 0);
                geometry.TriangleIndices.Add(zOffsetTop);		// wrapping back around
                geometry.TriangleIndices.Add(zOffsetTop + (numSides - 1) + 0);

                // Bottom/Right triangle
                geometry.TriangleIndices.Add(zOffsetBottom + (numSides - 1) + 0);
                geometry.TriangleIndices.Add(zOffsetBottom);
                geometry.TriangleIndices.Add(zOffsetTop);

                #endregion

                pointOffset = geometry.Positions.Count;
            }
            private static void Middle_TubeHard(ref int pointOffset, ref double[] rotateAnglesForPerp, MeshGeometry3D geometry, Transform3D transform, int numSides, TubeRingRegularPolygon ring1, TubeRingRegularPolygon ring2, double curZ)
            {
                Point[] points = Math2D.GetCircle_Cached(numSides);

                int zOffset = pointOffset;

                for (int cntr = 0; cntr < numSides - 1; cntr++)
                {
                    // Top/Left triangle (each triangle gets its own 3 points)
                    geometry.Positions.Add(transform.Transform(new Point3D(points[cntr].X * ring1.RadiusX, points[cntr].Y * ring1.RadiusY, curZ)));
                    geometry.Positions.Add(transform.Transform(new Point3D(points[cntr + 1].X * ring2.RadiusX, points[cntr + 1].Y * ring2.RadiusY, curZ + ring2.DistFromPrevRing)));
                    geometry.Positions.Add(transform.Transform(new Point3D(points[cntr].X * ring2.RadiusX, points[cntr].Y * ring2.RadiusY, curZ + ring2.DistFromPrevRing)));

                    Vector3D normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                    geometry.Normals.Add(normal);		// the normals point straight out of the face
                    geometry.Normals.Add(normal);
                    geometry.Normals.Add(normal);

                    geometry.TriangleIndices.Add(zOffset + 0);
                    geometry.TriangleIndices.Add(zOffset + 1);
                    geometry.TriangleIndices.Add(zOffset + 2);

                    zOffset += 3;

                    // Bottom/Right triangle
                    geometry.Positions.Add(transform.Transform(new Point3D(points[cntr].X * ring1.RadiusX, points[cntr].Y * ring1.RadiusY, curZ)));
                    geometry.Positions.Add(transform.Transform(new Point3D(points[cntr + 1].X * ring1.RadiusX, points[cntr + 1].Y * ring1.RadiusY, curZ)));
                    geometry.Positions.Add(transform.Transform(new Point3D(points[cntr + 1].X * ring2.RadiusX, points[cntr + 1].Y * ring2.RadiusY, curZ + ring2.DistFromPrevRing)));

                    normal = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                    geometry.Normals.Add(normal);		// the normals point straight out of the face
                    geometry.Normals.Add(normal);
                    geometry.Normals.Add(normal);

                    geometry.TriangleIndices.Add(zOffset + 0);
                    geometry.TriangleIndices.Add(zOffset + 1);
                    geometry.TriangleIndices.Add(zOffset + 2);

                    zOffset += 3;
                }

                // Connecting the last 2 points to the first 2

                // Top/Left triangle
                geometry.Positions.Add(transform.Transform(new Point3D(points[numSides - 1].X * ring1.RadiusX, points[numSides - 1].Y * ring1.RadiusY, curZ)));
                geometry.Positions.Add(transform.Transform(new Point3D(points[0].X * ring2.RadiusX, points[0].Y * ring2.RadiusY, curZ + ring2.DistFromPrevRing)));
                geometry.Positions.Add(transform.Transform(new Point3D(points[numSides - 1].X * ring2.RadiusX, points[numSides - 1].Y * ring2.RadiusY, curZ + ring2.DistFromPrevRing)));

                Vector3D normal2 = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                geometry.Normals.Add(normal2);		// the normals point straight out of the face
                geometry.Normals.Add(normal2);
                geometry.Normals.Add(normal2);

                geometry.TriangleIndices.Add(zOffset + 0);
                geometry.TriangleIndices.Add(zOffset + 1);
                geometry.TriangleIndices.Add(zOffset + 2);

                zOffset += 3;

                // Bottom/Right triangle
                geometry.Positions.Add(transform.Transform(new Point3D(points[numSides - 1].X * ring1.RadiusX, points[numSides - 1].Y * ring1.RadiusY, curZ)));
                geometry.Positions.Add(transform.Transform(new Point3D(points[0].X * ring1.RadiusX, points[0].Y * ring1.RadiusY, curZ)));
                geometry.Positions.Add(transform.Transform(new Point3D(points[0].X * ring2.RadiusX, points[0].Y * ring2.RadiusY, curZ + ring2.DistFromPrevRing)));

                normal2 = GetNormal(geometry.Positions[zOffset + 0], geometry.Positions[zOffset + 1], geometry.Positions[zOffset + 2]);
                geometry.Normals.Add(normal2);		// the normals point straight out of the face
                geometry.Normals.Add(normal2);
                geometry.Normals.Add(normal2);

                geometry.TriangleIndices.Add(zOffset + 0);
                geometry.TriangleIndices.Add(zOffset + 1);
                geometry.TriangleIndices.Add(zOffset + 2);

                // Update ref param
                pointOffset = geometry.Positions.Count;
            }

            //TODO: See if normals should always be unit vectors or not
            private static Vector3D GetNormal(Point3D point0, Point3D point1, Point3D point2)
            {
                Vector3D dir1 = point0 - point1;
                Vector3D dir2 = point2 - point1;

                return Vector3D.CrossProduct(dir2, dir1).ToUnit();
            }

            #endregion
        }

        #endregion

        #region Declaration Section

        /// <summary>
        /// This is the dpi to use when creating a RenderTargetBitmap
        /// </summary>
        public const double DPI = 96;

        private const double INV256 = 1d / 256d;

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class POINT
        {
            public int x = 0; public int y = 0;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCursorPos(int x, int y);

        [DllImport("User32", EntryPoint = "ClientToScreen", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Auto)]
        private static extern int ClientToScreen(IntPtr hWnd, [In, Out] POINT pt);

        private static readonly Matrix3D _zeroMatrix = new Matrix3D(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        private static readonly Vector3D _xAxis = new Vector3D(1, 0, 0);
        private static readonly Vector3D _yAxis = new Vector3D(0, 1, 0);
        private static readonly Vector3D _zAxis = new Vector3D(0, 0, 1);

        #region fonts

        private static Lazy<FontInfo[]> _fonts = new Lazy<FontInfo[]>(() => GetSystemFontsInfo());

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetTextMetrics(IntPtr hdc, out TEXTMETRICW lptm);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiObj);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct TEXTMETRICW
        {
            public int tmHeight;
            public int tmAscent;
            public int tmDescent;
            public int tmInternalLeading;
            public int tmExternalLeading;
            public int tmAveCharWidth;
            public int tmMaxCharWidth;
            public int tmWeight;
            public int tmOverhang;
            public int tmDigitizedAspectX;
            public int tmDigitizedAspectY;
            public ushort tmFirstChar;
            public ushort tmLastChar;
            public ushort tmDefaultChar;
            public ushort tmBreakChar;
            public byte tmItalic;
            public byte tmUnderlined;
            public byte tmStruckOut;
            public byte tmPitchAndFamily;
            public byte tmCharSet;
        }

        public enum TEXTMETRICW_CharSet
        {
            Unknown = -1,       // this value is mine for when the win32 call failed

            Ansi = 0,
            Arabic = 178,
            Baltic = 186,
            ChineseBig5 = 136,      //The Traditional Chinese character set.
            Default = 1,        //The default character set.
            EastEurope = 238,
            Gb2312 = 134,       //The Gb2312 simplified Chinese character set.
            Greek = 161,
            Hangul = 129,       //The Hangul (Korean) character set.
            Hebrew = 177,
            Johab = 130,        //The Johab (Korean) character set.
            Mac = 77,       //The Macintosh character set.
            Oem = 255,
            Russian = 204,
            ShiftJis = 128,     //The ShiftJis (Japanese) character set.
            Symbol = 2,     //fonts like wingdings
            Thai = 222,
            Turkish = 162,
            Vietnamese = 163,
        }

        private class FontInfo
        {
            public FontFamily FontFamily { get; set; }

            public bool HasTextMetric { get; set; }
            public TEXTMETRICW TextMetric { get; set; }

            public TEXTMETRICW_CharSet CharSet => HasTextMetric ?
                (TEXTMETRICW_CharSet)TextMetric.tmCharSet :
                TEXTMETRICW_CharSet.Unknown;
        }

        #endregion

        #endregion

        #region color

        /// <summary>
        /// This returns a color that is the result of the two colors blended
        /// </summary>
        /// <remarks>
        /// NOTE: This is sort of a mashup (AlphaBlend was written years before OverlayColors and AverageColors)
        /// </remarks>
        /// <param name="alpha">0 is all back color, 1 is all fore color, .5 is half way between</param>
        public static Color AlphaBlend(Color foreColor, Color backColor, double alpha)
        {
            // Figure out the new color
            double a, r, g, b;
            if (foreColor.A == 0)
            {
                // Fore is completely transparent, so only worry about blending the alpha
                a = backColor.A + (((foreColor.A - backColor.A) * INV256) * alpha * 255d);
                r = backColor.R;
                g = backColor.G;
                b = backColor.B;
            }
            else if (backColor.A == 0)
            {
                // Back is completely transparent, so only worry about blending the alpha
                a = backColor.A + (((foreColor.A - backColor.A) * INV256) * alpha * 255d);
                r = foreColor.R;
                g = foreColor.G;
                b = foreColor.B;
            }
            else
            {
                a = backColor.A + (((foreColor.A - backColor.A) * INV256) * alpha * 255d);
                r = backColor.R + (((foreColor.R - backColor.R) * INV256) * alpha * 255d);
                g = backColor.G + (((foreColor.G - backColor.G) * INV256) * alpha * 255d);
                b = backColor.B + (((foreColor.B - backColor.B) * INV256) * alpha * 255d);
            }

            return GetColorCapped(a, r, g, b);
        }
        /// <summary>
        /// Profiling shows that creating a color is 6.5 times slower than a byte array
        /// </summary>
        /// <remarks>
        /// Code is copied for speed reasons
        /// </remarks>
        public static byte[] AlphaBlend(byte[] foreColor, byte[] backColor, double alpha)
        {
            if (backColor.Length == 4)
            {
                #region ARGB

                // Figure out the new color
                if (foreColor[0] == 0)
                {
                    // Fore is completely transparent, so only worry about blending the alpha
                    return new byte[]
                        {
                            GetByteCapped(backColor[0] + (((foreColor[0] - backColor[0]) * INV256) * alpha * 255d)),
                            backColor[1],
                            backColor[2],
                            backColor[3]
                        };
                }
                else if (backColor[0] == 0)
                {
                    // Back is completely transparent, so only worry about blending the alpha
                    return new byte[]
                        {
                            GetByteCapped(backColor[0] + (((foreColor[0] - backColor[0]) * INV256) * alpha * 255d)),
                            foreColor[1],
                            foreColor[2],
                            foreColor[3]
                        };
                }
                else
                {
                    return new byte[]
                        {
                            GetByteCapped(backColor[0] + (((foreColor[0] - backColor[0]) * INV256) * alpha * 255d)),
                            GetByteCapped(backColor[1] + (((foreColor[1] - backColor[1]) * INV256) * alpha * 255d)),
                            GetByteCapped(backColor[2] + (((foreColor[2] - backColor[2]) * INV256) * alpha * 255d)),
                            GetByteCapped(backColor[3] + (((foreColor[3] - backColor[3]) * INV256) * alpha * 255d))
                        };
                }

                #endregion
            }
            else
            {
                #region RGB

                return new byte[]
                    {
                        GetByteCapped(backColor[0] + (((foreColor[0] - backColor[0]) * INV256) * alpha * 255d)),
                        GetByteCapped(backColor[1] + (((foreColor[1] - backColor[1]) * INV256) * alpha * 255d)),
                        GetByteCapped(backColor[2] + (((foreColor[2] - backColor[2]) * INV256) * alpha * 255d))
                    };

                #endregion
            }
        }

        /// <summary>
        /// This lays the colors on top of each other, and returns the result
        /// </summary>
        /// <remarks>
        /// This treats each color like a plate of glass.  So setting a fully opaque plate halfway up the stack will completely block everything
        /// under it.  I tested this in wpf by placing rectangles on top of each other, and got the same color that wpf got.
        /// 
        /// This is a simplified copy of the other overload.  The code was copied for speed reasons
        /// </remarks>
        public static Color OverlayColors(IEnumerable<Color> colors)
        {
            bool isFirst = true;

            //  This represents the running return color (values from 0 to 1)
            double a = 0, r = 0, g = 0, b = 0;

            //  Shoot through the colors, and lay them on top of the running return color
            foreach (var color in colors)
            {
                if (color.A == 0)
                {
                    //  Ignore transparent colors
                    continue;
                }

                if (isFirst)
                {
                    //  Store the first color
                    a = color.A * INV256;
                    r = color.R * INV256;
                    g = color.G * INV256;
                    b = color.B * INV256;

                    isFirst = false;
                    continue;
                }

                double a2 = color.A * INV256;
                double r2 = color.R * INV256;
                double g2 = color.G * INV256;
                double b2 = color.B * INV256;

                //  Alpha is a bit funny, it's a control more than a color
                a = Math.Max(a, a2);

                //  Add the weighted difference between this color and the running color
                r += (r2 - r) * a2;
                g += (g2 - g) * a2;
                b += (b2 - b) * a2;
            }

            if (isFirst)
            {
                //  The list was empty, or all the colors were transparent
                return Colors.Transparent;
            }

            return GetColorCapped(a * 256d, r * 256d, g * 256d, b * 256d);
        }
        /// <summary>
        /// This overload adds an extra dial for transparency (the same result could be achieved by pre multiplying each color by its
        /// corresponding percent)
        /// </summary>
        /// <param name="colors">
        /// Item1=The color
        /// Item2=Percent (0 to 1)
        /// </param>
        public static Color OverlayColors(IEnumerable<Tuple<Color, double>> colors)
        {
            const double INV255 = 1d / 255d;

            bool isFirst = true;

            //  This represents the running return color (values from 0 to 1)
            double a = 0, r = 0, g = 0, b = 0;

            //  Shoot through the colors, and lay them on top of the running return color
            foreach (var color in colors)
            {
                if (color.Item1.A == 0 || color.Item2 == 0d)
                {
                    //  Ignore transparent colors
                    continue;
                }

                if (isFirst)
                {
                    //  Store the first color
                    a = (color.Item1.A * INV255) * color.Item2;
                    r = color.Item1.R * INV255;
                    g = color.Item1.G * INV255;
                    b = color.Item1.B * INV255;

                    isFirst = false;
                    continue;
                }

                double a2 = (color.Item1.A * INV255) * color.Item2;
                double r2 = color.Item1.R * INV255;
                double g2 = color.Item1.G * INV255;
                double b2 = color.Item1.B * INV255;

                //  Alpha is a bit funny, it's a control more than a color
                a = Math.Max(a, a2);

                //  Add the weighted difference between this color and the running color
                r += (r2 - r) * a2;
                g += (g2 - g) * a2;
                b += (b2 - b) * a2;
            }

            if (isFirst)
            {
                //  The list was empty, or all the colors were transparent
                return Colors.Transparent;
            }

            return GetColorCapped(a * 255d, r * 255d, g * 255d, b * 255d);
        }
        /// <summary>
        /// Made an overload for byte array, because byte arrays are faster than the color struct (6.5 times faster)
        /// </summary>
        public static byte[] OverlayColors(IEnumerable<byte[]> colors)
        {
            bool isFirst = true;

            //  This represents the running return color (values from 0 to 1)
            double a = 0, r = 0, g = 0, b = 0;

            //  Shoot through the colors, and lay them on top of the running return color
            foreach (var color in colors)
            {
                if (color[0] == 0)
                {
                    //  Ignore transparent colors
                    continue;
                }

                if (isFirst)
                {
                    //  Store the first color
                    a = color[0] * INV256;
                    r = color[1] * INV256;
                    g = color[2] * INV256;
                    b = color[3] * INV256;

                    isFirst = false;
                    continue;
                }

                double a2 = color[0] * INV256;
                double r2 = color[1] * INV256;
                double g2 = color[2] * INV256;
                double b2 = color[3] * INV256;

                //  Alpha is a bit funny, it's a control more than a color
                a = Math.Max(a, a2);

                //  Add the weighted difference between this color and the running color
                r += (r2 - r) * a2;
                g += (g2 - g) * a2;
                b += (b2 - b) * a2;
            }

            if (isFirst)
            {
                //  The list was empty, or all the colors were transparent
                return new byte[] { 0, 0, 0, 0 };
            }

            return new byte[]
            {
                GetByteCapped(a * 256d),
                GetByteCapped(r * 256d),
                GetByteCapped(g * 256d),
                GetByteCapped(b * 256d)
            };
        }

        /// <summary>
        /// This takes the weighted average of all the colors (using alpha as the weight multiplier)
        /// </summary>
        /// <remarks>
        /// This is a simplified copy of the other overload.  The code was copied for speed reasons
        /// </remarks>
        public static Color AverageColors(IEnumerable<Color> colors)
        {
            byte[] retVal = AverageColors(colors.Select(o => new[] { o.A, o.R, o.G, o.B }));
            return Color.FromArgb(retVal[0], retVal[1], retVal[2], retVal[3]);
        }
        public static byte[] AverageColors(IEnumerable<byte[]> colors)
        {
            const double INV255 = 1d / 255d;
            const double NEARZERO = .001d;

            #region convert to doubles

            List<Tuple<double, double, double, double>> doubles = new List<Tuple<double, double, double, double>>();

            double minAlpha = double.MaxValue;
            bool isAllTransparent = true;

            //  Convert to doubles from 0 to 1 (throw out fully transparent colors)
            foreach (var color in colors)
            {
                double a = color[0] * INV255;

                doubles.Add(Tuple.Create(a, color[1] * INV255, color[2] * INV255, color[3] * INV255));

                if (a > NEARZERO && a < minAlpha)
                {
                    isAllTransparent = false;
                    minAlpha = a;
                }
            }

            #endregion

            if (isAllTransparent)
            {
                return new byte[] { 0, 0, 0, 0 };       // Colors.Transparent;
            }

            #region weighted sum

            double sumA = 0, sumR = 0, sumG = 0, sumB = 0;
            double sumWeight = 0;

            foreach (var dbl in doubles)
            {
                if (dbl.Item1 <= NEARZERO)
                {
                    //  This is fully transparent.  It doesn't affect sumWeight, but the final alpha is divided by doubles.Count, so it does affect the final alpha (sumWeight
                    //  affects the color, not alpha)
                    continue;
                }

                double multiplier = dbl.Item1 / minAlpha;       //  dividing by min so that multiplier is always greater or equal to 1
                sumWeight += multiplier;

                sumA += dbl.Item1;      //  this one isn't weighted, it's a simple average
                sumR += dbl.Item2 * multiplier;
                sumG += dbl.Item3 * multiplier;
                sumB += dbl.Item4 * multiplier;
            }

            double divisor = 1d / sumWeight;

            #endregion

            return GetColorCapped_Bytes((sumA / doubles.Count) * 255d, sumR * divisor * 255d, sumG * divisor * 255d, sumB * divisor * 255d);
        }

        /// <summary>
        /// This takes the weighted average of all the colors (using alpha as the weight multiplier)
        /// </summary>
        /// <param name="colors">
        /// Item1=Color
        /// Item2=% (0 to 1)
        /// </param>
        /// <remarks>
        /// The overlay method is what you would normally think of when alpha blending.  This method doesn't care about
        /// the order of the colors, it just averages them (so if several colors are passed in that are different hues, you'll just get gray)
        /// 
        /// http://www.investopedia.com/terms/w/weightedaverage.asp
        /// </remarks>
        public static Color AverageColors(IEnumerable<Tuple<Color, double>> colors)
        {
            byte[] retVal = AverageColors(colors.Select(o => Tuple.Create(new[] { o.Item1.A, o.Item1.R, o.Item1.G, o.Item1.B }, o.Item2)));
            return Color.FromArgb(retVal[0], retVal[1], retVal[2], retVal[3]);
        }
        public static byte[] AverageColors(IEnumerable<Tuple<byte[], double>> colors)
        {
            const double INV255 = 1d / 255d;
            const double NEARZERO = .001d;

            #region convert to doubles

            //  A, R, G, B, %
            List<Tuple<double, double, double, double, double>> doubles = new List<Tuple<double, double, double, double, double>>();        //  I hate using such an ugly tuple, but the alternative is linq and anonymous types, and this method needs to be as fast as possible

            double minAlpha = double.MaxValue;
            bool isAllTransparent = true;

            //  Convert to doubles from 0 to 1 (throw out fully transparent colors)
            foreach (var color in colors)
            {
                double a = (color.Item1[0] * INV255);
                double a1 = a * color.Item2;

                doubles.Add(Tuple.Create(a, color.Item1[1] * INV255, color.Item1[2] * INV255, color.Item1[3] * INV255, color.Item2));

                if (a1 > NEARZERO && a1 < minAlpha)
                {
                    isAllTransparent = false;
                    minAlpha = a1;
                }
            }

            #endregion

            if (isAllTransparent)
            {
                return new byte[] { 0, 0, 0, 0 };       // Colors.Transparent;
            }

            #region weighted sum

            double sumA = 0, sumR = 0, sumG = 0, sumB = 0;
            double sumAlphaWeight = 0, sumWeight = 0;

            foreach (var dbl in doubles)
            {
                sumAlphaWeight += dbl.Item5;        //  Item5 should already be from 0 to 1

                if ((dbl.Item1 * dbl.Item5) <= NEARZERO)
                {
                    //  This is fully transparent.  It doesn't affect the sum of the color's weight, but does affect the sum of the alpha's weight
                    continue;
                }

                double multiplier = (dbl.Item1 * dbl.Item5) / minAlpha;       //  dividing by min so that multiplier is always greater or equal to 1
                sumWeight += multiplier;

                sumA += dbl.Item1;      //  alphas have their own weighting
                sumR += dbl.Item2 * multiplier;
                sumG += dbl.Item3 * multiplier;
                sumB += dbl.Item4 * multiplier;
            }

            double divisor = 1d / sumWeight;

            #endregion

            return GetColorCapped_Bytes((sumA / sumAlphaWeight) * 255d, sumR * divisor * 255d, sumG * divisor * 255d, sumB * divisor * 255d);
        }

        /// <summary>
        /// This makes a gray version of the color
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://www.tannerhelland.com/3643/grayscale-image-algorithm-vb6/
        /// </remarks>
        public static Color ConvertToGray(Color color)
        {
            byte gray = Convert.ToByte(ConvertToGray(color.R, color.G, color.B));
            return Color.FromArgb(color.A, gray, gray, gray);
        }
        /// <summary>
        /// This converts the color into a value from 0 to 255
        /// </summary>
        public static double ConvertToGray(byte r, byte g, byte b)
        {
            // These are some other approaches that could be used (they don't look as good though)
            //return (r + g + b) / 3d;        // Averge
            //return (Math3D.Max(r, g, b) + Math3D.Min(r, g, b)) / 2d;      // Desaturate
            //return r * 0.2126 + g * 0.7152 + b * 0.0722;        // BT.709

            return r * 0.299 + g * 0.587 + b * 0.114;     // BT.601
        }

        public static Color GetRandomColor(byte min, byte max)
        {
            return GetRandomColor(255, min, max);
        }
        public static Color GetRandomColor(byte alpha, byte min, byte max)
        {
            Random rand = StaticRandom.GetRandomForThread();
            return Color.FromArgb(alpha, Convert.ToByte(rand.Next(min, max + 1)), Convert.ToByte(rand.Next(min, max + 1)), Convert.ToByte(rand.Next(min, max + 1)));
        }
        public static Color GetRandomColor(byte alpha, byte minRed, byte maxRed, byte minGreen, byte maxGreen, byte minBlue, byte maxBlue)
        {
            Random rand = StaticRandom.GetRandomForThread();
            return Color.FromArgb(alpha, Convert.ToByte(rand.Next(minRed, maxRed + 1)), Convert.ToByte(rand.Next(minGreen, maxGreen + 1)), Convert.ToByte(rand.Next(minBlue, maxBlue + 1)));
        }
        public static Color GetRandomColor(byte alpha, byte baseR, byte baseG, byte baseB, int drift)
        {
            Random rand = StaticRandom.GetRandomForThread();

            int newR = rand.Next(baseR - drift, baseR + drift + 1);
            int newG = rand.Next(baseG - drift, baseG + drift + 1);
            int newB = rand.Next(baseB - drift, baseB + drift + 1);

            return Color.FromArgb(alpha, newR.ToByte(), newG.ToByte(), newB.ToByte());
        }
        /// <summary>
        /// This will choose a random hue, find the equivalent color for that hue, then randomly drift the saturation and value
        /// </summary>
        public static ColorHSV GetRandomColor(double hueFrom, double hueTo, double saturationDrift, int valueDrift, EquivalentColor basedOn)
        {
            Random rand = StaticRandom.GetRandomForThread();

            double hue = rand.NextDouble(hueFrom, hueTo);

            ColorHSV equivalent = basedOn.GetEquivalent(hue);

            return new ColorHSV
            (
                equivalent.H,
                UtilityMath.Clamp(rand.NextDrift(equivalent.S, saturationDrift), 0, 100),
                UtilityMath.Clamp(rand.NextDrift(equivalent.V, valueDrift), 0, 100)
            );
        }

        //TODO: Make a version that chooses random points in an HSV cone
        /// <summary>
        /// This returns random colors that are as far from each other as possible
        /// </summary>
        /// <param name="existing">These are other colors that the returned colors should avoid.  If passed in, this list doesn't count toward the return count</param>
        public static Color[] GetRandomColors(int count, byte min, byte max, Color[] existing = null)
        {
            return GetRandomColors(count, 255, min, max, existing);
        }
        public static Color[] GetRandomColors(int count, byte alpha, byte min, byte max, Color[] existing = null)
        {
            return GetRandomColors(count, alpha, min, max, min, max, min, max, existing);
        }
        public static Color[] GetRandomColors(int count, byte alpha, byte minRed, byte maxRed, byte minGreen, byte maxGreen, byte minBlue, byte maxBlue, Color[] existing = null)
        {
            if (count < 1)
                return new Color[0];

            VectorND[] existingStatic = null;

            if (existing != null && existing.Length > 0)
            {
                existingStatic = existing.
                    Select(o => new double[] { o.R, o.G, o.B }.ToVectorND()).
                    ToArray();
            }
            else
            {
                Color staticColor = GetRandomColor(alpha, minRed, maxRed, minGreen, maxGreen, minBlue, maxBlue);

                if (count == 1)
                    return new[] { staticColor };

                existingStatic = new[] { new double[] { staticColor.R, staticColor.G, staticColor.B }.ToVectorND() };
            }

            var aabb = (new double[] { minRed, minGreen, minBlue }.ToVectorND(), new double[] { maxRed, maxGreen, maxBlue }.ToVectorND());

            // Treat each RGB value as a 3D vector.  Now inject the items in a cube defined by aabb.  Each point
            // pushes the others away, so the returned items are as far from each other as possible
            //NOTE: Using a single static color to get unique values across runs (otherwise they get stuck in corners,
            //and could be about the same from run to run)
            VectorND[] colors = MathND.GetRandomVectors_Cube_EventDist(count - 1, aabb, existingStaticPoints: existingStatic);

            return colors.
                Concat(existingStatic).
                Select(o => Color.FromArgb(alpha, o[0].ToByte_Round(), o[1].ToByte_Round(), o[2].ToByte_Round())).
                ToArray();
        }

        public static Color GetColorEGA(int number)
        {
            return GetColorEGA(255, number);
        }
        public static Color GetColorEGA(byte alpha, int number)
        {
            //http://en.wikipedia.org/wiki/Enhanced_Graphics_Adapter

            return number switch
            {
                0 => Color.FromArgb(alpha, 0, 0, 0),// black
                1 => Color.FromArgb(alpha, 0, 0, 170),// blue
                2 => Color.FromArgb(alpha, 0, 170, 0),// green
                3 => Color.FromArgb(alpha, 0, 170, 170),// cyan
                4 => Color.FromArgb(alpha, 170, 0, 0),// red
                5 => Color.FromArgb(alpha, 170, 0, 170),// magenta
                6 => Color.FromArgb(alpha, 170, 85, 0),// brown
                7 => Color.FromArgb(alpha, 170, 170, 170),// light gray
                8 => Color.FromArgb(alpha, 85, 85, 85),// dark gray
                9 => Color.FromArgb(alpha, 85, 85, 255),// bright blue
                10 => Color.FromArgb(alpha, 85, 255, 85),// bright green
                11 => Color.FromArgb(alpha, 85, 255, 255),// bright cyan
                12 => Color.FromArgb(alpha, 255, 85, 85),// bright red
                13 => Color.FromArgb(alpha, 255, 85, 255),// bright magenta
                14 => Color.FromArgb(alpha, 255, 255, 85),// bright yellow
                15 => Color.FromArgb(alpha, 255, 255, 255),// bright white
                _ => throw new ArgumentException("The number must be between 0 and 15: " + number.ToString()),
            };
        }

        /// <summary>
        /// This returns a color that is opposite of what is passed in (yellow becomes purple, white becomes black, etc)
        /// </summary>
        /// <param name="discourageGray">
        /// True: If the source color is near gray, the returned will tend toward white or black instead (useful if you don't want the two colors similar to each other)
        /// False: Simply returns value on the other side of 50
        /// </param>
        public static Color OppositeColor(Color color, bool discourageGray = true)
        {
            ColorHSV hsv = RGBtoHSV(color);

            // Hue (no need to cap between 0:360.  The ToRGB method will do that)
            double hue = hsv.H + 180;

            // Value
            double distanceFrom50 = hsv.V - 50;
            double value = 50 - distanceFrom50;

            if (discourageGray && Math.Abs(distanceFrom50) < 25)
            {
                // Instead of converging on 50, converge on 0 or 100
                if (distanceFrom50 < 0)
                {
                    value = 100 + distanceFrom50;       // dist is negative, so is actually subtraction
                }
                else
                {
                    value = 0 + distanceFrom50;
                }
            }

            // Leave saturation alone
            return HSVtoRGB(hue, hsv.S, value);
        }
        /// <summary>
        /// This returns either black or white
        /// </summary>
        public static Color OppositeColor_BW(Color color)
        {
            ColorHSV oppositeColor = UtilityWPF.OppositeColor(color).ToHSV();

            if (oppositeColor.V > 50)
            {
                return Colors.White;
            }
            else
            {
                return Colors.Black;
            }
        }

        public static ColorHSV GetEquivalentColor(ColorHSV colorToMatch, double requestHue)
        {
            // Putting a link in UtilityWPF to make it easier to find
            return EquivalentColor.GetEquivalent(colorToMatch, requestHue);
        }

        /// <summary>
        /// This is just a wrapper to the color converter (why can't they have a method off the color class with all
        /// the others?)
        /// </summary>
        public static Color ColorFromHex(string hexValue)
        {
            string final = hexValue;

            if (!final.StartsWith("#"))
            {
                final = "#" + final;
            }

            if (final.Length == 4)      // compressed format, no alpha
            {
                // #08F -> #0088FF
                final = new string(new[] { '#', final[1], final[1], final[2], final[2], final[3], final[3] });
            }
            else if (final.Length == 5)     // compressed format, has alpha
            {
                // #8A4F -> #88AA44FF
                final = new string(new[] { '#', final[1], final[1], final[2], final[2], final[3], final[3], final[4], final[4] });
            }

            return (Color)ColorConverter.ConvertFromString(final);
        }
        public static string ColorToHex(Color color, bool includeAlpha = true, bool includePound = true)
        {
            // I think color.ToString does the same thing, but this is explicit
            return string.Format("{0}{1}{2}{3}{4}",
                includePound ? "#" : "",
                includeAlpha ? color.A.ToString("X2") : "",
                color.R.ToString("X2"),
                color.G.ToString("X2"),
                color.B.ToString("X2"));
        }

        public static Brush BrushFromHex(string hexValue)
        {
            return new SolidColorBrush(ColorFromHex(hexValue));
        }

        /// <summary>
        /// Returns the color as hue/saturation/value
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://stackoverflow.com/questions/4123998/algorithm-to-switch-between-rgb-and-hsb-color-values
        /// </remarks>
        /// <param name="h">from 0 to 360</param>
        /// <param name="s">from 0 to 100</param>
        /// <param name="v">from 0 to 100</param>
        public static ColorHSV RGBtoHSV(Color color)
        {
            // Normalize the RGB values by scaling them to be between 0 and 1
            double red = color.R / 255d;
            double green = color.G / 255d;
            double blue = color.B / 255d;

            double minValue = Math1D.Min(red, green, blue);
            double maxValue = Math1D.Max(red, green, blue);
            double delta = maxValue - minValue;

            double h, s, v;

            v = maxValue;

            #region Get Hue

            // Calculate the hue (in degrees of a circle, between 0 and 360)
            if (red >= green && red >= blue)
            {
                if (green >= blue)
                {
                    if (delta <= 0)
                    {
                        h = 0d;
                    }
                    else
                    {
                        h = 60d * (green - blue) / delta;
                    }
                }
                else
                {
                    h = 60d * (green - blue) / delta + 360d;
                }
            }
            else if (green >= red && green >= blue)
            {
                h = 60d * (blue - red) / delta + 120d;
            }
            else //if (blue >= red && blue >= green)
            {
                h = 60d * (red - green) / delta + 240d;
            }

            #endregion

            // Calculate the saturation (between 0 and 1)
            if (maxValue == 0d)
            {
                s = 0d;
            }
            else
            {
                s = 1d - (minValue / maxValue);
            }

            // Scale the saturation and value to a percentage between 0 and 100
            s *= 100d;
            v *= 100d;

            #region Cap Values

            if (h < 0d)
            {
                h = 0d;
            }
            else if (h > 360d)
            {
                h = 360d;
            }

            if (s < 0d)
            {
                s = 0d;
            }
            else if (s > 100d)
            {
                s = 100d;
            }

            if (v < 0d)
            {
                v = 0d;
            }
            else if (v > 100d)
            {
                v = 100d;
            }

            #endregion

            return new ColorHSV(color.A, h, s, v);
        }

        public static Color HSVtoRGB(double h, double s, double v)
        {
            return HSVtoRGB(255, h, s, v);
        }
        /// <summary>
        /// Converts hue/saturation/value to a color
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://stackoverflow.com/questions/4123998/algorithm-to-switch-between-rgb-and-hsb-color-values
        /// </remarks>
        /// <param name="a">0 to 255</param>
        /// <param name="h">0 to 360</param>
        /// <param name="s">0 to 100</param>
        /// <param name="v">0 to 100</param>
        public static Color HSVtoRGB(byte a, double h, double s, double v)
        {
            // Scale the Saturation and Value components to be between 0 and 1
            double hue = GetHueCapped(h);
            double sat = s / 100d;
            double val = v / 100d;

            double r, g, b;		// these go between 0 and 1

            if (sat == 0d)
            {
                #region gray

                // If the saturation is 0, then all colors are the same.
                // (This is some flavor of gray.)
                r = val;
                g = val;
                b = val;

                #endregion
            }
            else
            {
                #region color

                // Calculate the appropriate sector of a 6-part color wheel
                double sectorPos = hue / 60d;
                int sectorNumber = Convert.ToInt32(Math.Floor(sectorPos));

                // Get the fractional part of the sector (that is, how many degrees into the sector you are)
                double fractionalSector = sectorPos - sectorNumber;

                // Calculate values for the three axes of the color
                double p = val * (1d - sat);
                double q = val * (1d - (sat * fractionalSector));
                double t = val * (1d - (sat * (1d - fractionalSector)));

                // Assign the fractional colors to red, green, and blue
                // components based on the sector the angle is in
                switch (sectorNumber)
                {
                    case 0:
                    case 6:
                        r = val;
                        g = t;
                        b = p;
                        break;

                    case 1:
                        r = q;
                        g = val;
                        b = p;
                        break;

                    case 2:
                        r = p;
                        g = val;
                        b = t;
                        break;

                    case 3:
                        r = p;
                        g = q;
                        b = val;
                        break;

                    case 4:
                        r = t;
                        g = p;
                        b = val;
                        break;

                    case 5:
                        r = val;
                        g = p;
                        b = q;
                        break;

                    default:
                        throw new ArgumentException("Invalid hue: " + h.ToString());
                }

                #endregion
            }

            #region scale/cap 255

            // Scale to 255 (using int to make it easier to handle overflow)
            int rNew = Convert.ToInt32(Math.Round(r * 255d));
            int gNew = Convert.ToInt32(Math.Round(g * 255d));
            int bNew = Convert.ToInt32(Math.Round(b * 255d));

            // Make sure the values are in range
            if (rNew < 0)
            {
                rNew = 0;
            }
            else if (rNew > 255)
            {
                rNew = 255;
            }

            if (gNew < 0)
            {
                gNew = 0;
            }
            else if (gNew > 255)
            {
                gNew = 255;
            }

            if (bNew < 0)
            {
                bNew = 0;
            }
            else if (bNew > 255)
            {
                bNew = 255;
            }

            #endregion

            return Color.FromArgb(a, Convert.ToByte(rNew), Convert.ToByte(gNew), Convert.ToByte(bNew));
        }

        public static bool IsTransparent(Color color)
        {
            return color.A == 0;
        }
        public static bool IsTransparent(Brush brush)
        {
            if (brush is SolidColorBrush)
            {
                return IsTransparent(((SolidColorBrush)brush).Color);
            }
            else if (brush is GradientBrush)
            {
                GradientBrush brushCast = (GradientBrush)brush;
                if (brushCast.Opacity == 0d)
                {
                    return true;
                }

                return !brushCast.GradientStops.Any(o => !IsTransparent(o.Color));		// if any are non-transparent, return false
            }

            // Not sure what it is, probably a bitmap or something, so just assume it's not transparent
            return false;
        }

        public static Color ExtractColor(Brush brush)
        {
            if (brush is SolidColorBrush)
            {
                return ((SolidColorBrush)brush).Color;
            }
            else if (brush is GradientBrush)
            {
                GradientBrush brushCast = (GradientBrush)brush;

                Color average = AverageColors(brushCast.GradientStops.Select(o => o.Color));

                if (brushCast.Opacity.IsNearZero())
                {
                    return Color.FromArgb(0, average.R, average.G, average.B);
                }
                else if (brushCast.Opacity.IsNearValue(1))
                {
                    return average;
                }
                else
                {
                    double opacity = average.A / 255d;
                    opacity *= brushCast.Opacity;

                    return Color.FromArgb((opacity * 255d).ToByte_Round(), average.R, average.G, average.B);
                }
            }
            else
            {
                throw new ArgumentException("Unsupported brush type: " + brush.GetType().ToString());
            }
        }

        /// <summary>
        /// This returns the distance between the two hues
        /// </summary>
        /// <remarks>
        /// It gets a bit complicated, because hue wraps at 360
        /// 
        /// examples:
        ///     80, 90 -> 10
        ///     40, 30 -> 10
        ///     0, 360 -> 0
        ///     350, 10 -> 20
        /// </remarks>
        public static double GetHueDistance(double hue1, double hue2)
        {
            if (hue1 < 0 || hue1 > 360 || hue2 < 0 || hue2 > 360)
            {
                throw new ArgumentException(string.Format("The hues must be between 0 and 360.  hue1={0}, hue2={1}", hue1.ToString(), hue2.ToString()));
            }

            double retVal = Math.Abs(hue1 - hue2);

            if (retVal <= 180)
            {
                return retVal;
            }

            // They straddle the 0 degree line.  Add 360 to the smaller one to bring it closer to the larger one

            double min = Math.Min(hue1, hue2);
            double max = Math.Max(hue1, hue2);

            return Math.Abs(min + 360 - max);
        }

        public static double GetHueCapped(double hue)
        {
            double retVal = hue;

            while (true)
            {
                if (retVal < 0)
                {
                    retVal += 360;
                }
                else if (retVal >= 360)
                {
                    retVal -= 360;
                }
                else
                {
                    return retVal;
                }
            }
        }

        #endregion

        #region bitmaps

        /// <summary>
        /// This tells a visual to render itself, and returns a custom class that returns colors at various positions
        /// </summary>
        /// <param name="cacheColorsUpFront">
        /// True:  The entire byte array will be converted into Color structs up front (taking an up front hit, but repeated requests for colors are cheap).
        /// False:  The byte array is stored, and any requests for colors are done on the fly (good if only a subset of the pixels will be looked at, of if you want another thread to take the hit).
        /// </param>
        /// <param name="outOfBoundsColor">If requests for pixels outside of width/height are made, this is the color that should be returned (probably either use transparent or black)</param>
        public static IBitmapCustom RenderControl(FrameworkElement visual, int width, int height, bool cacheColorsUpFront, Color outOfBoundsColor, bool isInVisualTree)
        {
            // Populate a wpf bitmap with a snapshot of the visual
            BitmapSource bitmap = RenderControl(visual, width, height, isInVisualTree);

            return ConvertToColorArray(bitmap, cacheColorsUpFront, outOfBoundsColor);
        }

        /// <summary>
        /// This tells a visual to render itself to a wpf bitmap.  From there, you can get the bytes (colors), or run it through a converter
        /// to save as jpg, bmp files.
        /// </summary>
        /// <remarks>
        /// This fixes an issue where the rendered image is blank:
        /// http://blogs.msdn.com/b/jaimer/archive/2009/07/03/rendertargetbitmap-tips.aspx
        /// </remarks>
        public static BitmapSource RenderControl(FrameworkElement visual, int width, int height, bool isInVisualTree)
        {
            if (!isInVisualTree)
            {
                // If the visual isn't part of the visual tree, then it needs to be forced to finish its layout
                visual.Width = width;
                visual.Height = height;
                visual.Measure(new Size(width, height));        //  I thought these two statements would be expensive, but profiling shows it's mostly all on Render
                visual.Arrange(new Rect(0, 0, width, height));
            }

            RenderTargetBitmap retVal = new RenderTargetBitmap(width, height, DPI, DPI, PixelFormats.Pbgra32);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                VisualBrush vb = new VisualBrush(visual);
                ctx.DrawRectangle(vb, null, new Rect(new Point(0, 0), new Point(width, height)));
            }

            retVal.Render(dv);      //  profiling shows this is the biggest hit

            return retVal;
        }

        /// <summary>
        /// Converts the color array into a bitmap that can be set as an Image.Source
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://www.i-programmer.info/programming/wpf-workings/527-writeablebitmap.html
        /// </remarks>
        public static BitmapSource GetBitmap(Color[] colors, int width, int height)
        {
            if (colors.Length != width * height)
            {
                throw new ArgumentException(string.Format("The array isn't the same as width*height.  ArrayLength={0}, Width={1}, Height={2}", colors.Length, width, height));
            }

            WriteableBitmap retVal = new WriteableBitmap(width, height, DPI, DPI, PixelFormats.Pbgra32, null);      // may want Bgra32 if performance is an issue

            int pixelWidth = retVal.Format.BitsPerPixel / 8;
            int stride = retVal.PixelWidth * pixelWidth;      // this is the length of one row of pixels

            byte[] pixels = new byte[retVal.PixelHeight * stride];

            for (int rowCntr = 0; rowCntr < height; rowCntr++)
            {
                int rowOffset = rowCntr * stride;
                int yOffset = rowCntr * width;

                for (int columnCntr = 0; columnCntr < width; columnCntr++)
                {
                    int offset = rowOffset + (columnCntr * pixelWidth);

                    Color color = colors[columnCntr + yOffset];

                    pixels[offset + 3] = color.A;
                    pixels[offset + 2] = color.R;
                    pixels[offset + 1] = color.G;
                    pixels[offset + 0] = color.B;
                }
            }

            retVal.WritePixels(new Int32Rect(0, 0, retVal.PixelWidth, retVal.PixelHeight), pixels, stride, 0);

            return retVal;
        }
        /// <summary>
        /// This overload is faster
        /// Each color should be an array of 4 bytes (A,R,G,B)
        /// </summary>
        public static BitmapSource GetBitmap(byte[][] colors, int width, int height)
        {
            //if (colors.Length != width * height)
            //{
            //    throw new ArgumentException(string.Format("The array isn't the same as width*height.  ArrayLength={0}, Width={1}, Height={2}", colors.Length, width, height));
            //}

            WriteableBitmap retVal = new WriteableBitmap(width, height, DPI, DPI, PixelFormats.Pbgra32, null);      // may want Bgra32 if performance is an issue

            int pixelWidth = retVal.Format.BitsPerPixel / 8;
            int stride = retVal.PixelWidth * pixelWidth;      // this is the length of one row of pixels

            byte[] pixels = new byte[retVal.PixelHeight * stride];

            for (int rowCntr = 0; rowCntr < height; rowCntr++)
            {
                int rowOffset = rowCntr * stride;
                int yOffset = rowCntr * width;

                for (int columnCntr = 0; columnCntr < width; columnCntr++)
                {
                    int offset = rowOffset + (columnCntr * pixelWidth);

                    byte[] color = colors[columnCntr + yOffset];

                    pixels[offset + 3] = color[0];
                    pixels[offset + 2] = color[1];
                    pixels[offset + 1] = color[2];
                    pixels[offset + 0] = color[3];
                }
            }

            retVal.WritePixels(new Int32Rect(0, 0, retVal.PixelWidth, retVal.PixelHeight), pixels, stride, 0);

            return retVal;
        }
        /// <summary>
        /// This overload takes a double array that represents a grayscale
        /// </summary>
        /// <param name="grayValueScale">
        /// If the values in the double array are 0 to 1, then leave this as 255.  If they are already 0 to 255, set to 1
        /// </param>
        /// <param name="invert">
        /// False: 0=Black, 1=White
        /// True: 0=White, 0=Black
        /// </param>
        public static BitmapSource GetBitmap(double[] grayColors, int width, int height, double grayValueScale = 255, bool invert = false)
        {
            //if (colors.Length != width * height)
            //{
            //    throw new ArgumentException(string.Format("The array isn't the same as width*height.  ArrayLength={0}, Width={1}, Height={2}", colors.Length, width, height));
            //}

            WriteableBitmap retVal = new WriteableBitmap(width, height, DPI, DPI, PixelFormats.Pbgra32, null);      // may want Bgra32 if performance is an issue

            int pixelWidth = retVal.Format.BitsPerPixel / 8;
            int stride = retVal.PixelWidth * pixelWidth;      // this is the length of one row of pixels

            byte[] pixels = new byte[retVal.PixelHeight * stride];

            for (int rowCntr = 0; rowCntr < height; rowCntr++)
            {
                int rowOffset = rowCntr * stride;
                int yOffset = rowCntr * width;

                for (int columnCntr = 0; columnCntr < width; columnCntr++)
                {
                    int offset = rowOffset + (columnCntr * pixelWidth);

                    byte gray = (grayColors[columnCntr + yOffset] * grayValueScale).ToByte_Round();
                    if (invert)
                    {
                        gray = (255 - gray).ToByte();
                    }

                    pixels[offset + 3] = 255;
                    pixels[offset + 2] = gray;
                    pixels[offset + 1] = gray;
                    pixels[offset + 0] = gray;
                }
            }

            retVal.WritePixels(new Int32Rect(0, 0, retVal.PixelWidth, retVal.PixelHeight), pixels, stride, 0);

            return retVal;
        }
        /// <summary>
        /// This has triples (rgb) for every pixel, so the array is three times larger
        /// </summary>
        public static BitmapSource GetBitmap_RGB(double[] rgbColors, int width, int height, double colorValueScale = 255)
        {
            //if (colors.Length != width * height * 3)
            //{
            //    throw new ArgumentException(string.Format("The array isn't the same as width*height*3.  ArrayLength={0}, Width={1}, Height={2}", colors.Length, width, height));
            //}

            WriteableBitmap retVal = new WriteableBitmap(width, height, DPI, DPI, PixelFormats.Pbgra32, null);      // may want Bgra32 if performance is an issue

            int pixelWidth = retVal.Format.BitsPerPixel / 8;
            int stride = retVal.PixelWidth * pixelWidth;      // this is the length of one row of pixels

            byte[] pixels = new byte[retVal.PixelHeight * stride];

            for (int rowCntr = 0; rowCntr < height; rowCntr++)
            {
                int rowOffset = rowCntr * stride;
                int yOffset = rowCntr * width * 3;

                for (int columnCntr = 0; columnCntr < width; columnCntr++)
                {
                    int offset = rowOffset + (columnCntr * pixelWidth);
                    int xOffset = columnCntr * 3;

                    byte r = (rgbColors[yOffset + xOffset + 0] * colorValueScale).ToByte_Round();
                    byte g = (rgbColors[yOffset + xOffset + 1] * colorValueScale).ToByte_Round();
                    byte b = (rgbColors[yOffset + xOffset + 2] * colorValueScale).ToByte_Round();

                    pixels[offset + 3] = 255;
                    pixels[offset + 2] = r;
                    pixels[offset + 1] = g;
                    pixels[offset + 0] = b;
                }
            }

            retVal.WritePixels(new Int32Rect(0, 0, retVal.PixelWidth, retVal.PixelHeight), pixels, stride, 0);

            return retVal;
        }

        /// <summary>
        /// This draws a small number of colors onto a larger image
        /// WARNING: This draws each of colors as a rectangle, so if there are a lot, it gets SLOOOOW
        /// </summary>
        /// <remarks>
        /// This is meant for drawing really small color patches onto a larger image control.  I couldn't figure out how to
        /// get Image to scale up a tiny bitmap without antialiasing
        /// </remarks>
        public static BitmapSource GetBitmap_Aliased(Color[] colors, int colorsWidth, int colorsHeight, int imageWidth, int imageHeight)
        {
            if (colors.Length != colorsWidth * colorsHeight)
            {
                throw new ArgumentException(string.Format("The array isn't the same as colorsWidth*colorsHeight.  ArrayLength={0}, Width={1}, Height={2}", colors.Length, colorsWidth, colorsHeight));
            }

            double scaleX = Convert.ToDouble(imageWidth) / Convert.ToDouble(colorsWidth);
            double scaleY = Convert.ToDouble(imageHeight) / Convert.ToDouble(colorsHeight);

            RenderTargetBitmap retVal = new RenderTargetBitmap(imageWidth, imageHeight, DPI, DPI, PixelFormats.Pbgra32);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                int index = 0;

                for (int y = 0; y < colorsHeight; y++)
                {
                    for (int x = 0; x < colorsWidth; x++)
                    {
                        ctx.DrawRectangle(new SolidColorBrush(colors[index]), null, new Rect(x * scaleX, y * scaleY, scaleX, scaleY));

                        index++;
                    }
                }
            }

            retVal.Render(dv);

            return retVal;
        }
        public static BitmapSource GetBitmap_Aliased(byte[][] colors, int colorsWidth, int colorsHeight, int imageWidth, int imageHeight)
        {
            if (colors.Length != colorsWidth * colorsHeight)
            {
                throw new ArgumentException(string.Format("The array isn't the same as colorsWidth*colorsHeight.  ArrayLength={0}, Width={1}, Height={2}", colors.Length, colorsWidth, colorsHeight));
            }

            double scaleX = Convert.ToDouble(imageWidth) / Convert.ToDouble(colorsWidth);
            double scaleY = Convert.ToDouble(imageHeight) / Convert.ToDouble(colorsHeight);

            RenderTargetBitmap retVal = new RenderTargetBitmap(imageWidth, imageHeight, DPI, DPI, PixelFormats.Pbgra32);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                int index = 0;

                for (int y = 0; y < colorsHeight; y++)
                {
                    for (int x = 0; x < colorsWidth; x++)
                    {
                        Color color = Color.FromArgb(colors[index][0], colors[index][1], colors[index][2], colors[index][3]);

                        ctx.DrawRectangle(new SolidColorBrush(color), null, new Rect(x * scaleX, y * scaleY, scaleX, scaleY));

                        index++;
                    }
                }
            }

            retVal.Render(dv);

            return retVal;
        }
        public static BitmapSource GetBitmap_Aliased(double[] grayColors, int colorsWidth, int colorsHeight, int imageWidth, int imageHeight, double grayValueScale = 255, bool invert = false)
        {
            if (grayColors.Length != colorsWidth * colorsHeight)
            {
                throw new ArgumentException(string.Format("The array isn't the same as colorsWidth*colorsHeight.  ArrayLength={0}, Width={1}, Height={2}", grayColors.Length, colorsWidth, colorsHeight));
            }

            double scaleX = Convert.ToDouble(imageWidth) / Convert.ToDouble(colorsWidth);
            double scaleY = Convert.ToDouble(imageHeight) / Convert.ToDouble(colorsHeight);

            RenderTargetBitmap retVal = new RenderTargetBitmap(imageWidth, imageHeight, DPI, DPI, PixelFormats.Pbgra32);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                int index = 0;

                for (int y = 0; y < colorsHeight; y++)
                {
                    for (int x = 0; x < colorsWidth; x++)
                    {
                        byte gray = (grayColors[index] * grayValueScale).ToByte_Round();
                        if (invert)
                        {
                            gray = (255 - gray).ToByte();
                        }

                        Color color = Color.FromRgb(gray, gray, gray);

                        ctx.DrawRectangle(new SolidColorBrush(color), null, new Rect(x * scaleX, y * scaleY, scaleX, scaleY));

                        index++;
                    }
                }
            }

            retVal.Render(dv);

            return retVal;
        }
        public static BitmapSource GetBitmap_Aliased_RGB(double[] rgbColors, int colorsWidth, int colorsHeight, int imageWidth, int imageHeight, double colorValueScale = 255)
        {
            if (rgbColors.Length != colorsWidth * colorsHeight * 3)
            {
                throw new ArgumentException(string.Format("The array isn't the same as colorsWidth*colorsHeight*3.  ArrayLength={0}, Width={1}, Height={2}", rgbColors.Length, colorsWidth, colorsHeight));
            }

            double scaleX = Convert.ToDouble(imageWidth) / Convert.ToDouble(colorsWidth);
            double scaleY = Convert.ToDouble(imageHeight) / Convert.ToDouble(colorsHeight);

            RenderTargetBitmap retVal = new RenderTargetBitmap(imageWidth, imageHeight, DPI, DPI, PixelFormats.Pbgra32);

            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                int index = 0;

                for (int y = 0; y < colorsHeight; y++)
                {
                    for (int x = 0; x < colorsWidth; x++)
                    {
                        int r = (rgbColors[index + 0] * colorValueScale).ToInt_Round();
                        if (r < 0) r = 0;
                        if (r > 255) r = 255;

                        int g = (rgbColors[index + 1] * colorValueScale).ToInt_Round();
                        if (g < 0) g = 0;
                        if (g > 255) g = 255;

                        int b = (rgbColors[index + 2] * colorValueScale).ToInt_Round();
                        if (b < 0) b = 0;
                        if (b > 255) b = 255;

                        Color color = Color.FromRgb(Convert.ToByte(r), Convert.ToByte(g), Convert.ToByte(b));

                        ctx.DrawRectangle(new SolidColorBrush(color), null, new Rect(x * scaleX, y * scaleY, scaleX, scaleY));

                        index += 3;
                    }
                }
            }

            retVal.Render(dv);

            return retVal;
        }

        /// <summary>
        /// If you use "new BitmapImage(new Uri(filename", it locks the file, even if you set the image to null.  So this method
        /// reads the file into bytes, and returns the bitmap a different way
        /// </summary>
        public static BitmapSource GetBitmap(string filename)
        {
            BitmapImage retVal = new BitmapImage();

            using (FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                retVal.BeginInit();
                retVal.CacheOption = BitmapCacheOption.OnLoad;
                retVal.StreamSource = stream;
                retVal.EndInit();
            }

            return retVal;
        }

        /// <param name="convertToColors">
        /// True:  The entire byte array will be converted into Color structs up front (or byte[][])
        ///     Use this if you want color structs (expensive, but useful)
        ///     This takes an up front cache hit, but repeated requests for colors are cheap
        ///     Only useful if you plan to do many gets from this class
        /// 
        /// False:  The byte array is stored in the file's format, and any requests for colors are done on the fly
        ///     This is more useful if you plan to get the colors in other formats (byte[][] or convolution), or if you just want an array of color[]
        ///     Also good if only a subset of the pixels will be looked at
        ///     Another use for this is if you want another thread to take the hit
        /// </param>
        /// <param name="outOfBoundsColor">If requests for pixels outside of width/height are made, this is the color that should be returned (probably either use transparent or black)</param>
        /// <param name="convertToColors_IsColor">
        /// True: cached as colors
        /// False: cached as bytes      -- there may be a case where this is more efficient than using BitmapCustomCachedBytes???
        /// </param>
        public static IBitmapCustom ConvertToColorArray(BitmapSource bitmap, bool convertToColors, Color outOfBoundsColor, bool convertToColors_IsColor = true)
        {
            if (bitmap.Format != PixelFormats.Pbgra32 && bitmap.Format != PixelFormats.Bgr32)
            {
                // I've only coded against the above two formats, so put it in that
                bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0);      //http://stackoverflow.com/questions/1176910/finding-specific-pixel-colors-of-a-bitmapimage
            }

            // Get a byte array
            int stride = (bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8;		//http://msdn.microsoft.com/en-us/magazine/cc534995.aspx
            byte[] bytes = new byte[stride * bitmap.PixelHeight];

            bitmap.CopyPixels(bytes, stride, 0);

            BitmapStreamInfo info = new BitmapStreamInfo(bytes, bitmap.PixelWidth, bitmap.PixelHeight, stride, bitmap.Format, outOfBoundsColor);

            if (convertToColors)
            {
                return new BitmapCustomCachedColors(info, convertToColors_IsColor);
            }
            else
            {
                return new BitmapCustomCachedBytes(info);
            }
        }

        public static Convolution2D ConvertToConvolution(BitmapSource bitmap, double scaleTo = 255d, string description = "")
        {
            // This got tedious to write, so I made a simpler method around it
            return ((BitmapCustomCachedBytes)ConvertToColorArray(bitmap, false, Colors.Transparent)).
                ToConvolution(scaleTo, description);
        }
        public static Tuple<Convolution2D, Convolution2D, Convolution2D> ConvertToConvolution_RGB(BitmapSource bitmap, double scaleTo = 255d, string description = "")
        {
            return ((BitmapCustomCachedBytes)ConvertToColorArray(bitmap, false, Colors.Transparent)).
                ToConvolution_RGB(scaleTo, description);
        }

        /// <summary>
        /// This will keep the same aspect ratio
        /// </summary>
        /// <param name="shouldEnlargeIfTooSmall">
        /// True: This will enlarge if needed.
        /// False: This will only reduce (returns the original if already smaller)
        /// </param>
        public static BitmapSource ResizeImage(BitmapSource bitmap, int maxSize, bool shouldEnlargeIfTooSmall = false)
        {
            if (!shouldEnlargeIfTooSmall && bitmap.PixelWidth <= maxSize && bitmap.PixelHeight <= maxSize)
            {
                return bitmap;
            }

            double aspectRatio = bitmap.PixelWidth.ToDouble() / bitmap.PixelHeight.ToDouble();

            int width, height;

            if (aspectRatio > 1)
            {
                // Width is larger
                width = maxSize;
                height = (width / aspectRatio).ToInt_Round();
            }
            else
            {
                // Height is larger
                height = maxSize;
                width = (height * aspectRatio).ToInt_Round();
            }

            if (width < 1) width = 1;
            if (height < 1) height = 1;

            if (width == bitmap.PixelWidth && height == bitmap.PixelHeight)
            {
                return bitmap;
            }

            return ResizeImage(bitmap, width, height);
        }
        public static BitmapSource ResizeImage(BitmapSource bitmap, int width, int height)
        {
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawImage(bitmap, new Rect(0, 0, width, height));
            }

            RenderTargetBitmap retVal = new RenderTargetBitmap(width, height, DPI, DPI, PixelFormats.Pbgra32);
            retVal.Render(drawingVisual);

            return retVal;
        }

        /// <summary>
        /// This will create a file
        /// NOTE: It will always store in png format, so that's the extension you should use
        /// </summary>
        public static void SaveBitmapPNG(BitmapSource bitmap, string filename)
        {
            using (var fileStream = new FileStream(filename, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(fileStream);
            }
        }

        /// <summary>
        /// Gets a single pixel
        /// Got this here: http://stackoverflow.com/questions/14876989/how-to-read-pixels-in-four-corners-of-a-bitmapsource
        /// </summary>
        /// <remarks>
        /// If only a single pixel is needed, this method is a bit easier than RenderControl().GetColor()
        /// </remarks>
        public static Color GetPixelColor(BitmapSource bitmap, int x, int y)
        {
            int bytesPerPixel = (bitmap.Format.BitsPerPixel + 7) / 8;
            byte[] bytes = new byte[bytesPerPixel];
            Int32Rect rect = new Int32Rect(x, y, 1, 1);

            bitmap.CopyPixels(rect, bytes, bytesPerPixel, 0);

            Color color;
            if (bitmap.Format == PixelFormats.Pbgra32)
            {
                color = Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
            }
            else if (bitmap.Format == PixelFormats.Bgr32)
            {
                color = Color.FromArgb(0xFF, bytes[2], bytes[1], bytes[0]);
            }
            // handle other required formats
            else
            {
                color = Colors.Black;
            }

            return color;
        }

        /// <summary>
        /// This converts a wpf visual into a mouse cursor (call this.RenderControl to get the bitmapsource)
        /// NOTE: Needs to be a standard size (16x16, 32x32, etc)
        /// TODO: Fix this for semitransparency
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://stackoverflow.com/questions/46805/custom-cursor-in-wpf
        /// </remarks>
        public static Cursor ConvertToCursor(BitmapSource bitmapSource, Point hotSpot)
        {
            int width = bitmapSource.PixelWidth;
            int height = bitmapSource.PixelHeight;

            // Get a byte array
            int stride = (width * bitmapSource.Format.BitsPerPixel + 7) / 8;		//http://msdn.microsoft.com/en-us/magazine/cc534995.aspx
            byte[] bytes = new byte[stride * height];

            bitmapSource.CopyPixels(bytes, stride, 0);

            // Convert to System.Drawing.Bitmap
            var bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * stride;
                int yOffset = y * width;

                for (int x = 0; x < width; x++)
                {
                    int offset = rowOffset + (x * 4);		// this is assuming that bitmap.Format.BitsPerPixel is 32, which would be four bytes per pixel

                    bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(bytes[offset + 3], bytes[offset + 2], bytes[offset + 1], bytes[offset + 0]));
                    //bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(64, 255, 0, 0));
                }
            }

            // Save to .ico format
            MemoryStream stream = new MemoryStream();
            System.Drawing.Icon.FromHandle(bitmap.GetHicon()).Save(stream);

            // Convert saved file into .cur format
            stream.Seek(2, SeekOrigin.Begin);
            stream.WriteByte(2);        // convert to cur format

            //stream.Seek(8, SeekOrigin.Begin);     // trying to wipe out the color pallete to be able to support transparency, but has no effect
            //stream.WriteByte(0);
            //stream.WriteByte(0);

            stream.Seek(10, SeekOrigin.Begin);
            stream.WriteByte((byte)(int)(hotSpot.X * width));
            stream.WriteByte((byte)(int)(hotSpot.Y * height));
            stream.Seek(0, SeekOrigin.Begin);

            // Construct Cursor
            return new Cursor(stream);
        }

        #endregion

        #region math

        //---------------------------------------------------------------------------
        //
        // (c) Copyright Microsoft Corporation.
        // This source is subject to the Microsoft Limited Permissive License.
        // See http://www.microsoft.com/resources/sharedsource/licensingbasics/limitedpermissivelicense.mspx
        // All other rights reserved.
        //
        // This file is part of the 3D Tools for Windows Presentation Foundation
        // project.  For more information, see:
        // 
        // http://CodePlex.com/Wiki/View.aspx?ProjectName=3DTools
        //
        //---------------------------------------------------------------------------

        private static Matrix3D GetViewMatrix(ProjectionCamera camera)
        {
            if (camera == null) throw new ArgumentNullException("camera");

            // This math is identical to what you find documented for
            // D3DXMatrixLookAtRH with the exception that WPF uses a
            // LookDirection vector rather than a LookAt point.

            Vector3D zAxis = -camera.LookDirection;
            zAxis.Normalize();

            Vector3D xAxis = Vector3D.CrossProduct(camera.UpDirection, zAxis);
            xAxis.Normalize();

            Vector3D yAxis = Vector3D.CrossProduct(zAxis, xAxis);

            Vector3D position = (Vector3D)camera.Position;
            double offsetX = -Vector3D.DotProduct(xAxis, position);
            double offsetY = -Vector3D.DotProduct(yAxis, position);
            double offsetZ = -Vector3D.DotProduct(zAxis, position);

            Matrix3D m = new Matrix3D(
                xAxis.X, yAxis.X, zAxis.X, 0,
                xAxis.Y, yAxis.Y, zAxis.Y, 0,
                xAxis.Z, yAxis.Z, zAxis.Z, 0,
                offsetX, offsetY, offsetZ, 1);

            return m;
        }
        /// <summary>
        /// Computes the effective view matrix for the given camera
        /// </summary>
        public static Matrix3D GetViewMatrix(Camera camera)
        {
            if (camera == null)
                throw new ArgumentNullException("camera");

            ProjectionCamera projectionCamera = camera as ProjectionCamera;

            if (projectionCamera != null)
            {
                return GetViewMatrix(projectionCamera);
            }

            MatrixCamera matrixCamera = camera as MatrixCamera;

            if (matrixCamera != null)
            {
                return matrixCamera.ViewMatrix;
            }

            throw new ArgumentException(String.Format("Unsupported camera type '{0}'.", camera.GetType().FullName), "camera");
        }

        private static Matrix3D GetProjectionMatrix(OrthographicCamera camera, double aspectRatio)
        {
            if (camera == null) throw new ArgumentNullException("camera");

            // This math is identical to what you find documented for
            // D3DXMatrixOrthoRH with the exception that in WPF only
            // the camera's width is specified.  Height is calculated
            // from width and the aspect ratio.

            double w = camera.Width;
            double h = w / aspectRatio;
            double zn = camera.NearPlaneDistance;
            double zf = camera.FarPlaneDistance;

            double m33 = 1 / (zn - zf);
            double m43 = zn * m33;

            return new Matrix3D(
                2 / w, 0, 0, 0,
                  0, 2 / h, 0, 0,
                  0, 0, m33, 0,
                  0, 0, m43, 1);
        }
        private static Matrix3D GetProjectionMatrix(PerspectiveCamera camera, double aspectRatio)
        {
            if (camera == null) throw new ArgumentNullException("camera");

            // This math is identical to what you find documented for
            // D3DXMatrixPerspectiveFovRH with the exception that in
            // WPF the camera's horizontal rather the vertical
            // field-of-view is specified.

            double hFoV = Math1D.DegreesToRadians(camera.FieldOfView);
            double zn = camera.NearPlaneDistance;
            double zf = camera.FarPlaneDistance;

            double xScale = 1 / Math.Tan(hFoV / 2);
            double yScale = aspectRatio * xScale;
            double m33 = (zf == double.PositiveInfinity) ? -1 : (zf / (zn - zf));
            double m43 = zn * m33;

            return new Matrix3D(
                xScale, 0, 0, 0,
                     0, yScale, 0, 0,
                     0, 0, m33, -1,
                     0, 0, m43, 0);
        }
        /// <summary>
        ///     Computes the effective projection matrix for the given
        ///     camera.
        /// </summary>
        public static Matrix3D GetProjectionMatrix(Camera camera, double aspectRatio)
        {
            if (camera == null) throw new ArgumentNullException("camera");

            PerspectiveCamera perspectiveCamera = camera as PerspectiveCamera;

            if (perspectiveCamera != null)
            {
                return GetProjectionMatrix(perspectiveCamera, aspectRatio);
            }

            OrthographicCamera orthographicCamera = camera as OrthographicCamera;

            if (orthographicCamera != null)
            {
                return GetProjectionMatrix(orthographicCamera, aspectRatio);
            }

            MatrixCamera matrixCamera = camera as MatrixCamera;

            if (matrixCamera != null)
            {
                return matrixCamera.ProjectionMatrix;
            }

            throw new ArgumentException(String.Format("Unsupported camera type '{0}'.", camera.GetType().FullName), "camera");
        }

        public static Matrix3D GetHomogeneousToViewportTransform(Rect viewport)
        {
            double scaleX = viewport.Width / 2;
            double scaleY = viewport.Height / 2;
            double offsetX = viewport.X + scaleX;
            double offsetY = viewport.Y + scaleY;

            return new Matrix3D(
                 scaleX, 0, 0, 0,
                      0, -scaleY, 0, 0,
                      0, 0, 1, 0,
                offsetX, offsetY, 0, 1);
        }

        public static Matrix3D GetViewportToHomogeneousTransform(Rect viewport)
        {
            double scaleX = 1;
            double scaleY = 1;
            double offsetX = -viewport.X - (viewport.Width / 2);
            double offsetY = +viewport.Y + (viewport.Height / 2);

            return new Matrix3D(
                 scaleX, 0, 0, 0,
                      0, -scaleY, 0, 0,
                      0, 0, 1, 0,
                offsetX, offsetY, 0, 1);
        }

        /// <summary>
        ///     Computes the transform from world space to the Viewport3DVisual's
        ///     inner 2D space.
        /// 
        ///     This method can fail if Camera.Transform is non-invertable
        ///     in which case the camera clip planes will be coincident and
        ///     nothing will render.  In this case success will be false.
        /// </summary>
        public static Matrix3D TryWorldToViewportTransform(Viewport3DVisual visual, out bool success)
        {
            Matrix3D result = TryWorldToCameraTransform(visual, out success);

            if (success)
            {
                result.Append(GetProjectionMatrix(visual.Camera, Math2D.GetAspectRatio(visual.Viewport.Size)));
                result.Append(GetHomogeneousToViewportTransform(visual.Viewport));
                success = true;
            }

            return result;
        }

        /// <summary>
        ///     Computes the transform from world space to camera space
        /// 
        ///     This method can fail if Camera.Transform is non-invertable
        ///     in which case the camera clip planes will be coincident and
        ///     nothing will render.  In this case success will be false.
        /// </summary>
        public static Matrix3D TryWorldToCameraTransform(Viewport3DVisual visual, out bool success)
        {
            success = false;

            Camera camera = (visual != null) ? visual.Camera : null;
            if (camera == null)
            {
                return _zeroMatrix;
            }

            Rect viewport = visual.Viewport;
            if (viewport == Rect.Empty)
            {
                return _zeroMatrix;
            }

            Matrix3D result = Matrix3D.Identity;

            Transform3D cameraTransform = camera.Transform;
            if (cameraTransform != null)
            {
                Matrix3D m = cameraTransform.Value;

                if (!m.HasInverse)
                {
                    return _zeroMatrix;
                }

                m.Invert();
                result.Append(m);
            }

            result.Append(GetViewMatrix(camera));

            success = true;
            return result;
        }

        /// <summary>
        /// This converts the position into screen coords
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://blogs.msdn.com/llobo/archive/2006/05/02/Code-for-getting-screen-relative-Position-in-WPF.aspx
        /// </remarks>
        public static Point TransformToScreen(Point point, Visual relativeTo)
        {
            HwndSource hwndSource = PresentationSource.FromVisual(relativeTo) as HwndSource;
            Visual root = hwndSource.RootVisual;

            // Translate the point from the visual to the root.
            GeneralTransform transformToRoot = relativeTo.TransformToAncestor(root);
            Point pointRoot = transformToRoot.Transform(point);

            // Transform the point from the root to client coordinates.
            Matrix m = Matrix.Identity;
            Transform transform = VisualTreeHelper.GetTransform(root);

            if (transform != null)
            {
                m = Matrix.Multiply(m, transform.Value);
            }

            Vector offset = VisualTreeHelper.GetOffset(root);
            m.Translate(offset.X, offset.Y);

            Point pointClient = m.Transform(pointRoot);

            // Convert from “device-independent pixels” into pixels.
            pointClient = hwndSource.CompositionTarget.TransformToDevice.Transform(pointClient);

            POINT pointClientPixels = new POINT();
            pointClientPixels.x = (0 < pointClient.X) ? (int)(pointClient.X + 0.5) : (int)(pointClient.X - 0.5);
            pointClientPixels.y = (0 < pointClient.Y) ? (int)(pointClient.Y + 0.5) : (int)(pointClient.Y - 0.5);

            // Transform the point into screen coordinates.
            POINT pointScreenPixels = pointClientPixels;
            ClientToScreen(hwndSource.Handle, pointScreenPixels);
            return new Point(pointScreenPixels.x, pointScreenPixels.y);
        }

        #endregion

        #region 3D geometry

        //TODO: This function is flipping the colors of point2 and point3 (2 is 3's color, and 3 is 2's color)
        /// <summary>
        /// This textures a triangle with several gradient brushes
        /// </summary>
        /// <param name="blackPlate">
        /// If all three colors are opaque, the black background makes the colors stand out.  But if you're dealing with
        /// semitransparency, set this to false
        /// </param>
        public static Model3D GetGradientTriangle(Point3D point1, Point3D point2, Point3D point3, Color color1, Color color2, Color color3, bool blackPlate)
        {
            Vector3D normal = new Triangle_wpf(point1, point2, point3).Normal;

            MeshGeometry3D geometry = new MeshGeometry3D()
            {
                TriangleIndices = new Int32Collection(new[] { 0, 1, 2, 2, 1, 0 }),
                Normals = new Vector3DCollection(new[] { normal, normal, normal, normal, normal, normal }),
                TextureCoordinates = new PointCollection(new[] { new Point(0, 0), new Point(1, 0), new Point(0, 1) }),
                Positions = new Point3DCollection(new[] { point1, point2, point3 }),
            };

            Model3DGroup group = new Model3DGroup();

            #region back black

            if (blackPlate)
            {
                group.Children.Add(new GeometryModel3D()
                {
                    Material = new DiffuseMaterial(Brushes.Black),
                    Geometry = geometry,
                });
            }

            #endregion
            #region point 1

            group.Children.Add(new GeometryModel3D()
            {
                Material = new DiffuseMaterial(new RadialGradientBrush()
                {
                    Center = new Point(0, 0),
                    GradientOrigin = new Point(0, 0),
                    RadiusX = 1,
                    RadiusY = 1,
                    GradientStops = new GradientStopCollection(new[]
                    {
                        new GradientStop(color1, 0),
                        new GradientStop(Color.FromArgb(0, color1.R, color1.G, color1.B), 1),
                    }),
                }),
                Geometry = geometry,
            });

            #endregion
            #region point 2

            group.Children.Add(new GeometryModel3D()
            {
                Material = new DiffuseMaterial(new RadialGradientBrush()
                {
                    Center = new Point(1, 0),
                    GradientOrigin = new Point(1, 0),
                    RadiusX = 1,
                    RadiusY = 1,
                    GradientStops = new GradientStopCollection(new[]
                    {
                        new GradientStop(color2, 0),
                        new GradientStop(Color.FromArgb(0, color2.R, color2.G, color2.B), 1),
                    }),
                }),
                Geometry = geometry,
            });

            #endregion
            #region point 3

            group.Children.Add(new GeometryModel3D()
            {
                Material = new DiffuseMaterial(new RadialGradientBrush()
                {
                    Center = new Point(0.5, 1),
                    GradientOrigin = new Point(0.5, 1),
                    RadiusX = 1,
                    RadiusY = 1,
                    GradientStops = new GradientStopCollection(new[]
                    {
                        new GradientStop(color3, 0),
                        new GradientStop(Color.FromArgb(0, color3.R, color3.G, color3.B), 1),
                    }),
                }),
                Geometry = geometry,
            });

            #endregion

            return group;
        }

        /// <summary>
        /// ScreenSpaceLines3D makes a line that is the same thickness regardless of zoom/perspective.  This returns two bars in a cross
        /// pattern to make sort of a line.  This isn't meant to look very realistic when viewed up close, but is meant to be cheap
        /// </summary>
        public static MeshGeometry3D GetLine(Point3D from, Point3D to, double thickness)
        {
            double half = thickness / 2d;

            Vector3D line = to - from;
            if (line.X == 0 && line.Y == 0 && line.Z == 0) line.X = 0.000000001d;

            Vector3D orth1 = Math3D.GetArbitraryOrthogonal(line);
            orth1 = Math3D.RotateAroundAxis(orth1, line, StaticRandom.NextDouble() * Math.PI * 2d);		// give it a random rotation so that if many lines are created by this method, they won't all be oriented the same
            orth1 = orth1.ToUnit() * half;

            Vector3D orth2 = Vector3D.CrossProduct(line, orth1);
            orth2 = orth2.ToUnit() * half;

            // Define 3D mesh object
            MeshGeometry3D retVal = new MeshGeometry3D();

            #region plate 1

            // 0
            retVal.Positions.Add(from + orth1);
            retVal.TextureCoordinates.Add(new Point(0, 0));

            // 1
            retVal.Positions.Add(to + orth1);
            retVal.TextureCoordinates.Add(new Point(1, 1));

            // 2
            retVal.Positions.Add(from - orth1);
            retVal.TextureCoordinates.Add(new Point(0, 0));

            // 3
            retVal.Positions.Add(to - orth1);
            retVal.TextureCoordinates.Add(new Point(1, 1));

            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(1);

            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(3);

            retVal.Normals.Add(orth2);
            retVal.Normals.Add(orth2);
            retVal.Normals.Add(orth2);
            retVal.Normals.Add(orth2);
            retVal.Normals.Add(orth2);
            retVal.Normals.Add(orth2);

            #endregion

            #region plate 2

            // 4
            retVal.Positions.Add(from + orth2);
            retVal.TextureCoordinates.Add(new Point(0, 0));

            // 5
            retVal.Positions.Add(to + orth2);
            retVal.TextureCoordinates.Add(new Point(1, 1));

            // 6
            retVal.Positions.Add(from - orth2);
            retVal.TextureCoordinates.Add(new Point(0, 0));

            // 7
            retVal.Positions.Add(to - orth2);
            retVal.TextureCoordinates.Add(new Point(1, 1));

            retVal.TriangleIndices.Add(4);
            retVal.TriangleIndices.Add(7);
            retVal.TriangleIndices.Add(5);

            retVal.TriangleIndices.Add(4);
            retVal.TriangleIndices.Add(6);
            retVal.TriangleIndices.Add(7);

            retVal.Normals.Add(orth1);
            retVal.Normals.Add(orth1);
            retVal.Normals.Add(orth1);
            retVal.Normals.Add(orth1);
            retVal.Normals.Add(orth1);
            retVal.Normals.Add(orth1);

            #endregion

            //retVal.Freeze();
            return retVal;
        }

        public static MeshGeometry3D GetSquare2D(double size, double z = 0)
        {
            double halfSize = size / 2d;

            // Define 3D mesh object
            MeshGeometry3D retVal = new MeshGeometry3D();

            retVal.Positions.Add(new Point3D(-halfSize, -halfSize, z));
            retVal.Positions.Add(new Point3D(halfSize, -halfSize, z));
            retVal.Positions.Add(new Point3D(halfSize, halfSize, z));
            retVal.Positions.Add(new Point3D(-halfSize, halfSize, z));

            // Face
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(1);
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(0);

            // shouldn't I set normals?
            //retVal.Normals

            //retVal.Freeze();
            return retVal;
        }
        public static MeshGeometry3D GetSquare2D(Point min, Point max, double z = 0)
        {
            // Define 3D mesh object
            MeshGeometry3D retVal = new MeshGeometry3D();

            retVal.Positions.Add(new Point3D(min.X, min.Y, z));
            retVal.Positions.Add(new Point3D(max.X, min.Y, z));
            retVal.Positions.Add(new Point3D(max.X, max.Y, z));
            retVal.Positions.Add(new Point3D(min.X, max.Y, z));

            //TODO: Make sure Y isn't reversed
            retVal.TextureCoordinates.Add(new Point(0, 0));
            retVal.TextureCoordinates.Add(new Point(1, 0));
            retVal.TextureCoordinates.Add(new Point(1, 1));
            retVal.TextureCoordinates.Add(new Point(0, 1));

            // Face
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(1);
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(0);

            // shouldn't I set normals?
            //retVal.Normals

            //retVal.Freeze();
            return retVal;
        }

        public static MeshGeometry3D GetCube(double size)
        {
            double halfSize = size / 2d;

            // Define 3D mesh object
            MeshGeometry3D retVal = new MeshGeometry3D();

            retVal.Positions.Add(new Point3D(-halfSize, -halfSize, halfSize));		// 0
            retVal.Positions.Add(new Point3D(halfSize, -halfSize, halfSize));		// 1
            retVal.Positions.Add(new Point3D(halfSize, halfSize, halfSize));		// 2
            retVal.Positions.Add(new Point3D(-halfSize, halfSize, halfSize));		// 3

            retVal.Positions.Add(new Point3D(-halfSize, -halfSize, -halfSize));		// 4
            retVal.Positions.Add(new Point3D(halfSize, -halfSize, -halfSize));		// 5
            retVal.Positions.Add(new Point3D(halfSize, halfSize, -halfSize));		// 6
            retVal.Positions.Add(new Point3D(-halfSize, halfSize, -halfSize));		// 7

            // Front face
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(1);
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(0);

            // Back face
            retVal.TriangleIndices.Add(6);
            retVal.TriangleIndices.Add(5);
            retVal.TriangleIndices.Add(4);
            retVal.TriangleIndices.Add(4);
            retVal.TriangleIndices.Add(7);
            retVal.TriangleIndices.Add(6);

            // Right face
            retVal.TriangleIndices.Add(1);
            retVal.TriangleIndices.Add(5);
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(5);
            retVal.TriangleIndices.Add(6);
            retVal.TriangleIndices.Add(2);

            // Top face
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(6);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(6);
            retVal.TriangleIndices.Add(7);

            // Bottom face
            retVal.TriangleIndices.Add(5);
            retVal.TriangleIndices.Add(1);
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(4);
            retVal.TriangleIndices.Add(5);

            // Right face
            retVal.TriangleIndices.Add(4);
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(7);
            retVal.TriangleIndices.Add(4);

            // shouldn't I set normals?
            //retVal.Normals

            //retVal.Freeze();
            return retVal;
        }
        public static MeshGeometry3D GetCube(Point3D min, Point3D max)
        {
            // Define 3D mesh object
            MeshGeometry3D retVal = new MeshGeometry3D();

            retVal.Positions.Add(new Point3D(min.X, min.Y, max.Z));		// 0
            retVal.Positions.Add(new Point3D(max.X, min.Y, max.Z));		// 1
            retVal.Positions.Add(new Point3D(max.X, max.Y, max.Z));		// 2
            retVal.Positions.Add(new Point3D(min.X, max.Y, max.Z));		// 3

            retVal.Positions.Add(new Point3D(min.X, min.Y, min.Z));		// 4
            retVal.Positions.Add(new Point3D(max.X, min.Y, min.Z));		// 5
            retVal.Positions.Add(new Point3D(max.X, max.Y, min.Z));		// 6
            retVal.Positions.Add(new Point3D(min.X, max.Y, min.Z));		// 7

            // Front face
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(1);
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(0);

            // Back face
            retVal.TriangleIndices.Add(6);
            retVal.TriangleIndices.Add(5);
            retVal.TriangleIndices.Add(4);
            retVal.TriangleIndices.Add(4);
            retVal.TriangleIndices.Add(7);
            retVal.TriangleIndices.Add(6);

            // Right face
            retVal.TriangleIndices.Add(1);
            retVal.TriangleIndices.Add(5);
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(5);
            retVal.TriangleIndices.Add(6);
            retVal.TriangleIndices.Add(2);

            // Top face
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(6);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(6);
            retVal.TriangleIndices.Add(7);

            // Bottom face
            retVal.TriangleIndices.Add(5);
            retVal.TriangleIndices.Add(1);
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(4);
            retVal.TriangleIndices.Add(5);

            // Right face
            retVal.TriangleIndices.Add(4);
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(7);
            retVal.TriangleIndices.Add(4);

            // shouldn't I set normals?
            //retVal.Normals

            //retVal.Freeze();
            return retVal;
        }
        public static MeshGeometry3D GetCube_IndependentFaces(Vector3D min, Vector3D max)
        {
            return GetCube_IndependentFaces(min.ToPoint(), max.ToPoint());
        }
        /// <summary>
        /// GetCube shares verticies between faces, so light reflects blended and unnatural (but is a bit more efficient)
        /// This looks a bit better, but has a higher vertex count
        /// </summary>
        public static MeshGeometry3D GetCube_IndependentFaces(Point3D min, Point3D max)
        {
            // Define 3D mesh object
            MeshGeometry3D retVal = new MeshGeometry3D();

            //retVal.Positions.Add(new Point3D(min.X, min.Y, max.Z));		// 0
            //retVal.Positions.Add(new Point3D(max.X, min.Y, max.Z));		// 1
            //retVal.Positions.Add(new Point3D(max.X, max.Y, max.Z));		// 2
            //retVal.Positions.Add(new Point3D(min.X, max.Y, max.Z));		// 3

            //retVal.Positions.Add(new Point3D(min.X, min.Y, min.Z));		// 4
            //retVal.Positions.Add(new Point3D(max.X, min.Y, min.Z));		// 5
            //retVal.Positions.Add(new Point3D(max.X, max.Y, min.Z));		// 6
            //retVal.Positions.Add(new Point3D(min.X, max.Y, min.Z));		// 7

            // Front face
            retVal.Positions.Add(new Point3D(min.X, min.Y, max.Z));		// 0
            retVal.Positions.Add(new Point3D(max.X, min.Y, max.Z));		// 1
            retVal.Positions.Add(new Point3D(max.X, max.Y, max.Z));		// 2
            retVal.Positions.Add(new Point3D(min.X, max.Y, max.Z));		// 3
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(1);
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(2);
            retVal.TriangleIndices.Add(3);
            retVal.TriangleIndices.Add(0);

            // Back face
            retVal.Positions.Add(new Point3D(min.X, min.Y, min.Z));		// 4
            retVal.Positions.Add(new Point3D(max.X, min.Y, min.Z));		// 5
            retVal.Positions.Add(new Point3D(max.X, max.Y, min.Z));		// 6
            retVal.Positions.Add(new Point3D(min.X, max.Y, min.Z));		// 7
            retVal.TriangleIndices.Add(6);
            retVal.TriangleIndices.Add(5);
            retVal.TriangleIndices.Add(4);
            retVal.TriangleIndices.Add(4);
            retVal.TriangleIndices.Add(7);
            retVal.TriangleIndices.Add(6);

            // Right face
            retVal.Positions.Add(new Point3D(max.X, min.Y, max.Z));		// 1-8
            retVal.Positions.Add(new Point3D(max.X, max.Y, max.Z));		// 2-9
            retVal.Positions.Add(new Point3D(max.X, min.Y, min.Z));		// 5-10
            retVal.Positions.Add(new Point3D(max.X, max.Y, min.Z));		// 6-11
            retVal.TriangleIndices.Add(8);		// 1
            retVal.TriangleIndices.Add(10);		// 5
            retVal.TriangleIndices.Add(9);		// 2
            retVal.TriangleIndices.Add(10);		// 5
            retVal.TriangleIndices.Add(11);		// 6
            retVal.TriangleIndices.Add(9);		// 2

            // Top face
            retVal.Positions.Add(new Point3D(max.X, max.Y, max.Z));		// 2-12
            retVal.Positions.Add(new Point3D(min.X, max.Y, max.Z));		// 3-13
            retVal.Positions.Add(new Point3D(max.X, max.Y, min.Z));		// 6-14
            retVal.Positions.Add(new Point3D(min.X, max.Y, min.Z));		// 7-15
            retVal.TriangleIndices.Add(12);		// 2
            retVal.TriangleIndices.Add(14);		// 6
            retVal.TriangleIndices.Add(13);		// 3
            retVal.TriangleIndices.Add(13);		// 3
            retVal.TriangleIndices.Add(14);		// 6
            retVal.TriangleIndices.Add(15);		// 7

            // Bottom face
            retVal.Positions.Add(new Point3D(min.X, min.Y, max.Z));		// 0-16
            retVal.Positions.Add(new Point3D(max.X, min.Y, max.Z));		// 1-17
            retVal.Positions.Add(new Point3D(min.X, min.Y, min.Z));		// 4-18
            retVal.Positions.Add(new Point3D(max.X, min.Y, min.Z));		// 5-19
            retVal.TriangleIndices.Add(19);		// 5
            retVal.TriangleIndices.Add(17);		// 1
            retVal.TriangleIndices.Add(16);		// 0
            retVal.TriangleIndices.Add(16);		// 0
            retVal.TriangleIndices.Add(18);		// 4
            retVal.TriangleIndices.Add(19);		// 5

            // Right face
            retVal.Positions.Add(new Point3D(min.X, min.Y, max.Z));		// 0-20
            retVal.Positions.Add(new Point3D(min.X, max.Y, max.Z));		// 3-21
            retVal.Positions.Add(new Point3D(min.X, min.Y, min.Z));		// 4-22
            retVal.Positions.Add(new Point3D(min.X, max.Y, min.Z));		// 7-23
            retVal.TriangleIndices.Add(22);		// 4
            retVal.TriangleIndices.Add(20);		// 0
            retVal.TriangleIndices.Add(21);		// 3
            retVal.TriangleIndices.Add(21);		// 3
            retVal.TriangleIndices.Add(23);		// 7
            retVal.TriangleIndices.Add(22);		// 4

            // shouldn't I set normals?
            //retVal.Normals

            //retVal.Freeze();
            return retVal;
        }

        public static MeshGeometry3D GetSphere_LatLon(int separators, double radius)
        {
            return GetSphere_LatLon(separators, radius, radius, radius);
        }
        /// <summary>
        /// This creates a sphere using latitude, longitude
        /// </summary>
        /// <remarks>
        /// This looks good in a lot of cases, but the triangles get smaller near the poles
        /// </remarks>
        /// <param name="separators">
        /// 0=8 triangles
        /// 1=48
        /// 2=120
        /// 3=224
        /// 4=360
        /// 5=528
        /// 6=728
        /// 7=960
        /// 8=1,224 
        /// 9=1,520 
        /// 10=1,848
        /// 11=2,208
        /// 12=2,600
        /// 13=3,024
        /// 14=3,480
        /// 15=3,968
        /// 16=4,488
        /// 17=5,040
        /// 18=5,624
        /// 19=6,240
        /// 20=6,888
        /// 21=7,568 
        /// 22=8,280
        /// 23=9,024
        /// 24=9,800
        /// 25=10,608
        /// 26=11,448 
        /// 27=12,320
        /// 28=13,224
        /// 29=14,160 
        /// </param>
        public static MeshGeometry3D GetSphere_LatLon(int separators, double radiusX, double radiusY, double radiusZ)
        {
            double segmentRad = Math.PI / 2 / (separators + 1);
            int numberOfSeparators = 4 * separators + 4;

            MeshGeometry3D retVal = new MeshGeometry3D();

            // Calculate all the positions
            for (int e = -separators; e <= separators; e++)
            {
                double r_e = Math.Cos(segmentRad * e);
                double y_e = radiusY * Math.Sin(segmentRad * e);

                for (int s = 0; s <= (numberOfSeparators - 1); s++)
                {
                    double z_s = radiusZ * r_e * Math.Sin(segmentRad * s) * (-1);
                    double x_s = radiusX * r_e * Math.Cos(segmentRad * s);
                    retVal.Positions.Add(new Point3D(x_s, y_e, z_s));
                }
            }
            retVal.Positions.Add(new Point3D(0, radiusY, 0));
            retVal.Positions.Add(new Point3D(0, -1 * radiusY, 0));

            // Main Body
            int maxIterate = 2 * separators;
            for (int y = 0; y < maxIterate; y++)      // phi?
            {
                for (int x = 0; x < numberOfSeparators; x++)      // theta?
                {
                    retVal.TriangleIndices.Add(y * numberOfSeparators + (x + 1) % numberOfSeparators + numberOfSeparators);
                    retVal.TriangleIndices.Add(y * numberOfSeparators + x + numberOfSeparators);
                    retVal.TriangleIndices.Add(y * numberOfSeparators + x);

                    retVal.TriangleIndices.Add(y * numberOfSeparators + x);
                    retVal.TriangleIndices.Add(y * numberOfSeparators + (x + 1) % numberOfSeparators);
                    retVal.TriangleIndices.Add(y * numberOfSeparators + (x + 1) % numberOfSeparators + numberOfSeparators);
                }
            }

            // Top Cap
            for (int i = 0; i < numberOfSeparators; i++)
            {
                retVal.TriangleIndices.Add(maxIterate * numberOfSeparators + i);
                retVal.TriangleIndices.Add(maxIterate * numberOfSeparators + (i + 1) % numberOfSeparators);
                retVal.TriangleIndices.Add(numberOfSeparators * (2 * separators + 1));
            }

            // Bottom Cap
            for (int i = 0; i < numberOfSeparators; i++)
            {
                retVal.TriangleIndices.Add(numberOfSeparators * (2 * separators + 1) + 1);
                retVal.TriangleIndices.Add((i + 1) % numberOfSeparators);
                retVal.TriangleIndices.Add(i);
            }

            //retVal.Freeze();
            return retVal;
        }

        /// <summary>
        /// This creates a sphere based on a icosahedron (think of a 20 sided dice)
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://blog.andreaskahler.com/2009/06/creating-icosphere-mesh-in-code.html
        /// </remarks>
        /// <param name="numRecursions">
        /// 0=20 triangles
        /// 1=80
        /// 2=320
        /// 3=1,280
        /// 4=5,120
        /// 5=20,480
        /// 6=81,920
        /// 7=327,680        
        /// </param>
        public static MeshGeometry3D GetSphere_Ico(double radius, int numRecursions = 0, bool soft = false)
        {
            TriangleIndexed_wpf[] hull = Polytopes.GetIcosahedron(radius, numRecursions);

            if (soft)
            {
                return GetMeshFromTriangles(hull);
            }
            else
            {
                return GetMeshFromTriangles_IndependentFaces(hull);
            }
        }

        public static MeshGeometry3D GetCylinder_AlongX(int numSegments, double radius, double height, RotateTransform3D rotateTransform = null, bool incudeCaps = true)
        {
            //NOTE: All the other geometries in this class are along the x axis, so I want to follow suit, but I think best along the z axis.  So I'll transform the points before commiting them to the geometry
            //TODO: This is so close to GetMultiRingedTube, the only difference is the multi ring tube has "hard" faces, and this has "soft" faces (this one shares points and normals, so the lighting is smoother)

            if (numSegments < 3)
            {
                throw new ArgumentException("numSegments must be at least 3: " + numSegments.ToString(), "numSegments");
            }

            MeshGeometry3D retVal = new MeshGeometry3D();

            #region Initial calculations

            Transform3DGroup transform = new Transform3DGroup();
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 90d)));

            if (rotateTransform != null)
            {
                // This is in case they want it oriented other than along the x axis
                transform.Children.Add(rotateTransform);
            }

            double halfHeight = height / 2d;

            Point[] points = Math2D.GetCircle_Cached(numSegments);

            #endregion

            #region Side

            for (int cntr = 0; cntr < numSegments; cntr++)
            {
                retVal.Positions.Add(transform.Transform(new Point3D(points[cntr].X * radius, points[cntr].Y * radius, -halfHeight)));
                retVal.Positions.Add(transform.Transform(new Point3D(points[cntr].X * radius, points[cntr].Y * radius, halfHeight)));

                retVal.Normals.Add(transform.Transform(new Vector3D(points[cntr].X, points[cntr].Y, 0d)));		// the normals point straight out of the side
                retVal.Normals.Add(transform.Transform(new Vector3D(points[cntr].X, points[cntr].Y, 0d)));
            }

            for (int cntr = 0; cntr < numSegments - 1; cntr++)
            {
                // 0,2,3
                retVal.TriangleIndices.Add((cntr * 2) + 0);
                retVal.TriangleIndices.Add((cntr * 2) + 2);
                retVal.TriangleIndices.Add((cntr * 2) + 3);

                // 0,3,1
                retVal.TriangleIndices.Add((cntr * 2) + 0);
                retVal.TriangleIndices.Add((cntr * 2) + 3);
                retVal.TriangleIndices.Add((cntr * 2) + 1);
            }

            // Connecting the last 2 points to the first 2
            // last,0,1
            int offset = (numSegments - 1) * 2;
            retVal.TriangleIndices.Add(offset + 0);
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(1);

            // last,1,last+1
            retVal.TriangleIndices.Add(offset + 0);
            retVal.TriangleIndices.Add(1);
            retVal.TriangleIndices.Add(offset + 1);

            #endregion

            #region Caps

            if (incudeCaps)
            {
                int pointOffset = retVal.Positions.Count;

                //NOTE: The normals are backward from what you'd think

                GetCylinder_AlongX_EndCap(ref pointOffset, retVal, points, new Vector3D(0, 0, 1), radius, radius, -halfHeight, transform);
                GetCylinder_AlongX_EndCap(ref pointOffset, retVal, points, new Vector3D(0, 0, -1), radius, radius, halfHeight, transform);
            }

            #endregion

            //retVal.Freeze();
            return retVal;
        }
        private static void GetCylinder_AlongX_EndCap(ref int pointOffset, MeshGeometry3D geometry, Point[] points, Vector3D normal, double radiusX, double radiusY, double z, Transform3D transform)
        {
            //NOTE: This expects the cylinder's height to be along z, but will transform the points before commiting them to the geometry
            //TODO: This was copied from GetMultiRingedTubeSprtEndCap, make a good generic method

            #region Add points and normals

            for (int cntr = 0; cntr < points.Length; cntr++)
            {
                geometry.Positions.Add(transform.Transform(new Point3D(points[cntr].X * radiusX, points[cntr].Y * radiusY, z)));
                geometry.Normals.Add(transform.Transform(normal));
            }

            #endregion

            #region Add the triangles

            // Start with 0,1,2
            geometry.TriangleIndices.Add(pointOffset + 0);
            geometry.TriangleIndices.Add(pointOffset + 1);
            geometry.TriangleIndices.Add(pointOffset + 2);

            int lowerIndex = 2;
            int upperIndex = points.Length - 1;
            int lastUsedIndex = 0;
            bool shouldBumpLower = true;

            // Do the rest of the triangles
            while (lowerIndex < upperIndex)
            {
                geometry.TriangleIndices.Add(pointOffset + lowerIndex);
                geometry.TriangleIndices.Add(pointOffset + upperIndex);
                geometry.TriangleIndices.Add(pointOffset + lastUsedIndex);

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

            #endregion

            pointOffset += points.Length;
        }

        public static MeshGeometry3D GetCone_AlongX(int numSegments, double radius, double height, RotateTransform3D rotateTransform = null)
        {
            // This is a copy of GetCylinder_AlongX
            //TODO: This is so close to GetMultiRingedTube, the only difference is the multi ring tube has "hard" faces, and this has "soft" faces (this one shares points and normals, so the lighting is smoother)

            if (numSegments < 3)
            {
                throw new ArgumentException("numSegments must be at least 3: " + numSegments.ToString(), "numSegments");
            }

            MeshGeometry3D retVal = new MeshGeometry3D();

            #region Initial calculations

            Transform3DGroup transform = new Transform3DGroup();
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 90d)));

            if (rotateTransform != null)
            {
                // This is in case they want it oriented other than along the x axis
                transform.Children.Add(rotateTransform);
            }

            double halfHeight = height / 2d;

            Point[] points = Math2D.GetCircle_Cached(numSegments);

            double rotateAngleForPerp = Vector3D.AngleBetween(new Vector3D(1, 0, 0), new Vector3D(radius, 0, height));		// the 2nd vector is perpendicular to line formed from the edge of the cone to the tip

            #endregion

            #region Side

            for (int cntr = 0; cntr < numSegments; cntr++)
            {
                retVal.Positions.Add(transform.Transform(new Point3D(points[cntr].X * radius, points[cntr].Y * radius, -halfHeight)));
                retVal.Positions.Add(transform.Transform(new Point3D(0, 0, halfHeight)));		// creating a unique point for each face so that the lighting will be correct

                // The normal points straight out for a cylinder, but for a cone, it needs to be perpendicular to the slope of the cone
                Vector3D normal = new Vector3D(points[cntr].X, points[cntr].Y, 0d);
                Vector3D rotateAxis = new Vector3D(-points[cntr].Y, points[cntr].X, 0d);
                normal = normal.GetRotatedVector(rotateAxis, rotateAngleForPerp);

                retVal.Normals.Add(transform.Transform(normal));

                // This one just points straight up
                retVal.Normals.Add(transform.Transform(new Vector3D(0, 0, 1d)));
            }

            for (int cntr = 0; cntr < numSegments - 1; cntr++)
            {
                // 0,2,3
                retVal.TriangleIndices.Add((cntr * 2) + 0);
                retVal.TriangleIndices.Add((cntr * 2) + 2);
                retVal.TriangleIndices.Add((cntr * 2) + 3);

                // The cylinder has two triangles per face, but the cone only has one
                //// 0,3,1
                //retVal.TriangleIndices.Add((cntr * 2) + 0);
                //retVal.TriangleIndices.Add((cntr * 2) + 3);
                //retVal.TriangleIndices.Add((cntr * 2) + 1);
            }

            // Connecting the last 2 points to the first 2
            // last,0,1
            int offset = (numSegments - 1) * 2;
            retVal.TriangleIndices.Add(offset + 0);
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(1);

            //// last,1,last+1
            //retVal.TriangleIndices.Add(offset + 0);
            //retVal.TriangleIndices.Add(1);
            //retVal.TriangleIndices.Add(offset + 1);

            #endregion

            // Caps
            int pointOffset = retVal.Positions.Count;

            //NOTE: The normals are backward from what you'd think

            GetCylinder_AlongX_EndCap(ref pointOffset, retVal, points, new Vector3D(0, 0, 1), radius, radius, -halfHeight, transform);
            //GetCylinder_AlongXSprtEndCap(ref pointOffset, retVal, points, new Vector3D(0, 0, -1), radius, halfHeight, transform);

            //retVal.Freeze();
            return retVal;
        }

        /// <summary>
        /// This is a cylinder with a dome on each end.  The cylinder part's height is total height minus twice radius (the
        /// sum of the endcaps).  If there isn't enough height for all that, a sphere is returned instead (so height will never
        /// be less than diameter)
        /// </summary>
        /// <param name="numSegmentsPhi">This is the number of segments for one dome</param>
        public static MeshGeometry3D GetCapsule_AlongZ(int numSegmentsTheta, int numSegmentsPhi, double radius, double height, RotateTransform3D rotateTransform = null)
        {
            //NOTE: All the other geometries in this class are along the x axis, so I want to follow suit, but I think best along the z axis.  So I'll transform the points before commiting them to the geometry
            //TODO: This is so close to GetMultiRingedTube, the only difference is the multi ring tube has "hard" faces, and this has "soft" faces (this one shares points and normals, so the lighting is smoother)

            if (numSegmentsTheta < 3)
            {
                throw new ArgumentException("numSegmentsTheta must be at least 3: " + numSegmentsTheta.ToString(), "numSegmentsTheta");
            }

            if (height < radius * 2d)
            {
                //NOTE:  The separators aren't the same.  I believe the sphere method uses 2N+1 separators (or something like that)
                return GetSphere_LatLon(numSegmentsTheta, radius);
            }

            MeshGeometry3D retVal = new MeshGeometry3D();

            #region Initial calculations

            Transform3DGroup transform = new Transform3DGroup();
            //transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 90d)));

            if (rotateTransform != null)
            {
                // This is in case they want it oriented other than along the z axis
                transform.Children.Add(rotateTransform);
            }

            double halfHeight = (height - (radius * 2d)) / 2d;
            //double deltaTheta = 2d * Math.PI / numSegmentsTheta;
            //double theta = 0d;

            //Point[] points = new Point[numSegmentsTheta];		// these define a unit circle

            //for (int cntr = 0; cntr < numSegmentsTheta; cntr++)
            //{
            //    points[cntr] = new Point(Math.Cos(theta), Math.Sin(theta));
            //    theta += deltaTheta;
            //}

            Point[] points = Math2D.GetCircle_Cached(numSegmentsTheta);

            #endregion

            #region Side

            for (int cntr = 0; cntr < numSegmentsTheta; cntr++)
            {
                retVal.Positions.Add(transform.Transform(new Point3D(points[cntr].X * radius, points[cntr].Y * radius, -halfHeight)));
                retVal.Positions.Add(transform.Transform(new Point3D(points[cntr].X * radius, points[cntr].Y * radius, halfHeight)));

                retVal.Normals.Add(transform.Transform(new Vector3D(points[cntr].X, points[cntr].Y, 0d)));		// the normals point straight out of the side
                retVal.Normals.Add(transform.Transform(new Vector3D(points[cntr].X, points[cntr].Y, 0d)));
            }

            for (int cntr = 0; cntr < numSegmentsTheta - 1; cntr++)
            {
                // 0,2,3
                retVal.TriangleIndices.Add((cntr * 2) + 0);
                retVal.TriangleIndices.Add((cntr * 2) + 2);
                retVal.TriangleIndices.Add((cntr * 2) + 3);

                // 0,3,1
                retVal.TriangleIndices.Add((cntr * 2) + 0);
                retVal.TriangleIndices.Add((cntr * 2) + 3);
                retVal.TriangleIndices.Add((cntr * 2) + 1);
            }

            // Connecting the last 2 points to the first 2
            // last,0,1
            int offset = (numSegmentsTheta - 1) * 2;
            retVal.TriangleIndices.Add(offset + 0);
            retVal.TriangleIndices.Add(0);
            retVal.TriangleIndices.Add(1);

            // last,1,last+1
            retVal.TriangleIndices.Add(offset + 0);
            retVal.TriangleIndices.Add(1);
            retVal.TriangleIndices.Add(offset + 1);

            #endregion

            #region Caps

            //TODO: Get the dome to not recreate the same points that the cylinder part uses (not nessassary, just inefficient)

            int pointOffset = retVal.Positions.Count;

            Transform3DGroup domeTransform = new Transform3DGroup();
            domeTransform.Children.Add(new TranslateTransform3D(0, 0, halfHeight));
            domeTransform.Children.Add(transform);
            GetDome(ref pointOffset, retVal, points, domeTransform, numSegmentsPhi, radius, radius, radius);

            domeTransform = new Transform3DGroup();
            domeTransform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 180d)));
            domeTransform.Children.Add(new TranslateTransform3D(0, 0, -halfHeight));
            domeTransform.Children.Add(transform);
            GetDome(ref pointOffset, retVal, points, domeTransform, numSegmentsPhi, radius, radius, radius);

            #endregion

            //retVal.Freeze();
            return retVal;
        }

        /// <summary>
        /// Makes a torus in the xy plane
        /// </summary>
        /// <param name="spineSegments">The number of divisions around the major radius (outer radius)</param>
        /// <param name="fleshSegments">The number of divisions around the minor radius (inner radius)</param>
        /// <param name="innerRadius">This is the radius of the </param>
        /// <param name="outerRadius">This is the large radius</param>
        public static MeshGeometry3D GetTorus(int spineSegments, int fleshSegments, double innerRadius, double outerRadius)
        {
            MeshGeometry3D retVal = new MeshGeometry3D();

            // The spine is the circle around the hole in the
            // torus, the flesh is a set of circles around the
            // spine.
            int cp = 0; // Index of last added point.

            for (int i = 0; i < spineSegments; i++)
            {
                double spineParam = ((double)i) / ((double)spineSegments);
                double spineAngle = Math.PI * 2 * spineParam;
                Vector3D spineVector = new Vector3D(Math.Cos(spineAngle), Math.Sin(spineAngle), 0);

                for (int j = 0; j < fleshSegments; j++)
                {
                    double fleshParam = ((double)j) / ((double)fleshSegments);
                    double fleshAngle = Math.PI * 2 * fleshParam;
                    Vector3D fleshVector = spineVector * Math.Cos(fleshAngle) + new Vector3D(0, 0, Math.Sin(fleshAngle));
                    Point3D p = new Point3D(0, 0, 0) + outerRadius * spineVector + innerRadius * fleshVector;

                    retVal.Positions.Add(p);
                    retVal.Normals.Add(fleshVector);
                    retVal.TextureCoordinates.Add(new Point(spineParam, fleshParam));

                    // Now add a quad that has it's upper-right corner at the point we just added.
                    // i.e. cp . cp-1 . cp-1-_fleshSegments . cp-_fleshSegments
                    int a = cp;
                    int b = cp - 1;
                    int c = cp - (int)1 - fleshSegments;
                    int d = cp - fleshSegments;

                    // The next two if statements handle the wrapping around of the torus.  For either i = 0 or j = 0
                    // the created quad references vertices that haven't been created yet.
                    if (j == 0)
                    {
                        b += fleshSegments;
                        c += fleshSegments;
                    }

                    if (i == 0)
                    {
                        c += fleshSegments * spineSegments;
                        d += fleshSegments * spineSegments;
                    }

                    retVal.TriangleIndices.Add((ushort)a);
                    retVal.TriangleIndices.Add((ushort)b);
                    retVal.TriangleIndices.Add((ushort)c);

                    retVal.TriangleIndices.Add((ushort)a);
                    retVal.TriangleIndices.Add((ushort)c);
                    retVal.TriangleIndices.Add((ushort)d);
                    cp++;
                }
            }

            //retVal.Freeze();
            return retVal;
        }

        public static MeshGeometry3D GetTorusEllipse(int spineSegments, int fleshSegments, double innerRadius, double outerRadiusX, double outerRadiusY)
        {
            MeshGeometry3D retVal = new MeshGeometry3D();

            // The spine is the circle around the hole in the
            // torus, the flesh is a set of circles around the
            // spine.
            int cp = 0; // Index of last added point.

            for (int i = 0; i < spineSegments; i++)
            {
                double spineParam = ((double)i) / ((double)spineSegments);
                double spineAngle = Math.PI * 2 * spineParam;
                Vector3D spineVector = new Vector3D(Math.Cos(spineAngle), Math.Sin(spineAngle), 0);

                for (int j = 0; j < fleshSegments; j++)
                {
                    double fleshParam = ((double)j) / ((double)fleshSegments);
                    double fleshAngle = Math.PI * 2 * fleshParam;


                    //TODO: This is just a stretched copy of the circular torus function.  It's fine as long as the ellipse isn't
                    //too eccentric and the inner radius is small.  The proper way is for this to be perpendicular to the curve at
                    //the current point (instead of a stretched direction toward the center)
                    Vector3D fleshVector = spineVector * Math.Cos(fleshAngle) + new Vector3D(0, 0, Math.Sin(fleshAngle));


                    Point3D p = new Point3D(spineVector.X * outerRadiusX, spineVector.Y * outerRadiusY, 0) + fleshVector * innerRadius;

                    retVal.Positions.Add(p);
                    retVal.Normals.Add(fleshVector);
                    retVal.TextureCoordinates.Add(new Point(spineParam, fleshParam));

                    // Now add a quad that has it's upper-right corner at the point we just added.
                    // i.e. cp . cp-1 . cp-1-_fleshSegments . cp-_fleshSegments
                    int a = cp;
                    int b = cp - 1;
                    int c = cp - (int)1 - fleshSegments;
                    int d = cp - fleshSegments;

                    // The next two if statements handle the wrapping around of the torus.  For either i = 0 or j = 0
                    // the created quad references vertices that haven't been created yet.
                    if (j == 0)
                    {
                        b += fleshSegments;
                        c += fleshSegments;
                    }

                    if (i == 0)
                    {
                        c += fleshSegments * spineSegments;
                        d += fleshSegments * spineSegments;
                    }

                    retVal.TriangleIndices.Add((ushort)a);
                    retVal.TriangleIndices.Add((ushort)b);
                    retVal.TriangleIndices.Add((ushort)c);

                    retVal.TriangleIndices.Add((ushort)a);
                    retVal.TriangleIndices.Add((ushort)c);
                    retVal.TriangleIndices.Add((ushort)d);
                    cp++;
                }
            }

            //retVal.Freeze();
            return retVal;
        }

        /// <summary>
        /// Same as GetTorus, but with start/stop angles (in degrees)
        /// </summary>
        /// <param name="spineSegments_fullCircle">
        /// Actual spine segment count is calculated.  Doing it this way so the caller doesn't have to do
        /// any calculations and can get consistent density of spine segments, regardless of the angles
        /// passed in
        /// </param>
        public static MeshGeometry3D GetTorusArc(int spineSegments_fullCircle, int fleshSegments, double innerRadius, double outerRadius, double fromAngle, double toAngle)
        {
            if (toAngle < fromAngle)
                return GetTorusArc(spineSegments_fullCircle, fleshSegments, innerRadius, outerRadius, toAngle, fromAngle);

            if (toAngle - fromAngle >= 360)
                return GetTorus(spineSegments_fullCircle, fleshSegments, innerRadius, outerRadius);     // not sure why they would do this, but it's easy enough to handle

            MeshGeometry3D retVal = new MeshGeometry3D();

            // The spine is the circle around the hole in the torus, the flesh is a set of circles around the spine
            int cp = 0;     // Index of last added point

            double percent_circle = Math.Abs(toAngle - fromAngle) / 360d;
            int actual_spine_segments = Math.Max(1, (spineSegments_fullCircle * percent_circle).ToInt_Ceiling());

            Point first_point, last_point;

            for (int i = 0; i < actual_spine_segments; i++)
            {
                double spineParam = ((double)i) / ((double)actual_spine_segments);
                double spineAngle = UtilityMath.GetScaledValue(fromAngle, toAngle, 0, actual_spine_segments - 1, i);

                last_point = new Point(Math.Cos(Math1D.DegreesToRadians(spineAngle)), Math.Sin(Math1D.DegreesToRadians(spineAngle)));
                if (i == 0)
                    first_point = last_point;

                Vector3D spineVector = new Vector3D(last_point.X, last_point.Y, 0);

                for (int j = 0; j < fleshSegments; j++)
                {
                    double fleshParam = ((double)j) / ((double)fleshSegments);
                    double fleshAngle = Math.PI * 2 * fleshParam;
                    Vector3D fleshVector = spineVector * Math.Cos(fleshAngle) + new Vector3D(0, 0, Math.Sin(fleshAngle));
                    Point3D p = new Point3D(0, 0, 0) + outerRadius * spineVector + innerRadius * fleshVector;

                    retVal.Positions.Add(p);
                    retVal.Normals.Add(fleshVector);
                    retVal.TextureCoordinates.Add(new Point(spineParam, fleshParam));

                    // Now add a quad that has it's upper-right corner at the point we just added.
                    // i.e. cp . cp-1 . cp-1-_fleshSegments . cp-_fleshSegments
                    int a = cp;
                    int b = cp - 1;
                    int c = cp - 1 - fleshSegments;
                    int d = cp - fleshSegments;

                    // Wrap around, point to indices that have yet to be created
                    if (j == 0)
                    {
                        b += fleshSegments;
                        c += fleshSegments;
                    }

                    if (i != 0)     // zero would wrap back, which is useful for a full torus, but invalid for an arc
                    {
                        retVal.TriangleIndices.Add((ushort)a);
                        retVal.TriangleIndices.Add((ushort)b);
                        retVal.TriangleIndices.Add((ushort)c);

                        retVal.TriangleIndices.Add((ushort)a);
                        retVal.TriangleIndices.Add((ushort)c);
                        retVal.TriangleIndices.Add((ushort)d);
                    }

                    cp++;
                }
            }

            //TODO: Have an option for dome endcaps

            int point_count = retVal.Positions.Count;       // saving this, because the end caps add points

            GetTorusArc_FlatCap(retVal, 0, fleshSegments - 1, new Vector3D(first_point.Y, -first_point.X, 0));
            GetTorusArc_FlatCap(retVal, point_count - fleshSegments, point_count - 1, new Vector3D(-last_point.Y, last_point.X, 0));

            //retVal.Freeze();
            return retVal;
        }
        private static void GetTorusArc_FlatCap(MeshGeometry3D retVal, int index_from, int index_to, Vector3D normal)
        {
            // Copied from GetCircle2D

            for (int i = index_from; i <= index_to; i++)
            {
                retVal.Positions.Add(retVal.Positions[i].ToVector().ToPoint());
                retVal.Normals.Add(normal);
            }

            int new_from = retVal.Positions.Count - 1 - (index_to - index_from);        // positions and normals should be the same size
            //int new_to = retVal.Positions.Count - 1;

            // Start with 0,1,2
            retVal.TriangleIndices.Add(new_from + 0);
            retVal.TriangleIndices.Add(new_from + 1);
            retVal.TriangleIndices.Add(new_from + 2);

            int lowerIndex = 2;
            int upperIndex = index_to - index_from;
            int lastUsedIndex = 0;
            bool shouldBumpLower = true;

            // Do the rest of the triangles
            while (lowerIndex < upperIndex)
            {
                retVal.TriangleIndices.Add(new_from + lowerIndex);
                retVal.TriangleIndices.Add(new_from + upperIndex);
                retVal.TriangleIndices.Add(new_from + lastUsedIndex);

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
        }

        public static MeshGeometry3D GetRing(int numSides, double innerRadius, double outerRadius, double height, Transform3D transform = null, bool includeInnerRingFaces = true, bool includeOuterRingFaces = true)
        {
            MeshGeometry3D retVal = new MeshGeometry3D();

            if (transform == null)
            {
                transform = Transform3D.Identity;
            }

            Point[] points = Math2D.GetCircle_Cached(numSides);
            double halfHeight = height * .5d;

            int pointOffset = 0;
            int zOffsetBottom, zOffsetTop;

            #region Outer Ring

            #region Positions/Normals

            for (int cntr = 0; cntr < numSides; cntr++)
            {
                retVal.Positions.Add(transform.Transform(new Point3D(points[cntr].X * outerRadius, points[cntr].Y * outerRadius, -halfHeight)));
                retVal.Normals.Add(transform.Transform(new Vector3D(points[cntr].X * outerRadius, points[cntr].Y * outerRadius, 0d).ToUnit()));		// the normals point straight out of the side
            }

            for (int cntr = 0; cntr < numSides; cntr++)
            {
                retVal.Positions.Add(transform.Transform(new Point3D(points[cntr].X * outerRadius, points[cntr].Y * outerRadius, halfHeight)));
                retVal.Normals.Add(transform.Transform(new Vector3D(points[cntr].X * outerRadius, points[cntr].Y * outerRadius, 0d).ToUnit()));		// the normals point straight out of the side
            }

            #endregion

            if (includeOuterRingFaces)
            {
                #region Triangles

                zOffsetBottom = pointOffset;
                zOffsetTop = zOffsetBottom + numSides;

                for (int cntr = 0; cntr < numSides - 1; cntr++)
                {
                    // Top/Left triangle
                    retVal.TriangleIndices.Add(zOffsetBottom + cntr + 0);
                    retVal.TriangleIndices.Add(zOffsetTop + cntr + 1);
                    retVal.TriangleIndices.Add(zOffsetTop + cntr + 0);

                    // Bottom/Right triangle
                    retVal.TriangleIndices.Add(zOffsetBottom + cntr + 0);
                    retVal.TriangleIndices.Add(zOffsetBottom + cntr + 1);
                    retVal.TriangleIndices.Add(zOffsetTop + cntr + 1);
                }

                // Connecting the last 2 points to the first 2
                // Top/Left triangle
                retVal.TriangleIndices.Add(zOffsetBottom + (numSides - 1) + 0);
                retVal.TriangleIndices.Add(zOffsetTop);		// wrapping back around
                retVal.TriangleIndices.Add(zOffsetTop + (numSides - 1) + 0);

                // Bottom/Right triangle
                retVal.TriangleIndices.Add(zOffsetBottom + (numSides - 1) + 0);
                retVal.TriangleIndices.Add(zOffsetBottom);
                retVal.TriangleIndices.Add(zOffsetTop);

                #endregion
            }

            pointOffset = retVal.Positions.Count;

            #endregion

            #region Inner Ring

            #region Positions/Normals

            for (int cntr = 0; cntr < numSides; cntr++)
            {
                retVal.Positions.Add(transform.Transform(new Point3D(points[cntr].X * innerRadius, points[cntr].Y * innerRadius, -halfHeight)));
                retVal.Normals.Add(transform.Transform(new Vector3D(points[cntr].X * innerRadius, points[cntr].Y * innerRadius, 0d).ToUnit() * -1d));		// the normals point straight in from the side
            }

            for (int cntr = 0; cntr < numSides; cntr++)
            {
                retVal.Positions.Add(transform.Transform(new Point3D(points[cntr].X * innerRadius, points[cntr].Y * innerRadius, halfHeight)));
                retVal.Normals.Add(transform.Transform(new Vector3D(points[cntr].X * innerRadius, points[cntr].Y * innerRadius, 0d).ToUnit() * -1d));		// the normals point straight in from the side
            }

            #endregion

            if (includeInnerRingFaces)
            {
                #region Triangles

                zOffsetBottom = pointOffset;
                zOffsetTop = zOffsetBottom + numSides;

                for (int cntr = 0; cntr < numSides - 1; cntr++)
                {
                    // Top/Left triangle
                    retVal.TriangleIndices.Add(zOffsetBottom + cntr + 0);
                    retVal.TriangleIndices.Add(zOffsetTop + cntr + 0);
                    retVal.TriangleIndices.Add(zOffsetTop + cntr + 1);

                    // Bottom/Right triangle
                    retVal.TriangleIndices.Add(zOffsetBottom + cntr + 0);
                    retVal.TriangleIndices.Add(zOffsetTop + cntr + 1);
                    retVal.TriangleIndices.Add(zOffsetBottom + cntr + 1);
                }

                // Connecting the last 2 points to the first 2
                // Top/Left triangle
                retVal.TriangleIndices.Add(zOffsetBottom + (numSides - 1) + 0);
                retVal.TriangleIndices.Add(zOffsetTop + (numSides - 1) + 0);
                retVal.TriangleIndices.Add(zOffsetTop);		// wrapping back around

                // Bottom/Right triangle
                retVal.TriangleIndices.Add(zOffsetBottom + (numSides - 1) + 0);
                retVal.TriangleIndices.Add(zOffsetTop);
                retVal.TriangleIndices.Add(zOffsetBottom);

                #endregion
            }

            pointOffset = retVal.Positions.Count;

            #endregion

            #region Top Cap

            Transform3DGroup capTransform = new Transform3DGroup();
            capTransform.Children.Add(new TranslateTransform3D(0, 0, halfHeight));
            capTransform.Children.Add(transform);

            GetRing_Cap(ref pointOffset, retVal, capTransform, points, numSides, innerRadius, outerRadius);

            #endregion

            #region Bottom Cap

            capTransform = new Transform3DGroup();
            capTransform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 180d)));
            capTransform.Children.Add(new TranslateTransform3D(0, 0, -halfHeight));
            capTransform.Children.Add(transform);

            GetRing_Cap(ref pointOffset, retVal, capTransform, points, numSides, innerRadius, outerRadius);

            #endregion

            return retVal;
        }
        private static void GetRing_Cap(ref int pointOffset, MeshGeometry3D geometry, Transform3D transform, Point[] points, int numSides, double innerRadius, double outerRadius)
        {
            // Points/Normals
            for (int cntr = 0; cntr < numSides; cntr++)
            {
                geometry.Positions.Add(transform.Transform(new Point3D(points[cntr].X * outerRadius, points[cntr].Y * outerRadius, 0d)));
                geometry.Normals.Add(transform.Transform(new Vector3D(0, 0, 1)).ToUnit());
            }

            for (int cntr = 0; cntr < numSides; cntr++)
            {
                geometry.Positions.Add(transform.Transform(new Point3D(points[cntr].X * innerRadius, points[cntr].Y * innerRadius, 0d)));
                geometry.Normals.Add(transform.Transform(new Vector3D(0, 0, 1)).ToUnit());
            }

            int zOffsetOuter = pointOffset;
            int zOffsetInner = zOffsetOuter + numSides;

            // Triangles
            for (int cntr = 0; cntr < numSides - 1; cntr++)
            {
                // Bottom Right triangle
                geometry.TriangleIndices.Add(zOffsetOuter + cntr + 0);
                geometry.TriangleIndices.Add(zOffsetOuter + cntr + 1);
                geometry.TriangleIndices.Add(zOffsetInner + cntr + 1);

                // Top Left triangle
                geometry.TriangleIndices.Add(zOffsetOuter + cntr + 0);
                geometry.TriangleIndices.Add(zOffsetInner + cntr + 1);
                geometry.TriangleIndices.Add(zOffsetInner + cntr + 0);
            }

            // Connecting the last 2 points to the first 2
            // Bottom/Right triangle
            geometry.TriangleIndices.Add(zOffsetOuter + (numSides - 1) + 0);
            geometry.TriangleIndices.Add(zOffsetOuter);
            geometry.TriangleIndices.Add(zOffsetInner);

            // Top/Left triangle
            geometry.TriangleIndices.Add(zOffsetOuter + (numSides - 1) + 0);
            geometry.TriangleIndices.Add(zOffsetInner);		// wrapping back around
            geometry.TriangleIndices.Add(zOffsetInner + (numSides - 1) + 0);

            pointOffset = geometry.Positions.Count;
        }

        public static MeshGeometry3D GetCircle2D(int numSides, Transform3D transform, Transform3D normalTransform)
        {
            //NOTE: This also sets the texture coordinates

            int pointOffset = 0;
            Point[] pointsTheta = Math2D.GetCircle_Cached(numSides);

            MeshGeometry3D retVal = new MeshGeometry3D();

            #region Positions/Normals

            for (int thetaCntr = 0; thetaCntr < pointsTheta.Length; thetaCntr++)
            {
                Point3D point = new Point3D(pointsTheta[thetaCntr].X, pointsTheta[thetaCntr].Y, 0d);
                retVal.Positions.Add(transform.Transform(point));

                Point texturePoint = new Point(.5d + (pointsTheta[thetaCntr].X * .5d), .5d + (pointsTheta[thetaCntr].Y * .5d));
                retVal.TextureCoordinates.Add(texturePoint);

                Vector3D normal = new Vector3D(0, 0, 1);
                retVal.Normals.Add(normalTransform.Transform(normal));
            }

            #endregion

            #region Add the triangles

            // Start with 0,1,2
            retVal.TriangleIndices.Add(pointOffset + 0);
            retVal.TriangleIndices.Add(pointOffset + 1);
            retVal.TriangleIndices.Add(pointOffset + 2);

            int lowerIndex = 2;
            int upperIndex = pointsTheta.Length - 1;
            int lastUsedIndex = 0;
            bool shouldBumpLower = true;

            // Do the rest of the triangles
            while (lowerIndex < upperIndex)
            {
                retVal.TriangleIndices.Add(pointOffset + lowerIndex);
                retVal.TriangleIndices.Add(pointOffset + upperIndex);
                retVal.TriangleIndices.Add(pointOffset + lastUsedIndex);

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

            #endregion

            return retVal;
        }

        /// <summary>
        /// This can be used to visualize a plane.  Drawing a single square is difficult to visualize the perspective, so this draws
        /// a set of tiles, and a border.  It's always square to also help visualize perspective.  Also, wpf doesn't deal with semi
        /// transparency very well, so that's why this model is mostly gaps
        /// NOTE: Most of the methods return meshes, this returns an entire model
        /// </summary>
        /// <param name="center">
        /// This gives a chance to be explicit about the center of the drawn plane.  There is no check to be sure center lies on the plane,
        /// but it should.  (this was added because Math3D.GetPlane() doesn't build a triangle centered on the point passed in)
        /// If this is left null, then the center of the triangle will be used
        /// </param>
        public static Model3D GetPlane(ITriangle_wpf plane, double size, Color color, Color? reflectiveColor = null, int numCells = 12, Point3D? center = null)
        {
            double halfSize = size / 2;

            double cellSize = size / numCells;

            double tileSizeHalf = cellSize / (4d * 2d);

            Model3DGroup retVal = new Model3DGroup();

            #region Border

            var segments = new[]
                {
                    Tuple.Create(new Point3D(-halfSize, -halfSize, 0), new Point3D(halfSize, -halfSize, 0)),
                    Tuple.Create(new Point3D(halfSize, -halfSize, 0), new Point3D(halfSize, halfSize, 0)),
                    Tuple.Create(new Point3D(halfSize, halfSize, 0), new Point3D(-halfSize, halfSize, 0)),
                    Tuple.Create(new Point3D(-halfSize, halfSize, 0), new Point3D(-halfSize, -halfSize, 0)),
                };

            Color lineColor = Color.FromArgb(96, color.R, color.G, color.B);
            //double lineThickness = .015;
            double lineThickness = size / 666.666666667d;

            foreach (var segment in segments)
            {
                retVal.Children.Add(new Controls3D.BillboardLine3D() { Color = lineColor, IsReflectiveColor = false, Thickness = lineThickness, FromPoint = segment.Item1, ToPoint = segment.Item2 }.Model);
            }

            #endregion

            #region Tiles

            // Material
            MaterialGroup materials = new MaterialGroup();
            materials.Children.Add(new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(48, color.R, color.G, color.B))));
            if (reflectiveColor != null)
            {
                materials.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(96, reflectiveColor.Value.R, reflectiveColor.Value.G, reflectiveColor.Value.B)), 5d));
            }

            // Tiles
            for (int xCntr = 0; xCntr <= numCells; xCntr++)
            {
                double x = -halfSize + (xCntr * cellSize);
                double left = xCntr == 0 ? 0 : -tileSizeHalf;
                double right = xCntr == numCells ? 0 : tileSizeHalf;

                for (int yCntr = 0; yCntr <= numCells; yCntr++)
                {
                    double y = -halfSize + (yCntr * cellSize);
                    double up = yCntr == 0 ? 0 : -tileSizeHalf;
                    double down = yCntr == numCells ? 0 : tileSizeHalf;

                    // Geometry Model
                    GeometryModel3D geometry = new GeometryModel3D();
                    geometry.Material = materials;
                    geometry.BackMaterial = materials;
                    geometry.Geometry = UtilityWPF.GetSquare2D(new Point(x + left, y + up), new Point(x + right, y + down));

                    retVal.Children.Add(geometry);
                }
            }

            #endregion

            Transform3DGroup transform = new Transform3DGroup();
            transform.Children.Add(new RotateTransform3D(new QuaternionRotation3D(Math3D.GetRotation(new Vector3D(0, 0, 1), plane.Normal))));
            if (center == null)
            {
                transform.Children.Add(new TranslateTransform3D(plane.GetCenterPoint().ToVector()));
            }
            else
            {
                transform.Children.Add(new TranslateTransform3D(center.Value.ToVector()));
            }
            retVal.Transform = transform;

            return retVal;
        }

        /// <summary>
        /// This turns the font and text into a 3D model
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://msdn.microsoft.com/en-us/magazine/cc163349.aspx
        /// 
        /// TODO: Instead of making a flat plate, take in a mesh to snap to (like a cylinder, or wavy terrain)
        /// 
        /// TODO: Don't use a drawing brush for the faces.  Instead, convert into triangles.  Math2D.GetTrianglesFromConcavePoly can't handle holes, but the polygons could be sliced into multiple to get rid of the holes
        /// </remarks>
        public static Model3D GetText3D(string text, FontFamily font, Material faceMaterial, Material edgeMaterial, double height, double depth = 0, FontStyle? style = null, FontWeight? weight = null, FontStretch? stretch = null, TextAlignment alignment = TextAlignment.Center)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text3D doesn't support pure whitespace (there must be text)");

            style = style ?? FontStyles.Normal;
            weight = weight ?? FontWeights.Normal;
            stretch = stretch ?? FontStretches.Normal;

            FormattedText formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(font, style.Value, weight.Value, stretch.Value), height, Brushes.Transparent);
            formattedText.TextAlignment = alignment;
            Geometry textGeometry = formattedText.BuildGeometry(new Point(0, 0));

            Model3DGroup retVal = new Model3DGroup();

            GeometryModel3D geometry;

            #region Edges

            // Turn the text geometry into triangles
            List<Point3D> vertices;
            List<Vector3D> normals;
            List<int> indices;
            List<Point> textures;
            TriangulateText(out vertices, out normals, out indices, out textures, textGeometry, depth);

            Vector3D offset = Math3D.GetCenter(vertices).ToVector();

            if (!Math1D.IsNearZero(depth))      // only make an edge if there is depth
            {
                // Convert to triangles (centered at origin)
                Point3D[] allPoints = vertices.Select(o => o - offset).ToArray();
                List<TriangleIndexed_wpf> triangles = new List<TriangleIndexed_wpf>();

                for (int cntr = 0; cntr < indices.Count; cntr += 3)
                {
                    triangles.Add(new TriangleIndexed_wpf(indices[cntr], indices[cntr + 1], indices[cntr + 2], allPoints));
                }

                // Geometry
                geometry = new GeometryModel3D();
                geometry.Material = edgeMaterial;
                geometry.BackMaterial = edgeMaterial;
                geometry.Geometry = UtilityWPF.GetMeshFromTriangles(triangles);

                retVal.Children.Add(geometry);
            }

            #endregion

            #region Front/Back

            Material textMaterial = ConvertToTextMaterial(faceMaterial, textGeometry);

            double[] zs;
            if (Math1D.IsNearZero(depth))
            {
                zs = new double[] { 0 };        // no depth, so just create one
            }
            else
            {
                double halfDepth = depth / 2d;
                zs = new[] { -halfDepth, halfDepth };
            }

            Rect bounds = textGeometry.Bounds;

            foreach (double z in zs)
            {
                MeshGeometry3D mesh = new MeshGeometry3D();

                mesh.Positions.Add(new Point3D(bounds.Left - offset.X, -bounds.Top - offset.Y, z));     //NOTE: Y is reversed (same as the edges)
                mesh.Positions.Add(new Point3D(bounds.Right - offset.X, -bounds.Top - offset.Y, z));
                mesh.Positions.Add(new Point3D(bounds.Right - offset.X, -bounds.Bottom - offset.Y, z));
                mesh.Positions.Add(new Point3D(bounds.Left - offset.X, -bounds.Bottom - offset.Y, z));

                mesh.TextureCoordinates.Add(new Point(0, 0));       // TextureCoordinates are mandatory because the material is a drawing brush (not needed when it's just a solid color brush)
                mesh.TextureCoordinates.Add(new Point(1, 0));
                mesh.TextureCoordinates.Add(new Point(1, 1));
                mesh.TextureCoordinates.Add(new Point(0, 1));

                mesh.TriangleIndices.Add(0);
                mesh.TriangleIndices.Add(1);
                mesh.TriangleIndices.Add(2);

                mesh.TriangleIndices.Add(0);
                mesh.TriangleIndices.Add(2);
                mesh.TriangleIndices.Add(3);

                geometry = new GeometryModel3D();
                geometry.Material = textMaterial;
                geometry.BackMaterial = textMaterial;
                geometry.Geometry = mesh;

                retVal.Children.Add(geometry);
            }

            #endregion

            return retVal;
        }

        /// <summary>
        /// This can be used to create all kinds of shapes.  Each ring defines either an end cap or the tube's side
        /// </summary>
        /// <remarks>
        /// If you pass in 2 rings, equal radius, you have a cylinder
        /// If one of them goes to a point, it will be a cone
        /// etc
        /// 
        /// softSides:
        /// If you are making something discreet, like dice, or a jewel, you will want each face to reflect like mirrors (not soft).  But if you
        /// are making a cylinder, etc, you don't want the faces to stand out.  Instead, they should blend together, so you will want soft
        /// </remarks>
        /// <param name="softSides">
        /// True: The normal for each vertex point out from the vertex, so the faces appear to blend together.
        /// False: The normal for each vertex of a triangle is that triangle's normal, so each triangle reflects like cut glass.
        /// </param>
        /// <param name="shouldCenterZ">
        /// True: Centers the object along Z (so even though you start the rings at Z of zero, that first ring will be at negative half height).
        /// False: Goes from 0 to height
        /// </param>
        public static MeshGeometry3D GetMultiRingedTube_ORIG(int numSides, List<TubeRingDefinition_ORIG> rings, bool softSides, bool shouldCenterZ)
        {
            #region Validate

            if (rings.Count == 1)
            {
                if (rings[0].RingType == TubeRingType_ORIG.Point)
                {
                    throw new ArgumentException("Only a single ring was passed in, and it's a point");
                }
            }

            #endregion

            MeshGeometry3D retVal = new MeshGeometry3D();

            #region Calculate total height

            double height = 0d;
            for (int cntr = 1; cntr < rings.Count; cntr++)     // the first ring's distance from prev ring is ignored
            {
                if (rings[cntr].DistFromPrevRing <= 0)
                {
                    throw new ArgumentException("DistFromPrevRing must be positive: " + rings[cntr].DistFromPrevRing.ToString());
                }

                height += rings[cntr].DistFromPrevRing;
            }

            #endregion

            double curZ = 0;
            if (shouldCenterZ)
            {
                curZ = height * -.5d;      // starting in the negative
            }

            // Get the points (unit circle)
            Point[] points = GetMultiRingedTubeSprtPoints_ORIG(numSides);

            int pointOffset = 0;

            if (rings[0].RingType == TubeRingType_ORIG.Ring_Closed)
            {
                GetMultiRingedTubeSprtEndCap_ORIG(ref pointOffset, retVal, numSides, points, rings[0], true, curZ);
            }

            for (int cntr = 0; cntr < rings.Count - 1; cntr++)
            {
                if ((cntr > 0 && cntr < rings.Count - 1) &&
                    (rings[cntr].RingType == TubeRingType_ORIG.Dome || rings[cntr].RingType == TubeRingType_ORIG.Point))
                {
                    throw new ArgumentException("Rings aren't allowed to be points in the middle of the tube");
                }

                GetMultiRingedTubeSprtBetweenRings_ORIG(ref pointOffset, retVal, numSides, points, rings[cntr], rings[cntr + 1], curZ);

                curZ += rings[cntr + 1].DistFromPrevRing;
            }

            if (rings.Count > 1 && rings[rings.Count - 1].RingType == TubeRingType_ORIG.Ring_Closed)
            {
                GetMultiRingedTubeSprtEndCap_ORIG(ref pointOffset, retVal, numSides, points, rings[rings.Count - 1], false, curZ);
            }

            //retVal.Freeze();
            return retVal;
        }

        //NOTE: The original had x,y as diameter.  This one has them as radius
        public static MeshGeometry3D GetMultiRingedTube(int numSides, List<TubeRingBase> rings, bool softSides, bool shouldCenterZ, Transform3D transform = null)
        {
            return BuildTube.Build(numSides, rings, softSides, shouldCenterZ, transform);
        }

        public static MeshGeometry3D GetMeshFromTriangles_IndependentFaces(IEnumerable<ITriangle_wpf> triangles)
        {
            MeshGeometry3D retVal = new MeshGeometry3D();

            int index = 0;

            foreach (ITriangle_wpf triangle in triangles)
            {
                retVal.Positions.Add(triangle.Point0);
                retVal.Positions.Add(triangle.Point1);
                retVal.Positions.Add(triangle.Point2);

                retVal.Normals.Add(triangle.Normal);
                retVal.Normals.Add(triangle.Normal);
                retVal.Normals.Add(triangle.Normal);

                retVal.TriangleIndices.Add(index * 3);
                retVal.TriangleIndices.Add((index * 3) + 1);
                retVal.TriangleIndices.Add((index * 3) + 2);

                index++;
            }

            //retVal.Freeze();
            return retVal;
        }
        public static MeshGeometry3D GetMeshFromTriangles(IEnumerable<ITriangleIndexed_wpf> triangles)
        {
            MeshGeometry3D retVal = new MeshGeometry3D();

            bool addedPoints = false;

            foreach (ITriangleIndexed_wpf triangle in triangles)
            {
                if (!addedPoints)
                {
                    addedPoints = true;

                    // All the triangles in the list of TriangleIndexed should share the same points, so just use the first triangle's list of points
                    foreach (Point3D point in triangle.AllPoints)
                    {
                        retVal.Positions.Add(point);
                    }
                }

                retVal.TriangleIndices.Add(triangle.Index0);
                retVal.TriangleIndices.Add(triangle.Index1);
                retVal.TriangleIndices.Add(triangle.Index2);
            }

            //retVal.Freeze();
            return retVal;
        }

        public static TriangleIndexed_wpf[] GetTrianglesFromMesh(MeshGeometry3D mesh, Transform3D transform = null, bool dedupePoints = true)
        {
            if (mesh == null)
            {
                return null;
            }

            return GetTrianglesFromMesh(new MeshGeometry3D[] { mesh }, new Transform3D[] { transform }, dedupePoints);
        }
        /// <summary>
        /// This will merge several meshes into a single set of triangles
        /// NOTE: The mesh array and transform array need to be the same size (each mesh could have its own transform)
        /// </summary>
        public static TriangleIndexed_wpf[] GetTrianglesFromMesh(MeshGeometry3D[] meshes, Transform3D[] transforms = null, bool dedupePoints = true)
        {
            if (dedupePoints)
            {
                return GetTrianglesFromMesh_Deduped(meshes, transforms);
            }
            else
            {
                return GetTrianglesFromMesh_Raw(meshes, transforms);
            }
        }

        /// <summary>
        /// This overload takes a model, which should contain a mesh
        /// </summary>
        public static Point3D[] GetPointsFromMesh(Model3D model, Transform3D transform = null)
        {
            List<Point3D> retVal = new List<Point3D>();

            if (model is Model3DGroup modelGroup)
            {
                foreach (var child in modelGroup.Children)
                {
                    // Recurse
                    retVal.AddRange(GetPointsFromMesh(child, transform));
                }
            }
            else if (model is GeometryModel3D modelGeometry)
            {
                Geometry3D geometry = modelGeometry.Geometry;
                if (geometry is MeshGeometry3D geometryMesh)
                {
                    retVal.AddRange(GetPointsFromMesh(geometryMesh, null));     //NOTE: Only applying the transform passed in once
                }
                else
                {
                    throw new ArgumentException("Unexpected type of geometry: " + geometry.GetType().ToString());
                }
            }
            else if (model is Light) { }     // ignore lights
            else
            {
                throw new ArgumentException("Unexpected type of model: " + model.GetType().ToString());
            }

            // Apply transforms
            IEnumerable<Point3D> transformed = retVal;

            if (model.Transform != null && model.Transform != Transform3D.Identity)
            {
                transformed = transformed.Select(o => model.Transform.Transform(o));
            }

            if (transform != null)
            {
                transformed = transformed.Select(o => model.Transform.Transform(o));
            }

            return transformed.ToArray();
        }
        /// <summary>
        /// This overload takes an actual mesh
        /// </summary>
        public static Point3D[] GetPointsFromMesh(MeshGeometry3D mesh, Transform3D transform = null)
        {
            if (mesh == null)
            {
                return null;
            }

            Point3D[] points = null;
            if (mesh.TriangleIndices != null && mesh.TriangleIndices.Count > 0)
            {
                // Referenced points
                points = mesh.TriangleIndices.Select(o => mesh.Positions[o]).ToArray();
            }
            else
            {
                // Directly used points
                points = mesh.Positions.ToArray();
            }

            if (transform != null)
            {
                transform.Transform(points);
            }

            return points;
        }

        /// <summary>
        /// This returns a material that is the same color regardless of lighting
        /// </summary>
        public static Material GetUnlitMaterial(Color color)
        {
            var retVal = GetUnlitMaterial_Components(color);

            retVal.final.Freeze();

            return retVal.final;
        }
        /// <summary>
        /// Use this overload if you intend to change the color later
        /// </summary>
        public static (Material final, DiffuseMaterial diffuse, EmissiveMaterial emissive) GetUnlitMaterial_Components(Color color)
        {
            var diffuseMat = new DiffuseMaterial();
            var emissiveMat = new EmissiveMaterial();

            UpdateUnlitColor(color, diffuseMat, emissiveMat);

            MaterialGroup final = new MaterialGroup();
            final.Children.Add(diffuseMat);
            final.Children.Add(emissiveMat);

            return (final, diffuseMat, emissiveMat);
        }
        /// <summary>
        /// This changes the color of an unlit material that was built using GetUnlitMaterial_Components
        /// </summary>
        public static void UpdateUnlitColor(Color color, DiffuseMaterial diffuse, EmissiveMaterial emissive)
        {
            Color diffuseColor = Colors.Black;
            diffuseColor.ScA = color.ScA;

            diffuse.Brush = new SolidColorBrush(diffuseColor);
            emissive.Brush = new SolidColorBrush(color);
        }

        #endregion

        #region misc

        /// <summary>
        /// Aparently, there are some known bugs with Mouse.GetPosition() - especially with dragdrop.  This method
        /// should always work
        /// </summary>
        /// <remarks>
        /// Got this here:
        /// http://www.switchonthecode.com/tutorials/wpf-snippet-reliably-getting-the-mouse-position
        /// </remarks>
        public static Point GetPositionCorrect(Visual relativeTo)
        {
            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);

            return relativeTo.PointFromScreen(new Point(w32Mouse.X, w32Mouse.Y));
        }

        /// <summary>
        /// This will move the mouse cursor to a new location
        /// </summary>
        public static void SetMousePosition(Visual relativeTo, Point? offset = null)
        {
            Point point = offset ?? new Point(0, 0);

            Point screenPos = relativeTo.PointToScreen(point);

            SetMousePosition(screenPos);
        }
        public static void SetMousePosition(Point position)
        {
            SetCursorPos(Convert.ToInt32(Math.Round(position.X)), Convert.ToInt32(Math.Round(position.Y)));
        }

        //TODO: May want to reword the name canvas to view rectangle - also, make an overload that takes Rects
        public static Transform GetMapToCanvasTransform(Point worldMin, Point worldMax, Point canvasMin, Point canvasMax)
        {
            Point worldCenter = new Point((worldMin.X + worldMax.X) / 2, (worldMin.Y + worldMax.Y) / 2);
            Point canvasCenter = new Point((canvasMax.X - canvasMin.X) / 2, (canvasMax.Y - canvasMin.Y) / 2);

            // Figure out zoom
            double zoomX = (canvasMax.X - canvasMin.X) / (worldMax.X - worldMin.X);
            double zoomY = (canvasMax.Y - canvasMin.Y) / (worldMax.Y - worldMin.Y);

            double zoom = Math.Min(zoomX, zoomY);

            TransformGroup retVal = new TransformGroup();
            retVal.Children.Add(new TranslateTransform((canvasCenter.X / zoom) - worldCenter.X, (canvasCenter.Y / zoom) - worldCenter.Y));
            retVal.Children.Add(new ScaleTransform(zoom, zoom));
            //retVal.Children.Add(new TranslateTransform(canvasCenter.X, canvasCenter.Y));

            return retVal;
        }

        /// <summary>
        /// This will cast a ray from the point (on _viewport) along the direction that the camera is looking, and returns hits
        /// against wpf visuals and the drag shape (sorted by distance from camera)
        /// </summary>
        /// <remarks>
        /// This only looks at the click ray, and won't do anything with offsets.  This method is useful for seeing where they
        /// clicked, then take further action from there
        /// </remarks>
        /// <param name="backgroundVisual">This is the UIElement that contains the viewport (and has the background set to non null)</param>
        public static List<MyHitTestResult> CastRay(out RayHitTestParameters clickRay, Point clickPoint, Visual backgroundVisual, PerspectiveCamera camera, Viewport3D viewport, bool returnAllHits, IEnumerable<Visual3D> ignoreVisuals = null, IEnumerable<Visual3D> onlyVisuals = null)
        {
            List<MyHitTestResult> retVal = new List<MyHitTestResult>();

            // This gets called every time there is a hit
            HitTestResultCallback resultCallback = delegate (HitTestResult result)
            {
                if (result is RayMeshGeometry3DHitTestResult)		// It could also be a RayHitTestResult, which isn't as exact as RayMeshGeometry3DHitTestResult
                {
                    RayMeshGeometry3DHitTestResult resultCast = (RayMeshGeometry3DHitTestResult)result;

                    bool shouldKeep = true;
                    if (ignoreVisuals != null && ignoreVisuals.Any(o => o == resultCast.VisualHit))
                    {
                        shouldKeep = false;
                    }

                    if (onlyVisuals != null && !onlyVisuals.Any(o => o == resultCast.VisualHit))
                    {
                        shouldKeep = false;
                    }

                    if (shouldKeep)
                    {
                        retVal.Add(new MyHitTestResult(resultCast));

                        if (!returnAllHits)
                        {
                            return HitTestResultBehavior.Stop;
                        }
                    }
                }

                return HitTestResultBehavior.Continue;
            };

            // Get hits against existing models
            VisualTreeHelper.HitTest(backgroundVisual, null, resultCallback, new PointHitTestParameters(clickPoint));

            // Also return the click ray
            clickRay = UtilityWPF.RayFromViewportPoint(camera, viewport, clickPoint);

            // Sort by distance
            if (retVal.Count > 1)
            {
                Point3D clickRayOrigin = clickRay.Origin;		// the compiler complains about anonymous methods using out params
                retVal = retVal.
                    OrderBy(o => o.GetDistanceFromPoint(clickRayOrigin)).
                    ToList();
            }

            return retVal;
        }

        /// <summary>
        /// Converts the 2D point into 3D world point and ray
        /// </summary>
        /// <remarks>
        /// This method uses reflection to get at an internal method off of camera (I think it's rigged up for a perspective camera)
        /// http://grokys.blogspot.com/2010/08/wpf-3d-translating-2d-point-into-3d.html
        /// </remarks>
        public static RayHitTestParameters RayFromViewportPoint(Camera camera, Viewport3D viewport, Point point)
        {
            Size viewportSize = new Size(viewport.ActualWidth, viewport.ActualHeight);

            System.Reflection.MethodInfo method = typeof(Camera).GetMethod("RayFromViewportPoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            double distanceAdjustment = 0;
            object[] parameters = new object[] { point, viewportSize, null, distanceAdjustment };

            return (RayHitTestParameters)method.Invoke(camera, parameters);
        }

        /// <summary>
        /// This projects a single point from 3D to 2D
        /// </summary>
        public static Point? Project3Dto2D(out bool isInFront, Viewport3D viewport, Point3D point)
        {
            // The viewport should always have something in it, even if it's just lights
            if (viewport.Children.Count == 0)
            {
                isInFront = false;
                return null;
            }

            Viewport3DVisual visual = VisualTreeHelper.GetParent(viewport.Children[0]) as Viewport3DVisual;

            bool success;
            Matrix3D matrix = TryWorldToViewportTransform(visual, out success);
            if (!success)
            {
                isInFront = false;
                return null;
            }

            Point3D retVal = matrix.Transform(point);
            isInFront = retVal.Z > 0d;
            return new Point(retVal.X, retVal.Y);
        }
        /// <summary>
        /// This projects a sphere into a circle
        /// NOTE: the return type could be null
        /// </summary>
        public static Tuple<Point, double> Project3Dto2D(out bool isInFront, Viewport3D viewport, Point3D point, double radius)
        {
            // The viewport should always have something in it, even if it's just lights
            if (viewport.Children.Count == 0)
            {
                isInFront = false;
                return null;
            }

            Viewport3DVisual visual = VisualTreeHelper.GetParent(viewport.Children[0]) as Viewport3DVisual;

            bool success;
            Matrix3D matrix = TryWorldToViewportTransform(visual, out success);
            if (!success)
            {
                isInFront = false;
                return null;
            }

            // Transform the center point
            Point3D point2D = matrix.Transform(point);
            Point retVal = new Point(point2D.X, point2D.Y);

            // Cast a ray from that point
            var ray = RayFromViewportPoint(visual.Camera, viewport, retVal);

            // Get an orthogonal to that (this will be a line that is in the plane that the camera is looking at)
            Vector3D orth = Math3D.GetArbitraryOrthogonal(ray.Direction);
            orth = orth.ToUnit() * radius;

            // Now that the direction of the line is known, project the point along that line, the length of radius into 2D
            Point3D point2D_rad = matrix.Transform(point + orth);

            // The distance between the two 2D points is the size of the circle on screen
            double length2D = (new Point(point2D_rad.X, point2D_rad.Y) - retVal).Length;

            isInFront = point2D.Z + length2D > 0d;
            return Tuple.Create(retVal, length2D);
        }

        /// <summary>
        /// This sets the attenuation and range of a light so that it's a percent of its intensity at some distance
        /// </summary>
        public static void SetAttenuation(PointLightBase light, double distance, double percentAtDistance)
        {
            // % = 1/max(1, q*d^2)
            // % = 1/(q*d^2)
            // q=1(d^2 * %)
            light.ConstantAttenuation = 0d;
            light.LinearAttenuation = 0d;
            light.QuadraticAttenuation = 1 / (distance * percentAtDistance);

            // Now limit the range
            light.Range = 1 / Math.Sqrt(.01 * light.QuadraticAttenuation);		// stop it at 1% intensity
        }

        /// <summary>
        /// This will tell you what to set top/left to for a window to not straddle monitors
        /// </summary>
        public static Point EnsureWindowIsOnScreen(Point position, Size size)
        {
            //TODO: See if there is a way to do this without using winform dll

            int x = Convert.ToInt32(position.X);
            int y = Convert.ToInt32(position.Y);

            int width = Convert.ToInt32(size.Width);
            int height = Convert.ToInt32(size.Height);

            // See what monitor this is sitting on
            System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(x, y));

            if (x + width > screen.WorkingArea.Right)
            {
                x = screen.WorkingArea.Right - width;
            }

            if (x < screen.WorkingArea.Left)
            {
                x = screen.WorkingArea.Left;		// doing this second so that if the window is larger than the screen, it will be the right that overflows
            }

            if (y + height > screen.WorkingArea.Bottom)
            {
                y = screen.WorkingArea.Bottom - height - 2;
            }

            if (y < screen.WorkingArea.Top)
            {
                y = screen.WorkingArea.Top;		// doing this second so the top is always visible when the window is too tall for the monitor
            }

            return new Point(x, y);
        }

        public static Rect GetCurrentScreen(Point position)
        {
            int x = Convert.ToInt32(position.X);
            int y = Convert.ToInt32(position.Y);

            // See what monitor this is sitting on
            System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(x, y));

            return new Rect(screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height);
        }

        public static FontFamily GetFont(string desiredFont)
        {
            return GetFont(new[] { desiredFont });
        }
        /// <summary>
        /// This finds the font by name
        /// </summary>
        /// <param name="desiredFonts">Pass in multiple in case the first choice isn't installed</param>
        public static FontFamily GetFont(IEnumerable<string> desiredFonts)
        {
            FontFamily[] sysFonts = Fonts.SystemFontFamilies.ToArray();

            foreach (string desired in desiredFonts)
            {
                string desiredTrim = desired.Trim();

                FontFamily retVal = sysFonts.
                    Where(o => o.Source.Trim().Equals(desiredTrim, StringComparison.OrdinalIgnoreCase)).
                    FirstOrDefault();

                if (retVal != null)
                {
                    return retVal;
                }
            }

            // Worst case, just return something (should be arial)
            return sysFonts[0];
        }

        /// <summary>
        /// Picks a random installed font
        /// </summary>
        /// <param name="includeStandard">Any font that's not a symbol (like wingding)</param>
        /// <param name="includeSymbol">Non classic (like wingding)</param>
        public static FontFamily GetRandomFont(bool includeStandard, bool includeSymbol)
        {
            if (includeStandard && includeSymbol)
            {
                return StaticRandom.NextItem(_fonts.Value).FontFamily;
            }
            else if (includeStandard)
            {
                return StaticRandom.NextItem(_fonts.Value.Where(o => o.CharSet != TEXTMETRICW_CharSet.Symbol).ToArray()).FontFamily;
            }
            else if (includeSymbol)
            {
                return StaticRandom.NextItem(_fonts.Value.Where(o => o.CharSet == TEXTMETRICW_CharSet.Symbol).ToArray()).FontFamily;
            }
            else
            {
                throw new ArgumentException("At least one bool must be true");
            }
        }

        /// <summary>
        /// This makes sure that angle stays between 0 and 360
        /// </summary>
        public static double GetCappedAngle(double angle)
        {
            double retVal = angle;

            while (true)
            {
                if (retVal < 0d)
                {
                    retVal += 360d;
                }
                else if (retVal >= 360d)
                {
                    retVal -= 360d;
                }
                else
                {
                    return retVal;
                }
            }
        }

        #endregion

        #region Private Methods

        private static void GetDome(ref int pointOffset, MeshGeometry3D geometry, Point[] pointsTheta, Transform3D transform, int numSegmentsPhi, double radiusX, double radiusY, double radiusZ)
        {
            #region Initial calculations

            // NOTE: There is one more than what the passed in
            Point[] pointsPhi = new Point[numSegmentsPhi + 1];

            pointsPhi[0] = new Point(1d, 0d);		// along the equator
            pointsPhi[numSegmentsPhi] = new Point(0d, 1d);		// north pole

            if (pointsPhi.Length > 2)
            {
                // Need to go from 0 to half pi
                double halfPi = Math.PI * .5d;
                double deltaPhi = halfPi / pointsPhi.Length;		// there is one more point than numSegmentsPhi

                for (int cntr = 1; cntr < numSegmentsPhi; cntr++)
                {
                    double phi = deltaPhi * cntr;		// phi goes from 0 to pi for a full sphere, so start halfway up
                    pointsPhi[cntr] = new Point(Math.Cos(phi), Math.Sin(phi));
                }
            }

            #endregion

            #region Positions/Normals

            // Can't use all of the transform passed in for the normal, because translate portions will skew the normal funny
            Transform3DGroup normalTransform = new Transform3DGroup();
            if (transform is Transform3DGroup)
            {
                foreach (var subTransform in ((Transform3DGroup)transform).Children)
                {
                    if (!(subTransform is TranslateTransform3D))
                    {
                        normalTransform.Children.Add(subTransform);
                    }
                }
            }
            else if (transform is TranslateTransform3D)
            {
                normalTransform.Children.Add(Transform3D.Identity);
            }
            else
            {
                normalTransform.Children.Add(transform);
            }

            //for (int phiCntr = 0; phiCntr < numSegmentsPhi; phiCntr++)		// The top point will be added after this loop
            for (int phiCntr = pointsPhi.Length - 1; phiCntr > 0; phiCntr--)
            {
                for (int thetaCntr = 0; thetaCntr < pointsTheta.Length; thetaCntr++)
                {
                    // Phi points are going from bottom to equator.  

                    Point3D point = new Point3D(
                        radiusX * pointsTheta[thetaCntr].X * pointsPhi[phiCntr].Y,
                        radiusY * pointsTheta[thetaCntr].Y * pointsPhi[phiCntr].Y,
                        radiusZ * pointsPhi[phiCntr].X);

                    geometry.Positions.Add(transform.Transform(point));

                    //TODO: For a standalone dome, the bottom rings will point straight out.  But for something like a snow cone, the normal will have to be averaged with the cone
                    geometry.Normals.Add(normalTransform.Transform(point).ToVector().ToUnit());		// the normal is the same as the point for a sphere (but no tranlate transform)
                }
            }

            // This is north pole point
            geometry.Positions.Add(transform.Transform(new Point3D(0, 0, radiusZ)));
            geometry.Normals.Add(transform.Transform(new Vector3D(0, 0, 1)));

            #endregion

            #region Triangles - Rings

            int zOffsetBottom = pointOffset;
            int zOffsetTop;

            for (int phiCntr = 0; phiCntr < numSegmentsPhi - 1; phiCntr++)		// The top cone will be added after this loop
            {
                zOffsetTop = zOffsetBottom + pointsTheta.Length;

                for (int thetaCntr = 0; thetaCntr < pointsTheta.Length - 1; thetaCntr++)
                {
                    // Top/Left triangle
                    geometry.TriangleIndices.Add(zOffsetBottom + thetaCntr + 0);
                    geometry.TriangleIndices.Add(zOffsetTop + thetaCntr + 1);
                    geometry.TriangleIndices.Add(zOffsetTop + thetaCntr + 0);

                    // Bottom/Right triangle
                    geometry.TriangleIndices.Add(zOffsetBottom + thetaCntr + 0);
                    geometry.TriangleIndices.Add(zOffsetBottom + thetaCntr + 1);
                    geometry.TriangleIndices.Add(zOffsetTop + thetaCntr + 1);
                }

                // Connecting the last 2 points to the first 2
                // Top/Left triangle
                geometry.TriangleIndices.Add(zOffsetBottom + (pointsTheta.Length - 1) + 0);
                geometry.TriangleIndices.Add(zOffsetTop);		// wrapping back around
                geometry.TriangleIndices.Add(zOffsetTop + (pointsTheta.Length - 1) + 0);

                // Bottom/Right triangle
                geometry.TriangleIndices.Add(zOffsetBottom + (pointsTheta.Length - 1) + 0);
                geometry.TriangleIndices.Add(zOffsetBottom);
                geometry.TriangleIndices.Add(zOffsetTop);

                // Prep for the next ring
                zOffsetBottom = zOffsetTop;
            }

            #endregion
            #region Triangles - Cap

            int topIndex = geometry.Positions.Count - 1;

            for (int cntr = 0; cntr < pointsTheta.Length - 1; cntr++)
            {
                geometry.TriangleIndices.Add(zOffsetBottom + cntr + 0);
                geometry.TriangleIndices.Add(zOffsetBottom + cntr + 1);
                geometry.TriangleIndices.Add(topIndex);
            }

            // The last triangle links back to zero
            geometry.TriangleIndices.Add(zOffsetBottom + pointsTheta.Length - 1 + 0);
            geometry.TriangleIndices.Add(zOffsetBottom + 0);
            geometry.TriangleIndices.Add(topIndex);

            #endregion

            pointOffset = geometry.Positions.Count;
        }

        /// <summary>
        /// This isn't meant to be used by anything.  It's just a pure implementation of a dome that can be a template for the
        /// real methods
        /// </summary>
        private static MeshGeometry3D GetDome_Template(int numSegmentsTheta, int numSegmentsPhi, double radiusX, double radiusY, double radiusZ)
        {
            // This will be along the z axis.  It will go from z=0 to z=radiusZ

            if (numSegmentsTheta < 3)
            {
                throw new ArgumentException("numSegments must be at least 3: " + numSegmentsTheta.ToString(), "numSegments");
            }

            MeshGeometry3D retVal = new MeshGeometry3D();

            #region Initial calculations

            //Transform3D transform = Transform3D.Identity;

            double deltaTheta = 2d * Math.PI / numSegmentsTheta;

            Point[] pointsTheta = new Point[numSegmentsTheta];		// these define a unit circle

            for (int cntr = 0; cntr < numSegmentsTheta; cntr++)
            {
                pointsTheta[cntr] = new Point(Math.Cos(deltaTheta * cntr), Math.Sin(deltaTheta * cntr));
            }

            // NOTE: There is one more than what the passed in
            Point[] pointsPhi = new Point[numSegmentsPhi + 1];

            pointsPhi[0] = new Point(1d, 0d);		// along the equator
            pointsPhi[numSegmentsPhi] = new Point(0d, 1d);		// north pole

            if (pointsPhi.Length > 2)
            {
                //double halfPi = Math.PI * .5d;
                ////double deltaPhi = halfPi / numSegmentsPhi;
                //double deltaPhi = halfPi / pointsPhi.Length;		// there is one more point than numSegmentsPhi
                ////double deltaPhi = Math.PI / numSegmentsPhi;

                //for (int cntr = 1; cntr < numSegmentsPhi; cntr++)
                //{
                //    double phi = halfPi + (deltaPhi * cntr);		// phi goes from 0 to pi for a full sphere, so start halfway up
                //    //double phi = deltaPhi * cntr;
                //    pointsPhi[cntr] = new Point(Math.Cos(phi), Math.Sin(phi));
                //}



                // Need to go from 0 to half pi
                double halfPi = Math.PI * .5d;
                double deltaPhi = halfPi / pointsPhi.Length;		// there is one more point than numSegmentsPhi

                for (int cntr = 1; cntr < numSegmentsPhi; cntr++)
                {
                    double phi = deltaPhi * cntr;		// phi goes from 0 to pi for a full sphere, so start halfway up
                    pointsPhi[cntr] = new Point(Math.Cos(phi), Math.Sin(phi));
                }


            }

            #endregion

            #region Positions/Normals

            //for (int phiCntr = 0; phiCntr < numSegmentsPhi; phiCntr++)		// The top point will be added after this loop
            for (int phiCntr = pointsPhi.Length - 1; phiCntr > 0; phiCntr--)
            {
                for (int thetaCntr = 0; thetaCntr < numSegmentsTheta; thetaCntr++)
                {

                    // I think phi points are going from bottom to equator.  

                    Point3D point = new Point3D(
                        radiusX * pointsTheta[thetaCntr].X * pointsPhi[phiCntr].Y,
                        radiusY * pointsTheta[thetaCntr].Y * pointsPhi[phiCntr].Y,
                        radiusZ * pointsPhi[phiCntr].X);

                    //point = transform.Transform(point);

                    retVal.Positions.Add(point);

                    //TODO: For a standalone dome, the bottom ring's will point straight out.  But for something like a snow cone, the normal will have to be averaged with the cone
                    retVal.Normals.Add(point.ToVector().ToUnit());		// the normal is the same as the point for a sphere
                }
            }

            // This is north pole point
            //retVal.Positions.Add(transform.Transform(new Point3D(0, 0, radiusZ)));
            //retVal.Normals.Add(transform.Transform(new Vector3D(0, 0, 1)));
            retVal.Positions.Add(new Point3D(0, 0, radiusZ));
            retVal.Normals.Add(new Vector3D(0, 0, 1));

            #endregion

            #region Triangles - Rings

            int zOffsetBottom = 0;
            int zOffsetTop;

            for (int phiCntr = 0; phiCntr < numSegmentsPhi - 1; phiCntr++)		// The top cone will be added after this loop
            {
                zOffsetTop = zOffsetBottom + numSegmentsTheta;

                for (int thetaCntr = 0; thetaCntr < numSegmentsTheta - 1; thetaCntr++)
                {
                    // Top/Left triangle
                    retVal.TriangleIndices.Add(zOffsetBottom + thetaCntr + 0);
                    retVal.TriangleIndices.Add(zOffsetTop + thetaCntr + 1);
                    retVal.TriangleIndices.Add(zOffsetTop + thetaCntr + 0);

                    // Bottom/Right triangle
                    retVal.TriangleIndices.Add(zOffsetBottom + thetaCntr + 0);
                    retVal.TriangleIndices.Add(zOffsetBottom + thetaCntr + 1);
                    retVal.TriangleIndices.Add(zOffsetTop + thetaCntr + 1);
                }

                // Connecting the last 2 points to the first 2
                // Top/Left triangle
                retVal.TriangleIndices.Add(zOffsetBottom + (numSegmentsTheta - 1) + 0);
                retVal.TriangleIndices.Add(zOffsetTop);		// wrapping back around
                retVal.TriangleIndices.Add(zOffsetTop + (numSegmentsTheta - 1) + 0);

                // Bottom/Right triangle
                retVal.TriangleIndices.Add(zOffsetBottom + (numSegmentsTheta - 1) + 0);
                retVal.TriangleIndices.Add(zOffsetBottom);
                retVal.TriangleIndices.Add(zOffsetTop);

                // Prep for the next ring
                zOffsetBottom = zOffsetTop;
            }

            #endregion
            #region Triangles - Cap

            int topIndex = retVal.Positions.Count - 1;

            for (int cntr = 0; cntr < numSegmentsTheta - 1; cntr++)
            {
                retVal.TriangleIndices.Add(zOffsetBottom + cntr + 0);
                retVal.TriangleIndices.Add(zOffsetBottom + cntr + 1);
                retVal.TriangleIndices.Add(topIndex);
            }

            // The last triangle links back to zero
            retVal.TriangleIndices.Add(zOffsetBottom + numSegmentsTheta - 1 + 0);
            retVal.TriangleIndices.Add(zOffsetBottom + 0);
            retVal.TriangleIndices.Add(topIndex);

            #endregion

            //retVal.Freeze();
            return retVal;
        }

        #region GetMultiRingedTube helpers (ORIG)

        private static Point[] GetMultiRingedTubeSprtPoints_ORIG(int numSides)
        {
            // This calculates the points (note they are 2D, because each ring will have its own z anyway)
            // Also, the points are around a circle with diameter of 1.  Each ring will define it's own x,y scale

            Point[] retVal = new Point[numSides];

            //double stepAngle = 360d / numSides;
            double stepRadians = (Math.PI * 2d) / numSides;

            for (int cntr = 0; cntr < numSides; cntr++)
            {
                double radians = stepRadians * cntr;

                double x = .5d * Math.Cos(radians);
                double y = .5d * Math.Sin(radians);

                retVal[cntr] = new Point(x, y);
            }

            return retVal;
        }
        private static void GetMultiRingedTubeSprtEndCap_ORIG(ref int pointOffset, MeshGeometry3D geometry, int numSides, Point[] points, TubeRingDefinition_ORIG ring, bool isFirst, double z)
        {
            #region Figure out the normal

            Vector3D normal;
            if (isFirst)
            {
                // The first is in the negative Z, so the normal points down
                // For some reason, it's backward from what I think it should be
                normal = new Vector3D(0, 0, 1);
            }
            else
            {
                normal = new Vector3D(0, 0, 1);
            }

            #endregion

            #region Add points and normals

            for (int cntr = 0; cntr < numSides; cntr++)
            {
                double x = ring.RadiusX * points[cntr].X;
                double y = ring.RadiusY * points[cntr].Y;

                geometry.Positions.Add(new Point3D(x, y, z));

                geometry.Normals.Add(normal);
            }

            #endregion

            #region Add the triangles

            // Start with 0,1,2
            geometry.TriangleIndices.Add(pointOffset + 0);
            geometry.TriangleIndices.Add(pointOffset + 1);
            geometry.TriangleIndices.Add(pointOffset + 2);

            int lowerIndex = 2;
            int upperIndex = numSides - 1;
            int lastUsedIndex = 0;
            bool shouldBumpLower = true;

            // Do the rest of the triangles
            while (lowerIndex < upperIndex)
            {
                geometry.TriangleIndices.Add(pointOffset + lowerIndex);
                geometry.TriangleIndices.Add(pointOffset + upperIndex);
                geometry.TriangleIndices.Add(pointOffset + lastUsedIndex);

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

            #endregion

            pointOffset += numSides;
        }
        private static void GetMultiRingedTubeSprtBetweenRings_ORIG(ref int pointOffset, MeshGeometry3D geometry, int numSides, Point[] points, TubeRingDefinition_ORIG ring1, TubeRingDefinition_ORIG ring2, double z)
        {
            if (ring1.RingType == TubeRingType_ORIG.Point || ring2.RingType == TubeRingType_ORIG.Point)
            {
                GetMultiRingedTubeSprtBetweenRingsSprtPyramid_ORIG(ref pointOffset, geometry, numSides, points, ring1, ring2, z);
            }
            else
            {
                GetMultiRingedTubeSprtBetweenRingsSprtTube_ORIG(ref pointOffset, geometry, numSides, points, ring1, ring2, z);
            }

            #region OLD
            /*
            if (ring1.IsPoint)
            {
            }
            else if (ring2.IsPoint)
            {
                #region Pyramid 2

                #region Add points and normals

                // Determine 3D positions (they are referenced a lot, so just calculating them once
                Point3D tipPoint = new Point3D(0, 0, z + ring2.DistFromPrevRing);

                Point3D[] sidePoints = new Point3D[numSides];
                for (int cntr = 0; cntr < numSides; cntr++)
                {
                    sidePoints[cntr] = new Point3D(ring1.SizeX * points[cntr].X, ring1.SizeY * points[cntr].Y, z);
                }

                Vector3D v1, v2;

                // Sides - adding the points twice, since 2 triangles will use each point (and each triangle gets its own point)
                for (int cntr = 0; cntr < numSides; cntr++)
                {
                    // Even
                    geometry.Positions.Add(sidePoints[cntr]);

                    #region normal

                    // (tip - cur) x (prev - cur)

                    v1 = tipPoint.ToVector() - sidePoints[cntr].ToVector();
                    if (cntr == 0)
                    {
                        v2 = sidePoints[sidePoints.Length - 1].ToVector() - sidePoints[0].ToVector();
                    }
                    else
                    {
                        v2 = sidePoints[cntr - 1].ToVector() - sidePoints[cntr].ToVector();
                    }

                    geometry.Normals.Add(Vector3D.CrossProduct(v1, v2));

                    #endregion

                    // Odd
                    geometry.Positions.Add(sidePoints[cntr]);

                    #region normal

                    // (next - cur) x (tip - cur)

                    if (cntr == sidePoints.Length - 1)
                    {
                        v1 = sidePoints[0].ToVector() - sidePoints[cntr].ToVector();
                    }
                    else
                    {
                        v1 = sidePoints[cntr + 1].ToVector() - sidePoints[cntr].ToVector();
                    }

                    v2 = tipPoint.ToVector() - sidePoints[cntr].ToVector();

                    geometry.Normals.Add(Vector3D.CrossProduct(v1, v2));

                    #endregion
                }

                int lastPoint = numSides * 2;

                // Top point (all triangles use this same one)
                geometry.Positions.Add(tipPoint);

                // This one is straight up
                geometry.Normals.Add(new Vector3D(0, 0, 1));

                #endregion

                #region Add the triangles

                for (int cntr = 0; cntr < numSides; cntr++)
                {
                    geometry.TriangleIndices.Add(pointOffset + ((cntr * 2) + 1));      // this will be the second of the pair of points at this location

                    if (cntr == numSides - 1)
                    {
                        geometry.TriangleIndices.Add(pointOffset);  // on the last point, so loop back to point zero
                    }
                    else
                    {
                        geometry.TriangleIndices.Add(pointOffset + ((cntr + 1) * 2));      // this will be the first of the pair of points at the next location
                    }

                    geometry.TriangleIndices.Add(pointOffset + lastPoint);   // the tip
                }

                #endregion

                #endregion
            }
            else
            {
            }
            */
            #endregion
        }
        private static void GetMultiRingedTubeSprtBetweenRingsSprtPyramid_ORIG(ref int pointOffset, MeshGeometry3D geometry, int numSides, Point[] points, TubeRingDefinition_ORIG ring1, TubeRingDefinition_ORIG ring2, double z)
        {
            #region Add points and normals

            #region Determine 3D positions (they are referenced a lot, so just calculating them once

            Point3D tipPoint;
            Vector3D tipNormal;
            double sideZ, sizeX, sizeY;
            if (ring1.RingType == TubeRingType_ORIG.Point)
            {
                // Upside down pyramid
                tipPoint = new Point3D(0, 0, z);
                tipNormal = new Vector3D(0, 0, -1);
                sideZ = z + ring2.DistFromPrevRing;
                sizeX = ring2.RadiusX;
                sizeY = ring2.RadiusY;
            }
            else
            {
                // Rightside up pyramid
                tipPoint = new Point3D(0, 0, z + ring2.DistFromPrevRing);
                tipNormal = new Vector3D(0, 0, 1);
                sideZ = z;
                sizeX = ring1.RadiusX;
                sizeY = ring1.RadiusY;
            }

            Point3D[] sidePoints = new Point3D[numSides];
            for (int cntr = 0; cntr < numSides; cntr++)
            {
                sidePoints[cntr] = new Point3D(sizeX * points[cntr].X, sizeY * points[cntr].Y, sideZ);
            }

            #endregion

            Vector3D v1, v2;

            // Sides - adding the points twice, since 2 triangles will use each point (and each triangle gets its own point)
            for (int cntr = 0; cntr < numSides; cntr++)
            {
                // Even
                geometry.Positions.Add(sidePoints[cntr]);

                #region normal

                // (tip - cur) x (prev - cur)

                v1 = tipPoint.ToVector() - sidePoints[cntr].ToVector();
                if (cntr == 0)
                {
                    v2 = sidePoints[sidePoints.Length - 1].ToVector() - sidePoints[0].ToVector();
                }
                else
                {
                    v2 = sidePoints[cntr - 1].ToVector() - sidePoints[cntr].ToVector();
                }

                geometry.Normals.Add(Vector3D.CrossProduct(v1, v2));

                #endregion

                // Odd
                geometry.Positions.Add(sidePoints[cntr]);

                #region normal

                // (next - cur) x (tip - cur)

                if (cntr == sidePoints.Length - 1)
                {
                    v1 = sidePoints[0].ToVector() - sidePoints[cntr].ToVector();
                }
                else
                {
                    v1 = sidePoints[cntr + 1].ToVector() - sidePoints[cntr].ToVector();
                }

                v2 = tipPoint.ToVector() - sidePoints[cntr].ToVector();

                geometry.Normals.Add(Vector3D.CrossProduct(v1, v2));

                #endregion
            }

            int lastPoint = numSides * 2;

            //TODO: This is wrong, each triangle should have its own copy of the point with the normal the same as the 2 base points
            // Top point (all triangles use this same one)
            geometry.Positions.Add(tipPoint);

            // This one is straight up
            geometry.Normals.Add(tipNormal);

            #endregion

            #region Add the triangles

            for (int cntr = 0; cntr < numSides; cntr++)
            {
                geometry.TriangleIndices.Add(pointOffset + ((cntr * 2) + 1));      // this will be the second of the pair of points at this location

                if (cntr == numSides - 1)
                {
                    geometry.TriangleIndices.Add(pointOffset);  // on the last point, so loop back to point zero
                }
                else
                {
                    geometry.TriangleIndices.Add(pointOffset + ((cntr + 1) * 2));      // this will be the first of the pair of points at the next location
                }

                geometry.TriangleIndices.Add(pointOffset + lastPoint);   // the tip
            }

            #endregion

            pointOffset += lastPoint + 1;
        }
        private static void GetMultiRingedTubeSprtBetweenRingsSprtTube_ORIG(ref int pointOffset, MeshGeometry3D geometry, int numSides, Point[] points, TubeRingDefinition_ORIG ring1, TubeRingDefinition_ORIG ring2, double z)
        {
            #region Add points and normals

            #region Determine 3D positions (they are referenced a lot, so just calculating them once

            Point3D[] sidePoints1 = new Point3D[numSides];
            Point3D[] sidePoints2 = new Point3D[numSides];
            for (int cntr = 0; cntr < numSides; cntr++)
            {
                sidePoints1[cntr] = new Point3D(ring1.RadiusX * points[cntr].X, ring1.RadiusY * points[cntr].Y, z);
                sidePoints2[cntr] = new Point3D(ring2.RadiusX * points[cntr].X, ring2.RadiusY * points[cntr].Y, z + ring2.DistFromPrevRing);
            }

            #endregion
            #region Determine normals

            Vector3D v1, v2;

            // The normal at 0 is for the face between 0 and 1.  The normal at 1 is the face between 1 and 2, etc.
            Vector3D[] sideNormals = new Vector3D[numSides];
            for (int cntr = 0; cntr < numSides; cntr++)
            {
                // (next1 - cur1) x (cur2 - cur1)

                if (cntr == numSides - 1)
                {
                    v1 = sidePoints1[0] - sidePoints1[cntr];
                }
                else
                {
                    v1 = sidePoints1[cntr + 1] - sidePoints1[cntr];
                }

                v2 = sidePoints2[cntr] - sidePoints1[cntr];

                sideNormals[cntr] = Vector3D.CrossProduct(v1, v2);
            }

            #endregion

            #region Commit points/normals

            for (int ringCntr = 1; ringCntr <= 2; ringCntr++)     // I want all the points in ring1 laid down before doing ring2 (this stays similar to the pyramid method's logic)
            {
                // Sides - adding the points twice, since 2 triangles will use each point (and each triangle gets its own point)
                for (int cntr = 0; cntr < numSides; cntr++)
                {
                    // Even
                    if (ringCntr == 1)
                    {
                        geometry.Positions.Add(sidePoints1[cntr]);
                    }
                    else
                    {
                        geometry.Positions.Add(sidePoints2[cntr]);
                    }

                    // even always selects the previous side's normal
                    if (cntr == 0)
                    {
                        geometry.Normals.Add(sideNormals[numSides - 1]);
                    }
                    else
                    {
                        geometry.Normals.Add(sideNormals[cntr - 1]);
                    }

                    // Odd
                    if (ringCntr == 1)
                    {
                        geometry.Positions.Add(sidePoints1[cntr]);
                    }
                    else
                    {
                        geometry.Positions.Add(sidePoints2[cntr]);
                    }

                    // odd always selects the current side's normal
                    geometry.Normals.Add(sideNormals[cntr]);
                }
            }

            #endregion

            int ring2Start = numSides * 2;

            #endregion

            #region Add the triangles

            for (int cntr = 0; cntr < numSides; cntr++)
            //for (int cntr = 0; cntr < 1; cntr++)
            {
                //--------------Bottom Right Triangle

                // Ring 1, bottom left
                geometry.TriangleIndices.Add(pointOffset + ((cntr * 2) + 1));      // this will be the second of the pair of points at this location

                // Ring 1, bottom right
                if (cntr == numSides - 1)
                {
                    geometry.TriangleIndices.Add(pointOffset);  // on the last point, so loop back to point zero
                }
                else
                {
                    geometry.TriangleIndices.Add(pointOffset + ((cntr + 1) * 2));      // this will be the first of the pair of points at the next location
                }

                // Ring 2, top right (adding twice, because it starts the next triangle)
                if (cntr == numSides - 1)
                {
                    geometry.TriangleIndices.Add(pointOffset + ring2Start);  // on the last point, so loop back to point zero
                    geometry.TriangleIndices.Add(pointOffset + ring2Start);
                }
                else
                {
                    geometry.TriangleIndices.Add(pointOffset + ring2Start + ((cntr + 1) * 2));      // this will be the first of the pair of points at the next location
                    geometry.TriangleIndices.Add(pointOffset + ring2Start + ((cntr + 1) * 2));
                }

                //--------------Top Left Triangle

                // Ring 2, top left
                geometry.TriangleIndices.Add(pointOffset + ring2Start + ((cntr * 2) + 1));      // this will be the second of the pair of points at this location

                // Ring 1, bottom left (same as the very first point added in this for loop)
                geometry.TriangleIndices.Add(pointOffset + ((cntr * 2) + 1));      // this will be the second of the pair of points at this location
            }

            #endregion

            pointOffset += numSides * 4;
        }

        #endregion

        /// <summary>
        /// This takes values from 0 to 255
        /// </summary>
        private static Color GetColorCapped(double a, double r, double g, double b)
        {
            if (a < 0)
            {
                a = 0;
            }
            else if (a > 255)
            {
                a = 255;
            }

            if (r < 0)
            {
                r = 0;
            }
            else if (r > 255)
            {
                r = 255;
            }

            if (g < 0)
            {
                g = 0;
            }
            else if (g > 255)
            {
                g = 255;
            }

            if (b < 0)
            {
                b = 0;
            }
            else if (b > 255)
            {
                b = 255;
            }

            return Color.FromArgb(Convert.ToByte(a), Convert.ToByte(r), Convert.ToByte(g), Convert.ToByte(b));
        }
        private static byte[] GetColorCapped_Bytes(double a, double r, double g, double b)
        {
            if (a < 0)
            {
                a = 0;
            }
            else if (a > 255)
            {
                a = 255;
            }

            if (r < 0)
            {
                r = 0;
            }
            else if (r > 255)
            {
                r = 255;
            }

            if (g < 0)
            {
                g = 0;
            }
            else if (g > 255)
            {
                g = 255;
            }

            if (b < 0)
            {
                b = 0;
            }
            else if (b > 255)
            {
                b = 255;
            }

            return new[] { Convert.ToByte(a), Convert.ToByte(r), Convert.ToByte(g), Convert.ToByte(b) };
        }
        private static Color GetColorCapped(int a, int r, int g, int b)
        {
            if (a < 0)
            {
                a = 0;
            }
            else if (a > 255)
            {
                a = 255;
            }

            if (r < 0)
            {
                r = 0;
            }
            else if (r > 255)
            {
                r = 255;
            }

            if (g < 0)
            {
                g = 0;
            }
            else if (g > 255)
            {
                g = 255;
            }

            if (b < 0)
            {
                b = 0;
            }
            else if (b > 255)
            {
                b = 255;
            }

            return Color.FromArgb(Convert.ToByte(a), Convert.ToByte(r), Convert.ToByte(g), Convert.ToByte(b));
        }
        private static byte[] GetColorCapped_Bytes(int a, int r, int g, int b)
        {
            if (a < 0)
            {
                a = 0;
            }
            else if (a > 255)
            {
                a = 255;
            }

            if (r < 0)
            {
                r = 0;
            }
            else if (r > 255)
            {
                r = 255;
            }

            if (g < 0)
            {
                g = 0;
            }
            else if (g > 255)
            {
                g = 255;
            }

            if (b < 0)
            {
                b = 0;
            }
            else if (b > 255)
            {
                b = 255;
            }

            return new[] { Convert.ToByte(a), Convert.ToByte(r), Convert.ToByte(g), Convert.ToByte(b) };
        }

        private static byte GetByteCapped(double value)
        {
            if (value < 0)
            {
                return 0;
            }
            else if (value > 255)
            {
                return 255;
            }
            else
            {
                return Convert.ToByte(value);
            }
        }

        // For speed reasons, the code was duplicated
        private static TriangleIndexed_wpf[] GetTrianglesFromMesh_Raw(MeshGeometry3D[] meshes, Transform3D[] transforms = null)
        {
            #region Points

            List<Point3D> allPointsList = new List<Point3D>();

            for (int cntr = 0; cntr < meshes.Length; cntr++)
            {
                Point3D[] positions = meshes[cntr].Positions.ToArray();

                if (transforms != null && transforms[cntr] != null)
                {
                    transforms[cntr].Transform(positions);
                }

                allPointsList.AddRange(positions);
            }

            Point3D[] allPoints = allPointsList.ToArray();

            #endregion

            List<TriangleIndexed_wpf> retVal = new List<TriangleIndexed_wpf>();

            #region Triangles

            int posOffset = 0;

            foreach (MeshGeometry3D mesh in meshes)
            {
                //string report = mesh.ReportGeometry();

                if (mesh.TriangleIndices.Count % 3 != 0)
                {
                    throw new ArgumentException("The mesh's triangle indicies need to be divisible by 3");
                }

                int numTriangles = mesh.TriangleIndices.Count / 3;

                for (int cntr = 0; cntr < numTriangles; cntr++)
                {
                    TriangleIndexed_wpf triangle = new TriangleIndexed_wpf(
                        posOffset + mesh.TriangleIndices[cntr * 3],
                        posOffset + mesh.TriangleIndices[(cntr * 3) + 1],
                        posOffset + mesh.TriangleIndices[(cntr * 3) + 2],
                        allPoints);

                    double normalLength = triangle.NormalLength;
                    if (!Math1D.IsNearZero(normalLength) && !Math1D.IsInvalid(normalLength))      // don't include bad triangles (the mesh seems to be ok with bad triangles, so just skip them)
                    {
                        retVal.Add(triangle);
                    }
                }

                posOffset += mesh.Positions.Count;
            }

            #endregion

            return retVal.ToArray();
        }
        private static TriangleIndexed_wpf[] GetTrianglesFromMesh_Deduped(MeshGeometry3D[] meshes, Transform3D[] transforms = null)
        {
            #region Points

            // Key=original index
            // Value=new index
            SortedList<int, int> pointMap = new SortedList<int, int>();

            // Points
            List<Point3D> allPointsList = new List<Point3D>();

            int posOffset = 0;

            for (int m = 0; m < meshes.Length; m++)
            {
                Point3D[] positions = meshes[m].Positions.ToArray();

                if (transforms != null && transforms[m] != null)
                {
                    transforms[m].Transform(positions);
                }

                for (int p = 0; p < positions.Length; p++)
                {
                    int dupeIndex = IndexOfDupe(allPointsList, positions[p]);

                    if (dupeIndex < 0)
                    {
                        allPointsList.Add(positions[p]);
                        pointMap.Add(posOffset + p, allPointsList.Count - 1);
                    }
                    else
                    {
                        pointMap.Add(posOffset + p, dupeIndex);
                    }
                }

                posOffset += meshes[m].Positions.Count;
            }

            Point3D[] allPoints = allPointsList.ToArray();

            #endregion

            List<TriangleIndexed_wpf> retVal = new List<TriangleIndexed_wpf>();

            #region Triangles

            posOffset = 0;

            foreach (MeshGeometry3D mesh in meshes)
            {
                //string report = mesh.ReportGeometry();

                if (mesh.TriangleIndices.Count % 3 != 0)
                {
                    throw new ArgumentException("The mesh's triangle indicies need to be divisible by 3");
                }

                int numTriangles = mesh.TriangleIndices.Count / 3;

                for (int cntr = 0; cntr < numTriangles; cntr++)
                {
                    TriangleIndexed_wpf triangle = new TriangleIndexed_wpf(
                        pointMap[posOffset + mesh.TriangleIndices[cntr * 3]],
                        pointMap[posOffset + mesh.TriangleIndices[(cntr * 3) + 1]],
                        pointMap[posOffset + mesh.TriangleIndices[(cntr * 3) + 2]],
                        allPoints);

                    double normalLength = triangle.NormalLength;
                    if (!Math1D.IsNearZero(normalLength) && !Math1D.IsInvalid(normalLength))      // don't include bad triangles (the mesh seems to be ok with bad triangles, so just skip them)
                    {
                        retVal.Add(triangle);
                    }
                }

                posOffset += mesh.Positions.Count;
            }

            #endregion

            return retVal.ToArray();
        }

        private static int IndexOfDupe(List<Point3D> points, Point3D test)
        {
            for (int cntr = 0; cntr < points.Count; cntr++)
            {
                if (Math3D.IsNearValue(points[cntr], test))
                {
                    return cntr;
                }
            }

            return -1;
        }

        private static void TriangulateText(out List<Point3D> vertices, out List<Vector3D> normals, out List<int> indices, out List<Point> textures, Geometry geometry, double depth)
        {
            //Got this here:
            //http://msdn.microsoft.com/en-us/magazine/cc163349.aspx

            vertices = new List<Point3D>();
            normals = new List<Vector3D>();
            indices = new List<int>();
            textures = new List<Point>();

            Point origin = new Point(0, 0);     // origin was passed into the making of geometry.  Don't think it's need twice

            // Convert TextGeometry to series of closed polylines.
            PathGeometry path = geometry.GetFlattenedPathGeometry(0.001, ToleranceType.Relative);

            List<Point> list = new List<Point>();

            foreach (PathFigure fig in path.Figures)
            {
                list.Clear();
                list.Add(fig.StartPoint);

                foreach (PathSegment seg in fig.Segments)
                {
                    if (seg is LineSegment)
                    {
                        LineSegment lineseg = seg as LineSegment;
                        list.Add(lineseg.Point);
                    }
                    else if (seg is PolyLineSegment)
                    {
                        PolyLineSegment polyline = seg as PolyLineSegment;
                        for (int i = 0; i < polyline.Points.Count; i++)
                            list.Add(polyline.Points[i]);
                    }
                }

                // Figure is complete. Post-processing follows.
                if (list.Count > 0)
                {
                    // Remove last point if it's the same as the first.
                    if (list[0] == list[list.Count - 1])
                        list.RemoveAt(list.Count - 1);

                    // Convert points to Y increasing up.
                    for (int i = 0; i < list.Count; i++)
                    {
                        Point pt = list[i];
                        pt.Y = 2 * origin.Y - pt.Y;
                        list[i] = pt;
                    }

                    // For each figure, process the points.
                    ProcessTextFigure(list, vertices, normals, indices, textures, depth);
                }
            }
        }
        private static void ProcessTextFigure(List<Point> input, List<Point3D> vertices, List<Vector3D> normals, List<int> indices, List<Point> textures, double depth)
        {
            double halfDepth = depth / 2d;
            int offset = vertices.Count;

            for (int i = 0; i <= input.Count; i++)
            {
                Point pt = i == input.Count ? input[0] : input[i];

                // Set vertices.
                vertices.Add(new Point3D(pt.X, pt.Y, -halfDepth));
                vertices.Add(new Point3D(pt.X, pt.Y, halfDepth));

                // Set texture coordinates.
                textures.Add(new Point((double)i / input.Count, 0));
                textures.Add(new Point((double)i / input.Count, 1));

                // Set triangle indices.
                if (i < input.Count)
                {
                    indices.Add(offset + i * 2 + 0);
                    indices.Add(offset + i * 2 + 2);
                    indices.Add(offset + i * 2 + 1);
                    indices.Add(offset + i * 2 + 1);
                    indices.Add(offset + i * 2 + 2);
                    indices.Add(offset + i * 2 + 3);
                }
            }
        }

        private static Material ConvertToTextMaterial(Material material, Geometry textGeometry)
        {
            // Create a material that is sort of like a clone of the material passed in, but instead of being an
            // infinite plane, it is shaped like the geometry passed in

            if (material is MaterialGroup)
            {
                #region MaterialGroup

                MaterialGroup retVal = new MaterialGroup();

                foreach (Material childMaterial in ((MaterialGroup)material).Children)
                {
                    // Recurse
                    retVal.Children.Add(ConvertToTextMaterial(childMaterial, textGeometry));
                }

                return retVal;

                #endregion
            }

            Brush embeddedBrush = null;
            Brush newBrush = null;

            if (material is DiffuseMaterial)
            {
                #region DiffuseMaterial

                DiffuseMaterial materialCast1 = (DiffuseMaterial)material;

                if (materialCast1.Brush == null)
                {
                    embeddedBrush = new SolidColorBrush(materialCast1.AmbientColor);
                }
                else
                {
                    embeddedBrush = materialCast1.Brush;
                }

                newBrush = new DrawingBrush(new GeometryDrawing(embeddedBrush, null, textGeometry));

                return new DiffuseMaterial(newBrush);

                #endregion
            }
            else if (material is SpecularMaterial)
            {
                #region SpecularMaterial

                SpecularMaterial materialCast2 = (SpecularMaterial)material;

                embeddedBrush = materialCast2.Brush;

                newBrush = new DrawingBrush(new GeometryDrawing(embeddedBrush, null, textGeometry));

                return new SpecularMaterial(newBrush, materialCast2.SpecularPower);

                #endregion
            }
            else if (material is EmissiveMaterial)
            {
                #region EmissiveMaterial

                EmissiveMaterial materialCast3 = (EmissiveMaterial)material;

                if (materialCast3.Brush == null)
                {
                    embeddedBrush = new SolidColorBrush(materialCast3.Color);
                }
                else
                {
                    embeddedBrush = materialCast3.Brush;
                }

                newBrush = new DrawingBrush(new GeometryDrawing(embeddedBrush, null, textGeometry));

                return new EmissiveMaterial(newBrush);

                #endregion
            }

            throw new ApplicationException("Unknown type of material: " + material.GetType().ToString());
        }

        private static FontInfo[] GetSystemFontsInfo()
        {
            var winformLabel = new System.Windows.Forms.Label();

            using (System.Drawing.Graphics graphics = winformLabel.CreateGraphics())
            {
                IntPtr hDC = graphics.GetHdc();

                try
                {
                    return Fonts.SystemFontFamilies.
                    Select(o =>
                    {
                        winformLabel.Font = new System.Drawing.Font(o.ToString(), 10);

                        var result = GetFontInfo(graphics, hDC, winformLabel.Font);

                        return new FontInfo
                        {
                            FontFamily = o,
                            HasTextMetric = result.success,
                            TextMetric = result.textMetric,
                        };
                    }).
                    ToArray();
                }
                finally
                {
                    graphics.ReleaseHdc();
                }
            }
        }
        private static (bool success, TEXTMETRICW textMetric) GetFontInfo(System.Drawing.IDeviceContext dc, IntPtr hDC, System.Drawing.Font font)
        {
            IntPtr hFont = IntPtr.Zero;

            try
            {
                hFont = font.ToHfont();
                SelectObject(hDC, hFont);

                bool success = GetTextMetrics(hDC, out TEXTMETRICW textMetric);

                return (success, textMetric);
            }
            finally
            {
                if (hFont != IntPtr.Zero)
                {
                    DeleteObject(hFont);
                }
            }
        }

        #endregion
    }

    #region tube rings

    #region enum: TubeRingType_ORIG

    //TODO: Turn dome into Dome_Hemisphere, Dome_Tangent.  If it's tangent, then the height will be calculated on the fly (allow for acute and obtuse angles)
    //If you have a dome_tangent - ring - dome_tangent, they both emulate dome_hemisphere (so a sphere with a radius defined by the middle ring)
    public enum TubeRingType_ORIG
    {
        Point,
        Ring_Open,
        Ring_Closed,
        Dome
    }

    #endregion
    #region class: TubeRingDefinition_ORIG

    /// <summary>
    /// This defines a single ring - if it's the first or last in the list, then it's an end cap, and has more options of what it can
    /// be (rings in the middle can only be rings)
    /// </summary>
    /// <remarks>
    /// It might be worth it to get rid of the enum, and go with a base abstract class with derived ring/point/dome classes.
    /// ring and dome would both need radiusx/y though.  But it would make it easier to know what type uses what properties
    /// </remarks>
    public class TubeRingDefinition_ORIG
    {
        #region Constructor

        /// <summary>
        /// This overload defines a point (can only be used as an end cap)
        /// </summary>
        public TubeRingDefinition_ORIG(double distFromPrevRing, bool mergeNormalWithPrevIfSoft)
        {
            this.RingType = TubeRingType_ORIG.Point;
            this.DistFromPrevRing = distFromPrevRing;
            this.MergeNormalWithPrevIfSoft = mergeNormalWithPrevIfSoft;
        }

        /// <summary>
        /// This overload defines a polygon
        /// NOTE:  The number of points aren't defined within this class, but is the same for a list of these classes
        /// </summary>
        /// <param name="isClosedIfEndCap">
        /// Ignored if this is a middle ring.
        /// True: If this is an end cap, a disc is created to seal up the hull (like a lid on a cylinder).
        /// False: If this is an end cap, the space is left open (like an open bucket)
        /// </param>
        public TubeRingDefinition_ORIG(double radiusX, double radiusY, double distFromPrevRing, bool isClosedIfEndCap, bool mergeNormalWithPrevIfSoft)
        {
            if (isClosedIfEndCap)
            {
                this.RingType = TubeRingType_ORIG.Ring_Closed;
            }
            else
            {
                this.RingType = TubeRingType_ORIG.Ring_Open;
            }

            this.DistFromPrevRing = distFromPrevRing;
            this.RadiusX = radiusX;
            this.RadiusY = radiusY;
            this.MergeNormalWithPrevIfSoft = mergeNormalWithPrevIfSoft;
        }

        /// <summary>
        /// This overload defines a dome (can only be used as an end cap)
        /// TODO: Give an option for a partial dome (not a full hemisphere, but calculate where to start it so the dome is tangent with the neighboring ring)
        /// </summary>
        /// <param name="numSegmentsPhi">This lets you fine tune how many vertical separations there are in the dome (usually just use the same number as horizontal segments)</param>
        public TubeRingDefinition_ORIG(double radiusX, double radiusY, double distFromPrevRing, int numSegmentsPhi, bool mergeNormalWithPrevIfSoft)
        {
            this.RingType = TubeRingType_ORIG.Dome;
            this.RadiusX = radiusX;
            this.RadiusY = radiusY;
            this.DistFromPrevRing = distFromPrevRing;
            this.NumSegmentsPhi = numSegmentsPhi;
            this.MergeNormalWithPrevIfSoft = mergeNormalWithPrevIfSoft;
        }

        #endregion

        #region Public Properties

        public TubeRingType_ORIG RingType
        {
            get;
            private set;
        }

        /// <summary>
        /// This is how far this ring is (in Z) from the previous ring in the tube.  The first ring in the tube ignores this property
        /// </summary>
        public double DistFromPrevRing
        {
            get;
            private set;
        }

        /// <summary>
        /// This isn't the size of a single edge, but the radius of the ring.  So if you're making a cube with length one, you'd want the size
        /// to be .5*sqrt(2)
        /// </summary>
        public double RadiusX
        {
            get;
            private set;
        }
        public double RadiusY
        {
            get;
            private set;
        }

        /// <summary>
        /// This only has meaning if ringtype is dome
        /// </summary>
        public int NumSegmentsPhi
        {
            get;
            private set;
        }

        /// <summary>
        /// This is only looked at if doing soft sides
        /// True: When calculating normals, the prev ring is considered (this would make sense if the angle between the other ring and this one is low)
        /// False: The normals for this ring will be calculated independent of the prev ring (good for a traditional cone)
        /// </summary>
        /// <remarks>
        /// This only affects the normals at the bottom of the ring.  The top of the ring is defined by the next ring's property
        /// 
        /// This has no meaning for the very first ring
        /// 
        /// Examples:
        /// Cylinder - You would want false for both caps, because it's 90 degrees between the end cap and side
        /// Pyramid/Cone - You would want false, because it's greater than 90 degrees between the base cap and side
        /// Rings meant to look seamless - When the angle is low between two rings, that's when this should be true
        /// </remarks>
        public bool MergeNormalWithPrevIfSoft
        {
            get;
            private set;
        }

        #endregion
    }

    #endregion

    //TODO: Make a way to define a ring that isn't a regular polygon (like a rectangle), just takes a list of 2D Point.
    //Call it TubeRingPath.  See Game.Newt.v2.GameItems.ShipParts.ConverterRadiationToEnergyDesign.GetShape -
    //the generic one will be a lot harder because one could be 3 points, the next could be 4.  QuickHull is too complex,
    //but need something like that to see which points should make triangles from
    #region class: TubeRingRegularPolygon

    public class TubeRingRegularPolygon : TubeRingBase
    {
        #region Constructor

        public TubeRingRegularPolygon(double distFromPrevRing, bool mergeNormalWithPrevIfSoft, double radiusX, double radiusY, bool isClosedIfEndCap)
            : base(distFromPrevRing, mergeNormalWithPrevIfSoft)
        {
            this.RadiusX = radiusX;
            this.RadiusY = radiusY;
            this.IsClosedIfEndCap = isClosedIfEndCap;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// This isn't the size of a single edge, but the radius of the ring.  So if you're making a cube with length one, you'd want the size
        /// to be .5*sqrt(2)
        /// </summary>
        public double RadiusX
        {
            get;
            private set;
        }
        public double RadiusY
        {
            get;
            private set;
        }

        /// <summary>
        /// This property only has meaning when this ring is the first or last in the list.
        /// True: If this is an end cap, a disc is created to seal up the hull (like a lid on a cylinder).
        /// False: If this is an end cap, the space is left open (like an open bucket)
        /// </summary>
        public bool IsClosedIfEndCap
        {
            get;
            private set;
        }

        #endregion
    }

    #endregion
    #region class: TubeRingPoint

    /// <summary>
    /// This will end the tube in a point (like a cone or a pyramid)
    /// NOTE: This is only valid if this is the first or last ring in the list
    /// NOTE: This must be tied to a ring
    /// </summary>
    /// <remarks>
    /// This must be tied to a ring.  Two points become a line, a point directly to a dome has no meaning.
    /// 
    /// If you want to make a dual cone, you need three items in the list: { TubeRingPoint, TubeRing, TubeRingPoint }
    /// If you want to make an ice cream cone, you also need thee items: { TubeRingPoint, TubeRing, TubeRingDome }
    /// If you want a simple cone or pyramid, you only need two items: { TubeRingPoint, TubeRing }
    /// </remarks>
    public class TubeRingPoint : TubeRingBase
    {
        public TubeRingPoint(double distFromPrevRing, bool mergeNormalWithPrevIfSoft)
            : base(distFromPrevRing, mergeNormalWithPrevIfSoft) { }

        // This doesn't need any extra properties
    }

    #endregion
    #region class: TubeRingDome

    public class TubeRingDome : TubeRingBase
    {
        #region class: PointsSingleton

        private class PointsSingleton
        {
            #region Declaration Section

            private static readonly object _lockStatic = new object();
            private readonly object _lockInstance;

            /// <summary>
            /// The static constructor makes sure that this instance is created only once.  The outside users of this class
            /// call the static property Instance to get this one instance copy.  (then they can use the rest of the instance
            /// methods)
            /// </summary>
            private static PointsSingleton _instance;

            private SortedList<int, Point[]> _points;

            #endregion

            #region Constructor / Instance Property

            /// <summary>
            /// Static constructor.  Called only once before the first time you use my static properties/methods.
            /// </summary>
            static PointsSingleton()
            {
                lock (_lockStatic)
                {
                    // If the instance version of this class hasn't been instantiated yet, then do so
                    if (_instance == null)
                    {
                        _instance = new PointsSingleton();
                    }
                }
            }
            /// <summary>
            /// Instance constructor.  This is called only once by one of the calls from my static constructor.
            /// </summary>
            private PointsSingleton()
            {
                _lockInstance = new object();

                _points = new SortedList<int, Point[]>();
            }

            /// <summary>
            /// This is how you get at my instance.  The act of calling this property guarantees that the static constructor gets called
            /// exactly once (per process?)
            /// </summary>
            public static PointsSingleton Instance
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
                        // NOTE: There is one more than what the passed in
                        Point[] pointsPhi = new Point[numSides + 1];

                        pointsPhi[0] = new Point(1d, 0d);		// along the equator
                        pointsPhi[numSides] = new Point(0d, 1d);		// north pole

                        if (pointsPhi.Length > 2)
                        {
                            // Need to go from 0 to half pi
                            double halfPi = Math.PI * .5d;
                            double deltaPhi = halfPi / pointsPhi.Length;		// there is one more point than numSegmentsPhi

                            for (int cntr = 1; cntr < numSides; cntr++)
                            {
                                double phi = deltaPhi * cntr;		// phi goes from 0 to pi for a full sphere, so start halfway up
                                pointsPhi[cntr] = new Point(Math.Cos(phi), Math.Sin(phi));
                            }
                        }

                        _points.Add(numSides, pointsPhi);
                    }

                    return _points[numSides];
                }
            }

            #endregion
        }

        #endregion

        #region Constructor

        public TubeRingDome(double distFromPrevRing, bool mergeNormalWithPrevIfSoft, int numSegmentsPhi)
            : base(distFromPrevRing, mergeNormalWithPrevIfSoft)
        {
            this.NumSegmentsPhi = numSegmentsPhi;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// This lets you fine tune how many vertical separations there are in the dome (usually just use the same number as horizontal segments)
        /// </summary>
        public int NumSegmentsPhi { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// This returns points for phi going from pi/2 to pi (a full circle goes from 0 to pi, but this class is only a dome)
        /// NOTE: The array will hold numSides + 1 elements (because 1 side requires 2 ends)
        /// </summary>
        public Point[] GetUnitPointsPhi(int numSides)
        {
            return PointsSingleton.Instance.GetPoints(numSides);
        }

        #endregion
    }

    #endregion
    #region class: TubeRingBase

    public abstract class TubeRingBase
    {
        protected TubeRingBase(double distFromPrevRing, bool mergeNormalWithPrevIfSoft)
        {
            this.DistFromPrevRing = distFromPrevRing;
            this.MergeNormalWithPrevIfSoft = mergeNormalWithPrevIfSoft;
        }

        /// <summary>
        /// This is how far this ring is (in Z) from the previous ring in the tube.  The first ring in the tube ignores this property
        /// </summary>
        public double DistFromPrevRing { get; private set; }

        /// <summary>
        /// This is only looked at if doing soft sides
        /// True: When calculating normals, the prev ring is considered (this would make sense if the angle between the other ring and this one is low)
        /// False: The normals for this ring will be calculated independent of the prev ring (good for a traditional cone)
        /// </summary>
        /// <remarks>
        /// This only affects the normals at the bottom of the ring.  The top of the ring is defined by the next ring's property
        /// 
        /// This has no meaning for the very first ring
        /// 
        /// Examples:
        /// Cylinder - You would want false for both caps, because it's 90 degrees between the end cap and side
        /// Pyramid/Cone - You would want false, because it's greater than 90 degrees between the base cap and side
        /// Rings meant to look seamless - When the angle is low between two rings, that's when this should be true
        /// </remarks>
        public bool MergeNormalWithPrevIfSoft { get; private set; }

        #region Public Methods

        /// <summary>
        /// This creates a new list, and resizes all the items to match the new lengths passed in
        /// </summary>
        /// <remarks>
        /// This is made so that rings can just be defined using friendly units (whatever units the author feels like), then
        /// pass through this method to get final values
        /// </remarks>
        public static List<TubeRingBase> FitNewSize(List<TubeRingBase> rings, double radiusX, double radiusY, double length)
        {
            #region get sizes of the list passed in

            double origLength = rings.Skip(1).Sum(o => o.DistFromPrevRing);

            Tuple<double, double>[] origRadii = rings.
                Where(o => o is TubeRingRegularPolygon).
                Select(o => (TubeRingRegularPolygon)o).
                Select(o => Tuple.Create(o.RadiusX, o.RadiusY)).
                ToArray();

            double origRadX = origRadii.Max(o => o.Item1);
            double origRadY = origRadii.Max(o => o.Item2);

            #endregion

            List<TubeRingBase> retVal = new List<TubeRingBase>();

            for (int cntr = 0; cntr < rings.Count; cntr++)
            {
                double distance = 0d;
                if (cntr > 0)
                {
                    distance = (rings[cntr].DistFromPrevRing / origLength) * length;
                }

                if (rings[cntr] is TubeRingDome)
                {
                    #region dome

                    TubeRingDome dome = (TubeRingDome)rings[cntr];

                    retVal.Add(new TubeRingDome(distance, dome.MergeNormalWithPrevIfSoft, dome.NumSegmentsPhi));

                    #endregion
                }
                else if (rings[cntr] is TubeRingPoint)
                {
                    #region point

                    TubeRingPoint point = (TubeRingPoint)rings[cntr];

                    retVal.Add(new TubeRingPoint(distance, point.MergeNormalWithPrevIfSoft));

                    #endregion
                }
                else if (rings[cntr] is TubeRingRegularPolygon)
                {
                    #region regular polygon

                    TubeRingRegularPolygon poly = (TubeRingRegularPolygon)rings[cntr];

                    retVal.Add(new TubeRingRegularPolygon(distance, poly.MergeNormalWithPrevIfSoft,
                        (poly.RadiusX / origRadX) * radiusX,
                        (poly.RadiusY / origRadY) * radiusY,
                        poly.IsClosedIfEndCap));

                    #endregion
                }
                else
                {
                    throw new ApplicationException("Unknown type of ring: " + rings[cntr].GetType().ToString());
                }
            }

            return retVal;
        }

        public static double GetTotalHeight(List<TubeRingBase> rings)
        {
            double retVal = 0d;

            for (int cntr = 1; cntr < rings.Count; cntr++)     // the first ring's distance from prev ring is ignored
            {
                // I had a case where I wanted to make an arrow where the end cap comes backward a bit
                //if (rings[cntr].DistFromPrevRing <= 0)
                //{
                //    throw new ArgumentException("DistFromPrevRing must be positive: " + rings[cntr].DistFromPrevRing.ToString());
                //}

                retVal += rings[cntr].DistFromPrevRing;
            }

            return retVal;
        }

        #endregion
    }

    #endregion

    #endregion

    #region bitmap custom

    #region interface: IBitmapCustom

    //NOTE: The classes that implement this should be threadsafe
    //TODO: May want to add methods to set colors, and to populate an arbitrary BitmapSource with the modified pixels - but do so in a treadsafe way (returning a new IBitmapCustom)
    public interface IBitmapCustom
    {
        /// <summary>
        /// This gets the color of a single pixel
        /// NOTE: If the request is outside the bounds of the bitmap, a default color is returned, no exception is thrown
        /// </summary>
        Color GetColor(int x, int y);
        byte[] GetColor_Byte(int x, int y);

        /// <summary>
        /// This returns a rectangle of colors
        /// NOTE: If the request is outside the bounds of the bitmap, a default color is returned, no exception is thrown
        /// </summary>
        /// <remarks>
        /// I was debating whether to return a 1D array or 2D, and read that 2D arrays have slower performance.  So to get
        /// a cell out of the array, it's:
        ///    color[x + (y * width)]
        /// </remarks>
        Color[] GetColors(int x, int y, int width, int height);
        byte[][] GetColors_Byte(int x, int y, int width, int height);

        /// <summary>
        /// This returns the colors of the whole image
        /// </summary>
        Color[] GetColors();
        /// <summary>
        /// This returns the entire image as byte arrays (each array is length 4: A,R,G,B).
        /// This is faster than converting to the color struct
        /// </summary>
        byte[][] GetColors_Byte();

        int Width { get; }
        int Height { get; }
    }

    #endregion
    #region class: BitmapCustomCachedColors

    /// <summary>
    /// This caches the color array in the constructor.  This is slowest, and should only be used if you need as the color struct
    /// </summary>
    public class BitmapCustomCachedColors : IBitmapCustom
    {
        #region Declaration Section

        private readonly int _width;
        private readonly int _height;

        // only one of these two get cached
        private readonly byte[][] _bytes;
        private readonly Color[] _colors;

        // both of these get cached
        private readonly Color _outOfBoundsColor;
        private readonly byte[] _outOfBoundsBytes;

        #endregion

        #region Constructor

        /// <param name="cacheColors">
        /// Set this based on which types of methods will be used
        /// True: Cache colors
        /// False: Cache bytes
        /// </param>
        public BitmapCustomCachedColors(BitmapStreamInfo info, bool cacheColors)
        {
            _width = info.Width;
            _height = info.Height;
            _outOfBoundsColor = info.OutOfBoundsColor;
            _outOfBoundsBytes = info.OutOfBoundsBytes;

            if (cacheColors)
            {
                _colors = new BitmapCustomCachedBytes(info).GetColors();
                _bytes = null;
            }
            else
            {
                _bytes = new BitmapCustomCachedBytes(info).GetColors_Byte();
                _colors = null;
            }
        }

        #endregion

        #region IBitmapCustom Members

        public Color GetColor(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                return _outOfBoundsColor;
            }
            else
            {
                if (_colors != null)
                {
                    return _colors[x + (y * _width)];
                }
                else
                {
                    byte[] retVal = _bytes[x + (y * _width)];
                    return Color.FromArgb(retVal[0], retVal[1], retVal[2], retVal[3]);
                }
            }
        }
        public byte[] GetColor_Byte(int x, int y)
        {
            if (x < 0 || x >= _width || y < 0 || y >= _height)
            {
                return _outOfBoundsBytes;
            }
            else
            {
                if (_bytes != null)
                {
                    return _bytes[x + (y * _width)];
                }
                else
                {
                    Color retVal = _colors[x + (y * _width)];
                    return new[] { retVal.A, retVal.R, retVal.G, retVal.B };
                }
            }
        }

        public Color[] GetColors(int x, int y, int width, int height)
        {
            if (x == 0 && y == 0 && width == _width && height == _height)
            {
                //  Just return the array (much faster than building a new one, but it assumes that they don't try to manipulate the colors)
                if (_colors != null)
                {
                    return _colors;
                }
                else
                {
                    return GetColors();     // let this method do the loop
                }
            }

            Color[] retVal = new Color[width * height];

            //NOTE: Copying code for speed reasons

            int yOffsetLeft, yOffsetRight;

            if (x < 0 || x + width >= _width || y < 0 || y + height >= _height)
            {
                #region Some out of bounds

                // copy the logic to avoid an if at every color
                if (_colors != null)
                {
                    #region direct

                    int x2, y2;

                    for (int y1 = 0; y1 < height; y1++)
                    {
                        y2 = y + y1;

                        yOffsetLeft = y1 * width;		// offset into the return array
                        yOffsetRight = y2 * _width;		// offset into _colors array

                        for (int x1 = 0; x1 < width; x1++)
                        {
                            x2 = x + x1;

                            if (x2 < 0 || x2 >= _width || y2 < 0 || y2 >= _height)
                            {
                                retVal[x1 + yOffsetLeft] = _outOfBoundsColor;
                            }
                            else
                            {
                                retVal[x1 + yOffsetLeft] = _colors[x2 + yOffsetRight];
                            }
                        }
                    }

                    #endregion
                }
                else
                {
                    #region bytes -> color

                    int x2, y2;

                    for (int y1 = 0; y1 < height; y1++)
                    {
                        y2 = y + y1;

                        yOffsetLeft = y1 * width;		// offset into the return array
                        yOffsetRight = y2 * _width;		// offset into _colors array

                        for (int x1 = 0; x1 < width; x1++)
                        {
                            x2 = x + x1;

                            if (x2 < 0 || x2 >= _width || y2 < 0 || y2 >= _height)
                            {
                                retVal[x1 + yOffsetLeft] = _outOfBoundsColor;
                            }
                            else
                            {
                                byte[] bytes = _bytes[x2 + yOffsetRight];
                                retVal[x1 + yOffsetLeft] = Color.FromArgb(bytes[0], bytes[1], bytes[2], bytes[3]);
                            }
                        }
                    }

                    #endregion
                }

                #endregion
            }
            else
            {
                #region All in bounds

                // copy the logic to avoid an if at every color
                if (_colors != null)
                {
                    #region direct

                    for (int y1 = 0; y1 < height; y1++)
                    {
                        yOffsetLeft = y1 * width;		// offset into the return array
                        yOffsetRight = (y + y1) * _width;		// offset into _colors array

                        for (int x1 = 0; x1 < width; x1++)
                        {
                            retVal[x1 + yOffsetLeft] = _colors[x + x1 + yOffsetRight];
                        }
                    }

                    #endregion
                }
                else
                {
                    #region bytes -> color

                    for (int y1 = 0; y1 < height; y1++)
                    {
                        yOffsetLeft = y1 * width;		// offset into the return array
                        yOffsetRight = (y + y1) * _width;		// offset into _colors array

                        for (int x1 = 0; x1 < width; x1++)
                        {
                            byte[] bytes = _bytes[x + x1 + yOffsetRight];
                            retVal[x1 + yOffsetLeft] = Color.FromArgb(bytes[0], bytes[1], bytes[2], bytes[3]);
                        }
                    }

                    #endregion
                }

                #endregion
            }

            return retVal;
        }
        public byte[][] GetColors_Byte(int x, int y, int width, int height)
        {
            if (x == 0 && y == 0 && width == _width && height == _height)
            {
                //  Just return the array (much faster than building a new one, but it assumes that they don't try to manipulate the colors)
                if (_bytes != null)
                {
                    return _bytes;
                }
                else
                {
                    return GetColors_Byte();     // let this method do the loop
                }
            }

            byte[][] retVal = new byte[width * height][];

            //NOTE: Copying code for speed reasons

            int yOffsetLeft, yOffsetRight;

            if (x < 0 || x + width >= _width || y < 0 || y + height >= _height)
            {
                #region Some out of bounds

                // copy the logic to avoid an if at every color
                if (_bytes != null)
                {
                    #region direct

                    int x2, y2;

                    for (int y1 = 0; y1 < height; y1++)
                    {
                        y2 = y + y1;

                        yOffsetLeft = y1 * width;		// offset into the return array
                        yOffsetRight = y2 * _width;		// offset into _colors array

                        for (int x1 = 0; x1 < width; x1++)
                        {
                            x2 = x + x1;

                            if (x2 < 0 || x2 >= _width || y2 < 0 || y2 >= _height)
                            {
                                retVal[x1 + yOffsetLeft] = _outOfBoundsBytes;
                            }
                            else
                            {
                                retVal[x1 + yOffsetLeft] = _bytes[x2 + yOffsetRight];
                            }
                        }
                    }

                    #endregion
                }
                else
                {
                    #region color -> bytes

                    int x2, y2;

                    for (int y1 = 0; y1 < height; y1++)
                    {
                        y2 = y + y1;

                        yOffsetLeft = y1 * width;		// offset into the return array
                        yOffsetRight = y2 * _width;		// offset into _colors array

                        for (int x1 = 0; x1 < width; x1++)
                        {
                            x2 = x + x1;

                            if (x2 < 0 || x2 >= _width || y2 < 0 || y2 >= _height)
                            {
                                retVal[x1 + yOffsetLeft] = _outOfBoundsBytes;
                            }
                            else
                            {
                                Color color = _colors[x2 + yOffsetRight];
                                retVal[x1 + yOffsetLeft] = new[] { color.A, color.R, color.G, color.B };
                            }
                        }
                    }

                    #endregion
                }

                #endregion
            }
            else
            {
                #region All in bounds

                // copy the logic to avoid an if at every color
                if (_bytes != null)
                {
                    #region direct

                    for (int y1 = 0; y1 < height; y1++)
                    {
                        yOffsetLeft = y1 * width;		// offset into the return array
                        yOffsetRight = (y + y1) * _width;		// offset into _colors array

                        for (int x1 = 0; x1 < width; x1++)
                        {
                            retVal[x1 + yOffsetLeft] = _bytes[x + x1 + yOffsetRight];
                        }
                    }

                    #endregion
                }
                else
                {
                    #region color -> bytes

                    for (int y1 = 0; y1 < height; y1++)
                    {
                        yOffsetLeft = y1 * width;		// offset into the return array
                        yOffsetRight = (y + y1) * _width;		// offset into _colors array

                        for (int x1 = 0; x1 < width; x1++)
                        {
                            Color color = _colors[x + x1 + yOffsetRight];
                            retVal[x1 + yOffsetLeft] = new[] { color.A, color.R, color.G, color.B };
                        }
                    }

                    #endregion
                }

                #endregion
            }

            return retVal;
        }

        public Color[] GetColors()
        {
            //  Just return the array (much faster than building a new one, but it assumes that they don't try to manipulate the colors)
            if (_colors != null)
            {
                return _colors;
            }
            else
            {
                return _bytes.
                    Select(o => Color.FromArgb(o[0], o[1], o[2], o[3])).
                    ToArray();
            }
        }
        public byte[][] GetColors_Byte()
        {
            //  Just return the array (much faster than building a new one, but it assumes that they don't try to manipulate the colors)
            if (_bytes != null)
            {
                return _bytes;
            }
            else
            {
                return _colors.
                    Select(o => new[] { o.A, o.R, o.G, o.B }).
                    ToArray();
            }
        }

        public int Width { get { return _width; } }
        public int Height { get { return _height; } }

        #endregion
    }

    #endregion
    #region class: BitmapCustomCachedBytes

    /// <summary>
    /// This stores the stream as they come from the file.  It is more efficient if you want the colors in a format other than
    /// a color struct
    /// </summary>
    /// <remarks>
    /// Since each get from this class will need to reevaluate the original bytes, the fastest usage of this class is to do a single
    /// conversion to your final format (convolution, byte[][])
    /// </remarks>
    public class BitmapCustomCachedBytes : IBitmapCustom
    {
        #region Declaration Section

        private readonly BitmapStreamInfo _info;

        #endregion

        #region Constructor

        public BitmapCustomCachedBytes(BitmapStreamInfo info)
        {
            _info = info;
        }

        #endregion

        #region IBitmapCustom Members

        public int Width { get { return _info.Width; } }
        public int Height { get { return _info.Height; } }

        public Color GetColor(int x, int y)
        {
            byte[] bytes = _info.GetColorBytes(x, y);
            return Color.FromArgb(bytes[0], bytes[1], bytes[2], bytes[3]);
        }
        public byte[] GetColor_Byte(int x, int y)
        {
            return _info.GetColorBytes(x, y);
        }

        public Color[] GetColors(int x, int y, int width, int height)
        {
            Color[] retVal = new Color[width * height];

            for (int y1 = 0; y1 < height; y1++)
            {
                int yOffset = y1 * width;		// offset into the return array

                for (int x1 = 0; x1 < width; x1++)
                {
                    byte[] color = _info.GetColorBytes(x1, y1);
                    retVal[x1 + yOffset] = Color.FromArgb(color[0], color[1], color[2], color[3]);
                }
            }

            return retVal;
        }
        public byte[][] GetColors_Byte(int x, int y, int width, int height)
        {
            byte[][] retVal = new byte[width * height][];

            for (int y1 = 0; y1 < height; y1++)
            {
                int yOffset = y1 * width;		// offset into the return array

                for (int x1 = 0; x1 < width; x1++)
                {
                    retVal[x1 + yOffset] = _info.GetColorBytes(x1, y1);
                }
            }

            return retVal;
        }

        public Color[] GetColors()
        {
            return GetColors(0, 0, _info.Width, _info.Height);
        }
        public byte[][] GetColors_Byte()
        {
            return GetColors_Byte(0, 0, _info.Width, _info.Height);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// This converts into a gray scale convolution
        /// </summary>
        public Convolution2D ToConvolution(double scaleTo = 255d, string description = "")
        {
            double[] values = new double[_info.Width * _info.Height];

            double scale = scaleTo / 255d;

            for (int y = 0; y < _info.Height; y++)
            {
                int yOffset = y * _info.Width;		// offset into the return array

                for (int x = 0; x < _info.Width; x++)
                {
                    byte[] color = _info.GetColorBytes(x, y);

                    double percent = color[0] / 255d;
                    double gray = UtilityWPF.ConvertToGray(color[1], color[2], color[3]);

                    values[x + yOffset] = percent * gray * scale;
                }
            }

            return new Convolution2D(values, _info.Width, _info.Height, false, description: description);
        }
        /// <summary>
        /// This converts into three convolutions.  One of R, one of G, one of B
        /// </summary>
        public Tuple<Convolution2D, Convolution2D, Convolution2D> ToConvolution_RGB(double scaleTo, string description)
        {
            double[] r = new double[_info.Width * _info.Height];
            double[] g = new double[_info.Width * _info.Height];
            double[] b = new double[_info.Width * _info.Height];

            double scale = scaleTo / 255d;

            for (int y = 0; y < _info.Height; y++)
            {
                int yOffset = y * _info.Width;		// offset into the return array

                for (int x = 0; x < _info.Width; x++)
                {
                    byte[] color = _info.GetColorBytes(x, y);

                    double percent = color[0] / 255d;

                    r[x + yOffset] = percent * color[1] * scale;
                    g[x + yOffset] = percent * color[2] * scale;
                    b[x + yOffset] = percent * color[3] * scale;
                }
            }

            return Tuple.Create(
                new Convolution2D(r, _info.Width, _info.Height, false, description: description),
                new Convolution2D(g, _info.Width, _info.Height, false, description: description),
                new Convolution2D(b, _info.Width, _info.Height, false, description: description));
        }

        #endregion
    }

    #endregion
    #region class: BitmapStreamInfo

    public class BitmapStreamInfo
    {
        #region enum: SupportedPixelFormats

        private enum SupportedPixelFormats
        {
            Pbgra32,
            Bgr32,
            NotSupported,
        }

        #endregion

        #region Constructor

        public BitmapStreamInfo(byte[] bytes, int width, int height, int stride, PixelFormat format, Color outOfBoundsColor)
        {
            this.Bytes = bytes;
            this.Width = width;
            this.Height = height;
            this.Stride = stride;
            this.Format = format;
            this.OutOfBoundsColor = outOfBoundsColor;
            this.OutOfBoundsBytes = new[] { outOfBoundsColor.A, outOfBoundsColor.R, outOfBoundsColor.G, outOfBoundsColor.B };

            // GetPixel is likely to get called a lot, and it's slightly cheaper to do a switch than cascading if
            if (this.Format == PixelFormats.Pbgra32)
            {
                _formatEnum = SupportedPixelFormats.Pbgra32;
            }
            else if (this.Format == PixelFormats.Bgr32)
            {
                _formatEnum = SupportedPixelFormats.Bgr32;
            }
            else
            {
                _formatEnum = SupportedPixelFormats.NotSupported;
            }
        }

        #endregion

        public readonly byte[] Bytes;

        public readonly int Width;
        public readonly int Height;

        public readonly int Stride;
        public readonly PixelFormat Format;
        private readonly SupportedPixelFormats _formatEnum;

        public readonly Color OutOfBoundsColor;
        public readonly byte[] OutOfBoundsBytes;

        #region Public Methods

        public byte[] GetColorBytes(int x, int y)
        {
            if (x < 0 || x >= this.Width || y < 0 || y >= this.Height)
            {
                return this.OutOfBoundsBytes;
            }

            int offset;

            //NOTE: Instead of coding against a ton of different formats, just use FormatConvertedBitmap to convert into one of these (look in UtilityWPF.ConvertToColorArray for example)
            switch (_formatEnum)
            {
                case SupportedPixelFormats.Pbgra32:
                    #region Pbgra32

                    offset = (y * this.Stride) + (x * 4);		// this is assuming that bitmap.Format.BitsPerPixel is 32, which would be four bytes per pixel

                    return new[] { this.Bytes[offset + 3], this.Bytes[offset + 2], this.Bytes[offset + 1], this.Bytes[offset + 0] };

                #endregion

                case SupportedPixelFormats.Bgr32:
                    #region Bgr32

                    offset = (y * this.Stride) + (x * 4);		// this is assuming that bitmap.Format.BitsPerPixel is 32, which would be four bytes per pixel

                    return new byte[] { 255, this.Bytes[offset + 2], this.Bytes[offset + 1], this.Bytes[offset + 0] };

                #endregion

                default:
                    throw new ApplicationException("TODO: Handle more pixel formats: " + this.Format.ToString());
            }
        }

        #endregion
    }

    #endregion

    #endregion

    #region class: MyHitTestResult

    // This was copied from the ship editor
    public class MyHitTestResult
    {
        #region Constructor

        public MyHitTestResult(RayMeshGeometry3DHitTestResult modelHit)
        {
            this.ModelHit = modelHit;
        }

        #endregion

        public readonly RayMeshGeometry3DHitTestResult ModelHit;

        /// <summary>
        /// This is a helper property that returns the actual hit point
        /// </summary>
        public Point3D Point
        {
            get
            {
                if (this.ModelHit.VisualHit.Transform != null)
                {
                    return this.ModelHit.VisualHit.Transform.Transform(this.ModelHit.PointHit);
                }
                else
                {
                    return this.ModelHit.PointHit;
                }
            }
        }

        public double GetDistanceFromPoint(Point3D point)
        {
            return (point - this.Point).Length;
        }
    }

    #endregion

    #region struct: ColorHSV

    public struct ColorHSV
    {
        #region Constructor

        public ColorHSV(double h, double s, double v)
            : this(255, h, s, v) { }
        public ColorHSV(byte a, double h, double s, double v)
        {
            this.A = a;
            this.H = h;
            this.S = s;
            this.V = v;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Alpha: 0 to 255
        /// </summary>
        public readonly byte A;

        /// <summary>
        /// Hue: 0 to 360
        /// </summary>
        public readonly double H;

        /// <summary>
        /// Saturation: 0 to 100
        /// </summary>
        public readonly double S;

        /// <summary>
        /// Value: 0 to 100
        /// </summary>
        public readonly double V;

        #endregion

        #region Public Methods

        public string ToHex(bool includeAlpha = true, bool includePound = true)
        {
            return UtilityWPF.ColorToHex(ToRGB(), includeAlpha, includePound);
        }

        public Color ToRGB()
        {
            return UtilityWPF.HSVtoRGB(this.A, this.H, this.S, this.V);
        }

        public override string ToString()
        {
            return string.Format("A {1}{0}H {2}{0}S {3}{0}V {4}", "  |  ", GetFormatedNumber(this.A), GetFormatedNumber(this.H), GetFormatedNumber(this.S), GetFormatedNumber(this.V));
        }

        #endregion

        #region Private Methods

        private static string GetFormatedNumber(double value)
        {
            return Math.Round(value).
                ToString().
                PadLeft(3, ' ');     // padding left so columns line up (when viewing a list of colors)
        }

        #endregion
    }

    #endregion
}
