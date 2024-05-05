using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF.Viewers;
using System;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.WPF.FileHandlers3D
{
    /// <summary>
    /// Class that convert to/from obj_ classes
    /// </summary>
    public static class Obj_Util
    {
        public static Model3D ToModel3D(Obj_Object obj)
        {
            // Material
            var front_material = new MaterialGroup();
            front_material.Children.Add(new DiffuseMaterial(UtilityWPF.BrushFromHex("DDD")));
            front_material.Children.Add(new SpecularMaterial(UtilityWPF.BrushFromHex("18CDCF57"), 3));

            var back_material = new MaterialGroup();
            back_material.Children.Add(new DiffuseMaterial(UtilityWPF.BrushFromHex("333")));
            back_material.Children.Add(new SpecularMaterial(UtilityWPF.BrushFromHex("6048DB52"), 6));

            // Geometry
            var geometry = new MeshGeometry3D();

            foreach (var vertex in obj.Vertices)
                geometry.Positions.Add(vertex.Vertex.ToPoint());

            foreach (var face in obj.Faces)
            {
                foreach (var triangle in FaceToTriangles(face.Points))
                {
                    geometry.TriangleIndices.Add(GetZeroBasedIndex(triangle[0].Vertex_Index, obj.Vertices.Length));
                    geometry.TriangleIndices.Add(GetZeroBasedIndex(triangle[1].Vertex_Index, obj.Vertices.Length));
                    geometry.TriangleIndices.Add(GetZeroBasedIndex(triangle[2].Vertex_Index, obj.Vertices.Length));
                }
            }

            return new GeometryModel3D()
            {
                Material = front_material,
                BackMaterial = back_material,
                Geometry = geometry,
            };
        }

        #region Private Methods

        private static int GetZeroBasedIndex(int index, int vertex_count)
        {
            return index < 0 ?
                vertex_count + index :       // negative values are backward from end of list.  -1 is last element of list (so count - 1)
                index - 1;      // file is 1 based, c# is 0 based
        }

        private static Obj_Face_Point[][] FaceToTriangles(Obj_Face_Point[] points)
        {
            if (points.Length < 3)
                throw new ApplicationException();

            else if (points.Length == 3)
                return [points];

            else if (points.Length == 4)
                return
                [
                    [points[0], points[1], points[3]],
                    [points[1], points[2], points[3]],
                ];

            else
                throw new ApplicationException($"More than 4 points, need to implement this ({points.Length})");
        }

        private static void TestFourPointTriangulation()
        {
            Obj_Face_Point[] points =
            [
                new Obj_Face_Point() { Vertex_Index = 0, Vertex = new Obj_Vertex() { Vertex = new Vector3D(0,0,0) }},
                new Obj_Face_Point() { Vertex_Index = 1, Vertex = new Obj_Vertex() { Vertex = new Vector3D(1,0,0) }},
                new Obj_Face_Point() { Vertex_Index = 2, Vertex = new Obj_Vertex() { Vertex = new Vector3D(1,1,0) }},
                new Obj_Face_Point() { Vertex_Index = 3, Vertex = new Obj_Vertex() { Vertex = new Vector3D(0,1,0) }},
            ];

            TestFourPointTriangulation_Draw(points, "convex 4 point poly");


            points =
            [
                new Obj_Face_Point() { Vertex_Index = 0, Vertex = new Obj_Vertex() { Vertex = new Vector3D(0,0,0) }},
                new Obj_Face_Point() { Vertex_Index = 1, Vertex = new Obj_Vertex() { Vertex = new Vector3D(0.2,0.8,0) }},
                new Obj_Face_Point() { Vertex_Index = 2, Vertex = new Obj_Vertex() { Vertex = new Vector3D(1,1,0) }},
                new Obj_Face_Point() { Vertex_Index = 3, Vertex = new Obj_Vertex() { Vertex = new Vector3D(0,1,0) }},
            ];

            TestFourPointTriangulation_Draw(points, "concave 4 point poly");
        }
        private static void TestFourPointTriangulation_Draw(Obj_Face_Point[] points, string description)
        {
            var window = new Debug3DWindow()
            {
                Title = description,
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

        #endregion
    }
}
