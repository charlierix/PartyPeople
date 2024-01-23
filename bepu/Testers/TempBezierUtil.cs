using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using Game.Math_WPF.WPF.Viewers;
using GameItems;
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
        private const int COUNT_RESIZEPASS_A1 = 2;     // it oscillates around, which makes me think the scale expansion is too aggressive
        private const int COUNT_SLIDEPASS_A1 = 3;

        private const int COUNT_RESIZEPASS_A2 = 288;        //TODO: the final should be 24 to 60 total
        private const int COUNT_SLIDEPASS_A2 = 144;

        private const double DRAW_Y_INC = 1.25;

        public static PathSnippet[] GetPinchedMapping1(BezierUtil.CurvatureSample[] heatmap, int endpoint_count, BezierSegment3D_wpf[] beziers)
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

            for (int i = 0; i < COUNT_RESIZEPASS_A1; i++)
            {
                double[] areas = retVal.
                    Select(o => GetPopulation1(o, heatmap)).
                    ToArray();

                //DrawTestBucket(retVal, areas, heatmap, $"pass {i}");

                //double[] scales = GetScales1(areas);
                double[] scales = GetScales2(areas, retVal);
                retVal = ApplyForces2(retVal, scales, i);
            }

            return retVal;
        }
        //TODO: figure out why some pinch points are off
        //  is the heatmap to actual position mapping accurate?
        //  points are currently being distributed cell by cell.  add a global pass that pulls everything together proportionally
        //      this will help take from the endpoint (which is a long flat spot with too much representation) and give to a pinch in the middle of the curve
        public static PathSnippet[] GetPinchedMapping2(BezierUtil.CurvatureSample[] heatmap, int endpoint_count, BezierSegment3D_wpf[] beziers)
        {
            int return_count = ((endpoint_count - 1) * 3) + 1;      // this is kind of arbitrary, but should give a good amount of snippets to play with

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

            var window = new Debug3DWindow();
            var sizes = Debug3DWindow.GetDrawSizes(1);
            double draw_y = 0;

            //double min_width = (1d / return_count) * 0.5;        // can't have too much, or there end of being cluster islands of points (dense patch, sparse patch)
            //double max_width = (1d / return_count) * 3;
            double min_width = (1d / return_count) * 0.8;
            double max_width = (1d / return_count) * 1.4;

            //TODO: Modify PathSnippet.In instead of Out

            var retVal = initial;

            for (int i = 0; i < COUNT_RESIZEPASS_A2 + 1; i++)
            {
                bool should_draw = i % (COUNT_RESIZEPASS_A2 / 12) == 0;

                double[] areas = retVal.
                    Select(o => GetPopulation2(o, heatmap)).
                    ToArray();

                Point3D[] samples = BezierUtil.GetPoints_PinchImproved(beziers.Length * 12, beziers, retVal);

                if (should_draw)
                    DrawSnippetUsage(window, sizes, retVal, areas, samples, i.ToString(), ref draw_y);

                if (i >= COUNT_RESIZEPASS_A2)
                    break;

                retVal = RedistributeSizes(retVal, areas, min_width, max_width, window, sizes, draw_y - DRAW_Y_INC, should_draw);
            }

            window.Show();

            return retVal;
        }

        public static Point3D[] GetPinchedMapping3(BezierUtil.CurvatureSample[] heatmap, int endpoint_count, BezierSegment3D_wpf[] beziers)
        {
            // Walk the heatmap, finding local minimums/maximums
            HeatDiff[] inflection_points = FindExtremes(heatmap);

            return inflection_points.
                Skip(1).
                Select(o => heatmap[o.Index1].Point).
                Take(inflection_points.Length - 2).
                ToArray();
        }

        public static BezierSegment3D_wpf GetPinchedMapping4(BezierUtil.CurvatureSample[] heatmap, int endpoint_count, BezierSegment3D_wpf[] beziers)
        {
            // Walk the heatmap, finding local minimums/maximums
            HeatDiff[] inflection_points = FindExtremes(heatmap);



            // There isn't a way to know how many control points to use just by the number of inflection points
            // It's more about where they are along the total path, and which pinch points need extra attention


            // Can't use weight, need to use the actual dot products



            // -------- Attempt 1 --------
            // Create bars around pinch points
            //  width is how wide the pinch point is (use a fixed threshold)
            //  height is how strong the pinch point is

            // Create enough control points to be able to isolate those bars

            // Adjust the Y value of relevant control points to pull influence toward the pinch points




            return new BezierSegment3D_wpf(beziers[0].EndPoint0, beziers[^1].EndPoint1, new Point3D[0]);
        }

        #region get snippet population

        private record HeatBoundry
        {
            public BezierUtil.CurvatureSample From { get; init; }
            public BezierUtil.CurvatureSample To { get; init; }

            public int From_Index { get; init; }
            public int To_Index { get; init; }
        }

        /// <summary>
        /// This gets the area of the graph in the snippet's range
        /// </summary>
        /// <remarks>
        /// Think of the heatmap as a sawtooth line graph
        /// The snippet defines a left and right edge
        /// This function finds the area of the polygon
        /// </remarks>
        private static double GetPopulation1(PathSnippet snippet, BezierUtil.CurvatureSample[] heatmap)
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
                    edge_left.From.Weight,
                    edge_left.To.Weight,
                    (edge_left.To.Percent_Total - snippet.From_Percent_Out) / (edge_left.To.Percent_Total - edge_left.From.Percent_Total));

                retVal += GetPopulation_Area(snippet.From_Percent_Out, y1, edge_left.To.Percent_Total, edge_left.To.Weight);
            }

            if (edge_right.From_Index != edge_right.To_Index)
            {
                // Need the left portion of the right edge
                double y2 = UtilityMath.LERP(
                    edge_right.From.Weight,
                    edge_right.To.Weight,
                    (snippet.To_Percent_Out - edge_right.From.Percent_Total) / (edge_right.To.Percent_Total - edge_right.From.Percent_Total));

                retVal += GetPopulation_Area(edge_right.From.Percent_Total, edge_right.From.Weight, snippet.To_Percent_Out, y2);
            }

            // iterate over everything in between, adding the their entire area to the total
            for (int i = edge_left.To_Index; i < edge_right.From_Index; i++)
            {
                retVal += GetPopulation_Area(heatmap[i].Percent_Total, heatmap[i].Weight, heatmap[i + 1].Percent_Total, heatmap[i + 1].Weight);
            }

            return retVal;
        }
        private static double GetPopulation2(PathSnippet snippet, BezierUtil.CurvatureSample[] heatmap)
        {
            // get left and right
            var edge_left = GetPopulation_Edge(snippet.From_Percent_In, heatmap);
            var edge_right = GetPopulation_Edge(snippet.To_Percent_In, heatmap);

            double retVal = 0;

            // add the lerp portions of left and right to the total
            if (edge_left.From_Index != edge_left.To_Index)
            {
                // Need the right portion of the left edge
                double y1 = UtilityMath.LERP(
                    edge_left.From.Weight,
                    edge_left.To.Weight,
                    (edge_left.To.Percent_Total - snippet.From_Percent_In) / (edge_left.To.Percent_Total - edge_left.From.Percent_Total));

                retVal += GetPopulation_Area(snippet.From_Percent_In, y1, edge_left.To.Percent_Total, edge_left.To.Weight);
            }

            if (edge_right.From_Index != edge_right.To_Index)
            {
                // Need the left portion of the right edge
                double y2 = UtilityMath.LERP(
                    edge_right.From.Weight,
                    edge_right.To.Weight,
                    (snippet.To_Percent_In - edge_right.From.Percent_Total) / (edge_right.To.Percent_Total - edge_right.From.Percent_Total));

                retVal += GetPopulation_Area(edge_right.From.Percent_Total, edge_right.From.Weight, snippet.To_Percent_In, y2);
            }

            // iterate over everything in between, adding the their entire area to the total
            for (int i = edge_left.To_Index; i < edge_right.From_Index; i++)
            {
                retVal += GetPopulation_Area(heatmap[i].Percent_Total, heatmap[i].Weight, heatmap[i + 1].Percent_Total, heatmap[i + 1].Weight);
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

        #region snippets attempt 1

        private static double[] GetScales1(double[] areas)
        {
            double MAX_SHRINK_PERCENT = 0.5;
            double MAX_GROW_PERCENT = 2;

            double min = areas.Min();
            double max = areas.Max();

            var avg = Math1D.Avg(areas);

            double max_diff1 = avg - min;
            double max_diff2 = max - avg;

            double[] retVal = new double[areas.Length];

            for (int i = 0; i < retVal.Length; i++)
            {
                if (areas[i] > avg)
                {
                    // Shrink
                    //double percent = (max - areas[i]) / max_diff2;
                    //percent = 1 - percent;

                    //retVal[i] = MAX_SHRINK_PERCENT * percent;

                    retVal[i] = UtilityMath.GetScaledValue(MAX_SHRINK_PERCENT, 1, max_diff2, 0, max - areas[i]);
                }
                else
                {
                    // Grow
                    //double percent = (areas[i] - min) / max_diff1;
                    //percent = 1 - percent;

                    //retVal[i] = MAX_GROW_PERCENT * percent;

                    retVal[i] = UtilityMath.GetScaledValue(MAX_GROW_PERCENT, 1, 0, max_diff1, areas[i] - min);
                }
            }

            return retVal;
        }
        private static double[] GetScales2(double[] areas, PathSnippet[] snippets)
        {
            //double GROW_PERCENT = 1;
            //double SHRINK_PERCENT = 6;

            //double CLAMP_MIN = 0.15;
            //double CLAMP_MAX = 1.5;

            double GROW_PERCENT = 1.1;
            double SHRINK_PERCENT = 4;

            double CLAMP_MIN = 0.15;
            double CLAMP_MAX = 1.5;


            //double sum_area = areas.Sum();
            //double min_area = areas.Min();
            //double max_area = areas.Max();
            double avg_area = areas.Average();

            double[] retVal = new double[areas.Length];

            for (int i = 0; i < areas.Length; i++)
            {
                double distance = areas[i] - avg_area;
                double width = snippets[i].To_Percent_Out - snippets[i].From_Percent_Out;
                double height = areas[i] / width;

                double percent = distance < 0 ?
                    GROW_PERCENT :
                    SHRINK_PERCENT;

                double new_area = areas[i] + (-distance * percent);
                double new_width = new_area / height;

                double scale = new_width / width;

                retVal[i] = UtilityMath.Clamp(scale, CLAMP_MIN, CLAMP_MAX);
            }

            return retVal;
        }

        private static PathSnippet[] ApplyForces1(PathSnippet[] snippets, double[] scales, int iteration)
        {
            var positions = ConvertSnippets(snippets);

            DrawSnippetSlide(positions, $"orig {iteration}");

            positions = ScaleSnippets(positions, scales);

            for (int i = 0; i < COUNT_SLIDEPASS_A1; i++)
            {
                positions = SlideSnippets1(positions, iteration, i);
            }

            positions = SolidifySnippets(positions);

            return AdjustSnippets(snippets, positions);
        }
        private static PathSnippet[] ApplyForces2(PathSnippet[] snippets, double[] scales, int iteration)
        {
            var positions = ConvertSnippets(snippets);

            positions = ScaleSnippets(positions, scales);

            double[] half_widths = positions.
                Select(o => o.Width / 2).
                ToArray();

            //DrawSnippetSlide(positions, $"before {iteration}");

            for (int i = 0; i < COUNT_SLIDEPASS_A1; i++)
            {
                positions = positions.
                    Select((o, j) => o with
                    {
                        From = o.Center - half_widths[j],
                        To = o.Center + half_widths[j],
                    }).
                    ToArray();

                positions = SlideSnippets3(positions);
                positions = SolidifySnippets(positions);
            }

            DrawSnippetSlide(positions, $"after {iteration}");

            return AdjustSnippets(snippets, positions);
        }

        private static SnippetPos[] ConvertSnippets(PathSnippet[] snippets)
        {
            return snippets.
                Select(o =>
                {
                    double width = o.To_Percent_Out - o.From_Percent_Out;

                    return new SnippetPos
                    {
                        From = o.From_Percent_Out,
                        To = o.To_Percent_Out,
                        Center = o.From_Percent_Out + (width / 2),
                    };
                }).
                ToArray();
        }
        private static PathSnippet[] AdjustSnippets(PathSnippet[] snippets, SnippetPos[] new_loc)
        {
            return Enumerable.Range(0, snippets.Length).
                Select(o => snippets[o] with
                {
                    From_Percent_Out = new_loc[o].From,
                    To_Percent_Out = new_loc[o].To,
                }).
                ToArray();
        }

        private static SnippetPos[] ScaleSnippets(SnippetPos[] snippets, double[] scales)
        {
            return Enumerable.Range(0, snippets.Length).
                Select(o =>
                {
                    double width = snippets[o].Width * scales[o];
                    double half_width = width / 2;

                    return snippets[o] with
                    {
                        From = snippets[o].Center - half_width,
                        To = snippets[o].Center + half_width,
                    };
                }).
                ToArray();
        }

        private static SnippetPos[] SlideSnippets1(SnippetPos[] positions, int iteration_outer, int iteration_inner)
        {
            var retVal = positions.ToArray();

            // Move the left and right edges, don't touch them the rest of the function
            retVal[0] = SlideSnippet_Left(positions[0], 0);
            retVal[^1] = SlideSnippet_Right(positions[^1], 1);

            //DrawSnippetSlide(retVal, $"scaled {iteration_outer} - {iteration_inner}");

            // Find the two most squeezed snippets, push them away
            var gaps = Enumerable.Range(0, positions.Length - 1).
                Select(o => new
                {
                    index_left = o,
                    index_right = o + 1,
                    gap = Math.Abs(positions[o].To - positions[o + 1].From),
                }).
                OrderByDescending(o => o.gap).
                ToArray();

            var move = gaps[0];

            if (move.index_left == 0)
            {
                // Only the right one can move
                retVal[move.index_right] = SlideSnippet_Left(retVal[move.index_right], retVal[move.index_left].To);
            }
            else if (move.index_right == positions.Length - 1)
            {
                // Only the left one can move
                retVal[move.index_left] = SlideSnippet_Right(retVal[move.index_left], retVal[move.index_right].From);
            }
            else
            {
                // Pull both apart equally
                double boundry_pos = (retVal[move.index_left].To + retVal[move.index_right].From) / 2;

                retVal[move.index_left] = SlideSnippet_Right(retVal[move.index_left], boundry_pos);
                retVal[move.index_right] = SlideSnippet_Left(retVal[move.index_right], boundry_pos);
            }

            return retVal;
        }
        private static SnippetPos[] SlideSnippets2(SnippetPos[] positions, int iteration_outer, int iteration_inner)
        {
            var retVal = positions.ToArray();

            // Move the left and right edges, don't touch them the rest of the function
            retVal[0] = SlideSnippet_Left(positions[0], 0);
            retVal[^1] = SlideSnippet_Right(positions[^1], 1);

            var gaps = Enumerable.Range(0, positions.Length - 1).
                Select(o => new
                {
                    index_left = o,
                    index_right = o + 1,
                    gap = Math.Abs(positions[o].To - positions[o + 1].From),
                }).
                ToArray();

            double[] forces = new double[positions.Length - 2];

            for (int i = 1; i < positions.Length - 1; i++)
            {
                forces[i - 1] = gaps[i - 1].gap + gaps[i].gap;
            }





            return retVal;
        }
        private static SnippetPos[] SlideSnippets3(SnippetPos[] positions)
        {
            var retVal = positions.ToArray();

            // Move the left and right edges, don't touch them the rest of the function
            retVal[0] = SlideSnippet_Left(retVal[0], 0);
            retVal[^1] = SlideSnippet_Right(retVal[^1], 1);

            VectorND min = new VectorND(retVal[0].To);
            VectorND max = new VectorND(retVal[^1].From);

            VectorND[] movable = Enumerable.Range(0, retVal.Length - 1).
                Select(o => new VectorND(retVal[o + 1].Center)).
                ToArray();

            double[] mults = Enumerable.Range(0, retVal.Length - 1).
                Select(o => retVal[o + 1].Width).
                ToArray();

            var moved = MathND.GetRandomVectors_Cube_EventDist(movable, (min, max), mults, stopIterationCount: COUNT_SLIDEPASS_A1);

            for (int i = 0; i < moved.Length; i++)
            {
                retVal[i + 1] = SlideSnippet_Center(retVal[i + 1], moved[i][0]);
            }

            return retVal;
        }

        private static SnippetPos SlideSnippet_Left(SnippetPos snippet, double new_left)
        {
            return snippet with
            {
                From = new_left,
                Center = new_left + snippet.Width / 2,
                To = new_left + snippet.Width,
            };
        }
        private static SnippetPos SlideSnippet_Center(SnippetPos snippet, double new_center)
        {
            double half_width = snippet.Width / 2;

            return snippet with
            {
                From = new_center - half_width,
                Center = new_center,
                To = new_center + half_width,
            };
        }
        private static SnippetPos SlideSnippet_Right(SnippetPos snippet, double new_right)
        {
            return snippet with
            {
                From = new_right - snippet.Width,
                Center = new_right - snippet.Width / 2,
                To = new_right,
            };
        }

        private static SnippetPos[] SolidifySnippets(SnippetPos[] positions)
        {
            var retVal = new SnippetPos[positions.Length];

            double[] boundries = Enumerable.Range(0, positions.Length - 1).
                Select(o => SolidifySnippets_Center(positions[o].To, positions[o + 1].From)).
                ToArray();

            for (int i = 0; i < positions.Length; i++)
            {
                double x1 = i == 0 ?
                    0 :
                    boundries[i - 1];

                double x2 = i == positions.Length - 1 ?
                    1 :
                    boundries[i];

                double width = x2 - x1;

                retVal[i] = new SnippetPos()
                {
                    From = x1,
                    To = x2,
                    Center = x1 + (width / 2),
                };
            }

            return retVal;
        }
        private static double SolidifySnippets_Center(double x1, double x2)
        {
            return x1 + ((x2 - x1) / 2);
        }

        private static void DrawTestBucket(PathSnippet[] snippets, double[] areas, BezierUtil.CurvatureSample[] heatmap, string title)
        {
            var sizes = Debug3DWindow.GetDrawSizes(1);

            var window = new Debug3DWindow()
            {
                Title = title,
            };

            // black horizontal line
            window.AddLine(new Point3D(0, 0, 0), new Point3D(1, 0, 0), sizes.line, Colors.Black);
            window.AddLine(new Point3D(0, 0, 0), new Point3D(1, 1, 0), sizes.line, Colors.DarkGray);

            // vertical lines that represent snippet boundries
            for (int i = 0; i < snippets.Length; i++)
            {
                window.AddLine(new Point3D(snippets[i].From_Percent_Out, 0, 0), new Point3D(snippets[i].From_Percent_Out, 1, 0), sizes.line, Colors.CadetBlue);
                window.AddLine(new Point3D(snippets[i].To_Percent_Out, 0, 0), new Point3D(snippets[i].To_Percent_Out, 1, 0), sizes.line, Colors.CadetBlue);

                double width = snippets[i].To_Percent_Out - snippets[i].From_Percent_Out;
                double height = areas[i] / width;

                window.AddSquare(new Point(snippets[i].From_Percent_Out, 0), new Point(snippets[i].To_Percent_Out, -height), Colors.DimGray);
            }

            // sawtooth points/lines that represent heatmap
            Point3D[] heat_lines = heatmap.
                Select(o => new Point3D(o.Percent_Total, o.Weight, 0)).
                ToArray();

            window.AddLines(heat_lines, sizes.line, Colors.OliveDrab);

            // turn those heatmap values into something that can directly turned into forces
            // may need to normalize them somehow?  softmax?

            window.Show();
        }
        private static void DrawSnippetSlide(SnippetPos[] positions, string title)
        {
            var sizes = Debug3DWindow.GetDrawSizes(1);

            var window = new Debug3DWindow()
            {
                Title = title,
            };

            double bar_y = 0.5;
            window.AddLine(new Point3D(0, -bar_y, 0), new Point3D(1, -bar_y, 0), sizes.line, Colors.Black);
            window.AddLine(new Point3D(0, bar_y, 0), new Point3D(1, bar_y, 0), sizes.line, Colors.Black);
            window.AddLine(new Point3D(0, -bar_y, 0), new Point3D(0, bar_y, 0), sizes.line, Colors.Black);
            window.AddLine(new Point3D(1, -bar_y, 0), new Point3D(1, bar_y, 0), sizes.line, Colors.Black);

            var draw_snippet = new Action<SnippetPos, Color>((s, c) =>
            {
                double y = StaticRandom.NextDouble(-0.15, 0.15);

                window.AddDot(new Point3D(s.Center, y, 0), sizes.dot, c);
                window.AddLine(new Point3D(s.From, y, 0), new Point3D(s.To, y, 0), sizes.line, c);
                window.AddLine(new Point3D(s.From, y - 0.1, 0), new Point3D(s.From, y + 0.1, 0), sizes.line * 0.5, c);
                window.AddLine(new Point3D(s.To, y - 0.1, 0), new Point3D(s.To, y + 0.1, 0), sizes.line * 0.5, c);
            });

            var colors = UtilityWPF.GetRandomColors(positions.Length, 90, 180);

            for (int i = 0; i < positions.Length; i++)
            {
                draw_snippet(positions[i], colors[i]);
            }

            window.Show();
        }

        #endregion
        #region snippets attempt 2

        /// <summary>
        /// Widens/Shortens snippets to even out the area distribution
        /// </summary>
        /// <remarks>
        /// Each snippet represents a portion of the total path percent.  Pinch points need more of the total percent (pinch
        /// points have low area, flat spots have too much area)
        /// 
        /// So after this function, pinch points will have wider snippets (taken from the flat spots)
        /// 
        /// NOTE: Turning area into height to try to get something more comparable (this may be flawed and maybe area should
        /// be used instead)
        /// </remarks>
        private static PathSnippet[] RedistributeSizes(PathSnippet[] snippets, double[] areas, double min_width, double max_width, Debug3DWindow window, (double dot, double line) sizes, double draw_y, bool should_draw)
        {
            SnippetPos[] positions = ConvertSnippets(snippets);

            // Get the height of each bar
            var heights = Enumerable.Range(0, positions.Length).
                Select(o => areas[o] / (positions[o].To - positions[o].From)).
                ToArray();

            // Figure out how to redistribute heights (flows from high to low)
            var moves = Enumerable.Range(0, positions.Length).
                Select(o =>
                (
                    left: o == 0 ?
                        0d :
                        RedistributeSizes_GetMovement(heights[o], heights[o - 1]),

                    right: o == positions.Length - 1 ?
                        0d :
                        RedistributeSizes_GetMovement(heights[o], heights[o + 1])
                )).
                ToArray();

            // Move the heights
            double[] new_heights = heights.ToArray();

            for (int i = 0; i < positions.Length; i++)
            {
                if (i > 0 && moves[i].left > 0)
                {
                    new_heights[i - 1] += moves[i].left;
                    new_heights[i] -= moves[i].left;
                }

                if (i < positions.Length - 1 && moves[i].right > 0)
                {
                    new_heights[i + 1] += moves[i].right;
                    new_heights[i] -= moves[i].right;
                }
            }

            // Generate snippets according to their new heights
            SnippetPos[] retVal = Enumerable.Range(0, positions.Length).
                Select(o => RedistributeSizes_Rebuild(positions[o], heights[o], new_heights[o], min_width, max_width)).
                ToArray();

            // Make sure there are no overlaps or gaps
            if (should_draw)
                DrawSnippetSlide2(window, sizes, -3.75, draw_y, retVal);

            //retVal = SlideSnippets4(retVal);      // there is no need to do this, solidify lays them down perfectly
            retVal = SolidifySnippets2(retVal);

            if (should_draw)
                DrawSnippetSlide2(window, sizes, -2.5, draw_y, retVal);

            return AdjustSnippets(snippets, retVal);
        }

        /// <summary>
        /// Decides how much height should be transferred
        /// </summary>
        private static double RedistributeSizes_GetMovement(double height_source, double height_dest)
        {
            double PERCENT = 0.2;
            double MAX_MOVE_PERCENT = 0.1;

            double diff = height_source - height_dest;

            if (diff < 0)
                return 0;

            return Math.Min(diff * PERCENT, height_dest * MAX_MOVE_PERCENT);
        }

        /// <summary>
        /// Applies a height change
        /// </summary>
        private static SnippetPos RedistributeSizes_Rebuild(SnippetPos position, double old_height, double new_height, double min_width, double max_width)
        {
            double width = position.To - position.From;

            // treat the difference in height as a percent
            double percent = new_height / old_height;
            width *= percent;

            width = Math.Clamp(width, min_width, max_width);

            double half_width = width / 2;

            return position with
            {
                From = position.Center - half_width,
                To = position.Center + half_width,
            };
        }

        /// <summary>
        /// Applies ball of springs based on relative widths
        /// NOTE: these won't stay between 0 and 1, but will arrange themselves properly relative to each other
        /// </summary>
        private static SnippetPos[] SlideSnippets4(SnippetPos[] positions)
        {
            VectorND[] movable = positions.
                Select(o => new VectorND(o.Center)).
                ToArray();

            var getLink = new Func<int, int, (int, int, double)>((i1, i2) =>
            {
                double w1 = positions[i1].Width / 2;
                double w2 = positions[i2].Width / 2;
                return (i1, i2, w1 + w2);
            });

            var links = new List<(int, int, double)>();

            for (int i = 1; i < positions.Length - 1; i++)
            {
                links.Add(getLink(i - 1, i));
                links.Add(getLink(i, i + 1));
            }

            var moved = MathND.ApplyBallOfSprings(movable, links.ToArray(), COUNT_SLIDEPASS_A2);

            return Enumerable.Range(0, positions.Length).
                Select(o => SlideSnippet_Center(positions[o], moved[o][0])).
                ToArray();
        }

        /// <summary>
        /// Fits these to have a total length of 1.  All widths need to be adjusted proportionally.  First starts at zero, last ends at 1
        /// </summary>
        private static SnippetPos[] SolidifySnippets2(SnippetPos[] positions)
        {
            double total_width = positions.Sum(o => o.Width);
            double width_mult = 1 / total_width;

            SnippetPos[] retVal = new SnippetPos[positions.Length];

            double x = 0;

            for (int i = 0; i < positions.Length; i++)
            {
                double width = positions[i].Width * width_mult;

                retVal[i] = positions[i] with
                {
                    From = x,
                    To = x + width,
                    Center = x + width / 2,
                };

                x += width;
            }

            return retVal;
        }

        private static void DrawSnippetUsage(Debug3DWindow window, (double dot, double line) sizes, PathSnippet[] snippets, double[] areas, Point3D[] samples, string label, ref double draw_y)
        {
            window.AddText3D(label, new Point3D(0, draw_y + 0.5, 0), new Vector3D(0, 0, 1), 0.2, Colors.Black, false);

            DrawSnippetUsage_Snippets_Area(window, sizes, draw_y, snippets, areas);
            DrawSnippetUsage_Snippets_Bezier(window, sizes, draw_y, samples);

            // This will show regular interval points, snippet boundries, how the snippets transform those regular points
            //DrawSnippetUsage_Snippets_BezierSnippetLineup(window, sizes, draw_y, snippets);



            //TOO MUCH
            // This may need to be broken into a couple drawings.  Trying to figure out if the snippets really line up how I think they line up
            // Draw the bezier at regular samples, bars, how those bars relate to the sample locations, sum of the area contained in each bar
            //DrawSnippetUsage_Snippets_BezierBarHeat(window, sizes, draw_y, snippets, areas);




            draw_y += DRAW_Y_INC;
        }
        private static void DrawSnippetUsage_Snippets_Area(Debug3DWindow window, (double dot, double line) sizes, double draw_y, PathSnippet[] snippets, double[] areas)
        {
            const double OFFSET_X = -1.25;

            var heights = new List<double>();

            for (int i = 0; i < snippets.Length; i++)
            {
                double width = snippets[i].To_Percent_Out - snippets[i].From_Percent_Out;
                double height = areas[i] / width;

                Rect rect = new Rect(OFFSET_X + snippets[i].From_Percent_Out, draw_y, width, height);

                window.AddSquare(rect, Colors.DimGray);

                heights.Add(height);
            }

            double avg_height = Math1D.Avg(heights.ToArray());

            window.AddLine(new Point3D(OFFSET_X - 0.05, draw_y + avg_height, 0), new Point3D(OFFSET_X + 1.05, draw_y + avg_height, 0), sizes.line * 0.5, Colors.Gray);
        }
        private static void DrawSnippetUsage_Snippets_Bezier(Debug3DWindow window, (double dot, double line) sizes, double draw_y, Point3D[] samples)
        {
            const double OFFSET_X = 0.25;
            const double RADIUS = 0.5;

            // scale the samples to fit inside of a 1x1x1 cube
            Point3D[] samples_transformed = GetScaledPoints(samples, 0.5, new Point3D(OFFSET_X + RADIUS, draw_y + RADIUS, 0));

            window.AddDots(samples_transformed.Skip(1).Take(samples_transformed.Length - 2), sizes.dot * 0.66, UtilityWPF.ColorFromHex("222"));
            window.AddLines(samples_transformed, sizes.line, UtilityWPF.ColorFromHex("DDD"));

            window.AddDot(samples_transformed[0], sizes.dot, Colors.DarkGreen);
            window.AddDot(samples_transformed[^1], sizes.dot, Colors.DarkRed);
        }

        private static void DrawSnippetSlide2(Debug3DWindow window, (double dot, double line) sizes, double draw_x, double draw_y, SnippetPos[] positions)
        {
            draw_y += 0.5;

            double bar_y = 0.5;
            window.AddLine(new Point3D(draw_x + 0, draw_y - bar_y, 0), new Point3D(draw_x + 1, draw_y - bar_y, 0), sizes.line, Colors.Black);
            window.AddLine(new Point3D(draw_x + 0, draw_y + bar_y, 0), new Point3D(draw_x + 1, draw_y + bar_y, 0), sizes.line, Colors.Black);
            window.AddLine(new Point3D(draw_x + 0, draw_y - bar_y, 0), new Point3D(draw_x + 0, draw_y + bar_y, 0), sizes.line, Colors.Black);
            window.AddLine(new Point3D(draw_x + 1, draw_y - bar_y, 0), new Point3D(draw_x + 1, draw_y + bar_y, 0), sizes.line, Colors.Black);

            var draw_snippet = new Action<SnippetPos, Color>((s, c) =>
            {
                double y = draw_y + StaticRandom.NextDouble(-0.15, 0.15);

                window.AddDot(new Point3D(draw_x + s.Center, y, 0), sizes.dot, c);
                window.AddLine(new Point3D(draw_x + s.From, y, 0), new Point3D(draw_x + s.To, y, 0), sizes.line, c);
                window.AddLine(new Point3D(draw_x + s.From, y - 0.1, 0), new Point3D(draw_x + s.From, y + 0.1, 0), sizes.line * 0.5, c);
                window.AddLine(new Point3D(draw_x + s.To, y - 0.1, 0), new Point3D(draw_x + s.To, y + 0.1, 0), sizes.line * 0.5, c);
            });

            var colors = UtilityWPF.GetRandomColors(positions.Length, 90, 180);

            for (int i = 0; i < positions.Length; i++)
            {
                draw_snippet(positions[i], colors[i]);
            }
        }

        private static Point3D[] GetScaledPoints(Point3D[] points, double new_radius, Point3D new_center)
        {
            Point3D center = Math3D.GetCenter(points);

            Vector3D[] offsets = points.
                Select(o => o - center).
                ToArray();

            var aabb = Math3D.GetAABB(offsets);

            double max = Math1D.Max(aabb.max.X - aabb.min.X, aabb.max.Y - aabb.min.Y, aabb.max.Z - aabb.min.Z);

            double scale = new_radius / (max / 2);

            return offsets.
                Select(o => new_center + o * scale).
                ToArray();
        }

        #endregion

        private record HeatDiff
        {
            public int Index1 { get; init; }
            public int Index2 { get; init; }
            public double Diff { get; init; }
            public int Compare { get; init; }
        }

        private static HeatDiff[] FindExtremes(BezierUtil.CurvatureSample[] heatmap)
        {
            //double[] weights = heatmap.
            //    Select(o => o.Weight).
            //    ToArray();

            // Weights are marching from a low point (straight line) to a high point (max pinch)
            //
            // Since that march is in a single direction, taking the diff between two points tells what side of a rise/fall
            // curve the line segment is on
            var diffs = Enumerable.Range(0, heatmap.Length - 1).
                Select(o =>
                {
                    double diff = heatmap[o + 1].Weight - heatmap[o].Weight;

                    return new HeatDiff()
                    {
                        Index1 = o,
                        Index2 = o + 1,
                        Diff = diff,
                        Compare = diff > 0 ? 1 :
                                diff < 0 ? -1 :
                                0
                    };
                }).
                ToArray();

            var diffs_distinct = new List<HeatDiff>();
            diffs_distinct.Add(diffs[0]);

            for (int i = 1; i < diffs.Length; i++)
            {
                if (diffs[i].Compare != diffs_distinct[^1].Compare)
                    diffs_distinct.Add(diffs[i]);
            }

            if (diffs_distinct[^1].Index2 != diffs[^1].Index2)      // always want the last point
                diffs_distinct.Add(diffs[^1] with { Compare = -diffs[^1].Compare });



            // Draw
            var sizes = Debug3DWindow.GetDrawSizes(heatmap.Select(o => o.Point));

            var window = new Debug3DWindow();

            DrawStretch_Heatmap(window, sizes, diffs.ToArray(), heatmap);
            DrawStretch_Diffs(window, sizes, diffs_distinct.ToArray(), heatmap);
            DrawStretch_BezierBoundries(window, sizes, heatmap);        // draw a black circle around the first and last point of each bezier segment

            window.Show();


            //var retVal = new List<StretchSegment2>();

            // ???

            //return retVal.ToArray();

            return diffs_distinct.ToArray();
        }
        private static void DrawStretch_Heatmap(Debug3DWindow window, (double dot, double line) sizes, HeatDiff[] diffs, BezierUtil.CurvatureSample[] heatmap)
        {
            foreach (var heat in heatmap)
            {
                window.AddDot(heat.Point, sizes.dot, UtilityWPF.AlphaBlend(Colors.DarkSeaGreen, Colors.DarkRed, heat.Weight));
            }

            //var lines = new List<(Point3D, Point3D)>();
            //lines.AddRange(Enumerable.Range(0, heatmap.Length - 1).Select(o => (heatmap[o].Point, heatmap[o + 1].Point)));
            //window.AddLines(lines, sizes.line * 0.75, Colors.White);

            foreach (HeatDiff diff in diffs)
            {
                Color color = diff.Compare == 1 ? Colors.RosyBrown :
                    diff.Compare == -1 ? Colors.DarkSeaGreen :
                    Colors.WhiteSmoke;

                window.AddLine(heatmap[diff.Index1].Point, heatmap[diff.Index2].Point, sizes.line * 0.75, color);
            }

            Vector3D dir = (heatmap[0].Point - heatmap[1].Point).ToUnit();
            window.AddText3D("start", heatmap[0].Point + dir * sizes.dot * 3, dir, sizes.dot * 3, Colors.Black, false);

            dir = (heatmap[^1].Point - heatmap[^2].Point).ToUnit();
            window.AddText3D("stop", heatmap[^1].Point + dir * sizes.dot * 3, dir, sizes.dot * 3, Colors.Black, false);
        }
        private static void DrawStretch_Diffs(Debug3DWindow window, (double dot, double line) sizes, HeatDiff[] diffs_distinct, BezierUtil.CurvatureSample[] heatmap)
        {
            var drawSlice = new Action<Point3D, Vector3D, int>((pos, dir, compare) =>
            {
                Color color = compare == 1 ? Colors.DarkRed :
                    compare == -1 ? Colors.DarkSeaGreen :
                    Colors.Gray;

                window.AddCircle(pos, sizes.dot * 6, sizes.line, color, new Triangle_wpf(dir, pos));
            });

            drawSlice(heatmap[diffs_distinct[0].Index1].Point, heatmap[diffs_distinct[0].Index2].Point - heatmap[diffs_distinct[0].Index1].Point, diffs_distinct[0].Compare);
            drawSlice(heatmap[diffs_distinct[^1].Index2].Point, heatmap[diffs_distinct[^1].Index2].Point - heatmap[diffs_distinct[^1].Index1].Point, diffs_distinct[^1].Compare);      // draw at index2 for the very last point (instead of index1 for all the other points)

            for (int i = 1; i < diffs_distinct.Length - 1; i++)
            {
                Vector3D dir = GetDirection(heatmap[diffs_distinct[i].Index2].Point, heatmap[diffs_distinct[i].Index1].Point, heatmap[diffs_distinct[i].Index1 - 1].Point);
                drawSlice(heatmap[diffs_distinct[i].Index1].Point, dir, diffs_distinct[i].Compare);
            }
        }
        private static void DrawStretch_BezierBoundries(Debug3DWindow window, (double dot, double line) sizes, BezierUtil.CurvatureSample[] heatmap)
        {
            var drawSlice = new Action<Point3D, Vector3D>((pos, dir) =>
            {
                window.AddCircle(pos, sizes.dot * 8, sizes.line * 0.5, Colors.Black, new Triangle_wpf(dir, pos));
            });

            drawSlice(heatmap[0].Point, heatmap[1].Point - heatmap[0].Point);
            drawSlice(heatmap[^1].Point, heatmap[^2].Point - heatmap[^1].Point);      // draw the last point of the segment (the loop below will draw the first point of this last segment)

            int segment_index = heatmap[0].SegmentIndex;

            for (int i = 1; i < heatmap.Length - 1; i++)
            {
                if (heatmap[i].SegmentIndex == segment_index)
                    continue;

                segment_index = heatmap[i].SegmentIndex;

                Vector3D dir = GetDirection(heatmap[i + 1].Point, heatmap[i].Point, heatmap[i - 1].Point);

                drawSlice(heatmap[i].Point, dir);
            }
        }

        private static Vector3D GetDirection(Point3D p0, Point3D p1, Point3D p2)
        {
            Vector3D d10 = (p0 - p1).ToUnit();
            Vector3D d12 = (p2 - p1).ToUnit();

            double angle = Vector3D.AngleBetween(d10, d12);

            Quaternion quat = new Quaternion(Vector3D.CrossProduct(d10, d12), (angle / 2) - 90);        // bisect the two lines, then rotate another 90 (this is used as the normal for the disc - the disc is what needs to be bisecting)

            return quat.GetRotatedVector(d10);
        }
    }

    #region record: StretchSegment

    public record StretchSegment
    {
        public double From { get; init; }
        public double To { get; init; }

        public BezierUtil.CurvatureSample HeatPoint_From { get; init; }
        public BezierUtil.CurvatureSample HeatPoint_To { get; init; }

        /// <summary>
        /// True: From is the attraction point, To is the repulse point
        /// False: From is the repulse point, To is the attraction point
        /// </summary>
        public bool IsFromAttracting { get; init; }

        // something to do with a bezier
    }

    #endregion
    #region record: StretchSegment2

    public record StretchSegment2
    {
        public double From { get; init; }
        public double To { get; init; }

        /// <summary>
        /// Get a point from this bezier with local percent as input, use Y for what that percent should turn into
        /// </summary>
        public BezierSegment3D_wpf Transform { get; init; }

        public double TransformPercent(double global_percent)
        {
            double local_percent = UtilityMath.GetScaledValue(0, 1, From, To, global_percent);

            double local_transformed = BezierUtil.GetPoint(local_percent, Transform).Y;

            return UtilityMath.GetScaledValue(From, To, 0, 1, local_transformed);
        }
    }

    #endregion
}
