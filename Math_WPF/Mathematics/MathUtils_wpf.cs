﻿//---------------------------------------------------------------------------
//
// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Limited Permissive License.
// See http://www.microsoft.com/resources/sharedsource/licensingbasics/limitedpermissivelicense.mspx
// All other rights reserved.
//
// This file is part of the 3D Tools for Windows Presentation Foundation
// project.  For more information, see:
// 
// http://CodePlex.com/Wiki/View.aspx?ProjectName=3DTools
//
//---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using System.Windows;
using System.Collections;

namespace Game.Math_WPF.Mathematics
{
    public static partial class MathUtils
    {
        private static class Viewport3DHelper
        {
            #region class: FindResult<T>

            public class FindResult<T>
            {
                private readonly Visual3D _visual;
                private readonly T _item;

                public FindResult(Visual3D visual, T item)
                {
                    _visual = visual;
                    _item = item;
                }

                public Visual3D Visual
                {
                    get { return _visual; }
                }

                public T Item
                {
                    get { return _item; }
                }
            }

            #endregion

            public static Viewport3DVisual GetViewportVisual(DependencyObject visual)
            {
                if (!(visual is Visual3D))
                {
                    throw new ArgumentException("Must be of type Visual3D.", "visual");
                }

                while (visual != null)
                {
                    if (!(visual is ModelVisual3D))
                    {
                        break;
                    }

                    visual = VisualTreeHelper.GetParent(visual);
                }

                if (visual != null)
                {
                    Viewport3DVisual viewport = visual as Viewport3DVisual;

                    if (viewport == null)
                    {
                        // In WPF 3D v1 the only possible configuration is a chain of
                        // ModelVisual3Ds leading up to a Viewport3DVisual.

                        throw new ApplicationException(
                            String.Format("Unsupported type: '{0}'.  Expected tree of ModelVisual3Ds leading up to a Viewport3DVisual.",
                            visual.GetType().FullName));
                    }

                    return viewport;
                }
                else
                    return null;
            }
            public static Viewport3DVisual GetViewportVisual(Viewport3D viewport)
            {
                int count = VisualTreeHelper.GetChildrenCount(viewport);
                if (count > 0)
                    return (Viewport3DVisual)VisualTreeHelper.GetChild(viewport, 0);
                else
                    return null;
            }

            public static void Dispose(ICollection<Visual3D> items, bool clearChildren)
            {
                foreach (Visual3D item in items)
                {
                    Dispose(item, false, clearChildren);
                }

                if (clearChildren)
                    items.Clear();
            }
            public static void Dispose(DependencyObject item, bool removeSelf, bool clearChildren)
            {
                if (item is ModelVisual3D)
                {
                    Dispose(((ModelVisual3D)item).Children, clearChildren);
                }
                else
                {
                    if (clearChildren) throw new ArgumentException("Can not clear the children on a Visual.", "clearChildren");

                    for (int i = VisualTreeHelper.GetChildrenCount(item) - 1; i >= 0; i--)
                        Dispose(VisualTreeHelper.GetChild(item, i), removeSelf, clearChildren);
                }

                if (removeSelf)
                {
                    object p = VisualTreeHelper.GetParent(item);

                    if (p is ModelVisual3D)
                        ((ModelVisual3D)p).Children.Remove((Visual3D)item);
                    else if (p is Viewport3DVisual)
                        ((Viewport3DVisual)p).Children.Remove((Visual3D)item);
                    else if (p is Panel)
                        ((Panel)p).Children.Remove((UIElement)item);
                    else
                        new ArgumentException("Can only removeSelf for ModelVisual3D, Viewport3DVisual and Panel.", "removeSelf");
                }

                IDisposable disposable = (item as IDisposable);
                if (disposable != null)
                    disposable.Dispose();
            }
            public static void Dispose(DependencyObject item)
            {
                Dispose(item, true, false);
            }

            public static Visual3D IsParent(Visual3D visual, params Visual3D[] parents)
            {
                while (visual != null)
                {
                    int index = Array.IndexOf(parents, visual);
                    if (index >= 0)
                        return parents[index];

                    visual = (VisualTreeHelper.GetParent(visual) as Visual3D);
                }

                return null;
            }

