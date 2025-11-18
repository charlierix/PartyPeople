using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.WPF.DebugLogViewer.Models
{
    //NOTE: This class isn't what gets deserialized from json.  That's FileReader.Item_local, which then gets converted to this
    public abstract record ItemBase
    {
        // All of these properties are optional

        public Category category { get; init; }

        public Color? color { get; init; }

        public double? size_mult { get; init; }

        public string tooltip { get; init; }

        public abstract Point3D[] GetPoints();
    }
}
