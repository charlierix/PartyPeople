using BepuPhysics.Collidables;
using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF.Controls3D;
using Game.Math_WPF.WPF.Viewers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace Game.Bepu.Testers
{
    public partial class PlanesThruBezier : Window
    {
        #region Declaration Section

        private readonly string _folder = System.IO.Path.Combine(UtilityCore.GetOptionsFolder(), System.IO.Path.Combine("VRFlight", "Planes"));

        private List<Visual3D> _visuals = new List<Visual3D>();

        #endregion

        #region Constructor

        public PlanesThruBezier()
        {
            InitializeComponent();
        }

        #endregion

        #region Event Listeners

        private void LoadFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Prompt for file
                string filename = PromptForFileName();
                if (filename == null)
                    return;

                string jsonString = System.IO.File.ReadAllText(filename);

                PlaneSet planeSet = JsonSerializer.Deserialize<PlaneSet>(jsonString);

                //ClearVisuals();

                ShowPlanes(planeSet.Horizontal, "Horizontal");
                ShowPlanes(planeSet.Vertical, "Vertical");

                ShowDeltaRotation(planeSet.Horizontal, "Horizontal");
                ShowDeltaRotation(planeSet.Vertical, "Vertical");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private void ClearVisuals()
        {
            _viewport.Children.RemoveAll(_visuals);
            _visuals.Clear();
        }

        private static void ShowPlanes(Plane3[] planes, string name = null)
        {
            var window = new Debug3DWindow();

            if (!string.IsNullOrWhiteSpace(name))
                window.Title = name;

            var sizes = Debug3DWindow.GetDrawSizes(1);

            window.AddAxisLines(1, sizes.line);

            foreach (var plane in planes)
            {
                Point3D pos = Vec3.ToPoint(plane.pos);
                Vector3D norm = Vec3.ToVector(plane.norm);

                window.AddDot(pos, sizes.dot, Colors.White);
                window.AddLine(pos, pos + norm, sizes.line, Colors.White);

                window.AddPlane(new Triangle_wpf(norm, pos), 1, Colors.White, center: pos);
            }

            window.Show();
        }

        private static void ShowDeltaRotation(Plane3[] planes, string name = null)
        {
            const int STEPS = 24;

            var window = new Debug3DWindow();

            if (!string.IsNullOrWhiteSpace(name))
                window.Title = name;

            var sizes = Debug3DWindow.GetDrawSizes(1);

            for (int cntr = 0; cntr < planes.Length; cntr++)
            {
                window.AddLine(new Point3D(), Vec3.ToPoint(planes[cntr].norm), sizes.line, (cntr == 0 || cntr == planes.Length - 1) ? Colors.White : Colors.Black);
            }

            Quaternion delta = Math3D.GetRotation(Vec3.ToVector(planes[0].norm), Vec3.ToVector(planes[^1].norm));
            Vector3D from = Vec3.ToVector(planes[0].norm) * .9;

            for (int cntr = 1; cntr < STEPS - 1; cntr++)        // don't draw the first and last, because that will be the same as the white lines
            {
                Vector3D rotated = from.GetRotatedVector(delta.Axis, UtilityMath.GetScaledValue(0, delta.Angle, 0, STEPS - 1, cntr));

                window.AddLine(new Point3D(), rotated.ToPoint(), sizes.line * .5, Colors.Gray);
            }

            window.Show();
        }

        private string PromptForFileName()
        {
            string[] retVal = Prompt_DoIt(_folder, false);

            if (retVal == null || retVal.Length == 0)
                return null;
            else if (retVal.Length > 1)
                throw new ApplicationException($"There should never be more than one file: {retVal.Length}");
            else
                return retVal[0];
        }
        private string[] PromptForFileNames()
        {
            return Prompt_DoIt(_folder, true);
        }
        private static string[] Prompt_DoIt(string folder, bool isMultiSelect)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog()
            {
                InitialDirectory = folder,
                //Filter = "*.json|*.xml|*.*",
                Multiselect = isMultiSelect,
            };

            bool? result = dialog.ShowDialog();
            if (result == null || result.Value == false)
            {
                return null;
            }

            return dialog.FileNames;
        }

        #endregion
    }

    #region classes: serialized

    [Serializable]
    public class PlaneSet
    {
        // These are low to high (back to front)
        public Plane3[] Horizontal { get; set; }
        public Plane3[] Vertical { get; set; }
    }

    #endregion
}
