using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.Mathematics
{
    public struct Capsule_wpf
    {
        /// <summary>
        /// True: The points are inside the capsule (boundry between cylinder and domes) - full capsule height is 2R+H
        /// False: The points are at the tips of the capsule (poles of the domes)
        /// </summary>
        public bool IsInterior { get; set; }

        public Point3D From { get; set; }
        public Point3D To { get; set; }

        public double Radius { get; set; }

        public Capsule_wpf ToInterior()
        {
            Point3D interior_from, interior_to;

            if (IsInterior)
            {
                interior_from = From;
                interior_to = To;
            }
            else
            {
                Vector3D direction = To - From;
                if (direction.LengthSquared <= Radius * Radius)
                {
                    // It's too small to be a capsule, treat it like a sphere
                    Point3D center = Math3D.GetCenter(From, To);

                    interior_from = center;
                    interior_to = center;
                }
                else
                {
                    Vector3D dir_unit = direction.ToUnit();
                    interior_from = From + (dir_unit * Radius);
                    interior_to = To - (dir_unit * Radius);
                }
            }

            return new Capsule_wpf()
            {
                IsInterior = true,
                From = interior_from,
                To = interior_to,
                Radius = Radius,
            };
        }
        public Capsule_wpf ToExterior()
        {
            Point3D exterior_from, exterior_to;

            if (!IsInterior)
            {
                exterior_from = From;
                exterior_to = To;
            }
            else
            {
                Vector3D direction = To - From;
                if (direction.LengthSquared.IsNearZero())
                {
                    // It's just a sphere.  Pick two arbitrary points
                    exterior_from = new Point3D(From.X - Radius, From.Y, From.Z);
                    exterior_to = new Point3D(From.X + Radius, From.Y, From.Z);
                }
                else
                {
                    Vector3D dir_unit = direction.ToUnit();

                    exterior_from = From - (dir_unit * Radius);
                    exterior_to = To + (dir_unit * Radius);
                }
            }

            return new Capsule_wpf()
            {
                IsInterior = false,
                From = exterior_from,
                To = exterior_to,
                Radius = Radius,
            };
        }
    }
}
