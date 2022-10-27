using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers
{
    public static class TempBezierUtil
    {
        public static PathSnippet[] GetPinchedMapping(BezierUtil.CurvatureSample[] heatmap, int endpoint_count, BezierSegment3D_wpf[] beziers)
        {
            int return_count = ((endpoint_count - 1) * 3) + 1;      // this is kind of arbitrary, but should give a good amount of snippets to play with

            double inc = 1d / return_count;

            var initial = Enumerable.Range(0, return_count).
                Select(o => new
                {
                    from = (double)o / return_count,
                    to = (double)(o + 1) / return_count,
                }).
                Select(o => new PathSnippet()
                {
                    From_Percent_In = o.from,
                    From_Percent_Out = o.from,
                    To_Percent_In = o.to,
                    To_Percent_Out = o.to,
                }).
                ToArray();

            var retVal = initial;

            //for (int i = 0; i < 4; i++)
            for (int i = 0; i < 1; i++)
            {
                //double[] forces = GetForces(retVal, heatmap);
                //retVal = ApplyForces(retVal, forces);

                TestBuckets(retVal, heatmap, beziers);
            }

            return initial;
        }

        #region testbuckets

        private record HeatBoundry
        {
            public BezierUtil.CurvatureSample From { get; init; }
            public BezierUtil.CurvatureSample To { get; init; }

            public int From_Index { get; init; }
            public int To_Index { get; init; }
        }

        private static void TestBuckets(PathSnippet[] snippets, BezierUtil.CurvatureSample[] heatmap, BezierSegment3D_wpf[] beziers)
        {
            DrawTestBucket(snippets, heatmap);

            // get the area inside each snippet
            double[] areas = snippets.
                Select(o => GetPopulation(o, heatmap)).
                ToArray();




        }

        /// <summary>
        /// This gets the area of the graph in the snippet's range
        /// </summary>
        /// <remarks>
        /// Think of the heatmap as a sawtooth line graph
        /// The snippet defines a left and right edge
        /// This function finds the area of the polygon
        /// </remarks>
        private static double GetPopulation(PathSnippet snippet, BezierUtil.CurvatureSample[] heatmap)
        {
            // get left and right
            var edge_left = GetPopulation_Edge(snippet.From_Percent_Out, heatmap);
            var edge_right = GetPopulation_Edge(snippet.To_Percent_Out, heatmap);

            double retVal = 0;

            // add the lerp portions of left and right to the total
            if (edge_left.From_Index != edge_left.To_Index)
            {
                // Need the right portion of the left edge
                double y1 = UtilityMath.LERP(
                    edge_left.From.Dist_From_NegOne,
                    edge_left.To.Dist_From_NegOne,
                    (edge_left.To.Percent_Total - snippet.From_Percent_Out) / (edge_left.To.Percent_Total - edge_left.From.Percent_Total));

                retVal += GetPopulation_Area(snippet.From_Percent_Out, y1, edge_left.To.Percent_Total, edge_left.To.Dist_From_NegOne);
            }

            if (edge_right.From_Index != edge_right.To_Index)
            {
                // Need the left portion of the right edge
                double y2 = UtilityMath.LERP(
                    edge_right.From.Dist_From_NegOne,
                    edge_right.To.Dist_From_NegOne,
                    (snippet.To_Percent_Out - edge_right.From.Percent_Total) / (edge_right.To.Percent_Total - edge_right.From.Percent_Total));

                retVal += GetPopulation_Area(edge_right.From.Percent_Total, edge_right.From.Dist_From_NegOne, snippet.To_Percent_Out, y2);
            }

            // iterate over everything in between, adding the their entire area to the total
            for (int i = edge_left.To_Index; i < edge_right.From_Index; i++)
            {
                retVal += GetPopulation_Area(heatmap[i].Percent_Total, heatmap[i].Dist_From_NegOne, heatmap[i + 1].Percent_Total, heatmap[i + 1].Dist_From_NegOne);
            }

            return retVal;
        }
        private static HeatBoundry GetPopulation_Edge(double percent, BezierUtil.CurvatureSample[] heatmap)
        {
            for (int i = 0; i < heatmap.Length; i++)
            {
                if (heatmap[i].Percent_Total.IsNearValue(percent))
                    return new HeatBoundry()
                    {
                        From = heatmap[i],
                        To = heatmap[i],
                        From_Index = i,
                        To_Index = i,
                    };

                if (i == heatmap.Length - 1)
                    break;

                if (heatmap[i].Percent_Total < percent && heatmap[i + 1].Percent_Total > percent)
                    return new HeatBoundry()
                    {
                        From = heatmap[i],
                        To = heatmap[i + 1],
                        From_Index = i,
                        To_Index = i + 1,
                    };
            }

            throw new ApplicationException($"Didn't find percent: {percent}");
        }
        private static double GetPopulation_Area(double x1, double y1, double x2, double y2)
        {
            double height_square = Math.Min(y1, y2);
            double height_triangle = Math.Max(y1, y2) - height_square;

            double width = x2 - x1;

            return
                (width * height_square) +
                (width * height_triangle * 0.5);
        }

        #endregion

        private static void DrawTestBucket(PathSnippet[] snippets, BezierUtil.CurvatureSample[] heatmap)
        {
            var sizes = Debug3DWindow.GetDrawSizes(1);

            var window = new Debug3DWindow();

            // black horizontal line
            window.AddLine(new Point3D(0, 0, 0), new Point3D(1, 0, 0), sizes.line, Colors.Black);
            window.AddLine(new Point3D(0, 0, 0), new Point3D(1, 1, 0), sizes.line, Colors.DarkGray);

            // vertical lines that represent snippet boundries
            foreach (var snippet in snippets)
            {
                window.AddLine(new Point3D(snippet.From_Percent_Out, 0, 0), new Point3D(snippet.From_Percent_Out, 1, 0), sizes.line, Colors.CadetBlue);
                window.AddLine(new Point3D(snippet.To_Percent_Out, 0, 0), new Point3D(snippet.To_Percent_Out, 1, 0), sizes.line, Colors.CadetBlue);
            }

            // sawtooth points/lines that represent heatmap
            Point3D[] heat_lines = heatmap.
                Select(o => new Point3D(o.Percent_Total, o.Dist_From_NegOne, 0)).
                ToArray();

            window.AddLines(heat_lines, sizes.line, Colors.OliveDrab);

            // turn those heatmap values into something that can directly turned into forces
            // may need to normalize them somehow?  softmax?

            window.Show();
        }
    }

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
}
