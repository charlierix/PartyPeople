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

        public Quaternion rotation { get; init; }

        public double size { get; init; }
    }
}
