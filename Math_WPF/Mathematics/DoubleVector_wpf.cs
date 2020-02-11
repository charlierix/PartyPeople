using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.Mathematics
{
    public struct DoubleVector_wpf
    {
        public Vector3D Standard;
        public Vector3D Orth;

        public DoubleVector_wpf(Vector3D standard, Vector3D orthogonalToStandard)
        {
            this.Standard = standard;
            this.Orth = orthogonalToStandard;
        }
        public DoubleVector_wpf(double standardX, double standardY, double standardZ, double orthogonalX, double orthogonalY, double orthogonalZ)
        {
            this.Standard = new Vector3D(standardX, standardY, standardZ);
            this.Orth = new Vector3D(orthogonalX, orthogonalY, orthogonalZ);
        }

        public Quaternion GetRotation(DoubleVector_wpf destination)
        {
            return Math3D.GetRotation(this, destination);
        }

        /// <summary>
        /// Rotates the double vector around the angle in degrees
        /// </summary>
        public DoubleVector_wpf GetRotatedVector(Vector3D axis, double angle)
        {
            Matrix3D matrix = new Matrix3D();
            matrix.Rotate(new Quaternion(axis, angle));

            MatrixTransform3D transform = new MatrixTransform3D(matrix);

            return new DoubleVector_wpf(transform.Transform(this.Standard), transform.Transform(this.Orth));
        }
    }
}
