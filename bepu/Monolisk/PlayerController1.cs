using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace Game.Bepu.Monolisk
{
    /// <summary>
    /// This takes user inputs, moves the player, updates the camera
    /// </summary>
    /// <remarks>
    /// Also convert inputs into action events that something else will listen to
    /// 
    /// A lot of this was copied from TrackBallRoam.  TrackBallRoam is a generic snap in input to
    /// camera class.  But this player controller only needs a subset of that functionality and also is
    /// specialized to controlling a player character
    /// </remarks>
    public class PlayerController1
    {
        #region enum: KeyboardActions

        private enum KeyboardAction
        {
            Forward,
            Backward,
            Left,
            Right,
            Jump,
        }

        #endregion

        #region Declaration Section

        private readonly PerspectiveCamera _camera = null;
        private readonly FrameworkElement _eventSource = null;

        private Point _previousPosition2D;
        private Vector3D _previousPosition3D = new Vector3D(0, 0, 1);

        private List<KeyboardAction> _currentKeyboardActions = new List<KeyboardAction>();
        private DispatcherTimer _timerKeyboard = null;
        /// <summary>
        /// The time of the last occurance of KeyboardTimer_Tick.  This is used to know how much actual time has elapsed between
        /// ticks, which is used to keep the scroll distance output normalized to time instead of ticks (in cases with low FPS)
        /// </summary>
        private DateTime _lastKeyboardTick = DateTime.UtcNow;

        #endregion

        #region Constructor

        public PlayerController1(PerspectiveCamera camera, FrameworkElement eventSource)
        {
            _camera = camera;
            _eventSource = eventSource;

            _eventSource.MouseDown += EventSource_MouseDown;
            _eventSource.MouseUp += EventSource_MouseUp;
            _eventSource.MouseMove += EventSource_MouseMove;

            _eventSource.Focusable = true;      // if these two aren't set, the control won't get keyboard events
            _eventSource.Focus();
            _eventSource.PreviewKeyDown += EventSource_PreviewKeyDown;
            _eventSource.PreviewKeyUp += EventSource_PreviewKeyUp;
            _eventSource.LostKeyboardFocus += EventSource_LostKeyboardFocus;

            _timerKeyboard = new DispatcherTimer();     // this is only running when a movement key is held in
            _timerKeyboard.Interval = TimeSpan.FromMilliseconds(25);
            _timerKeyboard.Tick += new EventHandler(KeyboardTimer_Tick);
        }

        #endregion

        #region Public Properties

        private bool _isActive = false;
        public bool IsActive
        {
            get
            {
                return _isActive;
            }
            set
            {
                if (value == _isActive)
                {
                    return;
                }

                _isActive = value;

                if (_isActive)
                {
                    _timerKeyboard.Start();
                }
                else
                {
                    _timerKeyboard.Stop();
                    _currentKeyboardActions.Clear();
                }
            }
        }

        private double _rotateScale = .1;
        /// <summary>
        /// Default is .1
        /// </summary>
        public double RotateScale
        {
            get
            {
                return _rotateScale;
            }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException("RotateScale must be greater than zero: " + value.ToString());
                }

                _rotateScale = value;
            }
        }

        private double _panSpeed = 1;
        public double PanSpeed
        {
            get
            {
                return _panSpeed;
            }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException("PanScale must be greater than zero: " + value.ToString());
                }

                _panSpeed = value;
            }
        }

        #endregion

        #region Event Listeners

        private void EventSource_MouseDown(object sender, MouseEventArgs e)
        {
            if (!IsActive)
            {
                return;
            }

            if (e.RightButton == MouseButtonState.Pressed)
            {
                // By capturing the mouse, mouse events will still come in even when they are moving the mouse
                // outside the element/form
                Mouse.Capture(_eventSource, CaptureMode.SubTree);       // I had a case where I used the grid as the event source.  If they clicked one of the 3D objects, the scene would jerk.  But by saying subtree, I still get the event

                _previousPosition2D = e.GetPosition(_eventSource);
                _previousPosition3D = ProjectToTrackball(_eventSource.ActualWidth, _eventSource.ActualHeight, _previousPosition2D);
            }
        }
        private void EventSource_MouseUp(object sender, MouseEventArgs e)
        {
            if (!IsActive)
            {
                return;
            }

            if (e.RightButton == MouseButtonState.Released)
            {
                Mouse.Capture(_eventSource, CaptureMode.None);
            }
        }
        private void EventSource_MouseMove(object sender, MouseEventArgs e)
        {
            if (!IsActive || e.RightButton == MouseButtonState.Released)
            {
                return;
            }

            Point currentPosition = e.GetPosition(_eventSource);

            // Avoid any zero axis conditions
            if (currentPosition == _previousPosition2D)
            {
                return;
            }

            // Project the 2D position onto a sphere
            Vector3D currentPosition3D = ProjectToTrackball(_eventSource.ActualWidth, _eventSource.ActualHeight, currentPosition);

            RotateCamera(currentPosition);

            _previousPosition2D = currentPosition;
            _previousPosition3D = currentPosition3D;
        }

        private void EventSource_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsActive || e.IsRepeat)
            {
                // If they hold in the key, this event keeps firing
                return;
            }

            switch (e.Key)
            {
                case Key.LeftAlt:
                case Key.LeftCtrl:
                case Key.LeftShift:
                case Key.RightAlt:
                case Key.RightCtrl:
                case Key.RightShift:
                    // Ignore this (this is a modification key, not a primary driver for camera movement)
                    return;
            }

            // See if an action occured
            KeyboardAction? action = GetAction(e);
            if (action == null)
            {
                return;
            }

            // Add this to the list of active actions
            if (!_currentKeyboardActions.Contains(action.Value))
            {
                _currentKeyboardActions.Add(action.Value);
            }

            // Make sure the timer is running
            if (!_timerKeyboard.IsEnabled)
            {
                _lastKeyboardTick = DateTime.UtcNow;
                _timerKeyboard.Start();
            }
        }
        private void EventSource_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (_currentKeyboardActions.Count == 0)
            {
                // Nothing is currently firing
                return;
            }

            KeyboardAction? action = GetAction(e);
            if (action == null)
            {
                return;
            }

            // Find and kill any actions that this equals (there should never be more than one, but it's better to be safe)
            _currentKeyboardActions.RemoveWhere(o => o == action.Value);

            // If there's nothing left, then turn off the timer
            if (_currentKeyboardActions.Count == 0)
            {
                _timerKeyboard.Stop();
            }
        }
        private void EventSource_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Make sure to stop any keyboard actions (the mouse down captures the mouse, but the keyboard doesn't, so this extra check is needed)
            _currentKeyboardActions.Clear();
            if (_timerKeyboard != null)
            {
                _timerKeyboard.Stop();
            }
        }
        private void KeyboardTimer_Tick(object sender, EventArgs e)
        {
            // Account for slow machines
            DateTime now = DateTime.UtcNow;
            double elapsedTime = (now - _lastKeyboardTick).TotalSeconds;
            _lastKeyboardTick = now;

            bool removeJump = false;

            foreach (KeyboardAction movement in _currentKeyboardActions)
            {
                switch (movement)
                {
                    case KeyboardAction.Forward:
                        Pan(0, elapsedTime);
                        break;

                    case KeyboardAction.Backward:
                        Pan(180, elapsedTime);
                        break;

                    case KeyboardAction.Left:
                        Pan(90, elapsedTime);
                        break;

                    case KeyboardAction.Right:
                        Pan(-90, elapsedTime);
                        break;

                    case KeyboardAction.Jump:
                        removeJump = true;
                        break;

                    default:
                        throw new ApplicationException("Unexpected CameraMovement: " + movement);
                }
            }

            if (removeJump)
            {
                // Only want to initiate the jump once.  Let momentum take it from there
                _currentKeyboardActions.RemoveWhere(o => o == KeyboardAction.Jump);
            }
        }

        #endregion

        #region Private Methods - do movement

        private void RotateCamera_FULL(Point currentPosition)
        {
            Vector3D camZ = _camera.LookDirection.ToUnit();
            Vector3D camX = -Vector3D.CrossProduct(camZ, _camera.UpDirection).ToUnit();
            Vector3D camY = Vector3D.CrossProduct(camZ, camX).ToUnit();

            double dX = currentPosition.X - _previousPosition2D.X;
            double dY = currentPosition.Y - _previousPosition2D.Y;

            dX *= _rotateScale;
            dY *= -_rotateScale;

            AxisAngleRotation3D aarY = new AxisAngleRotation3D(camY, dX);
            AxisAngleRotation3D aarX = new AxisAngleRotation3D(camX, dY);

            RotateTransform3D rotY = new RotateTransform3D(aarY);
            RotateTransform3D rotX = new RotateTransform3D(aarX);

            camZ = camZ * rotY.Value * rotX.Value;
            camZ.Normalize();
            camY = camY * rotX.Value * rotY.Value;
            camY.Normalize();

            _camera.LookDirection = camZ;
            _camera.UpDirection = camY;
        }
        private void RotateCamera(Point currentPosition)
        {
            Vector3D camZ = _camera.LookDirection.ToUnit();
            Vector3D camX = -Vector3D.CrossProduct(camZ, _camera.UpDirection).ToUnit();
            Vector3D camY = Vector3D.CrossProduct(camZ, camX).ToUnit();

            double dX = currentPosition.X - _previousPosition2D.X;
            double dY = currentPosition.Y - _previousPosition2D.Y;

            dX *= -_rotateScale;
            dY *= -_rotateScale;

            AxisAngleRotation3D aarY = new AxisAngleRotation3D(camY, dX);
            AxisAngleRotation3D aarX = new AxisAngleRotation3D(camX, dY);

            RotateTransform3D rotY = new RotateTransform3D(aarY);
            RotateTransform3D rotX = new RotateTransform3D(aarX);

            camZ = camZ * rotY.Value * rotX.Value;
            camZ.Normalize();
            camY = camY * rotX.Value * rotY.Value;
            camY.Normalize();

            _camera.LookDirection = camZ;
            //_camera.UpDirection = camY;       // don't let them cock their head like a curious dog
        }

        private void Pan(double angle, double elapsedSeconds)
        {
            // Project the camera's look direction onto the xy plane
            Vector projected = _camera.LookDirection.ToVector2D().ToUnit(true);
            if (projected.IsInvalid())
            {
                // They are looking straight up or down.  Just wait until they look elsewhere
                return;
            }

            Vector3D direction = projected.
                ToVector3D().
                GetRotatedVector(new Vector3D(0, 0, 1), angle);

            direction.Z = 0;        // it should already be zero, but make sure there's no drift

            _camera.Position += direction * (elapsedSeconds * _panSpeed);
        }

        #endregion
        #region Private Methods

        /// <summary>
        /// This figures out which action to perform
        /// </summary>
        private KeyboardAction? GetAction(KeyEventArgs e)
        {
            //TODO: If some rely on modifies like shift or control, use Keyboard.IsKeyDown(Key.LeftShift)

            switch (e.Key)
            {
                case Key.W:
                    return KeyboardAction.Forward;

                case Key.S:
                    return KeyboardAction.Backward;

                case Key.A:
                    return KeyboardAction.Left;

                case Key.D:
                    return KeyboardAction.Right;

                case Key.Space:
                    return KeyboardAction.Jump;
            }

            return null;
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
            x = ProjectToTrackball_Wrap(out bool localInvert, x);
            shouldInvertZ |= localInvert;

            y = ProjectToTrackball_Wrap(out localInvert, y);
            shouldInvertZ |= localInvert;

            // Project onto a sphere
            double z2 = 1d - (x * x) - (y * y);       // z^2 = 1 - x^2 - y^2
            double z = z2 > 0d ?
                Math.Sqrt(z2) :
                0d;     // NOTE:  The wrap logic above should make it so this never happens

            if (shouldInvertZ)
            {
                z *= -1d;
            }

            return new Vector3D(x, y, z);
        }
        /// <summary>
        /// This wraps the value so it stays between -1 and 1
        /// </summary>
        private static double ProjectToTrackball_Wrap(out bool shouldInvertZ, double value)
        {
            // Everything starts over at 4 (4 becomes zero)
            double retVal = value % 4d;

            double absX = Math.Abs(retVal);
            bool isNegX = retVal < 0d;

            shouldInvertZ = false;

            if (absX >= 3d)
            {
                // Anything from 3 to 4 needs to be -1 to 0
                // Anything from -4 to -3 needs to be 0 to 1
                retVal = 4d - absX;

                if (!isNegX)
                {
                    retVal *= -1d;
                }
            }
            else if (absX > 1d)
            {
                // This is the back side of the sphere
                // Anything from 1 to 3 needs to be flipped (1 stays 1, 2 becomes 0, 3 becomes -1)
                // -1 stays -1, -2 becomes 0, -3 becomes 1
                retVal = 2d - absX;

                if (isNegX)
                {
                    retVal *= -1d;
                }

                shouldInvertZ = true;
            }

            return retVal;
        }

        #endregion
    }
}
