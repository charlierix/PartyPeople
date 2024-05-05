using Game.Core;
using Game.Math_WPF.WPF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml.Linq;

namespace Game.Bepu.Testers.EdgeDetect3D
{
    // TODO: Move this to Math_WPF

    /// <summary>
    /// This parses an .obj file
    /// </summary>
    public static class ObjReader
    {
        private const string DEFAULT_NAME = "object";

        public static Obj_File ReadFile(string filename)
        {
            var comments = new List<string>();
            var objects = new List<Obj_Object>();

            // These are for the current object
            string name = null;
            var vertices = new List<Obj_Vertex>();
            var texture_coords = new List<Vector>();
            var vertex_normals = new List<Vector3D>();
            var faces = new List<Obj_Face>();

            using (var reader = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith('#'))
                    {
                        if (objects.Count == 0 && !IsDirty(name, vertices, texture_coords, vertex_normals, faces))
                        {
                            Match match = Regex.Match(line, @"^#+\s*(?<remainder>.*)");
                            if (match.Success && match.Groups["remainder"].Length > 0)
                                comments.Add(match.Groups["remainder"].Value);
                        }
                        continue;
                    }

                    MatchCollection matches = Regex.Matches(line, @"[^\s]+");
                    if (matches.Count < 2)      // there should always be at least a qualifier and some value
                        continue;

                    switch (matches[0].Value.ToLower())
                    {
                        case "o":
                            PossiblyAddExistingObject(objects, ref name, vertices, texture_coords, vertex_normals, faces);
                            name = ParseObject(matches, line);
                            break;

                        case "v":
                            vertices.Add(ParseVertex(matches, line));
                            break;

                        case "vt":
                            texture_coords.Add(ParseTextureCoord(matches, line));
                            break;

                        case "vn":
                            vertex_normals.Add(ParseVertexNormal(matches, line));
                            break;

                        case "f":
                            faces.Add(ParseFace(matches, line));
                            break;

                        default:
                            continue;
                    }
                }
            }

            PossiblyAddExistingObject(objects, ref name, vertices, texture_coords, vertex_normals, faces);

