using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Math_WPF.WPF.DebugLogViewer.Models
{
    public record LogScene
    {
        public Category[] categories { get; init; }

        public LogFrame[] frames { get; init; }

        public Text[] text { get; init; }

        public bool isRightHanded { get; init; }
    }
}