            public static FindResult<T> Find<T>(Viewport3DVisual viewport)
                where T : DependencyObject
            {
                foreach (Visual3D item in viewport.Children)
                {
                    FindResult<T> result = Find<T>(item);
                    if (result != null)
                        return result;
                }

                return null;
            }
            public static FindResult<T> Find<T>(Viewport3D viewport)
                 where T : DependencyObject
            {
                foreach (Visual3D item in viewport.Children)
                {
                    FindResult<T> result = Find<T>(item);
                    if (result != null)
                        return result;
                }

                return null;
            }
            private static FindResult<T> Find<T>(Visual3D visual, IList<FindResult<T>> results)
                 where T : DependencyObject
            {
                if (visual == null)
                    return null;

                T item;

                item = (visual as T);
                if (item != null)
                    return new FindResult<T>(visual, item);

                ModelVisual3D model = (visual as ModelVisual3D);
                if (model != null)
                {
                    item = (model.Content as T);
                    if (item != null)
                        return new FindResult<T>(visual, item);

                    foreach (Visual3D i in model.Children)
                    {
                        if (results != null)
                        {
                            Find<T>(i, results);
                        }
                        else
                        {
                            FindResult<T> result = Find<T>(i, null);
                            if (result != null)
                                return result;
                        }
                    }
                }

                return null;
            }
            public static FindResult<T> Find<T>(Visual3D visual)
                 where T : DependencyObject
            {
                return Find<T>(visual, null);
            }

            public static IList<FindResult<T>> FindAll<T>(Visual3D visual)
                 where T : DependencyObject
            {
                List<FindResult<T>> results = new List<FindResult<T>>();
                Find<T>(visual, results);
                return results;
            }
            public static IList<FindResult<T>> FindAll<T>(Viewport3DVisual viewport)
                where T : DependencyObject
            {
                List<FindResult<T>> result = new List<FindResult<T>>();

                foreach (Visual3D item in viewport.Children)
                {
                    Find<T>(item, result);
                }

                return result;
            }
            public static IList<FindResult<T>> FindAll<T>(Viewport3D viewport)
                where T : DependencyObject
            {
                List<FindResult<T>> result = new List<FindResult<T>>();

                foreach (Visual3D item in viewport.Children)
                {
                    Find<T>(item, result);
                }

                return result;
            }

            public static void CopyChildren(Viewport3D targetViewport, Viewport3D sourceViewport)
            {
                CopyChildren(targetViewport.Children, sourceViewport.Children);
            }
            public static void CopyChildren(Viewport3DVisual targetViewport, Viewport3DVisual sourceViewport)
            {
                CopyChildren(targetViewport.Children, sourceViewport.Children);
            }
            public static void CopyChildren(Visual3DCollection targetCollection, Visual3DCollection sourceCollection)
            {
                foreach (Visual3D item in sourceCollection)
                {
                    Visual3D newVisual3D = (Visual3D)Activator.CreateInstance(item.GetType());
                    ModelVisual3D newModel = (newVisual3D as ModelVisual3D);
                    if (newModel != null)
                    {
                        ModelVisual3D sourceModel = (ModelVisual3D)item;
                        newModel.Content = sourceModel.Content;
                        newModel.Transform = sourceModel.Transform;

                        CopyChildren(newModel.Children, sourceModel.Children);
                    }
                    targetCollection.Add(newVisual3D);
                }
            }
        }

        /// <summary>
        /// Matrix3DStack is a stack of Matrix3Ds.
        /// </summary>
        public class Matrix3DStack : IEnumerable<Matrix3D>, ICollection
        {
            public Matrix3D Peek()
            {
                return _storage[_storage.Count - 1];
            }

            public void Push(Matrix3D item)
            {
                _storage.Add(item);
            }

            public void Append(Matrix3D item)
            {
                if (Count > 0)
                {
                    Matrix3D top = Peek();
                    top.Append(item);
                    Push(top);
                }
                else
                {
                    Push(item);
                }
            }

            public void Prepend(Matrix3D item)
            {
                if (Count > 0)
                {
                    Matrix3D top = Peek();
                    top.Prepend(item);
                    Push(top);
                }
                else
                {
                    Push(item);
                }
            }

            public Matrix3D Pop()
            {
                Matrix3D result = Peek();
                _storage.RemoveAt(_storage.Count - 1);

                return result;
            }

            public int Count
            {
                get { return _storage.Count; }
            }

