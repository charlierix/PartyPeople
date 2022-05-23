using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace Game.Bepu.Testers
{
    public partial class EvenDistribution : Window
    {
        #region class: Dot

        private class Dot
        {
            public Visual3D Visual { get; set; }
            public TranslateTransform3D Transform { get; set; }
            public Point3D Position { get; set; }
            public bool IsStatic { get; set; }
            public double SizeMult { get; set; }
        }

        #endregion
        #region class: CubeProps

        private class CubeProps
        {
            public int Count { get; set; }

            public double SizeX { get; set; }
            public double SizeY { get; set; }
            public double SizeZ { get; set; }

            public double DotSizeMult { get; set; }

            public int Iterations { get; set; }
        }

        #endregion
        #region class: ConeProps

        private class ConeProps
        {
            public int Count { get; set; }

            public double Angle { get; set; }

            public double HeightMin { get; set; }
            public double HeightMax { get; set; }

            public double DotSizeMult { get; set; }

            public int Iterations { get; set; }
        }

        #endregion
        #region class: Dots_Movable_Static

        /// <summary>
        /// This is the dots split into params that math even dist functions like
        /// </summary>
        private class Dots_Movable_Static
        {
            // These three are the same size
            public Vector3D[] Movable { get; set; }
            public double[] Movable_Mults { get; set; }
            /// <summary>
            /// Map from this.Movable to the index into dots
            /// </summary>
            public int[] Movable_Indices { get; set; }

            // These two could be null (these two are the same size if non null)
            public Vector3D[] Static { get; set; }
            public double[] Static_Mults { get; set; }

            public static Dots_Movable_Static Split(Dot[] dots)
            {
                var movable = new List<(Vector3D, double, int)>();
                var statiic = new List<(Vector3D, double)>();

                for (int i = 0; i < dots.Length; i++)
                {
                    if (dots[i].IsStatic)
                        statiic.Add((dots[i].Position.ToVector(), dots[i].SizeMult));
                    else
                        movable.Add((dots[i].Position.ToVector(), dots[i].SizeMult, i));
                }

                var retVal = new Dots_Movable_Static()
                {
                    Movable = movable.
                        Select(o => o.Item1).
                        ToArray(),

                    Movable_Mults = movable.
                        Select(o => o.Item2).
                        ToArray(),

                    Movable_Indices = movable.
                        Select(o => o.Item3).
                        ToArray(),
                };

                if (statiic.Count > 0)
                {
                    retVal.Static = statiic.
                        Select(o => o.Item1).
                        ToArray();

                    retVal.Static_Mults = statiic.
                        Select(o => o.Item2).
                        ToArray();
                }

                return retVal;
            }
        }

        #endregion

        #region Declaration Section

        private List<Dot> _dots = new List<Dot>();

        private TrackBallRoam _trackball = null;

        private readonly DropShadowEffect _errorEffect;

        private bool _initialized = false;

        #endregion

        #region Constructor

        public EvenDistribution()
        {
            InitializeComponent();

            _trackball = new TrackBallRoam(_camera);
            _trackball.EventSource = grdViewPort;       //NOTE:  If this control doesn't have a background color set, the trackball won't see events (I think transparent is ok, just not null)
            _trackball.AllowZoomOnMouseWheel = true;
            _trackball.Mappings.AddRange(TrackBallMapping.GetPrebuilt(TrackBallMapping.PrebuiltMapping.MouseComplete));
            _trackball.ShouldHitTestOnOrbit = false;

            _errorEffect = new DropShadowEffect()
            {
                Color = UtilityWPF.ColorFromHex("D03030"),
                Direction = 0,
                ShadowDepth = 0,
                BlurRadius = 12,
                Opacity = .8,
            };

            _initialized = true;
        }

        #endregion

        #region Events Listeners

        private void Cube_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (!_initialized)
                    return;

                GetCubeProps();     // using this to set/clear the error effect
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Cone_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (!_initialized)
                    return;

                GetConeProps();     // using this to set/clear the error effect
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CubeAddPoints_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CubeProps props = GetCubeProps();
                if (props == null)
                {
                    MessageBox.Show("Couldn't parse properties", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Vector3D min = new Vector3D(-props.SizeX / 2, -props.SizeY / 2, -props.SizeZ / 2);
                Vector3D max = new Vector3D(props.SizeX / 2, props.SizeY / 2, props.SizeZ / 2);

                var points = Enumerable.Range(0, props.Count).
                    Select(o => Math3D.GetRandomVector(min, max)).
                    ToArray();

                for (int i = 0; i < props.Count; i++)
                {
                    Dot dot = GetDot(chkCube_IsStatic.IsChecked.Value, points[i].ToPoint(), props.DotSizeMult);

                    _viewport.Children.Add(dot.Visual);
                    _dots.Add(dot);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ConeAddPoints_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConeProps props = GetConeProps();
                if (props == null)
                {
                    MessageBox.Show("Couldn't parse properties", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                //var points = Math3D.GetRandomVectors_ConeShell(props.Count, new Vector3D(0, 1, 0), -props.Angle, props.Angle, props.HeightMin, props.HeightMax);
                var points = Math3D.GetRandomVectors_ConeShell(props.Count, new Vector3D(0, 1, 0), 0, props.Angle, props.HeightMin, props.HeightMax);

                for (int i = 0; i < props.Count; i++)
                {
                    Dot dot = GetDot(chkCone_IsStatic.IsChecked.Value, points[i].ToPoint(), props.DotSizeMult);

                    _viewport.Children.Add(dot.Visual);
                    _dots.Add(dot);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CubeIterate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dots.Count == 0)
                {
                    MessageBox.Show("Need to add points first", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                CubeProps props = GetCubeProps();
                if (props == null)
                {
                    MessageBox.Show("Couldn't parse properties", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var split = Dots_Movable_Static.Split(_dots.ToArray());

                VectorND min = new VectorND(-props.SizeX / 2, -props.SizeY / 2, -props.SizeZ / 2);
                VectorND max = new VectorND(props.SizeX / 2, props.SizeY / 2, props.SizeZ / 2);

                VectorND[] movable = split.Movable.
                    Select(o => o.ToVectorND()).
                    ToArray();

                VectorND[] existing_static = null;
                if (split.Static != null)
                {
                    existing_static = split.Static.
                        Select(o => o.ToVectorND()).
                        ToArray();
                }

                var vectors = MathND.GetRandomVectors_Cube_EventDist(movable, (min, max), split.Movable_Mults, existing_static, split.Static_Mults, 0, props.Iterations);

                for (int i = 0; i < vectors.Length; i++)
                {
                    _dots[split.Movable_Indices[i]].Transform.OffsetX = vectors[i][0];
                    _dots[split.Movable_Indices[i]].Transform.OffsetY = vectors[i][1];
                    _dots[split.Movable_Indices[i]].Transform.OffsetZ = vectors[i][2];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ConeIterate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_dots.Count == 0)
                {
                    MessageBox.Show("Need to add points first", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ConeProps props = GetConeProps();
                if (props == null)
                {
                    MessageBox.Show("Couldn't parse properties", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var split = Dots_Movable_Static.Split(_dots.ToArray());

                var vectors = Math3D.GetRandomVectors_ConeShell_EvenDist(split.Movable, new Vector3D(0, 1, 0), props.Angle, props.HeightMin, props.HeightMax, split.Movable_Mults, split.Static, split.Static_Mults, 0, props.Iterations);

                for (int i = 0; i < vectors.Length; i++)
                {
                    _dots[split.Movable_Indices[i]].Transform.OffsetX = vectors[i].X;
                    _dots[split.Movable_Indices[i]].Transform.OffsetY = vectors[i].Y;
                    _dots[split.Movable_Indices[i]].Transform.OffsetZ = vectors[i].Z;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewport.Children.RemoveAll(_dots.Select(o => o.Visual));
                _dots.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        // These get stats as well as set/clear error effects
        private CubeProps GetCubeProps()
        {
            var retVal = new CubeProps();

            bool hadError = false;

            // Count
            var count = ParseTextBox_Int(txtCube_Count, _errorEffect);
            hadError |= count.hadError;
            retVal.Count = count.cast;

            // SizeX
            var sizeX = ParseTextBox_Float(txtCube_SizeX, _errorEffect);
            hadError |= sizeX.hadError;
            retVal.SizeX = sizeX.cast;

            // SizeY
            var sizeY = ParseTextBox_Float(txtCube_SizeY, _errorEffect);
            hadError |= sizeY.hadError;
            retVal.SizeY = sizeY.cast;

            // SizeZ
            var sizeZ = ParseTextBox_Float(txtCube_SizeZ, _errorEffect);
            hadError |= sizeZ.hadError;
            retVal.SizeZ = sizeZ.cast;

            // DotSizeMult
            var mult = ParseTextBox_Float(txtCube_DotSizeMult, _errorEffect);
            hadError |= mult.hadError;
            retVal.DotSizeMult = mult.cast;

            // Iterations
            var iter = ParseTextBox_Int(txtCube_Iterations, _errorEffect);
            hadError |= iter.hadError;
            retVal.Iterations = iter.cast;

            if (hadError)
                return null;
            else
                return retVal;
        }
        private ConeProps GetConeProps()
        {
            var retVal = new ConeProps();

            bool hadError = false;

            // Count
            var count = ParseTextBox_Int(txtCone_Count, _errorEffect);
            hadError |= count.hadError;
            retVal.Count = count.cast;

            // Angle
            var angle = ParseTextBox_Float(txtCone_Angle, _errorEffect);
            hadError |= angle.hadError;
            retVal.Angle = angle.cast;

            // HeightMin
            var heightMin = ParseTextBox_Float(txtCone_HeightMin, _errorEffect);
            hadError |= heightMin.hadError;
            retVal.HeightMin = heightMin.cast;

            // HeightMax
            var heightMax = ParseTextBox_Float(txtCone_HeightMax, _errorEffect);
            hadError |= heightMax.hadError;
            retVal.HeightMax = heightMax.cast;

            // DotSizeMult
            var mult = ParseTextBox_Float(txtCone_DotSizeMult, _errorEffect);
            hadError |= mult.hadError;
            retVal.DotSizeMult = mult.cast;

            // Iterations
            var iter = ParseTextBox_Int(txtCone_Iterations, _errorEffect);
            hadError |= iter.hadError;
            retVal.Iterations = iter.cast;

            if (hadError)
                return null;
            else
                return retVal;
        }

        private static (bool hadError, int cast) ParseTextBox_Int(TextBox textbox, Effect effect)
        {
            bool hadError = false;

            if (int.TryParse(textbox.Text, out int val_int))
            {
                textbox.Effect = null;
            }
            else
            {
                hadError = true;
                textbox.Effect = effect;
            }

            return (hadError, val_int);
        }
        private static (bool hadError, double cast) ParseTextBox_Float(TextBox textbox, Effect effect)
        {
            bool hadError = false;

            if (double.TryParse(textbox.Text, out double val_float))
            {
                textbox.Effect = null;
            }
            else
            {
                hadError = true;
                textbox.Effect = effect;
            }

            return (hadError, val_float);
        }

        private static Dot GetDot(bool isStatic, Point3D position, double sizeMult)
        {
            string color_diff, color_spec;
            if (isStatic)
            {
                color_diff = "8F7D79";
                color_spec = "40989898";
            }
            else
            {
                color_diff = "666069";
                color_spec = "40989898";
            }

            MaterialGroup material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(UtilityWPF.BrushFromHex(color_diff)));
            material.Children.Add(new SpecularMaterial(UtilityWPF.BrushFromHex(color_spec), 2));

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Material = material;
            geometry.BackMaterial = material;
            geometry.Geometry = UtilityWPF.GetSphere_Ico(0.05, 1, true);

            var transform = new TranslateTransform3D(position.ToVector());
            geometry.Transform = transform;

            var visual = new ModelVisual3D { Content = geometry };

            return new Dot()
            {
                Visual = visual,
                Transform = transform,

                IsStatic = isStatic,
                Position = position,
                SizeMult = sizeMult,
            };
        }

        #endregion
    }
}
