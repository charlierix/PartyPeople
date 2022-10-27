using Game.Core;
using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers
{
    public static class TempBezierUtil
    {
        #region get heatmap

        public static HM2[] GetHeatmap(BezierSegment3D_wpf[] beziers)
        {
            var retVal = new List<HM2>();

            retVal.Add(new HM2()        // having endpoints at 0 and 1 makes processing these easier
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
                    retVal.Add(GetHeatmap_Item(
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
                    retVal.Add(GetHeatmap_Item(samples[j - 1].Point, samples[j].Point, samples[j + 1].Point, lengths[j - 1], lengths[j], i, samples[j].Percent_Along));
                }
            }

            retVal.Add(new HM2()
            {
                Dist_From_NegOne = 0,
                Dot = -1,
                Percent_Along_Segment = 1,
                Point = beziers[^1].EndPoint1,
                SegmentIndex = beziers.Length - 1,
                Percent_Total = 1,
            });

            return GetHeatmap_ApplyTotalPercent(retVal.ToArray(), beziers);
        }
        private static HM2 GetHeatmap_Item(Point3D point_left, Point3D point_middle, Point3D point_right, double len_left, double len_right, int segment_index, double percent_middle)
        {
            Vector3D left = point_left - point_middle;
            Vector3D right = point_right - point_middle;

            double dot = Vector3D.DotProduct(left / len_left, right / len_right);
            double dist_from_negone = 1 + dot;

            return new HM2()
            {
                SegmentIndex = segment_index,
                Percent_Along_Segment = percent_middle,

                Percent_Total = -1,     // this will be populated in a post pass

                Point = point_middle,

                Dot = dot,
                Dist_From_NegOne = dist_from_negone,
            };
        }
        private static HM2[] GetHeatmap_ApplyTotalPercent(HM2[] heatmap, BezierSegment3D_wpf[] beziers)
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

        #endregion

        public static PathSnippet[] GetPinchedMapping(HM2[] heatmap, int endpoint_count, BezierSegment3D_wpf[] beziers)
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
                    From_X = o.from,
                    From_Y = o.from,
                    To_X = o.to,
                    To_Y = o.to,
                }).
                ToArray();

            var retVal = initial;

            for (int i = 0; i < 4; i++)
            {
                //double[] forces = GetForces(retVal, heatmap);
                //retVal = ApplyForces(retVal, forces);

                TestBuckets(retVal, heatmap, beziers);

            }

            return initial;
        }

        #region testbuckets 1

        private static void TestBuckets(PathSnippet[] snippets, HM2[] heatmap, BezierSegment3D_wpf[] beziers)
        {

            DrawTestBucket(snippets, heatmap);



            //TODO: clearly comment what is going on
            // what is currently here, what is being calculated

            var percents_desired = snippets.
                Skip(1).
                Select(o => o.From_Y).
                ToArray();

            var percents_actual = BezierUtil.ConvertToNormalizedPositions(percents_desired, beziers).
                ToArray();





            var populations = snippets.
                Select(o => GetPopulation1(o, percents_actual, heatmap)).       // GetPopulation1 is based on segment index / local percent.  Rewrite using total percent
                ToArray();




        }

        private static void DrawTestBucket(PathSnippet[] snippets, HM2[] heatmap)
        {

            // black horizontal line

            // vertical lines that represent snippet boundries

            // sawtooth points/lines that represent heatmap

            // turn those heatmap values into something that can directly turned into forces
            // may need to normalize them somehow?  softmax?

        }




        #region GetPopulation 1

        private static double GetPopulation1(PathSnippet snippet, BezierUtil.NormalizedPosPointer[] percents_actual, HM2[] heatmap)
        {
            // Using Y from to
            var loc_from = GetPopulation_Local(snippet.From_Y, percents_actual);
            var loc_to = GetPopulation_Local(snippet.To_Y, percents_actual);

            // Find the set of heatmap entries that straddle from/to points
            HM2[] straddle_heats = GetPopulation_Heats(loc_from, loc_to, heatmap);

            // Convert heats into points (X is total percent, Y is heat value at X)
            Point[] straddle_heat_points = straddle_heats.
                Select(o => new Point(o.Percent_Total, o.Dist_From_NegOne)).
                ToArray();



            // X is total percent, Y is heat value at X
            Point[] heat_points = GetPopulation_ConvertValues(snippet.From_Y, snippet.To_Y, straddle_heat_points);




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

        private static HM2[] GetPopulation_Heats(BezierUtil.NormalizedPosPointer from, BezierUtil.NormalizedPosPointer to, HM2[] heatmap)
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
        private static int GetPopulation_Heats_Left(BezierUtil.NormalizedPosPointer from, HM2[] heatmap)
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
        private static int GetPopulation_Heats_Right(BezierUtil.NormalizedPosPointer to, HM2[] heatmap)
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
        //private static double LERP_Heat_Y(HM2 heat_left, HM2 heat_right, int segment_index, double segment_local_percent)
        //{

        //}

        #endregion
        #endregion
    }

    #region record: HM2

    public record HM2
    {
        public int SegmentIndex { get; init; }
        public double Percent_Along_Segment { get; init; }

        public double Percent_Total { get; init; }

        public Point3D Point { get; init; }
        public double Dot { get; init; }
        public double Dist_From_NegOne { get; init; }
    }

    #endregion
    #region record: PathSnippet

    public record PathSnippet
    {
        public double From_X { get; init; }
        public double From_Y { get; init; }
        public double To_X { get; init; }
        public double To_Y { get; init; }
    }

    #endregion
}
