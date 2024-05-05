using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.FileHandlers3D;
using Game.Math_WPF.WPF.Viewers;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers.EdgeDetect3D
{
    public partial class EdgeDetection3D : Window
    {
        #region Declaration Section

        private readonly DropShadowEffect _errorEffect;

        private string _filename = null;

        #endregion

        #region Constructor

        public EdgeDetection3D()
        {
            InitializeComponent();

            _errorEffect = new DropShadowEffect()
            {
                Color = UtilityWPF.ColorFromHex("C02020"),
                Direction = 0,
                ShadowDepth = 0,
                BlurRadius = 8,
                Opacity = .8,
            };

            txtObjFile.Effect = _errorEffect;       // textchange sets this, but the first time must be happening before window load event
        }

        #endregion

        #region Event Listeners

        private void txtObjFile_PreviewDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }
        private void txtObjFile_Drop(object sender, DragEventArgs e)
        {
            try
            {
                string[] filenames = e.Data.GetData(DataFormats.FileDrop) as string[];

                if (filenames == null || filenames.Length == 0)
                {
                    MessageBox.Show("No files selected", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (filenames.Length > 1)
                {
                    MessageBox.Show("Only one file allowed", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                txtObjFile.Text = filenames[0];
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void txtObjFile_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (txtObjFile.Text.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                {
                    if (_filename != null && _filename.EndsWith(txtObjFile.Text, StringComparison.OrdinalIgnoreCase))
                    {
                        // textbox is showing the filename portion without folder, ignore this
                    }
                    else if (LoadFile(txtObjFile.Text))
                    {
                        _filename = txtObjFile.Text;
                        lblObjFile.Visibility = Visibility.Collapsed;
                        txtObjFile.Effect = null;
                        txtObjFile.Text = System.IO.Path.GetFileName(txtObjFile.Text);
                        txtObjFile.ToolTip = _filename;
                    }
                    else
                    {
                        UnloadFile();
                        _filename = null;
                        lblObjFile.Visibility = Visibility.Visible;
                        txtObjFile.Effect = _errorEffect;
                        txtObjFile.ToolTip = null;
                    }
                }
                else
                {
                    UnloadFile();
                    _filename = null;
                    lblObjFile.Visibility = Visibility.Visible;
                    txtObjFile.Effect = _errorEffect;
                    txtObjFile.ToolTip = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FourPointFace_Convex_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Obj_Face_Point[] points =
                [
                    new Obj_Face_Point() { Vertex_Index = 0, Vertex = new Obj_Vertex() { Vertex = new Vector3D(0,0,0) }},
                    new Obj_Face_Point() { Vertex_Index = 1, Vertex = new Obj_Vertex() { Vertex = new Vector3D(1,0,0) }},
                    new Obj_Face_Point() { Vertex_Index = 2, Vertex = new Obj_Vertex() { Vertex = new Vector3D(1,1,0) }},
                    new Obj_Face_Point() { Vertex_Index = 3, Vertex = new Obj_Vertex() { Vertex = new Vector3D(0,1,0) }},
                ];

                var window = new Debug3DWindow()
                {
                    Title = "convex 4 point poly",
                };

                var sizes = Debug3DWindow.GetDrawSizes(2);

                for (int i = 0; i < points.Length; i++)
                {
                    window.AddText3D(i.ToString(), new Point3D(points[i].Vertex.Vertex.X, points[i].Vertex.Vertex.Y, -0.2), new Vector3D(0, 0, 1), 0.2, Colors.Black, false);
                    window.AddDot(points[i].Vertex.Vertex.ToPoint(), sizes.dot, Colors.Black);
                }

                window.AddLine(points[0].Vertex.Vertex.ToPoint(), points[1].Vertex.Vertex.ToPoint(), sizes.line, Colors.White);
                window.AddLine(points[1].Vertex.Vertex.ToPoint(), points[2].Vertex.Vertex.ToPoint(), sizes.line, Colors.White);
                window.AddLine(points[2].Vertex.Vertex.ToPoint(), points[3].Vertex.Vertex.ToPoint(), sizes.line, Colors.White);
                window.AddLine(points[3].Vertex.Vertex.ToPoint(), points[0].Vertex.Vertex.ToPoint(), sizes.line, Colors.White);

                window.AddLine(points[1].Vertex.Vertex.ToPoint(), points[3].Vertex.Vertex.ToPoint(), sizes.line, Colors.Yellow);

                var triangle1 = new Triangle_wpf(points[0].Vertex.Vertex.ToPoint(), points[1].Vertex.Vertex.ToPoint(), points[3].Vertex.Vertex.ToPoint());
                var triangle2 = new Triangle_wpf(points[1].Vertex.Vertex.ToPoint(), points[2].Vertex.Vertex.ToPoint(), points[3].Vertex.Vertex.ToPoint());

                window.AddLine(triangle1.GetCenterPoint(), triangle1.GetCenterPoint() + triangle1.NormalUnit, sizes.line, Colors.DarkOliveGreen);
                window.AddLine(triangle2.GetCenterPoint(), triangle2.GetCenterPoint() + triangle2.NormalUnit, sizes.line, Colors.DarkOliveGreen);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void FourPointFace_Concave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Obj_Face_Point[] points =
                [
                    new Obj_Face_Point() { Vertex_Index = 0, Vertex = new Obj_Vertex() { Vertex = new Vector3D(0,0,0) }},
                    new Obj_Face_Point() { Vertex_Index = 1, Vertex = new Obj_Vertex() { Vertex = new Vector3D(0.2,0.8,0) }},
                    new Obj_Face_Point() { Vertex_Index = 2, Vertex = new Obj_Vertex() { Vertex = new Vector3D(1,1,0) }},
                    new Obj_Face_Point() { Vertex_Index = 3, Vertex = new Obj_Vertex() { Vertex = new Vector3D(0,1,0) }},
                ];

                var window = new Debug3DWindow()
                {
                    Title = "concave 4 point poly",
                };

                var sizes = Debug3DWindow.GetDrawSizes(2);

                for (int i = 0; i < points.Length; i++)
                {
                    window.AddText3D(i.ToString(), new Point3D(points[i].Vertex.Vertex.X, points[i].Vertex.Vertex.Y, -0.2), new Vector3D(0, 0, 1), 0.2, Colors.Black, false);
                    window.AddDot(points[i].Vertex.Vertex.ToPoint(), sizes.dot, Colors.Black);
                }

                window.AddLine(points[0].Vertex.Vertex.ToPoint(), points[1].Vertex.Vertex.ToPoint(), sizes.line, Colors.White);
                window.AddLine(points[1].Vertex.Vertex.ToPoint(), points[2].Vertex.Vertex.ToPoint(), sizes.line, Colors.White);
                window.AddLine(points[2].Vertex.Vertex.ToPoint(), points[3].Vertex.Vertex.ToPoint(), sizes.line, Colors.White);
                window.AddLine(points[3].Vertex.Vertex.ToPoint(), points[0].Vertex.Vertex.ToPoint(), sizes.line, Colors.White);

                window.AddLine(points[1].Vertex.Vertex.ToPoint(), points[3].Vertex.Vertex.ToPoint(), sizes.line, Colors.Yellow);

                var triangle1 = new Triangle_wpf(points[0].Vertex.Vertex.ToPoint(), points[1].Vertex.Vertex.ToPoint(), points[3].Vertex.Vertex.ToPoint());
                var triangle2 = new Triangle_wpf(points[1].Vertex.Vertex.ToPoint(), points[2].Vertex.Vertex.ToPoint(), points[3].Vertex.Vertex.ToPoint());

                window.AddLine(triangle1.GetCenterPoint(), triangle1.GetCenterPoint() + triangle1.NormalUnit, sizes.line, Colors.DarkOliveGreen);
                window.AddLine(triangle2.GetCenterPoint(), triangle2.GetCenterPoint() + triangle2.NormalUnit, sizes.line, Colors.DarkOliveGreen);

                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private void UnloadFile()
        {

        }
        private bool LoadFile(string filename)
        {
            if (!File.Exists(filename))
                return false;


            var test = Obj_FileReaderWriter.ReadFile(filename);



            // next test would be to create a visual3D for each object in the file (make a helper class for that)









            return true;
        }

        #endregion
    }
}
