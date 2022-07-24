using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Game.Math_WPF.WPF.Controls3D
{
    /// <summary>
    /// This views 3D objects defined in a json file that was built from in game
    /// </summary>
    /// <remarks>
    /// Models\LogScene is the root filetype
    /// 
    /// See txtFile_TextChanged
    /// 
    /// A lot of this code is just copied from Debug3DWindow and UtilityWPF
    /// </remarks>
    public partial class DebugLogWindow : Window
    {
        public DebugLogWindow()
        {
            InitializeComponent();
        }
    }
}
