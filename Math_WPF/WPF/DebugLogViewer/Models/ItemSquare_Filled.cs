using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.WPF.DebugLogViewer.Models
{
    public record ItemSquare_Filled : ItemBase
    {
        public Point3D center { get; init; }

        public Vector3D normal { get; init; }

        public double size_x { get; init; }

        public double size_y { get; init; }

        public override Point3D[] GetPoints()
        {
            double half_x = size_x / 2;
            double half_y = size_y / 2;

            Point3D[] points =
            [
                new Point3D(center.X - half_x, center.Y - half_y, 0),
                new Point3D(center.X + half_x, center.Y - half_y, 0),
                new Point3D(center.X + half_x, center.Y + half_y, 0),
                new Point3D(center.X - half_x, center.Y + half_y, 0),
            ];

            Quaternion quat = Math3D.GetRotation(new Vector3D(0, 0, 1), normal);

            quat.GetRotatedVector(points);

            return points;
        }
    }
}
