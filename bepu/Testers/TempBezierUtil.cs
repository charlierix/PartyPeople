﻿using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
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

        private const int COUNT_RESIZEPASS_A2 = 1;

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
                    Select(o => GetPopulation(o, heatmap)).
                    ToArray();

                //DrawTestBucket(retVal, areas, heatmap, $"pass {i}");

                //double[] scales = GetScales1(areas);
                double[] scales = GetScales2(areas, retVal);
                retVal = ApplyForces2(retVal, scales, i);
            }

            return retVal;
        }
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

            Debug3DWindow window = new Debug3DWindow();
            var sizes = Debug3DWindow.GetDrawSizes(1);
            double draw_y = 0;

            var retVal = initial;

            for (int i = 0; i < COUNT_RESIZEPASS_A2 + 1; i++)
            {
                double[] areas = retVal.
                    Select(o => GetPopulation(o, heatmap)).
                    ToArray();

                Point3D[] samples = BezierUtil.GetPoints_PinchImproved(beziers.Length * 12, beziers, retVal);

                DrawSnippetUsage(window, sizes, retVal, areas, samples, i.ToString(), ref draw_y);

                if (i >= COUNT_RESIZEPASS_A2)
                    break;

                retVal = RedistributeSizes(retVal, areas, window, sizes, draw_y);
            }

            window.Show();

            return retVal;
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

        #region resize snippets attempt 1

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

        private static PathSnippet[] RedistributeSizes(PathSnippet[] snippets, double[] areas, Debug3DWindow window, (double dot, double line) sizes, double draw_y)
        {
            // Get the height of each bar
            var heights = Enumerable.Range(0, snippets.Length).
                Select(o => areas[o] / (snippets[o].To_Percent_Out - snippets[o].From_Percent_Out)).
                ToArray();

            // Figure out how to redistribute heights (flows from high to low)
            var moves = Enumerable.Range(0, snippets.Length).
                Select(o =>
                (
                    left: o == 0 ?
                        0d :
                        RedistributeSizes_GetMovement(heights[o], heights[o - 1]),

                    right: o == snippets.Length - 1 ?
                        0d :
                        RedistributeSizes_GetMovement(heights[o], heights[o + 1])
                )).
                ToArray();

            // Move the heights
            double[] new_heights = heights.ToArray();

            for (int i = 0; i < snippets.Length; i++)
            {
                if (i > 0 && moves[i].left > 0)
                {
                    new_heights[i - 1] += moves[i].left;
                    new_heights[i] -= moves[i].left;
                }

                if (i < snippets.Length - 1 && moves[i].right > 0)
                {
                    new_heights[i + 1] += moves[i].right;
                    new_heights[i] -= moves[i].right;
                }
            }

            // Generate snippets according to their new heights
            PathSnippet[] retVal = Enumerable.Range(0, snippets.Length).
                Select(o => RedistributeSizes_Rebuild(snippets[o], heights[o], new_heights[o])).
                ToArray();

            // Make sure there are no overlaps or gaps
            DrawSnippetSlide2(window, sizes, -3.75, draw_y, retVal);

            retVal = RedistributeSizes_LockIn(retVal);

            DrawSnippetSlide2(window, sizes, -2.5, draw_y, retVal);

            return snippets;
        }
        private static double RedistributeSizes_GetMovement(double height_source, double height_dest)
        {
            double PERCENT = 0.2;
            double MAX_MOVE_PERCENT = 0.1;

            double diff = height_source - height_dest;

            if (diff < 0)
                return 0;

            return Math.Min(diff * PERCENT, height_dest * MAX_MOVE_PERCENT);
        }
        private static PathSnippet RedistributeSizes_Rebuild(PathSnippet snippet, double old_height, double new_height)
        {
            double width = snippet.To_Percent_Out - snippet.From_Percent_Out;

            double center = snippet.From_Percent_Out + width / 2;

            // treat the difference in height as a percent
            double percent = new_height / old_height;
            width *= percent;

            double half_width = width * percent / 2;

            return snippet with
            {
                From_Percent_Out = center - half_width,
                To_Percent_Out = center + half_width,
            };
        }
        private static PathSnippet[] RedistributeSizes_LockIn(PathSnippet[] snippets)
        {
            SnippetPos[] positions = ConvertSnippets(snippets);

            positions = SlideSnippets3(positions);

            positions = SolidifySnippets(positions);

            return AdjustSnippets(snippets, positions);
        }

        private static void DrawSnippetUsage(Debug3DWindow window, (double dot, double line) sizes, PathSnippet[] snippets, double[] areas, Point3D[] samples, string label, ref double draw_y)
        {
            window.AddText3D(label, new Point3D(0, draw_y + 0.5, 0), new Vector3D(0, 0, 1), 0.2, Colors.Black, false);

            DrawSnippetUsage_Snippets_Area(window, sizes, draw_y, snippets, areas);
            DrawSnippetUsage_Snippets_Bezier(window, sizes, draw_y, samples);

            draw_y += 1.25;
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

        private static void DrawSnippetSlide2(Debug3DWindow window, (double dot, double line) sizes, double draw_x, double draw_y, PathSnippet[] snippets)
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

            var colors = UtilityWPF.GetRandomColors(snippets.Length, 90, 180);

            for (int i = 0; i < snippets.Length; i++)
            {
                var pos = new SnippetPos()
                {
                    From = snippets[i].From_Percent_Out,
                    To = snippets[i].To_Percent_Out,
                    Center = Math1D.Avg(snippets[i].From_Percent_Out, snippets[i].To_Percent_Out),
                };

                draw_snippet(pos, colors[i]);
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
    }
}
