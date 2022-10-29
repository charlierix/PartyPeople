using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
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
        private const int COUNT_RESIZEPASS = 8;     // it oscillates around, which makes me think the scale expansion is too aggressive
        private const int COUNT_SLIDEPASS = 3;

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

            for (int i = 0; i < COUNT_RESIZEPASS; i++)
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
            double GROW_PERCENT = 1;
            double SHRINK_PERCENT = 6;

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

                retVal[i] = UtilityMath.Clamp(scale, 0.15, 2);
            }

            return retVal;
        }

        private static PathSnippet[] ApplyForces1(PathSnippet[] snippets, double[] scales, int iteration)
        {
            var positions = ConvertSnippets(snippets);

            DrawSnippetSlide(positions, $"orig {iteration}");

            positions = ScaleSnippets(positions, scales);

            for (int i = 0; i < COUNT_SLIDEPASS; i++)
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

            for (int i = 0; i < COUNT_SLIDEPASS; i++)
            {
                positions = positions.
                    Select((o,j) => o with
                    {
                        From = o.Center - half_widths[j],
                        To = o.Center + half_widths[j],
                    }).
                    ToArray();

                positions = SlideSnippets3(positions, iteration, i);
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
        private static SnippetPos[] SlideSnippets3(SnippetPos[] positions, int iteration_outer, int iteration_inner)
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

            var moved = MathND.GetRandomVectors_Cube_EventDist(movable, (min, max), mults, stopIterationCount: COUNT_SLIDEPASS);

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
    #region record: SnippetPos

    // This is just an easier structure to move around
    public record SnippetPos
    {
        public double From { get; init; }
        public double To { get; init; }
        public double Center { get; init; }
        public double Width => To - From;
    }


    #endregion
}
