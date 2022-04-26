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

        private void Cone_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (!_initialized)
                    return;

                GetConeProps();     // useing this to set/clear the error effect
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

                var points = Math3D.GetRandomVectors_ConeShell(props.Count, new Vector3D(0, 1, 0), -props.Angle, props.Angle, props.HeightMin, props.HeightMax);

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

                var vectors = Math3D.GetRandomVectors_ConeShell_EvenDist(split.Movable, new Vector3D(0, 1, 0), props.Angle, props.HeightMin, props.HeightMax, split.Movable_Mults, split.Static, split.Static_Mults, 0, props.Count);

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

        /// <summary>
        /// This gets stats as well as sets/clear error effect
        /// </summary>
        private ConeProps GetConeProps()
        {
            var retVal = new ConeProps();

            bool hadError = false;

            // Count
            if (int.TryParse(txtCone_Count.Text, out int val_int))
            {
                retVal.Count = val_int;
                txtCone_Count.Effect = null;
            }
            else
            {
                hadError = true;
                txtCone_Count.Effect = _errorEffect;
            }

            // Angle
            if (double.TryParse(txtCone_Angle.Text, out double val_float))
            {
                retVal.Angle = val_float;
                txtCone_Angle.Effect = null;
            }
            else
            {
                hadError = true;
                txtCone_Angle.Effect = _errorEffect;
            }

            // HeightMin
            if (double.TryParse(txtCone_HeightMin.Text, out val_float))
            {
                retVal.HeightMin = val_float;
                txtCone_HeightMin.Effect = null;
            }
            else
            {
                hadError = true;
                txtCone_HeightMin.Effect = _errorEffect;
            }

            // HeightMax
            if (double.TryParse(txtCone_HeightMax.Text, out val_float))
            {
                retVal.HeightMax = val_float;
                txtCone_HeightMax.Effect = null;
            }
            else
            {
                hadError = true;
                txtCone_HeightMax.Effect = _errorEffect;
            }

            // DotSizeMult
            if (double.TryParse(txtCone_DotSizeMult.Text, out val_float))
            {
                retVal.DotSizeMult = val_float;
                txtCone_DotSizeMult.Effect = null;
            }
            else
            {
                hadError = true;
                txtCone_DotSizeMult.Effect = _errorEffect;
            }

            // Iterations
            if (int.TryParse(txtCone_Iterations.Text, out val_int))
            {
                retVal.Iterations = val_int;
                txtCone_Iterations.Effect = null;
            }
            else
            {
                hadError = true;
                txtCone_Iterations.Effect = _errorEffect;
            }

            if (hadError)
                return null;
            else
                return retVal;
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
