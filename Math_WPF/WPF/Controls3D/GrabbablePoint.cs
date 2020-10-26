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
    /// <summary>
    /// Attach this script to an object that you want the player to be able to drag around.  When the left mouse
    /// button is down, the object will be placed under the mouse cursor
    /// </summary>
    /// <remarks>
    /// This doesn't update any visuals, just the position.  It's up to the caller to sync this position with whatever
    /// visual it represents
    /// </remarks>
    public class GrabbablePoint
    {
        private const string TITLE = "GrabbablePoint";

        private readonly PerspectiveCamera _camera;
        private readonly Viewport3D _viewport;
        private readonly UIElement _mouseSource;

        private readonly double _clickRadius;

        public Point3D Position { get; set; }

        private bool _isDragging = false;
        private ITriangle_wpf _clickPlane;

        public GrabbablePoint(PerspectiveCamera camera, Viewport3D viewport, UIElement mouseSource, Point3D position, double clickRadius)
        {
            _camera = camera;
            _viewport = viewport;
            _mouseSource = mouseSource;

            Position = position;
            _clickRadius = clickRadius;

            _mouseSource.MouseDown += MouseSource_MouseDown;
            _mouseSource.MouseMove += MouseSource_MouseMove;
            _mouseSource.MouseUp += MouseSource_MouseUp;
        }

        private void MouseSource_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton != MouseButton.Left || e.LeftButton != MouseButtonState.Pressed)
                    return;

                // When they first hit the mouse down, define the click point to be along the camera look and intersecting the grab point
                _clickPlane = new Triangle_wpf(_camera.LookDirection, Position);

                // Fire a ray from the mouse point
                Point3D? intersect = FireRay(_mouseSource, _camera, _viewport, _clickPlane, e);
                if (intersect == null)
                    return;

                if ((intersect.Value - Position).LengthSquared > _clickRadius * _clickRadius)
                    return;

                _isDragging = true;

                // Move the grab object to where the mouse ray intersects the click plane
                Position = intersect.Value;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), TITLE, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void MouseSource_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (!_isDragging)
                    return;

                Point3D? intersect = FireRay(_mouseSource, _camera, _viewport, _clickPlane, e);
                if (intersect == null)
                    return;

                Position = intersect.Value;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), TITLE, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void MouseSource_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (_isDragging && e.ChangedButton == MouseButton.Left)
                {
                    _isDragging = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), TITLE, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static Point3D? FireRay(UIElement mouseSource, PerspectiveCamera camera, Viewport3D viewport, ITriangle_wpf clickPlane, MouseEventArgs e)
        {
            Point clickPoint = e.GetPosition(mouseSource);
            var ray = UtilityWPF.RayFromViewportPoint(camera, viewport, clickPoint);

            return Math3D.GetIntersection_Plane_Ray(clickPlane, ray.Origin, ray.Direction);
        }
    }
}
