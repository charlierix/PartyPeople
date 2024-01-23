using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using Game.Math_WPF.WPF.Viewers;
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
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Game.Bepu.Testers
{
    public partial class Pendulum : Window
    {
        #region class: AnchoredBall

        private class AnchoredBall
        {
            public Visual3D[] Visuals { get; set; }

            public TranslateTransform3D Transform_Anchor { get; set; }
            public TranslateTransform3D Transform_Ball { get; set; }
            public BillboardLine3D Rope { get; set; }

            public Point3D Position_Anchor { get; set; }
            public Point3D Position_Ball { get; set; }
            public Vector3D Velocity_Ball { get; set; }

            public double RopeLength { get; set; }
        }

        #endregion
        #region record: TracePoint

        private record TracePoint
        {
            public Visual3D Visual { get; init; }
            public Point3D Point { get; init; }
            // maybe time
        }

        #endregion

        #region Declaration Section

        private const double MAP_MAX_HORZ = 72;
        private const double MAP_MAX_VERT = 36;

        private TrackBallRoam _trackball = null;

        private DispatcherTimer _timer = null;

        private AnchoredBall _ball = null;

        private List<TracePoint> _tracePoints = new List<TracePoint>();

        private bool _initialized = false;

        #endregion

        #region Constructor

        public Pendulum()
        {
            InitializeComponent();

            _trackball = new TrackBallRoam(_camera);
            _trackball.EventSource = grdViewPort;       //NOTE:  If this control doesn't have a background color set, the trackball won't see events (I think transparent is ok, just not null)
            _trackball.AllowZoomOnMouseWheel = true;
            _trackball.Mappings.AddRange(TrackBallMapping.GetPrebuilt(TrackBallMapping.PrebuiltMapping.MouseComplete));
            _trackball.ShouldHitTestOnOrbit = false;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(10);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            _initialized = true;
        }

        #endregion

        #region Event Listeners

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!_initialized)
                    return;

                if (_ball != null)
                    UpdateBall(_timer.Interval.TotalSeconds);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void chkTracePosition_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_initialized)
                    return;

                if (!chkTracePosition.IsChecked.Value)
                {
                    _viewport.Children.RemoveAll(_tracePoints.Select(o => o.Visual));
                    _tracePoints.Clear();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void trkRopeLength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (!_initialized || _ball == null)
                    return;

                _ball.RopeLength = trkRopeLength.Value;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void trkStartAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (!_initialized || _ball == null)
                    return;

                Clear();
                CreateBall();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AtOrigin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clear();
                CreateBall();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void MoveAnchor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_ball == null)
                {
                    MessageBox.Show("Need to create a ball first", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (chkMoveAnchor_InFrontVelocity.IsChecked.Value && chkMoveAnchor_BehindVelocity.IsChecked.Value)
                {
                    MessageBox.Show("Can't have both InFront/Behind constraints at the same time", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Vector3D arm;
                while (true)
                {
                    arm = Math3D.GetRandomVector_Spherical_Shell(_ball.RopeLength);

                    if (chkMoveAnchor_OnlyAbove.IsChecked.Value && arm.Z <= 0)
                        continue;

                    if (chkMoveAnchor_InFrontVelocity.IsChecked.Value && Vector3D.DotProduct(arm, _ball.Velocity_Ball) <= 0)
                        continue;

                    if (chkMoveAnchor_BehindVelocity.IsChecked.Value && Vector3D.DotProduct(arm, _ball.Velocity_Ball) >= 0)
                        continue;

                    break;
                }

                _ball.Position_Anchor = _ball.Position_Ball + arm;
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
                Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private void Clear()
        {
            if (_ball != null)
            {
                _viewport.Children.RemoveAll(_ball.Visuals);
                _ball = null;
            }
        }

        private void CreateBall()
        {
            //Point3D anchor_pos = Math3D.GetRandomVector(new Vector3D(-MAP_MAX_HORZ, -MAP_MAX_HORZ, -MAP_MAX_VERT), new Vector3D(MAP_MAX_HORZ, MAP_MAX_HORZ, MAP_MAX_VERT)).ToPoint();
            Point3D anchor_pos = new Point3D();     // no need to randomize the position when starting new
            Point3D ball_pos = GetAngledPosition(anchor_pos, trkRopeLength.Value, trkStartAngle.Value);

            var anchor = CreateBall_Anchor(anchor_pos);
            var ball = CreateBall_Ball(ball_pos);
            var rope = CreateBall_Rope(anchor_pos, ball_pos);

            _viewport.Children.Add(anchor.visual);
            _viewport.Children.Add(ball.visual);
            _viewport.Children.Add(rope.visual);

            _ball = new AnchoredBall()
            {
                RopeLength = trkRopeLength.Value,

                Position_Anchor = anchor_pos,
                Position_Ball = ball_pos,

                Rope = rope.line,

                Transform_Anchor = anchor.transform,
                Transform_Ball = ball.transform,

                Visuals = new[]
                {
                    anchor.visual,
                    ball.visual,
                    rope.visual,
                },

                Velocity_Ball = new Vector3D(),
            };
        }
        private static (Visual3D visual, TranslateTransform3D transform) CreateBall_Anchor(Point3D position)
        {
            Material material = Debug3DWindow.GetMaterial(true, UtilityWPF.ColorFromHex("838C80"));

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Material = material;
            geometry.BackMaterial = material;
            geometry.Geometry = UtilityWPF.GetSphere_Ico(0.25, 2, true);

            var transform = new TranslateTransform3D(position.ToVector());
            geometry.Transform = transform;

            return (new ModelVisual3D { Content = geometry }, transform);
        }
        private static (Visual3D visual, TranslateTransform3D transform) CreateBall_Ball(Point3D position)
        {
            Material material = Debug3DWindow.GetMaterial(true, UtilityWPF.ColorFromHex("A39B45"));

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Material = material;
            geometry.BackMaterial = material;
            geometry.Geometry = UtilityWPF.GetSphere_Ico(1, 1, false);

            var transform = new TranslateTransform3D(position.ToVector());
            geometry.Transform = transform;

            return (new ModelVisual3D { Content = geometry }, transform);
        }
        private static (Visual3D visual, BillboardLine3D line) CreateBall_Rope(Point3D from, Point3D to)
        {
            var line = new BillboardLine3D()
            {
                Color = UtilityWPF.ColorFromHex("805E6566"),
                IsReflectiveColor = false,
                Thickness = 0.05,
                FromPoint = from,
                ToPoint = to,
            };

            return (new ModelVisual3D() { Content = line.Model }, line);
        }

        private static Point3D GetAngledPosition(Point3D anchor, double ropeLength, double degrees)
        {
            Vector3D arm = new Vector3D(0, 0, -ropeLength);
            arm = arm.GetRotatedVector(new Vector3D(0, -1, 0), degrees);

            return anchor + arm;
        }

        private void UpdateBall(double deltaTime)
        {
            if (_ball == null)
                return;

            double accel_x = 0;
            double accel_y = 0;
            double accel_z = 0;

            RopeTension(ref accel_x, ref accel_y, ref accel_z, _ball.Position_Anchor, _ball.Position_Ball, _ball.Velocity_Ball, _ball.RopeLength, trkGravity.Value);

            accel_z -= trkGravity.Value;

            _ball.Velocity_Ball = new Vector3D(
                _ball.Velocity_Ball.X + (accel_x * deltaTime),
                _ball.Velocity_Ball.Y + (accel_y * deltaTime),
                _ball.Velocity_Ball.Z + (accel_z * deltaTime));

            _ball.Position_Ball += _ball.Velocity_Ball * deltaTime;

            _ball.Transform_Anchor.OffsetX = _ball.Position_Anchor.X;
            _ball.Transform_Anchor.OffsetY = _ball.Position_Anchor.Y;
            _ball.Transform_Anchor.OffsetZ = _ball.Position_Anchor.Z;

            _ball.Transform_Ball.OffsetX = _ball.Position_Ball.X;
            _ball.Transform_Ball.OffsetY = _ball.Position_Ball.Y;
            _ball.Transform_Ball.OffsetZ = _ball.Position_Ball.Z;

            _ball.Rope.FromPoint = _ball.Position_Anchor;
            _ball.Rope.ToPoint = _ball.Position_Ball;
        }

        private static void RopeTension(ref double accel_x, ref double accel_y, ref double accel_z, Point3D anchor, Point3D position, Vector3D velocity, double ropeLength, double gravity)
        {
            Vector3D direction = anchor - position;

            if (direction.LengthSquared <= ropeLength * ropeLength)
                return;

            direction = direction.ToUnit();

            accel_x += direction.X * gravity * 1.1;
            accel_y += direction.Y * gravity * 1.1;
            accel_z += direction.Z * gravity * 1.1;

            if (Vector3D.DotProduct(direction, velocity) >= 0)
                return;

            accel_x += direction.X * 72;
            accel_y += direction.Y * 72;
            accel_z += direction.Z * 72;
        }

        #endregion
    }
}
