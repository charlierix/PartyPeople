using Accord.Math.Optimization.Losses;
using Accord.Statistics.Models.Regression.Linear;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers
{
    public partial class CurveFitting : Window
    {
        private readonly DropShadowEffect _errorEffect;

        private Point[] _samples = new Point[0];

        #region Constructor

        public CurveFitting()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;

            _errorEffect = new DropShadowEffect()
            {
                Color = UtilityWPF.ColorFromHex("C02020"),
                Direction = 0,
                ShadowDepth = 0,
                BlurRadius = 8,
                Opacity = .8,
            };
        }

        #endregion

        #region Event Listeners

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                txtInputs.Text =
@"0 0
1 1
2 4";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtInputs_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                Point[] parsed = ParseSamplePoints(txtInputs.Text);
                if (parsed == null)
                {
                    txtInputs.Effect = _errorEffect;
                    return;
                }

                txtInputs.Effect = null;

                _samples = parsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void txtDegree_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (int.TryParse(txtDegree.Text, out _))
                {
                    txtDegree.Effect = null;
                }
                else
                {
                    txtDegree.Effect = _errorEffect;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PolyLeastSquares_Click(object sender, RoutedEventArgs e)
        {
            //http://accord-framework.net/docs/html/T_Accord_Statistics_Models_Regression_Linear_PolynomialRegression.htm

            try
            {
                if (_samples == null || _samples.Length == 0)
                {
                    MessageBox.Show("Invalid samples", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(txtDegree.Text, out int degree))
                {
                    MessageBox.Show("Invalid degree", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Retrieve the input and output data:
                double[] inputs = _samples.
                    Select(o => o.X).
                    ToArray();

                double[] outputs = _samples.
                    Select(o => o.Y).
                    ToArray();

                // Create a learning algorithm
                var ls = new PolynomialLeastSquares()
                {
                    Degree = degree,
                };

                // Use the algorithm to learn a polynomial
                PolynomialRegression poly = ls.Learn(inputs, outputs);

                // The learned polynomial will be given by
                //string str = poly.ToString("N1"); // "y(x) = 1.0x^2 + 0.0x^1 + 0.0"

                // Where its weights can be accessed using
                //double[] weights = poly.Weights;   // { 1.0000000000000024, -1.2407665029287351E-13 }
                //double intercept = poly.Intercept; // 1.5652369518855253E-12

                // Finally, use this polynomial to predict values for the input data
                double[] pred = poly.Transform(inputs);

                // Where the mean-squared-error (MSE) should be
                double error = new SquareLoss(outputs).Loss(pred); // 0.0


                txtResult.Text = $"{poly}\r\n{poly.ToString("N6")}\r\n{poly.ToString("N3")}\r\n{poly.ToString("N1")}";

                PlotResult(inputs, _samples, poly, error, false);
                PlotResult(inputs, _samples, poly, error, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private static Point[] ParseSamplePoints(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string[] lines = text.
                Replace("\r\n", "\n").
                Split('\n');

            var retVal = new List<Point>();

            foreach (string line in lines)
            {
                if (line.Trim() == "")
                    continue;

                Match split_xy = Regex.Match(line, @"^\s*(?<x>[^\s]+)\s+(?<y>[^\s]+)\s*$");
                if (!split_xy.Success)
                    return null;

                if (!double.TryParse(split_xy.Groups["x"].Value, out double x))
                    return null;

                if (!double.TryParse(split_xy.Groups["y"].Value, out double y))
                    return null;

                retVal.Add(new Point(x, y));
            }

            return retVal.ToArray();
        }

        private static void PlotResult(double[] inputs, Point[] samples, PolynomialRegression poly, double error, bool should_normalize)
        {
            var window = new Debug3DWindow()
            {
                Title = should_normalize ? "Normalized" : "1:1",
            };

            double min_x = inputs.Min();
            double max_x = inputs.Max();

            int sample_count = 144;

            var examples = Enumerable.Range(0, sample_count).
                Select(o => UtilityMath.GetScaledValue(min_x, max_x, 0, sample_count - 1, o)).
                Select(o => new Point3D(o, poly.Transform(o), 0)).
                ToArray();

            var aabb = Math3D.GetAABB(examples.Concat(samples.Select(o => o.ToPoint3D())));

            if (should_normalize)
            {
                var normalized = NormalizeExamples(samples, examples, aabb.min, aabb.max);
                samples = normalized.samples;
                examples = normalized.examples;
                aabb = Math3D.GetAABB(examples.Concat(samples.Select(o => o.ToPoint3D())));
            }

            var sizes = Debug3DWindow.GetDrawSizes(Math.Max(aabb.max.X - aabb.min.X, aabb.max.Y - aabb.min.Y));

            window.AddLine(new Point3D(aabb.min.X, aabb.min.Y, 0), new Point3D(aabb.max.X, aabb.min.Y, 0), sizes.line, Colors.Black);
            window.AddLine(new Point3D(aabb.min.X, aabb.min.Y, 0), new Point3D(aabb.min.X, aabb.max.Y, 0), sizes.line, Colors.Black);

            window.AddLines(examples, sizes.line, Colors.White);

            window.AddDots(samples.Select(o => o.ToPoint3D()), sizes.dot, Colors.DodgerBlue);

            window.AddText(poly.ToString("N2"));
            window.AddText($"error: {error}");


            window.Show();
        }

        private static (Point[] samples, Point3D[] examples) NormalizeExamples(Point[] samples, Point3D[] examples, Point3D aabb_min, Point3D aabb_max)
        {
            var samples2 = samples.
                Select(o => new Point(
                    UtilityMath.GetScaledValue(0, 1, aabb_min.X, aabb_max.X, o.X),
                    UtilityMath.GetScaledValue(0, 1, aabb_min.Y, aabb_max.Y, o.Y))).
                ToArray();

            var examples2 = examples.
                Select(o => new Point3D(
                    UtilityMath.GetScaledValue(0, 1, aabb_min.X, aabb_max.X, o.X),
                    UtilityMath.GetScaledValue(0, 1, aabb_min.Y, aabb_max.Y, o.Y),
                    UtilityMath.GetScaledValue(0, 1, aabb_min.Z, aabb_max.Z, o.Z))).
                ToArray();

            return (samples2, examples2);
        }

        #endregion
    }
}