            void Clear()
            {
                _storage.Clear();
            }

            bool Contains(Matrix3D item)
            {
                return _storage.Contains(item);
            }

            private readonly List<Matrix3D> _storage = new List<Matrix3D>();

            #region ICollection Members

            void ICollection.CopyTo(Array array, int index)
            {
                ((ICollection)_storage).CopyTo(array, index);
            }

            bool ICollection.IsSynchronized
            {
                get { return ((ICollection)_storage).IsSynchronized; }
            }

            object ICollection.SyncRoot
            {
                get { return ((ICollection)_storage).SyncRoot; }
            }

            #endregion

            #region IEnumerable Members

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable<Matrix3D>)this).GetEnumerator();
            }

            #endregion

            #region IEnumerable<Matrix3D> Members

            IEnumerator<Matrix3D> IEnumerable<Matrix3D>.GetEnumerator()
            {
                for (int i = _storage.Count - 1; i >= 0; i--)
                {
                    yield return _storage[i];
                }
            }

            #endregion
        }

        private static Matrix3D GetViewMatrix(ProjectionCamera camera)
        {
            if (camera == null) throw new ArgumentNullException("camera");

            // This math is identical to what you find documented for
            // D3DXMatrixLookAtRH with the exception that WPF uses a
            // LookDirection vector rather than a LookAt point.

            Vector3D zAxis = -camera.LookDirection;
            zAxis.Normalize();

            Vector3D xAxis = Vector3D.CrossProduct(camera.UpDirection, zAxis);
            xAxis.Normalize();

            Vector3D yAxis = Vector3D.CrossProduct(zAxis, xAxis);

            Vector3D position = (Vector3D)camera.Position;
            double offsetX = -Vector3D.DotProduct(xAxis, position);
            double offsetY = -Vector3D.DotProduct(yAxis, position);
            double offsetZ = -Vector3D.DotProduct(zAxis, position);

            Matrix3D m = new Matrix3D(
                xAxis.X, yAxis.X, zAxis.X, 0,
                xAxis.Y, yAxis.Y, zAxis.Y, 0,
                xAxis.Z, yAxis.Z, zAxis.Z, 0,
                offsetX, offsetY, offsetZ, 1);

            return m;
        }
        /// <summary>
        ///     Computes the effective view matrix for the given
        ///     camera.
        /// </summary>
        public static Matrix3D GetViewMatrix(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException("camera");

            ProjectionCamera projectionCamera = camera as ProjectionCamera;

            if (projectionCamera != null)
            {
                return GetViewMatrix(projectionCamera);
            }

            MatrixCamera matrixCamera = camera as MatrixCamera;

            if (matrixCamera != null)
            {
                return matrixCamera.ViewMatrix;
            }

            throw new ArgumentException(String.Format("Unsupported camera type '{0}'.", camera.GetType().FullName), "camera");
        }

        private static Matrix3D GetProjectionMatrix(OrthographicCamera camera, double aspectRatio)
        {
            if (camera == null) throw new ArgumentNullException("camera");

            // This math is identical to what you find documented for
            // D3DXMatrixOrthoRH with the exception that in WPF only
            // the camera's width is specified.  Height is calculated
            // from width and the aspect ratio.

            double w = camera.Width;
            double h = w / aspectRatio;
            double zn = camera.NearPlaneDistance;
            double zf = camera.FarPlaneDistance;

            double m33 = 1 / (zn - zf);
            double m43 = zn * m33;

            return new Matrix3D(
                2 / w, 0, 0, 0,
                  0, 2 / h, 0, 0,
                  0, 0, m33, 0,
                  0, 0, m43, 1);
        }
        private static Matrix3D GetProjectionMatrix(PerspectiveCamera camera, double aspectRatio)
        {
            if (camera == null) throw new ArgumentNullException("camera");

            // This math is identical to what you find documented for
            // D3DXMatrixPerspectiveFovRH with the exception that in
            // WPF the camera's horizontal rather the vertical
            // field-of-view is specified.

            double hFoV = Math1D.DegreesToRadians(camera.FieldOfView);
            double zn = camera.NearPlaneDistance;
            double zf = camera.FarPlaneDistance;

            double xScale = 1 / Math.Tan(hFoV / 2);
            double yScale = aspectRatio * xScale;
            double m33 = (zf == double.PositiveInfinity) ? -1 : (zf / (zn - zf));
            double m43 = zn * m33;

            return new Matrix3D(
                xScale, 0, 0, 0,
                     0, yScale, 0, 0,
                     0, 0, m33, -1,
                     0, 0, m43, 0);
        }
        /// <summary>
        ///     Computes the effective projection matrix for the given
        ///     camera.
        /// </summary>
        public static Matrix3D GetProjectionMatrix(Camera camera, double aspectRatio)
        {
            if (camera == null) throw new ArgumentNullException("camera");

            PerspectiveCamera perspectiveCamera = camera as PerspectiveCamera;

            if (perspectiveCamera != null)
            {
                return GetProjectionMatrix(perspectiveCamera, aspectRatio);
            }

            OrthographicCamera orthographicCamera = camera as OrthographicCamera;

            if (orthographicCamera != null)
            {
                return GetProjectionMatrix(orthographicCamera, aspectRatio);
            }

            MatrixCamera matrixCamera = camera as MatrixCamera;

            if (matrixCamera != null)
            {
                return matrixCamera.ProjectionMatrix;
            }

            throw new ArgumentException(String.Format("Unsupported camera type '{0}'.", camera.GetType().FullName), "camera");
        }

        public static Matrix3D GetHomogeneousToViewportTransform(Rect viewport)
        {
            double scaleX = viewport.Width / 2;
            double scaleY = viewport.Height / 2;
            double offsetX = viewport.X + scaleX;
            double offsetY = viewport.Y + scaleY;

            return new Matrix3D(
                 scaleX, 0, 0, 0,
                      0, -scaleY, 0, 0,
                      0, 0, 1, 0,
                offsetX, offsetY, 0, 1);
        }

        public static Matrix3D GetViewportToHomogeneousTransform(Rect viewport)
        {
            double scaleX = 1;
            double scaleY = 1;
            double offsetX = -viewport.X - (viewport.Width / 2);
            double offsetY = +viewport.Y + (viewport.Height / 2);

            return new Matrix3D(
                 scaleX, 0, 0, 0,
                      0, -scaleY, 0, 0,
                      0, 0, 1, 0,
                offsetX, offsetY, 0, 1);
        }

        /// <summary>
        ///     Computes the transform from world space to the Viewport3DVisual's
        ///     inner 2D space.
        /// 
        ///     This method can fail if Camera.Transform is non-invertable
        ///     in which case the camera clip planes will be coincident and
        ///     nothing will render.  In this case success will be false.
        /// </summary>
        public static Matrix3D TryWorldToViewportTransform(Viewport3DVisual visual, out bool success)
        {
            Matrix3D result = TryWorldToCameraTransform(visual, out success);

            if (success)
            {
                result.Append(GetProjectionMatrix(visual.Camera, Math2D.GetAspectRatio(visual.Viewport.Size)));
                result.Append(GetHomogeneousToViewportTransform(visual.Viewport));
                success = true;
            }

            return result;
        }

        public static Matrix3D GetWorldToViewportTransform(Viewport3DVisual visual)
        {
            bool success;
            Matrix3D result = TryWorldToViewportTransform(visual, out success);
            if (success)
                return result;
            else
                return ZeroMatrix;
        }
        public static Matrix3D GetWorldToViewportTransform(Viewport3D viewport)
        {
            return GetWorldToViewportTransform(Viewport3DHelper.GetViewportVisual(viewport));
        }

        public static Matrix3D GetTransformToLocal(DependencyObject visual)
        {
            Matrix3D result = GetTransformToWorld(visual);
            if (result.HasInverse)
            {
                result.Invert();
                return result;
            }
            else
                return ZeroMatrix;
        }

        /// <summary>
        ///     Computes the transform from world space to camera space
        /// 
        ///     This method can fail if Camera.Transform is non-invertable
        ///     in which case the camera clip planes will be coincident and
        ///     nothing will render.  In this case success will be false.
        /// </summary>
        public static Matrix3D TryWorldToCameraTransform(Viewport3DVisual visual, out bool success)
        {
            success = false;

            Camera camera = (visual != null) ? visual.Camera : null;
            if (camera == null)
            {
                return ZeroMatrix;
            }

            Rect viewport = visual.Viewport;
            if (viewport == Rect.Empty)
            {
                return ZeroMatrix;
            }

            Matrix3D result = Matrix3D.Identity;

            Transform3D cameraTransform = camera.Transform;
            if (cameraTransform != null)
            {
                Matrix3D m = cameraTransform.Value;

                if (!m.HasInverse)
                {
                    return ZeroMatrix;
                }

                m.Invert();
                result.Append(m);
            }

            result.Append(GetViewMatrix(camera));

            success = true;
            return result;
        }

        /// <summary>
        /// Gets the object space to world space transformation for the given DependencyObject
        /// </summary>
        /// <param name="visual">The visual whose world space transform should be found</param>
        /// <param name="viewport">The Viewport3DVisual the Visual is contained within</param>
        /// <returns>The world space transformation</returns>
        public static Matrix3D GetTransformToWorld(DependencyObject visual, out Viewport3DVisual viewport)
        {
            Matrix3D worldTransform = Matrix3D.Identity;
            viewport = null;

            if (visual == null)
                return worldTransform;

            if (visual is Viewport3DVisual)
                return worldTransform;

            if (!(visual is Visual3D))
                throw new ArgumentException("Must be of type Visual3D or Viewport3DVisual.", "visual");

            while (visual != null)
            {
                if (!(visual is ModelVisual3D))
                {
                    break;
                }

                Transform3D transform = (Transform3D)visual.GetValue(ModelVisual3D.TransformProperty);

                if (transform != null)
                {
                    worldTransform.Append(transform.Value);
                }

                visual = VisualTreeHelper.GetParent(visual);
            }

            viewport = visual as Viewport3DVisual;

            if (viewport == null)
            {
                if (visual != null)
                {
                    // In WPF 3D v1 the only possible configuration is a chain of
                    // ModelVisual3Ds leading up to a Viewport3DVisual.

                    throw new ApplicationException(
                        String.Format("Unsupported type: '{0}'.  Expected tree of ModelVisual3Ds leading up to a Viewport3DVisual.",
                        visual.GetType().FullName));
                }

                return ZeroMatrix;
            }

            return worldTransform;
        }
        /// <summary>
        /// Gets the object space to world space transformation for the given DependencyObject
        /// </summary>
        /// <param name="visual">The visual whose world space transform should be found</param>
        /// <returns>The world space transformation</returns>
        /// 
        public static Matrix3D GetTransformToWorld(DependencyObject visual)
        {
            Viewport3DVisual viewport;
            return GetTransformToWorld(visual, out viewport);
        }

        /// <summary>
        /// Computes the transform from the inner space of the given
        /// Visual3D to the 2D space of the Viewport3DVisual which
        /// contains it.
        /// The result will contain the transform of the given visual.
        /// This method can fail if Camera.Transform is non-invertable
        /// in which case the camera clip planes will be coincident and
        /// nothing will render.  In this case success will be false.
        /// </summary>
        /// <param name="visual">The visual.</param>
        /// <param name="viewport">The viewport.</param>
        /// <param name="success">if set to <c>true</c> [success].</param>
        public static Matrix3D TryTransformTo2DAncestor(DependencyObject visual, out Viewport3DVisual viewport, out bool success)
        {
            Matrix3D to2D = GetTransformToWorld(visual, out viewport);
            to2D.Append(TryWorldToViewportTransform(viewport, out success));

            if (!success)
            {
                return ZeroMatrix;
            }

            return to2D;
        }

        public static Matrix3D GetTransformToViewport(Visual3D visual)
        {
            Viewport3DVisual viewport;
            Matrix3D to2D = GetTransformToWorld(visual, out viewport);
            to2D.Append(GetWorldToViewportTransform(viewport));

            return to2D;
        }

        /// <summary>
        ///     Computes the transform from the inner space of the given
        ///     Visual3D to the camera coordinate space
        /// 
        ///     The result will contain the transform of the given visual.
        /// 
        ///     This method can fail if Camera.Transform is non-invertable
        ///     in which case the camera clip planes will be coincident and
        ///     nothing will render.  In this case success will be false.
        /// </summary>
        /// <param name="visual"></param>
        /// <param name="success"></param>
        /// <returns></returns>
        public static Matrix3D TryTransformToCameraSpace(DependencyObject visual, out Viewport3DVisual viewport, out bool success)
        {
            Matrix3D toViewSpace = GetTransformToWorld(visual, out viewport);
            toViewSpace.Append(TryWorldToCameraTransform(viewport, out success));

            if (!success)
            {
                return ZeroMatrix;
            }

            return toViewSpace;
        }

        /// <summary>
        ///     Transforms the axis-aligned bounding box 'bounds' by
        ///     'transform'
        /// </summary>
        /// <param name="bounds">The AABB to transform</param>
        /// <returns>Transformed AABB</returns>
        public static Rect3D TransformBounds(Rect3D bounds, Matrix3D transform)
        {
            double x1 = bounds.X;
            double y1 = bounds.Y;
            double z1 = bounds.Z;
            double x2 = bounds.X + bounds.SizeX;
            double y2 = bounds.Y + bounds.SizeY;
            double z2 = bounds.Z + bounds.SizeZ;

            Point3D[] points = new Point3D[] {
                new Point3D(x1, y1, z1),
                new Point3D(x1, y1, z2),
                new Point3D(x1, y2, z1),
                new Point3D(x1, y2, z2),
                new Point3D(x2, y1, z1),
                new Point3D(x2, y1, z2),
                new Point3D(x2, y2, z1),
                new Point3D(x2, y2, z2),
            };

            transform.Transform(points);

            // reuse the 1 and 2 variables to stand for smallest and largest
            Point3D p = points[0];
            x1 = x2 = p.X;
            y1 = y2 = p.Y;
            z1 = z2 = p.Z;

            for (int i = 1; i < points.Length; i++)
            {
                p = points[i];

                x1 = Math.Min(x1, p.X); y1 = Math.Min(y1, p.Y); z1 = Math.Min(z1, p.Z);
                x2 = Math.Max(x2, p.X); y2 = Math.Max(y2, p.Y); z2 = Math.Max(z2, p.Z);
            }

            return new Rect3D(x1, y1, z1, x2 - x1, y2 - y1, z2 - z1);
        }

        /// <summary>
        ///     Normalizes v if |v| > 0.
        /// 
        ///     This normalization is slightly different from Vector3D.Normalize. Here
        ///     we just divide by the length but Vector3D.Normalize tries to avoid
        ///     overflow when finding the length.
        /// </summary>
        /// <param name="v">The vector to normalize</param>
        /// <returns>'true' if v was normalized</returns>
        public static bool TryNormalize(ref Vector3D v)
        {
            double length = v.Length;

            if (length != 0)
            {
                v /= length;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Computes the center of 'box'
        /// </summary>
        /// <param name="box">The Rect3D we want the center of</param>
        /// <returns>The center point</returns>
        public static Point3D GetCenter(Rect3D box)
        {
            return new Point3D(box.X + box.SizeX / 2, box.Y + box.SizeY / 2, box.Z + box.SizeZ / 2);
        }

        public static Matrix3D GetViewportToWorldTransform(Viewport3DVisual viewport)
        {
            Matrix3D matrix = GetWorldToViewportTransform(viewport);
            if (matrix.HasInverse)
            {
                matrix.Invert();
                return matrix;
            }
            else
                return ZeroMatrix;
        }
        public static Matrix3D GetViewportToVisualTransform(Visual3D visual)
        {
            Matrix3D matrix = GetTransformToViewport(visual);
            if (matrix.HasInverse)
            {
                matrix.Invert();
                return matrix;
            }
            else
                return ZeroMatrix;
        }

        public static double MinMax(double? min, double value, double? max)
        {
            if ((min != null) && (value < (double)min))
                return (double)min;
            else if ((max != null) && (value > (double)max))
                return (double)max;
            else
                return value;
        }
        public static float MinMax(float min, float value, float max)
        {
            if (value < min)
                return min;
            else if (value > max)
                return max;
            else
                return value;
        }
        public static int MinMax(int min, int value, int max)
        {
            if (value < min)
                return min;
            else if (value > max)
                return max;
            else
                return value;
        }

        public static readonly Matrix3D ZeroMatrix = new Matrix3D(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        public static readonly Vector3D XAxis = new Vector3D(1, 0, 0);
        public static readonly Vector3D YAxis = new Vector3D(0, 1, 0);
        public static readonly Vector3D ZAxis = new Vector3D(0, 0, 1);
    }
}
