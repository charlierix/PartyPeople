using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.WPF.DebugLogViewer.Models
{
    public record ItemCircle_Edge : ItemBase
    {
        public Point3D center { get; init; }

        public Vector3D normal { get; init; }

        public double radius { get; init; }

        public override Point3D[] GetPoints()
        {
            Point[] circlePoints = Math2D.GetCircle_Cached(7);

            Quaternion quat = Math3D.GetRotation(new Vector3D(0, 0, 1), normal);

            Point3D[] rotated = new Point3D[circlePoints.Length];
            for (int i = 0; i < circlePoints.Length; i++)
                rotated[i] = new Point3D(circlePoints[i].X, circlePoints[i].Y, 0);

            quat.GetRotatedVector(rotated);

            var retVal = new Point3D[rotated.Length + 1];
            retVal[0] = center;
            for (int i = 0; i < rotated.Length; i++)
                retVal[i + 1] = rotated[i];

            return retVal;
        }
    }
}
