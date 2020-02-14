using Accord.MachineLearning.Clustering;
using Game.Math_WPF.Mathematics;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Game.ML
{
    public static class UtilityML
    {
        #region class: TextInput

        private class TextInput
        {
            [LoadColumn(0)]
            public string Text;

            public override string ToString()
            {
                return Text ?? "<null>";
            }
        }

        #endregion
        #region class: TSNEArgs

        public class TSNEArgs
        {
            /// <summary>
            /// 1 is 1D, 2 is 2D, 3 is 3D
            /// </summary>
            public int OutputDimensions { get; set; } = 2;

            public int MaxIterations { get; set; } = 1000;

            public CancellationToken Cancel { get; set; } = CancellationToken.None;

            public Action<TSNEResult> IntermediateResult { get; set; }

            //TODO: Option for how often the delagate should fire (probably milliseconds)
        }

        #endregion

        /// <summary>
        /// This is a wrapper to ML.Net's string to vector
        /// </summary>
        /// <remarks>
        /// Returning a custom struct so that there's no need for callers to reference ml.net
        /// </remarks>
        public static VectorND_Sparse[] StringToVector(string[] strings)
        {
            const string COLUMN = "Features";

            TextInput[] inputs = strings.
                Select(o => new TextInput() { Text = o }).
                ToArray();

            MLContext context = new MLContext();

            IDataView data = context.Data.LoadFromEnumerable(inputs);

            var transform = context.Transforms.Text.FeaturizeText(COLUMN, nameof(TextInput.Text));

            var preview = transform.Preview(data, strings.Length);

            var vectors = preview.RowView.
                Select(o => (VBuffer<float>)o.Values.First(p => p.Key == COLUMN).Value).
                ToArray();

            return vectors.
                Select(o => ConvertToSparse(o)).        // The vectors returned by ml.net's string to vector are almost certain to be sparse, so just pretend they are sparse
                ToArray();
        }

        public static void TSNE(VectorND_Sparse[] vectors, TSNEArgs args)
        {
            TSNE(vectors.Select(o => o.ToDense()).ToArray(), args);
        }
        public static void TSNE(VectorND[] vectors, TSNEArgs args)
        {
            double[][] pointsInputs = vectors.
                Select(o => o.ToArray()).
                ToArray();

            Task.Run(() =>
            {
                // Do TSNE
                var tsne = new TSNE_Custom()
                {
                    NumberOfInputs = pointsInputs[0].Length,
                    NumberOfOutputs = args.OutputDimensions,        // 2 is 2D, 3 is 3D
                    MaxIterations = args.MaxIterations,
                };

                tsne.Perplexity = GetPerplexity(pointsInputs.Length);

                tsne.Transform(pointsInputs, args.Cancel, args.IntermediateResult);
            });
        }

        #region Private Methods

        private static VectorND_Sparse ConvertToSparse(VBuffer<float> vector)
        {
            double[] values = vector.GetValues().
                ToArray().
                Select(o => Convert.ToDouble(o)).
                ToArray();

            int[] indices;
            if (vector.IsDense)
            {
                // This function is hard coded to return sparse
                indices = Enumerable.Range(0, vector.Length).ToArray();
            }
            else
            {
                indices = vector.GetIndices().ToArray();
            }

            return new VectorND_Sparse(indices, values, vector.Length);
        }

        private static double GetPerplexity(int numPoints)
        {
            // This is from the tsne class
            //if (N - 1 < 3 * perplexity) throw new Exception(String.Format("Perplexity too large for the number of data points. For {0} points, should be less than {1}", N, (N - 1) / 3.0));

            return Math.Min(((numPoints - 1) / 3d) * .9, 50);
        }

        #endregion

        #region debug draw

        //private static TranslateTransform3D[] AddOutputDots(Debug3DWindow window, int count)
        //{
        //    const double RADIUS = 25;

        //    // Copied from Debug3DWindow.GetDots

        //    var retVal = new TranslateTransform3D[count];

        //    var sizes = Debug3DWindow.GetDrawSizes(RADIUS);
        //    window.SetCamera(new Point3D(0, 0, RADIUS * .75), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0));

        //    Model3DGroup geometries = new Model3DGroup();

        //    // Visible Points
        //    Material material = Debug3DWindow.GetMaterial(false, Colors.Black);

        //    for (int cntr = 0; cntr < count; cntr++)
        //    {
        //        retVal[cntr] = new TranslateTransform3D();

        //        GeometryModel3D geometry = new GeometryModel3D()
        //        {
        //            Material = material,
        //            BackMaterial = material,
        //            Geometry = UtilityWPF.GetSphere_Ico(sizes.dot, 1, true),
        //            Transform = retVal[cntr],
        //        };

        //        geometries.Children.Add(geometry);
        //    }

        //    // Points for mouse cast detection
        //    //material = Debug3DWindow.GetMaterial(false, Colors.Transparent);

        //    //for (int cntr = 0; cntr < count; cntr++)
        //    //{
        //    //    //retVal[cntr] = new TranslateTransform3D();

        //    //    GeometryModel3D geometry = new GeometryModel3D()
        //    //    {
        //    //        Material = material,
        //    //        BackMaterial = material,
        //    //        Geometry = UtilityWPF.GetSphere_Ico(sizes.dot * 4, 1, true),
        //    //        Transform = retVal[cntr],
        //    //    };

        //    //    geometries.Children.Add(geometry);
        //    //}

        //    window.AddModel(geometries);

        //    return retVal;
        //}
        //private static void ShowTSNEOutput(Debug3DWindow window, TSNEResult result, TranslateTransform3D[] transforms/*, TextInput[] strings*/, double[][] pointsInputs, double perplexity, ref int numTimesCalled)
        //{
        //    window.Title = $"{result.OutputPoints[0].Length}D";

        //    Point3D[] points3D = result.OutputPoints.
        //        Select(o => o.ToPoint3D(false)).
        //        ToArray();

        //    window.Messages_Bottom.Clear();
        //    window.Messages_Top.Clear();
        //    //window.Visuals3D.Clear();

        //    for (int cntr = 0; cntr < result.OutputPoints.Length; cntr++)
        //    {
        //        transforms[cntr].OffsetX = points3D[cntr].X;
        //        transforms[cntr].OffsetY = points3D[cntr].Y;
        //        transforms[cntr].OffsetZ = points3D[cntr].Z;
        //    }


        //    // Only add it the first time
        //    if (numTimesCalled == 0)
        //    {
        //        window.MouseMove += (s2, e2) =>
        //        {
        //            var hits = window.CastRay(e2);

        //            if (hits.Count > 0)
        //            {
        //                var closest = result.OutputPoints.
        //                    Select((o, i) => new { index = i, dist = (points3D[i] - hits[0].Point).LengthSquared }).
        //                    OrderBy(o => o.dist).
        //                    First();

        //                //window.Title = $"\"{strings[closest.index].Text}\"";
        //                window.Title = closest.index.ToString();
        //            }
        //            else
        //            {
        //                window.Title = "";
        //            }
        //        };
        //    }


        //    window.AddText($"vector size: {pointsInputs[0].Length}");
        //    window.AddText($"num vectors: {pointsInputs.Length}");
        //    window.AddText($"perplexity: {perplexity.ToStringSignificantDigits(3)}");
        //    window.AddText($"iterations: {result.Iterations.ToString("N0")}");
        //    if (result.Error != null)
        //    {
        //        window.AddText($"error: {result.Error.Value.ToStringSignificantDigits(3)}");
        //    }

        //    double maxRadius = result.OutputPoints.
        //        SelectMany(o => o).
        //        Select(o => Math.Abs(o)).
        //        Max();

        //    window.AddText($"max radius: {maxRadius.ToStringSignificantDigits(3)}");



        //    numTimesCalled++;
        //}

        #endregion
    }
}
