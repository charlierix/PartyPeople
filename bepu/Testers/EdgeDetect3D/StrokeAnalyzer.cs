using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF.Viewers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers.EdgeDetect3D
{
    public static class StrokeAnalyzer
    {
        public static void Stroke(Point3D[] points, EdgeBackgroundWorker.WorkerResponse objects)
        {

            // Convert the raw points into something more uniform
            //GetUniformStrokePoints3(points);
            points = StrokeCleaner.CleanPath(points);





        }

        private static void GetUniformStrokePoints1(Point3D[] points)
        {
            Point3D center = Math3D.GetCenter(points);

            Point3D[] centered_points = points.
                Select(o => (o - center).ToPoint()).
                ToArray();

            var segments = BezierUtil.GetBezierSegments(centered_points);


            // This is uniform points, but also looses too much (points form too much of an average line)
            var test = new BezierSegment3D_wpf(centered_points[0], centered_points[^1], centered_points.Skip(1).Take(centered_points.Length - 2).Select(o => o).ToArray());
            var cleanedPoints2 = BezierUtil.GetPoints(144, test);



            // TODO: This is creating way too many points.  It was designed against far fewer segments
            //Point3D[] cleaned_points = BezierUtil.GetPoints(1, segments);



            //Smoothing Algorithms:
            //  Consider applying smoothing algorithms to your data.These algorithms can help reduce noise and create a smoother trajectory.Examples include moving average, Savitzky-Golay filter, or cubic spline interpolation.
            //
            //Downsampling:
            //  If you have a large number of points, downsampling can help. Remove some points while preserving the overall shape of the curve. You can use techniques like uniform sampling or random sampling.
            //
            //Bezier Curves:
            //  Fit a Bezier curve to your points.Bezier curves provide smooth interpolation between control points. You can adjust the degree of the curve to control the level of smoothness.
            //
            //B - spline Interpolation:
            //  B - spline interpolation is another method for creating smooth curves.It uses a set of control points and basis functions to generate a smooth curve that passes through those points.










            var window = new Debug3DWindow()
            {
                Title = "Stroke Points",
            };

            var sizes = Debug3DWindow.GetDrawSizes(centered_points);

            window.AddDots(centered_points, sizes.dot / 2, Colors.SaddleBrown);

            //window.AddDots(cleaned_points, sizes.dot, Colors.LimeGreen);
            window.AddDots(cleanedPoints2, sizes.dot, Colors.LimeGreen);

            window.Show();
        }
        private static void GetUniformStrokePoints2(Point3D[] points)
        {
            // Get the aabb of the points

            // Figure out a good average distance

            // Get distance squared between each point

            // Try to throw out points that are really close to each other

            // ---------------------------

            // Try to get subsets of lower resolution, but still keeping the ideal shape




        }

    }
}
