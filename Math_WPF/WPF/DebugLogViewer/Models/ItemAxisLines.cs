using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.WPF.DebugLogViewer.Models
{
    public record ItemAxisLines : ItemBase
    {
        public Point3D position { get; init; }

        // This has issues when converting between right and left handed.  It might have been an invalid mapping on my part,
        // or maybe the design is flawed (using quaternion to rotate +X, +Y, +Z vectors)
        //public Quaternion rotation { get; init; }

        // Going with vectors assigned at the time of logging
        // These are unit vectors
        public Vector3D axis_x { get; init; }
        public Vector3D axis_y { get; init; }
        public Vector3D axis_z { get; init; }

        public double size { get; init; }
    }
}
