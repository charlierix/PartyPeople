using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.WPF.DebugLogViewer.Models
{
    public record ItemLine : ItemBase
    {
        public Point3D point1 { get; init; }
        public Point3D point2 { get; init; }

        public override Point3D[] GetPoints()
        {
            return [point1, point2];
        }
    }
}
