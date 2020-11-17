using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using GameItems;
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
    /// <remarks>
    /// This was copied from asteroid miner's ChaseForcesWindow
    /// 
    /// It's to create a modified version of MapObject_ChaseOrientation_Torques
    /// 
    /// That class just has one controlling arm, but this will need an arbitrary number of them
    /// 
    /// Also, that was built as a subclass of Body.  This version should just have an inertial tensor and a quaternion
    /// </remarks>
    public partial class ChaseRotationWindow : Window
    {
        #region class: EndPoint

        private class EndPoint
        {
            public GrabbablePoint Grab { get; set; }
            public Visual3D Visual { get; set; }
            public TranslateTransform3D Transform { get; set; }

            public Point3D InitialPosition { get; set; }
        }

        #endregion

        #region Declaration Section

        private TrackBallRoam _trackball = null;

        private DispatcherTimer _timer = null;

        private List<Visual3D> _debugVisuals = new List<Visual3D>();
        private List<Visual3D> _debugLines = new List<Visual3D>();

        private QuaternionRotation3D _rotation = null;

        private EndPoint[] _endPoints = null;

        private DateTime _lastTick = DateTime.UtcNow;

        #endregion

        //private ChaseOrientation_Velocity _chaseOrientation = null;
        private ChaseOrientation_Torques _chaseOrientation = null;

        #region Constructor

        public ChaseRotationWindow()
        {
            InitializeComponent();

            _trackball = new TrackBallRoam(_camera);
            _trackball.KeyPanScale = 15d;
            _trackball.EventSource = grdViewPort;       //NOTE:  If this control doesn't have a background color set, the trackball won't see events (I think transparent is ok, just not null)
            _trackball.AllowZoomOnMouseWheel = true;
            _trackball.Mappings.AddRange(TrackBallMapping.GetPrebuilt(TrackBallMapping.PrebuiltMapping.MouseComplete_NoLeft));
            _trackball.Mappings.AddRange(TrackBallMapping.GetPrebuilt(TrackBallMapping.PrebuiltMapping.Keyboard_ASDW_In));
            _trackball.ShouldHitTestOnOrbit = false;
            //_trackball.UserMovedCamera += new EventHandler<UserMovedCameraArgs>(Trackball_UserMovedCamera);
            //_trackball.GetOrbitRadius += new EventHandler<GetOrbitRadiusArgs>(Trackball_GetOrbitRadius);

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(20);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        #endregion

        #region Event Listeners

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewport.Children.RemoveAll(_debugVisuals);
                _debugVisuals.Clear();
                _chaseOrientation = null;
                _rotation = null;

                // Create a visual
                // Create a rotation
                Visual3D visual;

                _rotation = new QuaternionRotation3D();

                visual = new ModelVisual3D()
                {
                    Content = GetModel(),
                    Transform = new RotateTransform3D(_rotation),
                };

                _debugVisuals.Add(visual);
                _viewport.Children.Add(visual);


                // Create N endpoint arms


                //Point3D pos = new Point3D(8, 8, 8);
                Point3D pos = new Point3D(8, 8, 0);

                EndPoint endpoint = new EndPoint()
                {
                    Visual = Debug3DWindow.GetDot(new Point3D(), 1, Colors.Yellow),     // it will be moved with the transform
                    Grab = new GrabbablePoint(_camera, _viewport, grdViewPort, pos, 2),
                    Transform = new TranslateTransform3D(),
                    InitialPosition = pos,
                };

                endpoint.Visual.Transform = endpoint.Transform;

                _debugVisuals.Add(endpoint.Visual);
                _viewport.Children.Add(endpoint.Visual);


                _endPoints = new[] { endpoint };



                // Chase Orientation
                _chaseOrientation = new ChaseOrientation_Torques(_rotation.Quaternion, 1, pos.ToVector())
                {
                    Torques = ChaseOrientation_Torques.GetStandard(),
                };

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ResetConstantVelocity_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewport.Children.RemoveAll(_debugVisuals);
                _debugVisuals.Clear();
                _chaseOrientation = null;
                _rotation = null;

                // Create a visual
                // Create a rotation
                Visual3D visual;

                _rotation = new QuaternionRotation3D();

                visual = new ModelVisual3D()
                {
                    Content = GetModel(),
                    Transform = new RotateTransform3D(_rotation),
                };

                _debugVisuals.Add(visual);
                _viewport.Children.Add(visual);




                _endPoints = new EndPoint[0];

                // Chase Orientation
                _chaseOrientation = new ChaseOrientation_Torques(_rotation.Quaternion, 1, new Vector3D(1, 0, 0))
                {
                    Torques = ChaseOrientation_Torques.GetStandard(),
                };




                _chaseOrientation.AngularVelocity = new Quaternion(new Vector3D(0, 0, 1), 48);


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UnitTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void TakeSnapshot_Click_TOLOCAL(object sender, RoutedEventArgs e)
        {
            try
            {
                var sizes = Debug3DWindow.GetDrawSizes(12);

                Debug3DWindow window = new Debug3DWindow();

                window.AddLine(new Point3D(), _chaseOrientation.Orientation.ToWorld(new Vector3D(4, 0, 0)).ToPoint(), sizes.line, Colors.Red);
                window.AddLine(new Point3D(), _chaseOrientation.Orientation.ToWorld(new Vector3D(0, 4, 0)).ToPoint(), sizes.line, Colors.Green);
                window.AddLine(new Point3D(), _chaseOrientation.Orientation.ToWorld(new Vector3D(0, 0, 4)).ToPoint(), sizes.line, Colors.Blue);


                window.AddLine(new Point3D(), _chaseOrientation._initialDirectionLocal.ToPoint(), sizes.line, Colors.Gray);
                window.AddLine(new Point3D(), _chaseOrientation.Orientation.FromWorld(_chaseOrientation._desiredOrientation.Value).ToPoint(), sizes.line, Colors.White);

                //window.AddLine()


                Quaternion rotation = Math3D.GetRotation(_chaseOrientation._initialDirectionLocal, _chaseOrientation.Orientation.FromWorld(_chaseOrientation._desiredOrientation.Value));
                if (!rotation.IsIdentity)
                {
                    var args = new ChaseOrientation_GetTorqueArgs(_chaseOrientation.MomentInertia, _chaseOrientation.AngularVelocity, rotation);

                    Vector3D? torque = null;

                    // Call each worker
                    foreach (var worker in _chaseOrientation.Torques)
                    {
                        Vector3D? localForce = worker.GetTorque(args);

                        if (localForce == null)
                            continue;


                        window.AddLine(new Point3D(), localForce.Value.ToPoint(), sizes.line, worker.IsDrag ? Colors.Pink : Colors.Chartreuse);


                        if (torque == null)
                        {
                            torque = localForce;
                        }
                        else
                        {
                            torque = torque.Value + localForce.Value;
                        }
                    }



                    if (torque != null)
                        window.AddLine(new Point3D(), torque.Value.ToPoint(), sizes.line, Colors.Magenta);





                }






                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void TakeSnapshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sizes = Debug3DWindow.GetDrawSizes(12);

                Debug3DWindow window = new Debug3DWindow();

                window.AddLine(new Point3D(), _chaseOrientation.Orientation.ToWorld(new Vector3D(4, 0, 0)).ToPoint(), sizes.line, Colors.Red);
                window.AddLine(new Point3D(), _chaseOrientation.Orientation.ToWorld(new Vector3D(0, 4, 0)).ToPoint(), sizes.line, Colors.Green);
                window.AddLine(new Point3D(), _chaseOrientation.Orientation.ToWorld(new Vector3D(0, 0, 4)).ToPoint(), sizes.line, Colors.Blue);


                window.AddLine(new Point3D(), _chaseOrientation._initialDirectionLocal.ToPoint(), sizes.line, Colors.Gray);
                window.AddLine(new Point3D(), _chaseOrientation.Orientation.FromWorld(_chaseOrientation._desiredOrientation.Value).ToPoint(), sizes.line, Colors.White);

                //window.AddLine()


                Quaternion rotation = Math3D.GetRotation(_chaseOrientation._initialDirectionLocal, _chaseOrientation.Orientation.FromWorld(_chaseOrientation._desiredOrientation.Value));
                if (!rotation.IsIdentity)
                {
                    var args = new ChaseOrientation_GetTorqueArgs(_chaseOrientation.MomentInertia, _chaseOrientation.AngularVelocity, rotation);

                    Vector3D? torque = null;

                    // Call each worker
                    foreach (var worker in _chaseOrientation.Torques)
                    {
                        Vector3D? localForce = worker.GetTorque(args);

                        if (localForce == null)
                            continue;


                        window.AddLine(new Point3D(), localForce.Value.ToPoint(), sizes.line, worker.IsDrag ? Colors.Pink : Colors.Chartreuse);


                        if (torque == null)
                        {
                            torque = localForce;
                        }
                        else
                        {
                            torque = torque.Value + localForce.Value;
                        }
                    }

                    if (torque != null)
                        window.AddLine(new Point3D(), torque.Value.ToPoint(), sizes.line, Colors.Magenta);
                }

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                DateTime tick = DateTime.UtcNow;
                double seconds = (tick - _lastTick).TotalSeconds;
                _lastTick = tick;


                if (_endPoints == null)
                    return;

                foreach (EndPoint endPoint in _endPoints)
                {
                    endPoint.Transform.OffsetX = endPoint.Grab.Position.X;
                    endPoint.Transform.OffsetY = endPoint.Grab.Position.Y;
                    endPoint.Transform.OffsetZ = endPoint.Grab.Position.Z;
                }

                if (_chaseOrientation != null && _endPoints.Length == 1)
                {
                    _chaseOrientation.SetOrientation(_endPoints[0].Grab.Position.ToVector());
                }

                _chaseOrientation.Tick(seconds);

                _rotation.Quaternion = _chaseOrientation.Orientation;

                RefreshDebugLines();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        //TODO: The problem is possibly with how the angular velocity is transformed
        //draw the vectors that are calculated in the constructor of ChaseOrientation_GetTorqueArgs

        private void RefreshDebugLines()
        {
            _viewport.Children.RemoveAll(_debugLines);
            _debugLines.Clear();

            if (_chaseOrientation == null)
                return;

            var toWorld = new Func<Vector3D, Vector3D>(v => _chaseOrientation.Orientation.ToWorld(v));

            var sizes = Debug3DWindow.GetDrawSizes(12);

            Visual3D visual;

            // Control Arms
            visual = Debug3DWindow.GetLine(new Point3D(), toWorld(_chaseOrientation._initialDirectionLocal).ToPoint(), sizes.line, Colors.Gray);
            _viewport.Children.Add(visual);
            _debugLines.Add(visual);

            Quaternion rotation = Quaternion.Identity;
            if (_chaseOrientation._desiredOrientation != null)
            {
                visual = Debug3DWindow.GetLine(new Point3D(), toWorld(_chaseOrientation.Orientation.FromWorld(_chaseOrientation._desiredOrientation.Value)).ToPoint(), sizes.line, Colors.White);
                _viewport.Children.Add(visual);
                _debugLines.Add(visual);

                //Quaternion rotation = Math3D.GetRotation(_chaseOrientation._initialDirectionLocal, _chaseOrientation.Orientation.FromWorld(_chaseOrientation._desiredOrientation.Value));
                rotation = Math3D.GetRotation(_chaseOrientation.Orientation.ToWorld(_chaseOrientation._initialDirectionLocal), _chaseOrientation._desiredOrientation.Value);

                #region show torques

                if (!rotation.IsIdentity)
                {
                    var args = new ChaseOrientation_GetTorqueArgs(_chaseOrientation.MomentInertia, _chaseOrientation.AngularVelocity, rotation);

                    Vector3D? torque = null;

                    // Call each worker
                    foreach (var worker in _chaseOrientation.Torques)
                    {
                        Vector3D? localForce = worker.GetTorque(args);

                        if (localForce == null)
                            continue;


                        visual = Debug3DWindow.GetLine(new Point3D(), toWorld(localForce.Value).ToPoint(), sizes.line, worker.IsDrag ? Colors.Pink : Colors.Chartreuse);
                        _viewport.Children.Add(visual);
                        _debugLines.Add(visual);


                        if (torque == null)
                        {
                            torque = localForce;
                        }
                        else
                        {
                            torque = torque.Value + localForce.Value;
                        }
                    }

                    if (torque != null)
                    {
                        visual = Debug3DWindow.GetLine(new Point3D(), toWorld(torque.Value).ToPoint(), sizes.line, Colors.Magenta);
                        _viewport.Children.Add(visual);
                        _debugLines.Add(visual);
                    }
                }

                #endregion
            }


            // The error seems to be with using the quaternion's axis as the angular velocity vector
            // Or the angular velocity quaternion is calculated wrong
            #region show velocites

            var targs = new ChaseOrientation_GetTorqueArgs(_chaseOrientation.MomentInertia, _chaseOrientation.AngularVelocity, rotation);

            visual = Debug3DWindow.GetLine(new Point3D(), (targs.AngVelocityUnit * targs.AngVelocityLength).ToPoint(), sizes.line, UtilityWPF.ColorFromHex("FFA229"));
            _viewport.Children.Add(visual);
            _debugLines.Add(visual);

            visual = Debug3DWindow.GetLine(new Point3D(), (targs.AngVelocityAlongUnit * targs.AngVelocityAlongLength).ToPoint(), sizes.line, UtilityWPF.ColorFromHex("D9734E"));
            _viewport.Children.Add(visual);
            _debugLines.Add(visual);

            visual = Debug3DWindow.GetLine(new Point3D(), (targs.AngVelocityOrthUnit * targs.AngVelocityOrthLength).ToPoint(), sizes.line, UtilityWPF.ColorFromHex("4FBDB2"));
            _viewport.Children.Add(visual);
            _debugLines.Add(visual);

            #endregion
        }

        #region Private Methods

        private static Model3D GetModel()
        {
            const double SIZE = 4;

            const string COLOR_X = "A33C39";
            const string COLOR_Y = "919426";
            const string COLOR_Z = "386E99";

            Model3DGroup retVal = new Model3DGroup();
            GeometryModel3D geometry;
            MaterialGroup material;

            var rhomb = Polytopes.GetRhombicuboctahedron(SIZE, SIZE, SIZE);
            TriangleIndexed_wpf[] triangles;

            #region X,Y,Z spikes

            double thickness = .2;
            double length = SIZE * 1.5;

            retVal.Children.Add(new BillboardLine3D()
            {
                Color = UtilityWPF.ColorFromHex("80" + COLOR_X),
                IsReflectiveColor = false,
                Thickness = thickness,
                FromPoint = new Point3D(0, 0, 0),
                ToPoint = new Point3D(length, 0, 0)
            }.Model);

            retVal.Children.Add(new BillboardLine3D()
            {
                Color = UtilityWPF.ColorFromHex("80" + COLOR_Y),
                IsReflectiveColor = false,
                Thickness = thickness,
                FromPoint = new Point3D(0, 0, 0),
                ToPoint = new Point3D(0, length, 0)
            }.Model);

            retVal.Children.Add(new BillboardLine3D()
            {
                Color = UtilityWPF.ColorFromHex("80" + COLOR_Z),
                IsReflectiveColor = false,
                Thickness = thickness,
                FromPoint = new Point3D(0, 0, 0),
                ToPoint = new Point3D(0, 0, length)
            }.Model);

            #endregion

            #region X plates

            geometry = new GeometryModel3D();

            material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(UtilityWPF.ColorFromHex("80" + COLOR_X))));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(UtilityWPF.ColorFromHex("70DCE01D")), 5));

            geometry.Material = material;
            geometry.BackMaterial = material;

            triangles = rhomb.Squares_Orth.
                SelectMany(o => o).
                Where(o => o.IndexArray.All(p => Math1D.IsNearValue(Math.Abs(o.AllPoints[p].X * 2), SIZE))).
                ToArray();

            geometry.Geometry = UtilityWPF.GetMeshFromTriangles_IndependentFaces(triangles);

            retVal.Children.Add(geometry);

            #endregion
            #region Y plates

            geometry = new GeometryModel3D();

            material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(UtilityWPF.ColorFromHex("80" + COLOR_Y))));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(UtilityWPF.ColorFromHex("702892BF")), 3));

            geometry.Material = material;
            geometry.BackMaterial = material;

            triangles = rhomb.Squares_Orth.
                SelectMany(o => o).
                Where(o => o.IndexArray.All(p => Math1D.IsNearValue(Math.Abs(o.AllPoints[p].Y * 2), SIZE))).
                ToArray();

            geometry.Geometry = UtilityWPF.GetMeshFromTriangles_IndependentFaces(triangles);

            retVal.Children.Add(geometry);

            #endregion
            #region Z plates

            geometry = new GeometryModel3D();

            material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(UtilityWPF.ColorFromHex("80" + COLOR_Z))));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(UtilityWPF.ColorFromHex("B0CF1919")), 17));

            geometry.Material = material;
            geometry.BackMaterial = material;

            triangles = rhomb.Squares_Orth.
                SelectMany(o => o).
                Where(o => o.IndexArray.All(p => Math1D.IsNearValue(Math.Abs(o.AllPoints[p].Z * 2), SIZE))).
                ToArray();

            geometry.Geometry = UtilityWPF.GetMeshFromTriangles_IndependentFaces(triangles);

            retVal.Children.Add(geometry);

            #endregion
            #region Base

            geometry = new GeometryModel3D();

            material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(UtilityWPF.ColorFromHex("305B687A"))));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(UtilityWPF.ColorFromHex("507E827A")), 12));

            geometry.Material = material;
            geometry.BackMaterial = material;

            triangles = UtilityCore.Iterate(
                rhomb.Squares_Diag.SelectMany(o => o),
                rhomb.Triangles
                ).ToArray();

            geometry.Geometry = UtilityWPF.GetMeshFromTriangles_IndependentFaces(triangles);

            retVal.Children.Add(geometry);

            #endregion

            return retVal;
        }

        #endregion
    }
}
