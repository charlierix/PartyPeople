using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace Game.Bepu.Testers
{
    public partial class BasicDrawingTests : Window
    {
        public BasicDrawingTests()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;
        }

        private void Torus_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new Debug3DWindow();

                var sizes = Debug3DWindow.GetDrawSizes(1);

                window.AddAxisLines(1, sizes.line);

                var mesh = UtilityWPF.GetTorus(30, 7, 0.2, 1);
                TriangleIndexed_wpf[] triangles = UtilityWPF.GetTrianglesFromMesh(mesh);
                Point3D[] points = triangles[0].AllPoints;
                var lines = TriangleIndexed_wpf.GetUniqueLines(triangles);

                window.AddMesh(mesh, Colors.Gray);
                window.AddLines(lines.Select(o => (points[o.Item1], points[o.Item2])), sizes.line, Colors.White);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TorusArc3_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new Debug3DWindow();

                var sizes = Debug3DWindow.GetDrawSizes(1);

                window.AddText($"from angle: {Math.Round(trkArcFrom.Value)}", color: "00C");
                window.AddText($"to angle: {Math.Round(trkArcTo.Value)}", color: "00C");

                window.AddAxisLines(1, sizes.line);

                // This should be a merge of TorusArc1_Click and UtilityWPF.GetTorus (not worrying about end caps yet)
                var mesh = GetTorusArc3(30, 7, 0.2, 1, trkArcFrom.Value, trkArcTo.Value, window, sizes.dot, sizes.line);
                window.AddMesh(mesh, Colors.Gray);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static MeshGeometry3D GetTorusArc3(int spineSegments_fullCircle, int fleshSegments, double innerRadius, double outerRadius, double fromAngle, double toAngle, Debug3DWindow window, double size_dot, double size_line)
        {
            if (toAngle < fromAngle)
                return GetTorusArc3(spineSegments_fullCircle, fleshSegments, innerRadius, outerRadius, toAngle, fromAngle, window, size_dot, size_line);

            MeshGeometry3D retVal = new MeshGeometry3D();

            // The spine is the circle around the hole in the
            // torus, the flesh is a set of circles around the
            // spine.
            int cp = 0; // Index of last added point.

            double percent_circle = Math.Abs(toAngle - fromAngle) / 360d;
            int actual_spine_segments = Math.Max(1, (spineSegments_fullCircle * percent_circle).ToInt_Ceiling());

            window.AddText($"% circle: {percent_circle.ToStringSignificantDigits(2)}");
            window.AddText($"spines: {actual_spine_segments}");

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

                    window.AddText3D(cp.ToString(), p, new Vector3D(0, 0, 1), size_dot * 3, Colors.White, false);

                    retVal.Positions.Add(p);
                    retVal.Normals.Add(-fleshVector);
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

            Point3D line_point = (first_point.ToVector3D() * outerRadius).ToPoint();
            window.AddLine(line_point, line_point + new Vector3D(first_point.Y, -first_point.X, 0), size_line, Colors.White);

            line_point = (last_point.ToVector3D() * outerRadius).ToPoint();
            window.AddLine(line_point, line_point + new Vector3D(-last_point.Y, last_point.X, 0), size_line, Colors.White);

            //TODO: Have an option for dome endcaps

            int point_count = retVal.Positions.Count;       // saving this, because the end caps add points

            GetTorusArc3_FlatCap(retVal, 0, fleshSegments - 1, new Vector3D(first_point.Y, -first_point.X, 0));
            GetTorusArc3_FlatCap(retVal, point_count - fleshSegments, point_count - 1, new Vector3D(-last_point.Y, last_point.X, 0));

            //retVal.Freeze();
            return retVal;
        }
        private static void GetTorusArc3_FlatCap(MeshGeometry3D retVal, int index_from, int index_to, Vector3D normal)
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

        private void TorusArc_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new Debug3DWindow();

                var sizes = Debug3DWindow.GetDrawSizes(1);

                window.AddText($"from angle: {Math.Round(trkArcFrom.Value)}", color: "00C");
                window.AddText($"to angle: {Math.Round(trkArcTo.Value)}", color: "00C");

                window.AddAxisLines(1, sizes.line);

                // This should be a merge of TorusArc1_Click and UtilityWPF.GetTorus (not worrying about end caps yet)
                var mesh = UtilityWPF.GetTorusArc(30, 7, 0.2, 1, trkArcFrom.Value, trkArcTo.Value);
                window.AddMesh(mesh, Colors.Gray);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TorusEllipse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new Debug3DWindow();

                var sizes = Debug3DWindow.GetDrawSizes(1);

                window.AddText($"radius x: {Math.Round(trkRadiusX.Value)}", color: "00C");
                window.AddText($"radius y: {Math.Round(trkRadiusY.Value)}", color: "00C");

                window.AddAxisLines(1, sizes.line);

                // This should be a merge of TorusArc1_Click and UtilityWPF.GetTorus (not worrying about end caps yet)
                var mesh = UtilityWPF.GetTorusEllipse(30, 7, 0.2, trkRadiusX.Value, trkRadiusY.Value);
                window.AddMesh(mesh, Colors.Gray);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region INTERMEDIATE

        //private void TorusArc1_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        double from_angle = 0;
        //        double to_angle = 90;
        //        double radius = 1;
        //        int spineSegments = 30;

        //        var window = new Debug3DWindow();

        //        var sizes = Debug3DWindow.GetDrawSizes(1);

        //        window.AddAxisLines(1, sizes.line);

        //        // Figure out a good number of divisions based on spineSegments / circle_percent
        //        double percent_circle = Math.Abs(to_angle - from_angle) / 360d;
        //        window.AddText($"% circle: {percent_circle.ToStringSignificantDigits(2)}");

        //        int actual_spine_segments = Math.Max(1, (spineSegments * percent_circle).ToInt_Ceiling());
        //        window.AddText($"spines: {actual_spine_segments}");


        //        // Draw a point at from and to
        //        double x = radius * Math.Cos(Math1D.DegreesToRadians(from_angle));
        //        double y = radius * Math.Sin(Math1D.DegreesToRadians(from_angle));

        //        window.AddDot(new Point3D(x, y, 0), sizes.dot, Colors.ForestGreen);


        //        x = radius * Math.Cos(Math1D.DegreesToRadians(to_angle));
        //        y = radius * Math.Sin(Math1D.DegreesToRadians(to_angle));

        //        window.AddDot(new Point3D(x, y, 0), sizes.dot, Colors.Firebrick);

        //        // Draw some intermediate points
        //        int count = actual_spine_segments + 1;      // one more point than spines
        //        for (int i = 1; i < count - 1; i++)
        //        {
        //            double angle = UtilityMath.GetScaledValue(from_angle, to_angle, 0, count - 1, i);

        //            x = radius * Math.Cos(Math1D.DegreesToRadians(angle));
        //            y = radius * Math.Sin(Math1D.DegreesToRadians(angle));

        //            window.AddDot(new Point3D(x, y, 0), sizes.dot, Colors.White);
        //        }


        //        // Use dome caps instead of flat



        //        window.Show();
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}
        //private void TorusArc2_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        var window = new Debug3DWindow();

        //        var sizes = Debug3DWindow.GetDrawSizes(1);

        //        window.AddAxisLines(1, sizes.line);

        //        // This should be a merge of TorusArc1_Click and UtilityWPF.GetTorus (not worrying about end caps yet)
        //        var mesh = GetTorusArc2(30, 7, 0.2, 1, 0, 90, window, sizes.dot, sizes.line);
        //        window.AddMesh(mesh, Colors.Gray);

        //        window.Show();
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //}

        //private static MeshGeometry3D GetTorusArc2(int spineSegments_fullCircle, int fleshSegments, double innerRadius, double outerRadius, double fromAngle, double toAngle, Debug3DWindow window, double size_dot, double sizes_line)
        //{
        //    MeshGeometry3D retVal = new MeshGeometry3D();

        //    // The spine is the circle around the hole in the
        //    // torus, the flesh is a set of circles around the
        //    // spine.
        //    int cp = 0; // Index of last added point.


        //    double percent_circle = Math.Abs(toAngle - fromAngle) / 360d;
        //    int actual_spine_segments = Math.Max(1, (spineSegments_fullCircle * percent_circle).ToInt_Ceiling());

        //    window.AddText($"% circle: {percent_circle.ToStringSignificantDigits(2)}");
        //    window.AddText($"spines: {actual_spine_segments}");

        //    double spineAngle_from = UtilityMath.GetScaledValue(fromAngle, toAngle, 0, actual_spine_segments - 1, 0);
        //    double spineAngle_to = UtilityMath.GetScaledValue(fromAngle, toAngle, 0, actual_spine_segments - 1, actual_spine_segments - 1);

        //    window.AddText($"spineAngle_from: {spineAngle_from}");
        //    window.AddText($"spineAngle_to: {spineAngle_to}");


        //    //int count = actual_spine_segments + 1;      // one more point than spines
        //    //for (int i = 1; i < count - 1; i++)
        //    for (int i = 0; i < actual_spine_segments; i++)
        //    {
        //        double spineParam = ((double)i) / ((double)actual_spine_segments);
        //        double spineAngle = UtilityMath.GetScaledValue(fromAngle, toAngle, 0, actual_spine_segments - 1, i);
        //        Vector3D spineVector = new Vector3D(Math.Cos(Math1D.DegreesToRadians(spineAngle)), Math.Sin(Math1D.DegreesToRadians(spineAngle)), 0);

        //        window.AddText($"spineAngle: {spineAngle}", color: "FFF");

        //        window.AddDot(spineVector, size_dot, Colors.Black);


        //        for (int j = 0; j < fleshSegments; j++)
        //        {
        //            double fleshParam = ((double)j) / ((double)fleshSegments);
        //            double fleshAngle = Math.PI * 2 * fleshParam;
        //            Vector3D fleshVector = spineVector * Math.Cos(fleshAngle) + new Vector3D(0, 0, Math.Sin(fleshAngle));
        //            Point3D p = new Point3D(0, 0, 0) + outerRadius * spineVector + innerRadius * fleshVector;

        //            //window.AddDot(p, size_dot, Colors.White);
        //            window.AddText3D(cp.ToString(), p, new Vector3D(0, 0, 1), size_dot * 3, Colors.White, false);

        //            retVal.Positions.Add(p);
        //            retVal.Normals.Add(-fleshVector);
        //            retVal.TextureCoordinates.Add(new Point(spineParam, fleshParam));

        //            // Now add a quad that has it's upper-right corner at the point we just added.
        //            // i.e. cp . cp-1 . cp-1-_fleshSegments . cp-_fleshSegments
        //            int a = cp;
        //            int b = cp - 1;
        //            int c = cp - (int)1 - fleshSegments;
        //            int d = cp - fleshSegments;



        //            // The next two if statements handle the wrapping around of the torus.  For either i = 0 or j = 0
        //            // the created quad references vertices that haven't been created yet.
        //            if (j == 0)
        //            {
        //                b += fleshSegments;
        //                c += fleshSegments;
        //            }

        //            //if (i == 0)
        //            //{
        //            //    c += fleshSegments * actual_spine_segments;
        //            //    d += fleshSegments * actual_spine_segments;
        //            //}

        //            if (i != 0)
        //            {
        //                retVal.TriangleIndices.Add((ushort)a);
        //                retVal.TriangleIndices.Add((ushort)b);
        //                retVal.TriangleIndices.Add((ushort)c);

        //                retVal.TriangleIndices.Add((ushort)a);
        //                retVal.TriangleIndices.Add((ushort)c);
        //                retVal.TriangleIndices.Add((ushort)d);
        //            }

        //            cp++;
        //        }
        //    }

        //    //retVal.Freeze();
        //    return retVal;
        //}

        #endregion
    }
}
