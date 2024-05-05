using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.WPF.FileHandlers3D
{
    public record Obj_File
    {
        // Comments at the top of the file before any real elements are read
        public string[] HeaderComments { get; init; }

        public Obj_Object[] Objects { get; init; }
    }

    public record Obj_Object
    {
        public string Name { get; init; }
        public bool? SmoothShaded { get; init; }
        public Obj_Vertex[] Vertices { get; init; }
        public Vector[] TextureCoordinates { get; init; }
        public Vector3D[] VertexNormals { get; init; }
        public Obj_Face[] Faces { get; init; }

        // ignoring lines, parameter space vertices
    }

    public record Obj_Face
    {
        public Obj_Face_Point[] Points { get; init; }
    }

    public record Obj_Face_Point
    {
        // NOTE: these indices are how the .obj defines them.  One based, negatives are an offset from count

        public int Vertex_Index { get; init; }
        public Obj_Vertex Vertex { get; init; }

        public int? TextureCoordinate_Index { get; init; }
        public Vector? TextureCoordinate { get; init; }

        public int? VertexNormal_Index { get; init; }
        public Vector3D? VertexNormal { get; init; }
    }

    public record Obj_Vertex
    {
        public Vector3D Vertex { get; init; }
        public Color? Color { get; init; }
    }
}
