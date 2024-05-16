using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF.Viewers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers.EdgeDetect3D
{
    /// <summary>
    /// Takes raw points from mouse move events and returns a subset of those points
    /// </summary>
    /// <remarks>
    /// This tries to have roughly uniform line segment lengths while still keeping the shape
    /// of the original path
    /// </remarks>
    public static class StrokeCleaner
    {
        private const bool SHOULD_DRAW = false;

        public static Point3D[] CleanPath(Point3D[] points)
        {
            points = RemoveDupes(points);

            Draw(Enumerable.Range(0, points.Length), points, "passed in");

            if (points.Length <= 72 * 1.25)
                return points;

            // Take the first N samples from the list.  Doing quite a few to get an even spread
            var indices = Initial(points.Length, 37);        // 37 points makes 36 segments
            Draw(indices, points, "initial");

            //for (int i = 0; i < 1; i++)     // each iteration doubles the amount of segments
            //{
            // Split each segment in two, but be more selective in which points to use
            indices = Split_Path(indices, points);
            Draw(indices, points, $"split {1}");
            //}

            Point3D[] retVal = new Point3D[indices.Length];

            for (int i = 0; i < retVal.Length; i++)
                retVal[i] = points[indices[i]];

            return retVal;
        }

        private static Point3D[] RemoveDupes(Point3D[] points)
        {
            var retVal = new List<Point3D>(points);

            int index = 0;

            while (index < retVal.Count - 1)
            {
                if (points[index].IsNearValue(points[index + 1]))
                    retVal.RemoveAt(index);
                else
                    index++;
            }

            return retVal.ToArray();
        }

        private static int[] Initial(int count, int initial_count)
        {
            int[] retVal = new int[initial_count];

            retVal[0] = 0;

            double step = (double)count / (double)initial_count;

            for (int i = 1; i < initial_count - 1; i++)
                retVal[i] = (i * step).ToInt_Round();

            retVal[^1] = count - 1;

            return retVal;
        }

        private static int[] Split_Path(int[] indices, Point3D[] points)
        {
            var retVal = new List<int>();

            for (int i = 0; i < indices.Length - 1; i++)
            {
                retVal.Add(indices[i]);

                int? new_index = Split_Segment(indices[i], indices[i + 1], points);
                if (new_index != null)
                    retVal.Add(new_index.Value);
            }

            retVal.Add(indices[^1]);

            return retVal.ToArray();
        }

        private static int? Split_Segment(int index_from, int index_to, Point3D[] points)
        {
            int gap = index_to - index_from;

            if (gap < 2)
                return null;        // no splits possible

            if (gap == 2)
                return index_from + 1;      // there's only one choice

            int[] candidates = GetCandidates(index_from, index_to);

            // Return the index that has the lowest error, which sum of distance sqr between raw points and the two
            // line segments from-mid-to
            return candidates.
                Select(o => new
                {
                    index = o,
                    error = GetSumDistSqr(index_from, index_to, o, points),
                }).
                OrderBy(o => o.error).
                FirstOrDefault()?.
                index;
        }

        private static int[] GetCandidates(int index_from, int index_to)
        {
            // Choose the mid point
            int index_mid = index_from + ((index_to - index_from) / 2d).ToInt_Round();

            // Choose a few between from and mid
            int[] pre = GetCandidates_SubSegment(index_from + 1, index_mid - 1, 2);

            // Choose a few between mid and to
            int[] post = GetCandidates_SubSegment(index_mid + 1, index_to - 1, 2);

            // Create a combined list
            // NOTE: pre or post could be empty
            int[] retVal = new int[pre.Length + 1 + post.Length];

            for (int i = 0; i < pre.Length; i++)
                retVal[i] = pre[i];

            retVal[pre.Length] = index_mid;

            for (int i = 0; i < post.Length; i++)
                retVal[pre.Length + 1 + i] = post[i];

            return retVal;
        }
        private static int[] GetCandidates_SubSegment(int index_from, int index_to, int count)
        {
            if (index_from >= index_to)
                return [];

            return UtilityCore.RandomRange(index_from, index_to - index_from, count).
                OrderBy().
                ToArray();
        }

        // Get the sum of dist squared between each raw point and the line segments
        private static double GetSumDistSqr(int index_from, int index_to, int index_mid, Point3D[] points)
        {
            double retVal = 0;

            for (int i = index_from + 1; i < index_mid; i++)
            {
                Point3D point_on_segment = Math3D.GetClosestPoint_LineSegment_Point(points[index_from], points[index_mid], points[i]);
                retVal += (point_on_segment - points[i]).LengthSquared;
            }

            for (int i = index_mid + 1; i < index_to; i++)
            {
                Point3D point_on_segment = Math3D.GetClosestPoint_LineSegment_Point(points[index_mid], points[index_to], points[i]);
                retVal += (point_on_segment - points[i]).LengthSquared;
            }

            return retVal;
        }

        private static void Draw(IEnumerable<int> end_points, Point3D[] points, string title)
        {
            if (!SHOULD_DRAW)
                return;

            Point3D center = Math3D.GetCenter(points);

            Point3D[] centered_points = points.
                Select(o => (o - center).ToPoint()).
                ToArray();

            var window = new Debug3DWindow()
            {
                Title = title,
            };

            var sizes = Debug3DWindow.GetDrawSizes(centered_points);

            window.AddDots(end_points.Select(o => centered_points[o]), sizes.dot, Colors.DarkOliveGreen);
            window.AddLines(end_points.Select(o => centered_points[o]), sizes.line, Colors.DarkSeaGreen);

            window.AddDots(centered_points, sizes.dot / 2, Colors.PaleGoldenrod);

            window.AddText($"segments: {end_points.Count() - 1}");
            window.AddText($"total points: {points.Length}");

            window.Show();
        }
    }
}