            return new Obj_File()
            {
                HeaderComments = comments.ToArray(),
                Objects = objects.ToArray(),
            };
        }

        private static bool IsDirty(string name, List<Obj_Vertex> vertices, List<Vector> texture_coords, List<Vector3D> vertex_normals, List<Obj_Face> faces)
        {
            return name != null || vertices.Count > 0 || texture_coords.Count > 0 || vertex_normals.Count > 0 || faces.Count > 0;
        }
        private static void Clear(ref string name, List<Obj_Vertex> vertices, List<Vector> texture_coords, List<Vector3D> vertex_normals, List<Obj_Face> faces)
        {
            name = null;
            vertices.Clear();
            texture_coords.Clear();
            vertex_normals.Clear();
            faces.Clear();
        }

        private static void PossiblyAddExistingObject(List<Obj_Object> objects, ref string name, List<Obj_Vertex> vertices, List<Vector> texture_coords, List<Vector3D> vertex_normals, List<Obj_Face> faces)
        {
            if (!IsDirty(name, vertices, texture_coords, vertex_normals, faces))
                return;

            objects.Add(FinishObject(name, vertices, texture_coords, vertex_normals, faces));

            Clear(ref name, vertices, texture_coords, vertex_normals, faces);
        }

        private static string ParseObject(MatchCollection matches, string line)
        {
            // o name

            if (matches.Count == 1)
                return DEFAULT_NAME;

            else if (matches.Count == 2)
                return matches[1].Value;        // there are no spaces in the name, so return the match

            // There are spaces in the name.  Return everything to the right of the qualifier
            Match match = Regex.Match(line, @"^\s*[^\s]+\s+(?<name>.+)\s*$");

            if (!match.Success)
                return DEFAULT_NAME;        // this should never happen

            return match.Groups["name"].Value;
        }

        private static Obj_Vertex ParseVertex(MatchCollection matches, string line)
        {
            // v x y z [w] r g b

            if (!matches.Count.In(4, 5, 7, 8))
                throw new ApplicationException($"Invalid vertex line\r\n{line}");

            if (!double.TryParse(matches[1].Value, out double x))
                throw new ApplicationException($"Couldn't parse x: {matches[1].Value}\r\n{line}");

            if (!double.TryParse(matches[2].Value, out double y))
                throw new ApplicationException($"Couldn't parse y: {matches[2].Value}\r\n{line}");

            if (!double.TryParse(matches[3].Value, out double z))
                throw new ApplicationException($"Couldn't parse z: {matches[3].Value}\r\n{line}");

            Color? color = null;
            if (matches.Count.In(7, 8))
            {
                int offset = matches.Count == 7 ? 0 : 1;

                if (!double.TryParse(matches[4 + offset].Value, out double r))
                    throw new ApplicationException($"Couldn't parse r: {matches[4 + offset].Value}\r\n{line}");

                if (!double.TryParse(matches[5 + offset].Value, out double g))
                    throw new ApplicationException($"Couldn't parse g: {matches[5 + offset].Value}\r\n{line}");

                if (!double.TryParse(matches[6 + offset].Value, out double b))
                    throw new ApplicationException($"Couldn't parse b: {matches[6 + offset].Value}\r\n{line}");

                color = UtilityWPF.ColorFromPercents(r, g, b);
            }

            return new Obj_Vertex()
            {
                Vertex = new Vector3D(x, y, z),
                Color = color,
            };
        }

        private static Vector ParseTextureCoord(MatchCollection matches, string line)
        {
            // vt u [v] [w]

            if (!matches.Count.In(2, 3, 4))
                throw new ApplicationException($"Invalid vertex texture line\r\n{line}");

            if (!double.TryParse(matches[1].Value, out double u))
                throw new ApplicationException($"Couldn't parse u: {matches[1].Value}\r\n{line}");

            double v = 0;
            if (matches.Count >= 3)
                if (!double.TryParse(matches[2].Value, out v))
                    throw new ApplicationException($"Couldn't parse v: {matches[2].Value}\r\n{line}");

            // ignoring w (not sure why 3D coords would be used)

            return new Vector(u, v);
        }

        private static Vector3D ParseVertexNormal(MatchCollection matches, string line)
        {
            // vn x y z

            if (matches.Count != 4)
                throw new ApplicationException($"Invalid vertex normal line\r\n{line}");

            if (!double.TryParse(matches[1].Value, out double x))
                throw new ApplicationException($"Couldn't parse x: {matches[1].Value}\r\n{line}");

            if (!double.TryParse(matches[2].Value, out double y))
                throw new ApplicationException($"Couldn't parse y: {matches[2].Value}\r\n{line}");

            if (!double.TryParse(matches[3].Value, out double z))
                throw new ApplicationException($"Couldn't parse z: {matches[3].Value}\r\n{line}");

            return new Vector3D(x, y, z);
        }

        private static Obj_Face ParseFace(MatchCollection matches, string line)
        {
            // f v1 v2 v3 ....

            if (matches.Count < 4)
                throw new ApplicationException($"Invalid face line\r\n{line}");

            var points = new List<Obj_Face_Point>();

            for (int i = 1; i < matches.Count; i++)
                points.Add(ParseFace_Point(matches[i].Value, line));

            return new Obj_Face()
            {
                Points = points.ToArray(),
            };
        }
        private static Obj_Face_Point ParseFace_Point(string point_text, string line)
        {
            // v
            // v/vt
            // v//vn
            // v/vt/vn

            string[] split = point_text.Split('/');

            if (!int.TryParse(split[0], out int vertex))
                throw new ApplicationException($"Couldn't parse face's vertex index: {split[0]}\r\n{line}");

            int? texture = null;
            if (split.Length > 1 && split[1] != "")
                if (int.TryParse(split[1], out int texture_parsed))
                    texture = texture_parsed;
                else
                    throw new ApplicationException($"Couldn't parse face's texture coordinate index: {split[1]}\r\n{line}");

            int? normal = null;
            if (split.Length > 2 && split[2] != "")
                if (int.TryParse(split[2], out int normal_parsed))
                    normal = normal_parsed;
                else
                    throw new ApplicationException($"Couldn't parse face's normal index: {split[2]}\r\n{line}");

            return new Obj_Face_Point()
            {
                Vertex_Index = vertex,
                TextureCoordinate_Index = texture,
                VertexNormal_Index = normal,
            };
        }

        private static Obj_Object FinishObject(string name, List<Obj_Vertex> vertices, List<Vector> texture_coords, List<Vector3D> vertex_normals, List<Obj_Face> faces)
        {
            return new Obj_Object()
            {
                Name = name ?? DEFAULT_NAME,
                Vertices = vertices.ToArray(),
                TextureCoordinates = texture_coords.ToArray(),
                VertexNormals = vertex_normals.ToArray(),

                // Faces passed in only have index populated.  Find the actual item
                // Waiting until now to find the items in case the file comes in out of order (face lines before vertex lines)
                Faces = faces.
                    AsParallel().
                    Select(o => o with
                    {
                        Points = o.Points.
                            Select(p => p with
                            {
                                Vertex = FindItem(vertices, p.Vertex_Index, "face pointing to vertex"),
                                TextureCoordinate = p.TextureCoordinate_Index == null ? null : FindItem(texture_coords, p.TextureCoordinate_Index.Value, "face pointing to texture coordinate"),
                                VertexNormal = p.VertexNormal_Index == null ? null : FindItem(vertex_normals, p.VertexNormal_Index.Value, "face pointing to vertex normal"),
                            }).
                            ToArray(),
                    }).
                    ToArray(),
            };
        }

        private static T FindItem<T>(List<T> items, int index, string description)
        {
            int actual_index = index < 0 ?
                items.Count + index :       // negative values are backward from end of list.  -1 is last element of list (so count - 1)
                index - 1;      // file is 1 based, c# is 0 based

            if (actual_index < 0 || actual_index >= items.Count)
                throw new IndexOutOfRangeException($"Invalid index ({actual_index}) for {description}.  Index from file: {index}.  Count: {items.Count}");

            return items[actual_index];
        }
    }

    public record Obj_File
    {
        // Comments at the top of the file before any real elements are read
        public string[] HeaderComments { get; init; }

        public Obj_Object[] Objects { get; init; }
    }

    public record Obj_Object
    {
        public string Name { get; init; }
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
