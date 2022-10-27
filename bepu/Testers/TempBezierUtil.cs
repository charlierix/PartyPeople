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

                //TestBuckets1(retVal, heatmap, beziers);
                TestBuckets2(retVal, heatmap, beziers);
            }

            return initial;
        }

        #region testbuckets 1

        private static void TestBuckets1(PathSnippet[] snippets, BezierUtil.CurvatureSample[] heatmap, BezierSegment3D_wpf[] beziers)
        {
            DrawTestBucket(snippets, heatmap);

            var percents_desired = snippets.
                Skip(1).
                Select(o => o.From_Percent_Out).
                ToArray();


            //this shouldn't be needed anymore, since heatmap now has total percent
            var percents_actual = BezierUtil.ConvertToNormalizedPositions(percents_desired, beziers).
                ToArray();



            var populations = snippets.
                Select(o => GetPopulation1(o, percents_actual, heatmap)).       // GetPopulation1 is based on segment index / local percent.  Rewrite using total percent
                ToArray();
        }

        private static double GetPopulation1(PathSnippet snippet, BezierUtil.NormalizedPosPointer[] percents_actual, BezierUtil.CurvatureSample[] heatmap)
        {
            // Using Y from to
            var loc_from = GetPopulation_Local(snippet.From_Percent_Out, percents_actual);
            var loc_to = GetPopulation_Local(snippet.To_Percent_Out, percents_actual);

            // Find the set of heatmap entries that straddle from/to points
            BezierUtil.CurvatureSample[] straddle_heats = GetPopulation_Heats(loc_from, loc_to, heatmap);

            // Convert heats into points (X is total percent, Y is heat value at X)
            Point[] straddle_heat_points = straddle_heats.
                Select(o => new Point(o.Percent_Total, o.Dist_From_NegOne)).
                ToArray();



            // X is total percent, Y is heat value at X
            Point[] heat_points = GetPopulation_ConvertValues(snippet.From_Percent_Out, snippet.To_Percent_Out, straddle_heat_points);




            // Add up heatmap values

            double retVal = 0;


            // need to turn these heat spike points into area







            return retVal;
        }

        private static BezierUtil.NormalizedPosPointer GetPopulation_Local(double total_percent, BezierUtil.NormalizedPosPointer[] percents_actual)
        {
            if (total_percent.IsNearZero())
                return new BezierUtil.NormalizedPosPointer()
                {
                    Desired_Index = -1,
                    Total_Percent = total_percent,
                    Segment_Index = 0,
                    Segment_Local_Percent = 0,
                };

            if (total_percent.IsNearValue(1))
                return new BezierUtil.NormalizedPosPointer()
                {
                    Desired_Index = -1,
                    Total_Percent = total_percent,
                    Segment_Index = percents_actual[^1].Segment_Index,
                    Segment_Local_Percent = 1,
                };

            for (int i = 0; i < percents_actual.Length; i++)
            {
                if (total_percent.IsNearValue(percents_actual[i].Total_Percent))
                    return percents_actual[i];
            }

            throw new ApplicationException($"Couldn't find percent mapping: {total_percent}");
        }

        private static BezierUtil.CurvatureSample[] GetPopulation_Heats(BezierUtil.NormalizedPosPointer from, BezierUtil.NormalizedPosPointer to, BezierUtil.CurvatureSample[] heatmap)
        {
            int index_left = GetPopulation_Heats_Left(from, heatmap);
            if (index_left < 0)
                throw new ApplicationException("Couldn't find from entry");

            int index_right = GetPopulation_Heats_Right(to, heatmap);
            if (index_right < 0)
                throw new ApplicationException("Couldn't find to entry");

            return Enumerable.Range(index_left, index_right - index_left + 1).
                Select(o => heatmap[o]).
                ToArray();
        }
        //TODO: rewrite these using total percent
        private static int GetPopulation_Heats_Left(BezierUtil.NormalizedPosPointer from, BezierUtil.CurvatureSample[] heatmap)
        {
            int retVal = -1;

            for (int i = 0; i < heatmap.Length; i++)
            {
                if (heatmap[i].SegmentIndex > from.Segment_Index)
                    break;      // went too far

                if (heatmap[i].SegmentIndex < from.Segment_Index)
                {
                    retVal = i;     // best so far
                    continue;
                }

                if (heatmap[i].Percent_Along_Segment.IsNearValue(from.Segment_Local_Percent))
                {
                    retVal = i;     // exact match
                    break;
                }

                if (heatmap[i].Percent_Along_Segment > from.Segment_Local_Percent)
                    break;      // went too far

                retVal = i;     // best so far
            }

            return retVal;
        }
        private static int GetPopulation_Heats_Right(BezierUtil.NormalizedPosPointer to, BezierUtil.CurvatureSample[] heatmap)
        {
            int retVal = -1;

            for (int i = heatmap.Length - 1; i >= 0; i--)
            {
                if (heatmap[i].SegmentIndex < to.Segment_Index)
                    break;      // went too far

                if (heatmap[i].SegmentIndex > to.Segment_Index)
                {
                    retVal = i;     // best so far
                    continue;
                }

                if (heatmap[i].Percent_Along_Segment.IsNearValue(to.Segment_Local_Percent))
                {
                    retVal = i;     // exact match
                    break;
                }

                if (heatmap[i].Percent_Along_Segment < to.Segment_Local_Percent)
                    break;      // went too far

                retVal = i;     // best so far
            }

            return retVal;
        }

        private static Point[] GetPopulation_ConvertValues(double from, double to, Point[] heat_points)
        {
            var retVal = new List<Point>();

            //int index = 0;

            //if (heats[0].SegmentIndex == from.Segment_Index && heats[0].Percent_Along_Segment.IsNearValue(from.Segment_Local_Percent))
            //{
            //    retVal.Add(new Point(from.Desired_Total_Percent, heats[0].Dist_From_NegOne));       // sitting exactly on the heat point, no need to lerp
            //    index = 1;
            //}
            //else
            //{
            //    retVal.Add(new Point(from.Desired_Total_Percent, LERP_Heat_Y(heats[0], heats[1], from.Segment_Index, from.Segment_Local_Percent)));
            //    retVal.Add(new Point(GetTotalPercentForLocal(), heats[1].Dist_From_NegOne));
            //    index = 2;
            //}

            //for(int i = index; i < heats.Length - 1; i++)
            //{
            //    retVal.Add(new Point(GetTotalPercentForLocal(), heats[i].Dist_From_NegOne));
            //}


            //TODO: Do the same with to that was done for from



            return retVal.ToArray();
        }
        //private static double LERP_Heat_Y(BezierUtil.HM2 heat_left, BezierUtil.HM2 heat_right, int segment_index, double segment_local_percent)
        //{

        //}

        #endregion
        #region testbuckets 2

        private record HeatBoundry
        {
            public BezierUtil.CurvatureSample From { get; init; }
            public BezierUtil.CurvatureSample To { get; init; }

            public int From_Index { get; init; }
            public int To_Index { get; init; }
        }

        private static void TestBuckets2(PathSnippet[] snippets, BezierUtil.CurvatureSample[] heatmap, BezierSegment3D_wpf[] beziers)
        {
            DrawTestBucket(snippets, heatmap);

            // get the area inside each snippet
            double[] areas = snippets.
                Select(o => GetPopulation2(o, heatmap)).
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
        private static double GetPopulation2(PathSnippet snippet, BezierUtil.CurvatureSample[] heatmap)
        {
            // get left and right
            var edge_left = GetPopulation2_Edge(snippet.From_Percent_Out, heatmap);
            var edge_right = GetPopulation2_Edge(snippet.To_Percent_Out, heatmap);

            double retVal = 0;

            // add the lerp portions of left and right to the total
            if (edge_left.From_Index != edge_left.To_Index)
            {
                // Need the right portion of the left edge
                double y1 = UtilityMath.LERP(
                    edge_left.From.Dist_From_NegOne,
                    edge_left.To.Dist_From_NegOne,
                    (edge_left.To.Percent_Total - snippet.From_Percent_Out) / (edge_left.To.Percent_Total - edge_left.From.Percent_Total));

                retVal += GetPopulation2_Area(snippet.From_Percent_Out, y1, edge_left.To.Percent_Total, edge_left.To.Dist_From_NegOne);
            }

            if (edge_right.From_Index != edge_right.To_Index)
            {
                // Need the left portion of the right edge
                double y2 = UtilityMath.LERP(
                    edge_right.From.Dist_From_NegOne,
                    edge_right.To.Dist_From_NegOne,
                    (snippet.To_Percent_Out - edge_right.From.Percent_Total) / (edge_right.To.Percent_Total - edge_right.From.Percent_Total));

                retVal += GetPopulation2_Area(edge_right.From.Percent_Total, edge_right.From.Dist_From_NegOne, snippet.To_Percent_Out, y2);
            }

            // iterate over everything in between, adding the their entire area to the total
            for (int i = edge_left.To_Index; i < edge_right.From_Index; i++)
            {
                retVal += GetPopulation2_Area(heatmap[i].Percent_Total, heatmap[i].Dist_From_NegOne, heatmap[i + 1].Percent_Total, heatmap[i + 1].Dist_From_NegOne);
            }

            return retVal;
        }

        private static HeatBoundry GetPopulation2_Edge(double percent, BezierUtil.CurvatureSample[] heatmap)
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

        private static double GetPopulation2_Area(double x1, double y1, double x2, double y2)
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
