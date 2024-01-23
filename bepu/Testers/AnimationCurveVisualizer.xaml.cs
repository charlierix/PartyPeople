using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF.Controls3D;
using Game.Math_WPF.WPF.Viewers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace Game.Bepu.Testers
{
    public partial class AnimationCurveVisualizer : Window
    {
        public AnimationCurveVisualizer()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;

            txtKeys.Text =
@"0
1
2
3";

            txtValues.Text =
@"0
3
2
2.5";
        }

        private void Visualize_Click(object sender, RoutedEventArgs e)
        {
            const int COUNT = 144;

            try
            {
                var (err_msg, keys, values) = ParseTextboxes(txtKeys.Text, txtValues.Text);
                if (err_msg != null) 
                {
                    MessageBox.Show(err_msg, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var curve = new AnimationCurve();
                for (int i = 0; i < keys.Length; i++)
                    curve.AddKeyValue(keys[i], values[i]);

                double min_key = keys.Min();
                double max_key = keys.Max();

                var window = new Debug3DWindow()
                {
                    Title = "Exact Key Min-Max",
                };

                double width = (max_key - min_key) / COUNT;

                double[] graph_values = Enumerable.Range(0, COUNT).
                    Select(o => curve.Evaluate(min_key + (width * o))).
                    ToArray();

                var graph = Debug3DWindow.GetGraph(graph_values);

                window.AddGraph(graph, new Point3D(), 12);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Visualize_Extra_Click(object sender, RoutedEventArgs e)
        {
            const int COUNT = 144;

            try
            {
                var (err_msg, keys, values) = ParseTextboxes(txtKeys.Text, txtValues.Text);
                if (err_msg != null)
                {
                    MessageBox.Show(err_msg, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var curve = new AnimationCurve();
                for (int i = 0; i < keys.Length; i++)
                    curve.AddKeyValue(keys[i], values[i]);

                double min_key = keys.Min();
                double max_key = keys.Max();

                var window = new Debug3DWindow()
                {
                    Title = "Extra before and after",
                };

                double margin = (max_key - min_key) * 0.1;
                double min = min_key - margin;
                double max = max_key + margin;
                double width = (max - min) / COUNT;

                var graph_values = Enumerable.Range(0, COUNT).
                    Select(o => curve.Evaluate(min + (width * o))).
                    ToArray();

                var graph = Debug3DWindow.GetGraph(graph_values);

                window.AddGraph(graph, new Point3D(), 12);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static (string err_msg, double[] keys, double[] values) ParseTextboxes(string keys_text, string values_text)
        {
            var keys = GetValues(keys_text);
            if (keys.err_msg != null)
                return ($"Key Error\r\n{keys.err_msg}", null, null);

            var values = GetValues(values_text);
            if (values.err_msg != null)
                return ($"Value Error\r\n{values.err_msg}", null, null);

            if (keys.values.Length == 0 && values.values.Length == 0)
                return ("Please enter keys and values", null, null);

            else if (keys.values.Length != values.values.Length)
                return ($"Need the same number of keys and values.  keys: {keys.values.Length}, values: {values.values.Length}", null, null);

            return (null, keys.values, values.values);
        }

        private static (double[] values, string err_msg) GetValues(string text)
        {
            string[] lines = text.
                Replace("\r\n", "\n").
                Split('\n');

            var values = new List<double>();

            foreach (string line1 in lines)
            {
                string line2 = line1.Trim();
                if (line2 == "")
                    continue;

                if (!double.TryParse(line2, out double value))
                    return (null, $"Couldn't parse: '{line2}'");

                values.Add(value);
            }

            return (values.ToArray(), null);
        }
    }
}
