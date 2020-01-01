using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Game.Mathematics
{
    public partial struct VectorND : IEnumerable<double>
    {
        public Point ToPoint(bool enforceSize = true)
        {
            double[] vector = VectorArray;

            if (enforceSize)
            {
                if (vector == null || vector.Length != 2)
                {
                    throw new InvalidOperationException("This vector isn't set up to return a 2D point: " + vector == null ? "null" : vector.Length.ToString());
                }

                return new Point(vector[0], vector[1]);
            }
            else
            {
                if (vector == null)
                {
                    return new Point();
                }

                return new Point
                (
                    vector.Length >= 1 ?
                        vector[0] :
                        0,
                    vector.Length >= 2 ?
                        vector[1] :
                        0
                );
            }
        }
        public Vector ToVector(bool enforceSize = true)
        {
            double[] vector = VectorArray;

            if (enforceSize)
            {
                if (vector == null || vector.Length != 2)
                {
                    throw new InvalidOperationException("This vector isn't set up to return a 2D vector: " + vector == null ? "null" : vector.Length.ToString());
                }

                return new Vector(vector[0], vector[1]);
            }
            else
            {
                if (vector == null)
                {
                    return new Vector();
                }

                return new Vector
                (
                    vector.Length >= 1 ?
                        vector[0] :
                        0,
                    vector.Length >= 2 ?
                        vector[1] :
                        0
                );
            }
        }
        public Point3D ToPoint3D(bool enforceSize = true)
        {
            double[] vector = VectorArray;

            if (enforceSize)
            {
                if (vector == null || vector.Length != 3)
                {
                    throw new InvalidOperationException("This vector isn't set up to return a 3D point: " + vector == null ? "null" : vector.Length.ToString());
                }

                return new Point3D(vector[0], vector[1], vector[2]);
            }
            else
            {
                if (vector == null)
                {
                    return new Point3D();
                }

                return new Point3D
                (
                    vector.Length >= 1 ?
                        vector[0] :
                        0,
                    vector.Length >= 2 ?
                        vector[1] :
                        0,
                    vector.Length >= 3 ?
                        vector[2] :
                        0
                );
            }
        }
        public Vector3D ToVector3D(bool enforceSize = true)
        {
            double[] vector = VectorArray;

            if (enforceSize)
            {
                if (vector == null || vector.Length != 3)
                {
                    throw new InvalidOperationException("This vector isn't set up to return a 3D point: " + vector == null ? "null" : vector.Length.ToString());
                }

                return new Vector3D(vector[0], vector[1], vector[2]);
            }
            else
            {
                if (vector == null)
                {
                    return new Vector3D();
                }

                return new Vector3D
                (
                    vector.Length >= 1 ?
                        vector[0] :
                        0,
                    vector.Length >= 2 ?
                        vector[1] :
                        0,
                    vector.Length >= 3 ?
                        vector[2] :
                        0
                );
            }
        }
    }
}
