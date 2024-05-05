using System;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.WPF
{
    /// <summary>
    /// Class that convert to/from obj_ classes
    /// </summary>
    public static class Obj_Util
    {
        public static Model3D ToModel3D(Obj_Object obj)
        {
            //











            var geometry = new MeshGeometry3D();


            return new GeometryModel3D()
            {
                //Material = ,
                //BackMaterial = ,
                Geometry = geometry,
            };
        }

        private static Obj_Face_Point[][] FaceToTriangles(Obj_Face_Point[] points)
        {
            if (points.Length < 3)
                throw new ApplicationException();


            // faces with more than 3 points will need to be turned into sets of triangles
            //
            // worry about that when the time comes.  Worst case would be concave.  Actually, worst case would be intersecting lines
            //
            // handle 4 points in a hardcoded way (dot product of normals should only be negative if it's a bow tie).  it looks like
            // it might be common





            return null;


        }
    }
}
