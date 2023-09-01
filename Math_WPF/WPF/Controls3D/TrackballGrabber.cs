using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.WPF.Controls3D
{

    // v1 was physically changing the camera's position.  Then setting lights in reverse.  That is the wrong way to work
    // with 3D, and fails when trying to use custom default directions

    // This should leave the camera and lights alone, and instead just apply a rotate transform to the 3d models (arrows)

    public class TrackballGrabber
    {
        #region Events

        public event EventHandler RotationChanged = null;

        // These are exposed so that other controls under the eventSource passed in to the constructor can have their Focasable and IsHitTestable set to false (otherwise they
        // will eat the mouse up) - but it's easier to just put this control in its own container that has no other controls in it
        public event EventHandler CapturingMouse = null;
        public event EventHandler ReleasedMouse = null;

        #endregion

        #region Declaration Section

        private const double GUIDELINE_THICKNESS = 0.02;
        private const double GUIDELINE_LENGTH = 0.75;

        private readonly FrameworkElement _eventSource;
        private readonly Viewport3D _viewport;
        private readonly PerspectiveCamera _camera;

        /// <summary>
        /// This is used to rotate mouse drags into the sphere's coords
        /// </summary>
        private readonly RotateTransform3D _project2Dto3DRotation;

        private readonly Visual3D[] _permanentVisuals;
        private readonly Visual3D[] _hoverVisuals;

        private readonly DoubleVector_wpf _default_direction;

        private readonly RotateTransform3D _transform;
        private readonly QuaternionRotation3D _transform_quat;

        private readonly ModelVisual3D _sphereModel = null;
        private readonly MaterialGroup _sphereMaterials = null;
        private readonly Material _sphereMaterialHover = null;
        private readonly ModelVisual3D _hoverLight = null;

        private bool _isMouseDown = false;

        private Point _previousPosition2D;
        private Vector3D _previousPosition3D = new Vector3D(0, 0, 1);

        #endregion

        #region Constructor

        /// <param name="eventSource">The parent of the Viewport3D.  Be sure that its background is non null, transparent is ok</param>
        /// <param name="viewport">The viewport that will be drawn on</param>
        /// <param name="permanentVisuals">These always show.  NOTE: This class sets the transform property with a rotate.  So make sure the visual will rotate about the origin</param>
        /// <param name="hoverVisuals">These are visuals that are only visible when the mouse is over the control.  Their transform is also set by this contructor</param>
        /// <param name="hoverLightColor">An extra light that shows during mouse over</param>
        /// <param name="default_direction">The direction that it will initially be pointing</param>
        public TrackballGrabber(FrameworkElement eventSource, Viewport3D viewport, Visual3D[] permanentVisuals, Visual3D[] hoverVisuals, double sphereRadius, Color hoverLightColor, DoubleVector_wpf default_direction)
        {
            if (viewport.Camera == null || !(viewport.Camera is PerspectiveCamera))
                throw new ArgumentException("This class requires a perspective camera to be tied to the viewport");

            _transform_quat = new QuaternionRotation3D(Quaternion.Identity);
            _transform = new RotateTransform3D(_transform_quat);

            _eventSource = eventSource;
            _viewport = viewport;
            _camera = (PerspectiveCamera)viewport.Camera;

            _permanentVisuals = permanentVisuals ?? new Visual3D[0];
            foreach (Visual3D visual in _permanentVisuals)
            {
                visual.Transform = _transform;
                _viewport.Children.Add(visual);
            }

            _hoverVisuals = hoverVisuals ?? new Visual3D[0];
            foreach (Visual3D visual in _hoverVisuals)
            {
                visual.Transform = _transform;
            }

            _default_direction = default_direction;


            //This is nuts.  Time to draw absolutely everything with Debug3DWindow
            //_project2Dto3DRotation = new RotateTransform3D(new QuaternionRotation3D(Math3D.GetRotation(new DoubleVector_wpf(new Vector3D(0, 0, -1), new Vector3D(0, 1, 0)), default_direction)));

            //_project2Dto3DRotation = new RotateTransform3D(new QuaternionRotation3D(Math3D.GetRotation(new DoubleVector_wpf(_camera.LookDirection, _camera.UpDirection), new DoubleVector_wpf(new Vector3D(0, 0, -1), new Vector3D(0, 1, 0)))));
            //_project2Dto3DRotation = new RotateTransform3D(new QuaternionRotation3D(Math3D.GetRotation(new DoubleVector_wpf(_camera.LookDirection, _camera.UpDirection), new DoubleVector_wpf(new Vector3D(0, 0, 1), new Vector3D(0, 1, 0)))));


            // This works for a 90 degree flip, maybe not all variants.  I think 180s are getting miscalculated (might
            // even be a problem in Math3D.GetRotation).  It works for the case I need it for, so I'm stopping here,
            // letting my future self to try to make it work universally
            _project2Dto3DRotation = new RotateTransform3D(new QuaternionRotation3D(Math3D.GetRotation(new DoubleVector_wpf(new Vector3D(0, 0, -1), new Vector3D(0, 1, 0)), new DoubleVector_wpf(_camera.LookDirection, _camera.UpDirection))));


            _eventSource.MouseEnter += new MouseEventHandler(EventSource_MouseEnter);
            _eventSource.MouseLeave += new MouseEventHandler(EventSource_MouseLeave);
            _eventSource.MouseDown += new MouseButtonEventHandler(EventSource_MouseDown);
            _eventSource.MouseUp += new MouseButtonEventHandler(EventSource_MouseUp);
            _eventSource.MouseMove += new MouseEventHandler(EventSource_MouseMove);

            #region sphere

            // Material
            _sphereMaterials = new MaterialGroup();
            _sphereMaterials.Children.Add(new DiffuseMaterial(UtilityWPF.BrushFromHex("18FFFFFF")));

            // This gets added/removed on mouse enter/leave
            _sphereMaterialHover = new SpecularMaterial(UtilityWPF.BrushFromHex("40808080"), 33d);

            // Geometry Model
            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Material = _sphereMaterials;
            geometry.BackMaterial = _sphereMaterials;
            geometry.Geometry = UtilityWPF.GetSphere_LatLon(20, sphereRadius);

            // Model Visual
            _sphereModel = new ModelVisual3D();
            _sphereModel.Content = geometry;

            // Add it
            _viewport.Children.Add(_sphereModel);

            #endregion
            #region hover light

            PointLight hoverLight = new PointLight();
            hoverLight.Color = hoverLightColor;
            hoverLight.Range = sphereRadius * 10;

            _hoverLight = new ModelVisual3D();
            _hoverLight.Content = hoverLight;

            #endregion
        }

        #endregion

        #region Public Properties

        public DoubleVector_wpf Direction
        {
            get => new DoubleVector_wpf(_transform.Transform(_default_direction.Standard), _transform.Transform(_default_direction.Orth));
            //set => _transform_quat.Quaternion = Math3D.GetRotation(_default_direction, value);        // this isn't working correctly.  It causes mouse drags to be wrong after this
        }

        #endregion

        #region Public Methods

        public void ResetToDefault()
        {
            _transform_quat.Quaternion = Quaternion.Identity;
        }

        public static Model3D GetMajorArrow(Axis axis, bool positiveDirection, DiffuseMaterial diffuse, SpecularMaterial specular)
        {
            return GetArrow(axis, positiveDirection, diffuse, specular, .075d, 1d, .15d, .3d, 1d + .1d);
        }
        public static Model3D GetMajorDoubleArrow(Axis axis, DiffuseMaterial diffuse, SpecularMaterial specular)
        {
            return GetDoubleArrow(axis, diffuse, specular, .075d, 2d, .15d, .3d, 1d + .1d);
        }

        public static Model3D GetMinorArrow(Axis axis, bool positiveDirection, DiffuseMaterial diffuse, SpecularMaterial specular)
        {
            return GetArrow(axis, positiveDirection, diffuse, specular, .05d, .5d, .075d, .2d, .5d + .1d);
        }
        public static Model3D GetMinorDoubleArrow(Axis axis, DiffuseMaterial diffuse, SpecularMaterial specular)
        {
            return GetDoubleArrow(axis, diffuse, specular, .05d, 1d, .075d, .2d, .5d + .1d);
        }

        public static Visual3D GetGuideLine(Axis axis, bool positiveDirection, Color color)
        {
            Transform3D transform = GetTransform(axis, positiveDirection);

            var retVal = new BillboardLine3DSet(false);
            retVal.Color = color;
            retVal.BeginAddingLines();
            retVal.AddLine(new Point3D(0, 0, 0), transform.Transform(new Point3D(GUIDELINE_LENGTH, 0, 0)), GUIDELINE_THICKNESS);
            retVal.EndAddingLines();

            return retVal;
        }
        public static Visual3D GetGuideLineDouble(Axis axis, Color color)
        {
            Transform3D transform = GetTransform(axis, true);

            var retVal = new BillboardLine3DSet(false);
            retVal.Color = color;
            retVal.BeginAddingLines();
            retVal.AddLine(transform.Transform(new Point3D(-GUIDELINE_LENGTH, 0, 0)), transform.Transform(new Point3D(GUIDELINE_LENGTH, 0, 0)), GUIDELINE_THICKNESS);
            retVal.EndAddingLines();

            return retVal;
        }

        #endregion

        #region Event Listeners

        private void EventSource_MouseEnter(object sender, MouseEventArgs e)
        {
            _sphereMaterials.Children.Add(_sphereMaterialHover);
            _viewport.Children.Add(_hoverLight);

            _viewport.Children.Remove(_sphereModel);

            foreach (Visual3D visual in _hoverVisuals)
            {
                _viewport.Children.Add(visual);
            }

            _viewport.Children.Add(_sphereModel);       // this must always be added last, because it's semitransparent
        }
        private void EventSource_MouseLeave(object sender, MouseEventArgs e)
        {
            _sphereMaterials.Children.Remove(_sphereMaterialHover);
            _viewport.Children.Remove(_hoverLight);

            foreach (Visual3D visual in _hoverVisuals)
            {
                if (_viewport.Children.Contains(visual))
                    _viewport.Children.Remove(visual);
            }
        }

        private void EventSource_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = true;

            _viewport.Children.Remove(_hoverLight);

            CapturingMouse?.Invoke(this, new EventArgs());      // Give the listener a chance to make other controls under _eventSource non hit testable

            // By capturing the mouse, mouse events will still come in even when they are moving the mouse
            // outside the element/form
            Mouse.Capture(_eventSource, CaptureMode.SubTree);		// there was a case where grid was the event source.  If they clicked one of the 3D objects, the scene would jerk.  But by saying subtree, the event still fires

            _previousPosition2D = e.GetPosition(_eventSource);
            _previousPosition3D = ProjectToTrackball(_eventSource.ActualWidth, _eventSource.ActualHeight, _previousPosition2D);
            _previousPosition3D = _project2Dto3DRotation.Transform(_previousPosition3D);
        }
        private void EventSource_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = false;

            Mouse.Capture(_eventSource, CaptureMode.None);

            ReleasedMouse?.Invoke(this, new EventArgs());       // Let the listener make children of _eventSource hit testable again

            if (_eventSource.IsMouseOver && !_viewport.Children.Contains(_hoverLight))		// ran into a case where they click down outside the viewport, then released over (the light was already on from the mouse enter)
                _viewport.Children.Add(_hoverLight);
        }

        private void EventSource_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown)
                return;

            Point currentPosition = e.GetPosition(_eventSource);

            // Avoid any zero axis conditions
            if (currentPosition == _previousPosition2D)
                return;

            // Project the 2D position onto a sphere
            Vector3D currentPosition3D = ProjectToTrackball(_eventSource.ActualWidth, _eventSource.ActualHeight, currentPosition);
            currentPosition3D = _project2Dto3DRotation.Transform(currentPosition3D);

            OrbitCamera(currentPosition3D);

            _previousPosition2D = currentPosition;
            _previousPosition3D = currentPosition3D;
        }

        #endregion

        #region Private Methods

        private void OrbitCamera(Vector3D currentPosition3D)
        {
            Quaternion delta = Math3D.GetRotation(_previousPosition3D, currentPosition3D);
            if (delta.IsIdentity)
                return;

            // The adjustment at this point is too late.  The correct place is when projecting 2D mouse point to 3D
            //delta = AdjustDelta3(delta, _default_direction);

            //_transform_quat.Quaternion = (_transform_quat.Quaternion * delta).ToUnit();     // tounit is probably unnecessary, it just feels safer
            _transform_quat.Quaternion = (delta * _transform_quat.Quaternion).ToUnit();

            RotationChanged?.Invoke(this, new EventArgs());
        }

        private static Quaternion AdjustDelta1(Quaternion delta, RotateTransform3D transform)
        {
            // Now need to rotate the axis into the camera's coords
            Matrix3D viewMatrix = transform.Value;
            viewMatrix.Invert();

            // Transform the trackball rotation axis relative to the camera orientation
            Vector3D axis = viewMatrix.Transform(delta.Axis);

            return new Quaternion(axis, delta.Angle);
        }
        private static Quaternion AdjustDelta2(Quaternion delta, DoubleVector_wpf default_direction)
        {
            //NOPE

            var local_direction = new DoubleVector_wpf(new Vector3D(0, 0, 1), new Vector3D(0, 1, 0));

            Quaternion to_local = Math3D.GetRotation(default_direction, local_direction);

            //return delta.RotateBy(to_local);
            return to_local.RotateBy(delta);
        }

        private static Vector3D ProjectToTrackball(double width, double height, Point point)
        {
            bool shouldInvertZ = false;

            // Scale the inputs so -1 to 1 is the edge of the screen
            double x = point.X / (width / 2d);    // Scale so bounds map to [0,0] - [2,2]
            double y = point.Y / (height / 2d);

            x = x - 1d;                           // Translate 0,0 to the center
            y = 1d - y;                           // Flip so +Y is up instead of down

            // Wrap (otherwise, everything greater than 1 will map to the permiter of the sphere where z = 0)
            bool localInvert;
            x = ProjectToTrackball_Wrap(out localInvert, x);
            shouldInvertZ |= localInvert;

            y = ProjectToTrackball_Wrap(out localInvert, y);
            shouldInvertZ |= localInvert;

            // Project onto a sphere
            double z2 = 1d - (x * x) - (y * y);       // z^2 = 1 - x^2 - y^2
            double z = 0d;
            if (z2 > 0d)
            {
                z = Math.Sqrt(z2);
            }
            else
            {
                // NOTE: The wrap logic above should make it so this never happens
                z = 0d;
            }

            if (shouldInvertZ)
                z *= -1d;

            return new Vector3D(x, y, z);
        }
        /// <summary>
        /// This wraps the value so it stays between -1 and 1
        /// </summary>
        /// <remarks>
        /// This function is only needed when they drag beyond the ball's bounds.  For example, they start dragging and keep
        /// dragging to the right.  Since the mouse is captured, mouse events keep firing even though the mouse is off the control.
        /// As they keep dragging, the value needs to wrap by multiples of the control's radius (value was normalized to between
        /// -1 and 1, so this can hardcode to 4)
        /// </remarks>
        private static double ProjectToTrackball_Wrap(out bool shouldInvertZ, double value)
        {
            // Everything starts over at 4 (4 becomes zero)
            double retVal = value % 4d;

            //Console.WriteLine($"value: {value} | mod4: {retVal}");

            double abs = Math.Abs(retVal);
            bool isNeg = retVal < 0d;

            shouldInvertZ = false;

            if (abs >= 3d)
            {
                // Anything from 3 to 4 needs to be -1 to 0
                // Anything from -4 to -3 needs to be 0 to 1
                retVal = 4d - abs;

                if (!isNeg)
                    retVal *= -1d;
            }
            else if (abs > 1d)
            {
                // This is the back side of the sphere
                // Anything from 1 to 3 needs to be flipped (1 stays 1, 2 becomes 0, 3 becomes -1)
                // -1 stays -1, -2 becomes 0, -3 becomes 1
                retVal = 2d - abs;

                if (isNeg)
                    retVal *= -1d;

                shouldInvertZ = true;
            }

            return retVal;
        }

        private static Model3D GetArrow(Axis axis, bool positiveDirection, DiffuseMaterial diffuse, SpecularMaterial specular, double cylinderRadius, double cylinderHeight, double coneRadius, double coneHeight, double coneOffset)
        {
            MaterialGroup materials = new MaterialGroup();
            materials.Children.Add(diffuse);
            materials.Children.Add(specular);

            Model3DGroup retVal = new Model3DGroup();

            Transform3D final_rotate = GetTransform(axis, positiveDirection);

            #region cylinder

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Material = materials;
            geometry.BackMaterial = materials;
            geometry.Geometry = UtilityWPF.GetCylinder_AlongZ(20, cylinderRadius, cylinderHeight);

            var cylinder_transform = new Transform3DGroup();
            cylinder_transform.Children.Add(new TranslateTransform3D(new Vector3D(cylinderHeight / 2d, 0, 0)));
            cylinder_transform.Children.Add(final_rotate);

            geometry.Transform = cylinder_transform;

            retVal.Children.Add(geometry);

            #endregion
            #region cone

            geometry = new GeometryModel3D();
            geometry.Material = materials;
            geometry.BackMaterial = materials;
            geometry.Geometry = UtilityWPF.GetCone_AlongX(10, coneRadius, coneHeight);

            var cone_transform = new Transform3DGroup();
            cone_transform.Children.Add(new TranslateTransform3D(new Vector3D(coneOffset, 0, 0)));
            cone_transform.Children.Add(final_rotate);

            geometry.Transform = cone_transform;

            retVal.Children.Add(geometry);

            #endregion
            #region cap

            geometry = new GeometryModel3D();
            geometry.Material = materials;
            geometry.BackMaterial = materials;
            geometry.Geometry = UtilityWPF.GetSphere_LatLon(20, cylinderRadius);

            geometry.Transform = final_rotate;

            retVal.Children.Add(geometry);

            #endregion

            //retVal.Transform = GetTransform(axis, positiveDirection);

            return retVal;
        }
        private static Model3D GetDoubleArrow(Axis axis, DiffuseMaterial diffuse, SpecularMaterial specular, double cylinderRadius, double cylinderHeight, double coneRadius, double coneHeight, double coneOffset)
        {
            MaterialGroup materials = new MaterialGroup();
            materials.Children.Add(diffuse);
            materials.Children.Add(specular);

            Model3DGroup retVal = new Model3DGroup();

            #region cylinder

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Material = materials;
            geometry.BackMaterial = materials;
            geometry.Geometry = UtilityWPF.GetCylinder_AlongZ(20, cylinderRadius, cylinderHeight);

            retVal.Children.Add(geometry);

            #endregion
            #region cone +

            geometry = new GeometryModel3D();
            geometry.Material = materials;
            geometry.BackMaterial = materials;
            geometry.Geometry = UtilityWPF.GetCone_AlongX(10, coneRadius, coneHeight);

            Transform3DGroup transform = new Transform3DGroup();		// rotate needs to be added before translate
            //transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(Math3D.GetRandomVectorSpherical(_rand, 10), Math3D.GetNearZeroValue(_rand, 360d))));
            transform.Children.Add(new TranslateTransform3D(new Vector3D(coneOffset, 0, 0)));

            geometry.Transform = transform;

            retVal.Children.Add(geometry);

            #endregion
            #region cone -

            geometry = new GeometryModel3D();
            geometry.Material = materials;
            geometry.BackMaterial = materials;
            geometry.Geometry = UtilityWPF.GetCone_AlongX(10, coneRadius, coneHeight);

            transform = new Transform3DGroup();		// rotate needs to be added before translate
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), 180)));
            transform.Children.Add(new TranslateTransform3D(new Vector3D(-coneOffset, 0, 0)));

            geometry.Transform = transform;

            retVal.Children.Add(geometry);

            #endregion

            retVal.Transform = GetTransform(axis, true);

            return retVal;
        }

        /// <summary>
        /// This tells how to rotate from 1,0,0 to the direction passed in
        /// </summary>
        private static Transform3D GetTransform(Axis axis, bool positiveDirection)
        {
            Vector3D desiredVector;

            switch (axis)
            {
                case Axis.X:
                    if (positiveDirection)
                        return Transform3D.Identity;        // It's already built along positive X
                    else
                        desiredVector = new Vector3D(-1, 0, 0);
                    break;

                case Axis.Y:
                    desiredVector = new Vector3D(0, positiveDirection ? 1 : -1, 0);
                    break;

                case Axis.Z:
                    desiredVector = new Vector3D(0, 0, positiveDirection ? 1 : -1);
                    break;

                default:
                    throw new ApplicationException($"Unknown Axis: {axis}");
            }

            return new RotateTransform3D(new QuaternionRotation3D(Math3D.GetRotation(new Vector3D(1, 0, 0), desiredVector)));
        }

        #endregion
    }
}
