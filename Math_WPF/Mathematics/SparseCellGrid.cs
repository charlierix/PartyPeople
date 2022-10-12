using Accord.Math;
using Game.Core;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.Mathematics
{
    /// <summary>
    /// This divides the world into an infinite set of cells
    /// </summary>
    /// <remarks>
    /// Only the cells that are used are stored, but the class supports querying any point and treats that as a cell
    /// </remarks>
    public class SparseCellGrid
    {
        #region record: CubeFace

        private record CubeFace
        {
            public ITriangleIndexed_wpf[] Triangles { get; init; }
            public long Token { get; init; }
        }

        #endregion
        #region record: IndicesByCubeFace

        private record IndicesByCubeFace
        {
            public CubeFace Face { get; init; }
            public VectorInt3[] Indices { get; init; }
        }

        #endregion

        #region record: MarkResult

        public record MarkResult
        {
            public VectorInt3 AABB_Min { get; init; }
            public VectorInt3 AABB_Max { get; init; }

            //NOTE: this only gets populated if the funtion was told to process immediately
            public VectorInt3[] MarkedCells { get; init; }
        }

        #endregion

        #region Declaration Section

        private readonly double _cell_size;
        public double CellSize => _cell_size;

        private readonly double _cell_half;

        private readonly List<VectorInt3> _marked = new List<VectorInt3>();

        #endregion

        #region Constructor

        public SparseCellGrid(double cell_size)
        {
            _cell_size = cell_size;
            _cell_half = cell_size / 2;
        }

        #endregion

        #region Public Methods

        public VectorInt3 GetIndex_Point(Point3D point)
        {
            return new VectorInt3
            (
                ((point.X - _cell_half) / _cell_size).ToInt_Ceiling(),
                ((point.Y - _cell_half) / _cell_size).ToInt_Ceiling(),
                ((point.Z - _cell_half) / _cell_size).ToInt_Ceiling()
            );
        }
        public VectorInt3[] GetIndices_Triangle(ITriangle_wpf triangle, bool show_debug = false)
        {
            var aabb = Math3D.GetAABB(triangle);

            // Get index of each corner of aabb
            VectorInt3[] corner_indices = Polytopes.GetCubePoints(aabb.min, aabb.max).
                Select(o => GetIndex_Point(o)).
                ToArray();

            // If all three vertices have the same index, then the entire triangle is inside a single cell
            if (corner_indices.Skip(1).All(o => o.Equals(corner_indices[0])))
            {
                if (show_debug)
                    ShowDebug_IndicesTriangle(triangle, aabb, corner_indices, null, true);

                return new[] { corner_indices[0] };
            }

            VectorInt3[,,] indices = GetIndexBlock(corner_indices);

            // Go through all the cells and get the faces that sit betwen the cells
            var face_stripes = GetCubeFaceStripes_ForAABBIndices(indices);

            // To map from indices to this array, take the index - marked_offset
            bool[,,] marked = new bool[indices.GetUpperBound(0) + 1, indices.GetUpperBound(1) + 1, indices.GetUpperBound(2) + 1];
            VectorInt3 marked_offset = new VectorInt3
            (
                corner_indices.Min(o => o.X),
                corner_indices.Min(o => o.Y),
                corner_indices.Min(o => o.Z)
            );

            //FillMarked(marked, marked_offset, _marked);       // this would be a nice optimization, but would polute the return array with existing marked cells

            // Intersect the cube's faces with triangle (this won't detect a triangle completely inside a singe cell, but that check was accounted for earlier)
            foreach (var stripe in face_stripes)
            {
                WalkStripe(triangle, stripe, marked, marked_offset, true);
                WalkStripe(triangle, stripe, marked, marked_offset, false);
            }

            VectorInt3[] marked_cells = GetMarkedCells(marked, marked_offset);

            // Store the unique matches
            _marked.AddRange(marked_cells.Except(_marked));

            if (show_debug)
                ShowDebug_IndicesTriangle(triangle, aabb, corner_indices, marked_cells, false);

            return marked_cells;
        }
        public VectorInt3[,] GetIndices_Rect2D(Rect rect, double z)
        {
            throw new ApplicationException("finish this");
        }

        //NOTE: these don't need to go to the expense of marking cells unless a cell within its aabb is requested
        //So calculate the index aabb for the triangle, and store that in a to work list
        public MarkResult Mark_Point(Point3D point, bool return_marked_cells = false)
        {
            throw new ApplicationException("finish this");
        }
        public MarkResult Mark_Triangle(ITriangle_wpf triangle, bool return_marked_cells = false)
        {
            throw new ApplicationException("finish this");
        }
        public MarkResult Mark_Rect2D(Rect rect, double z, bool return_marked_cells = false)
        {
            throw new ApplicationException("finish this");
        }

        public VectorInt3[] GetMarked()
        {
            throw new ApplicationException("finish this");
        }

        public (Rect3D rect, Point3D center) GetCell(VectorInt3 index)
        {
            Point3D center = new Point3D(index.X * _cell_size, index.Y * _cell_size, index.Z * _cell_size);

            return
            (
                new Rect3D(center.X - _cell_half, center.Y - _cell_half, center.Z - _cell_half, _cell_size, _cell_size, _cell_size),
                center
            );
        }

        /// <summary>
        /// Useful for drawing the cells as lines
        /// </summary>
        /// <remarks>
        /// When there are cells touching each other, this will merge the line segmets so it's as cheap as
        /// possible to draw
        /// </remarks>
        public ((int i1, int i2)[] index_pairs, Point3D[] all_points_distinct) GetDistinctLineSegments(VectorInt3[] indices)
        {
            var cells = indices.
                Select(o => GetCell(o)).
                ToArray();

            var lines = cells.
                SelectMany(o => Polytopes.GetCubeLines(o.rect.Location, o.rect.Location + o.rect.Size.ToVector()));

            var lines_distinct = Math3D.GetDistinctLineSegments(lines);

            var lines_x = FilterSegmentsAlong(lines_distinct.index_pairs, lines_distinct.all_points_distinct, new Vector3D(1, 0, 0));
            var lines_y = FilterSegmentsAlong(lines_distinct.index_pairs, lines_distinct.all_points_distinct, new Vector3D(0, 1, 0));
            var lines_z = FilterSegmentsAlong(lines_distinct.index_pairs, lines_distinct.all_points_distinct, new Vector3D(0, 0, 1));

            lines_x = ReduceChains(lines_x);
            lines_y = ReduceChains(lines_y);
            lines_z = ReduceChains(lines_z);

            var combined = lines_x.
                Concat(lines_y).
                Concat(lines_z).
                ToArray();

            return GetCondensedPoints(combined, lines_distinct.all_points_distinct);
        }

        #endregion

        #region Private Methods - line segments

        /// <summary>
        /// Returns the subset of line segments that are along the vector passed in
        /// </summary>
        private static (int i1, int i2)[] FilterSegmentsAlong((int i1, int i2)[] index_pairs, Point3D[] points, Vector3D along)
        {
            return index_pairs.
                //Where(o => Math.Abs(Vector3D.DotProduct(points[o.i2] - points[o.i1], along)).IsNearValue(cell_size)).     // this requires all line segments to be the length of cell_size, as well as along being the length of cell_size
                Where(o => !Math.Abs(Vector3D.DotProduct(points[o.i2] - points[o.i1], along)).IsNearZero()).        // this only works because the line segments are orthogonal
                ToArray();
        }

        /// <summary>
        /// (1,2) (2,3) -> (1,3)
        /// (1,2) (3,2) -> (1,3)
        /// (1,2) (2,3) (3,4) -> (1,4)
        /// </summary>
        private static (int i1, int i2)[] ReduceChains((int i1, int i2)[] pairs)
        {
            // This is a copy of a portion of UtilityCore.GetChains

            var retVal = pairs.
                ToList();

            // Keep trying to merge the pairs until no more merges are possible
            while (true)
            {
                if (retVal.Count == 1)
                    break;

                bool hadJoin = false;

                for (int outer = 0; outer < retVal.Count - 1; outer++)
                {
                    for (int inner = outer + 1; inner < retVal.Count; inner++)
                    {
                        if (ReduceChains_TryMerge(retVal[outer], retVal[inner], out var pair_new))
                        {
                            retVal.RemoveAt(inner);
                            retVal.RemoveAt(outer);

                            retVal.Add(pair_new);

                            hadJoin = true;
                            break;
                        }
                    }

                    if (hadJoin)
                        break;
                }

                if (!hadJoin)
                    break;        // compared all the mini chains, and there were no merges.  Quit looking
            }

            return retVal.ToArray();
        }
        private static bool ReduceChains_TryMerge((int i1, int i2) p1, (int i1, int i2) p2, out (int i1, int i2) pair_new)
        {
            if (p1.i1 == p2.i1)
            {
                pair_new = (p1.i2, p2.i2);
                return true;
            }

            if (p1.i1 == p2.i2)
            {
                pair_new = (p1.i2, p2.i1);
                return true;
            }

            if (p1.i2 == p2.i1)
            {
                pair_new = (p1.i1, p2.i2);
                return true;
            }

            if (p1.i2 == p2.i2)
            {
                pair_new = (p1.i1, p2.i1);
                return true;
            }

            pair_new = (-1, -1);
            return false;
        }

        /// <summary>
        /// Only returns the points that are referenced.  Remaps the segments to accurately point to the reduced list of points
        /// </summary>
        private static ((int i1, int i2)[] segments, Point3D[] all_points) GetCondensedPoints((int i1, int i2)[] segments, Point3D[] all_points)
        {
            // This is a copy of TriangleIndexed_wpf.GetCondensedPointMap

            var used = segments.
                SelectMany(o => new[] { o.i1, o.i2 }).
                Distinct().
                OrderBy(o => o).
                ToArray();

            //var unused = Enumerable.Range(0, all_points.Length).
            //    Except(used).
            //    OrderBy(o => o).
            //    ToArray();

            //if(unused.Length == 0)
            if (used.Length == all_points.Length)
                return (segments, all_points);

            Point3D[] used_points = used.
                Select(o => all_points[o]).
                ToArray();

            var old_new = new SortedList<int, int>();
            for (int i = 0; i < used.Length; i++)
            {
                old_new.Add(used[i], i);
            }

            var segments_remapped = segments.
                Select(o => (old_new[o.i1], old_new[o.i2])).
                ToArray();

            return (segments_remapped, used_points);
        }

        #endregion
        #region Private Methods - triangle indices

        /// <summary>
        /// This finds the min/max and returns a 3D block of indices
        /// </summary>
        private static VectorInt3[,,] GetIndexBlock(VectorInt3[] indices)
        {
            VectorInt3 min = new VectorInt3
            (
                indices.Min(o => o.X),
                indices.Min(o => o.Y),
                indices.Min(o => o.Z)
            );

            VectorInt3 max = new VectorInt3
            (
                indices.Max(o => o.X),
                indices.Max(o => o.Y),
                indices.Max(o => o.Z)
            );

            var retVal = new VectorInt3[max.X - min.X + 1, max.Y - min.Y + 1, max.Z - min.Z + 1];

            for (int x = 0; x < retVal.GetLength(0); x++)
            {
                for (int y = 0; y < retVal.GetLength(1); y++)
                {
                    for (int z = 0; z < retVal.GetLength(2); z++)
                    {
                        retVal[x, y, z] = new VectorInt3(min.X + x, min.Y + y, min.Z + z);
                    }
                }
            }

            return retVal;
        }

        private IndicesByCubeFace[][] GetCubeFaceStripes_ForAABBIndices(VectorInt3[,,] indices)
        {
            // X
            AxisFor axis_primary = new AxisFor(Axis.X, 0, indices.GetUpperBound(0));
            AxisFor axis_secondary1 = new AxisFor(Axis.Y, 0, indices.GetUpperBound(1));
            AxisFor axis_secondary2 = new AxisFor(Axis.Z, 0, indices.GetUpperBound(2));

            var faces_x = GetCubeFaces_Axis(indices, axis_primary, axis_secondary1, axis_secondary2);       // this marches along x, returning yz faces

            // Y
            axis_primary = new AxisFor(Axis.Y, 0, indices.GetUpperBound(1));
            axis_secondary1 = new AxisFor(Axis.X, 0, indices.GetUpperBound(0));
            axis_secondary2 = new AxisFor(Axis.Z, 0, indices.GetUpperBound(2));

            var faces_y = GetCubeFaces_Axis(indices, axis_primary, axis_secondary1, axis_secondary2);

            // Z
            axis_primary = new AxisFor(Axis.Z, 0, indices.GetUpperBound(2));
            axis_secondary1 = new AxisFor(Axis.X, 0, indices.GetUpperBound(0));
            axis_secondary2 = new AxisFor(Axis.Y, 0, indices.GetUpperBound(1));

            var faces_z = GetCubeFaces_Axis(indices, axis_primary, axis_secondary1, axis_secondary2);

            return faces_x.
                Concat(faces_y).
                Concat(faces_z).
                ToArray();
        }

        private IndicesByCubeFace[][] GetCubeFaces_Axis(VectorInt3[,,] indices, AxisFor axis_primary, AxisFor axis_secondary1, AxisFor axis_secondary2)
        {
            var retVal = new List<IndicesByCubeFace[]>();

            foreach (int sec1 in axis_secondary1.Iterate())
            {
                foreach (int sec2 in axis_secondary2.Iterate())
                {
                    var stripe = new List<IndicesByCubeFace>();

                    // Face to the left of the first item
                    GetCubeFaces_Axis_Initial(stripe, indices, axis_primary, axis_secondary1, axis_secondary2, sec1, sec2);

                    // Faces for the rest of the cells
                    foreach (int prim in axis_primary.Iterate())
                    {
                        GetCubeFaces_Axis_Standard(stripe, indices, axis_primary, axis_secondary1, axis_secondary2, prim, sec1, sec2);
                    }

                    retVal.Add(stripe.ToArray());
                }
            }

            return retVal.ToArray();
        }
        private void GetCubeFaces_Axis_Initial(List<IndicesByCubeFace> retVal, VectorInt3[,,] indices, AxisFor axis_primary, AxisFor axis_secondary1, AxisFor axis_secondary2, int sec1, int sec2)
        {
            int ix = 0, iy = 0, iz = 0;
            axis_primary.Set3DValue(ref ix, ref iy, ref iz, 0);
            axis_secondary1.Set3DValue(ref ix, ref iy, ref iz, sec1);
            axis_secondary2.Set3DValue(ref ix, ref iy, ref iz, sec2);

            int offset_x = 0, offset_y = 0, offset_z = 0;
            axis_primary.Set3DValue(ref offset_x, ref offset_y, ref offset_z, -1);
            axis_secondary1.Set3DValue(ref offset_x, ref offset_y, ref offset_z, 0);
            axis_secondary2.Set3DValue(ref offset_x, ref offset_y, ref offset_z, 0);

            var index = new VectorInt3(indices[ix, iy, iz].X + offset_x, indices[ix, iy, iz].Y + offset_y, indices[ix, iy, iz].Z + offset_z);

            CubeFace face_pre = GetCubeFace_Max(index, axis_primary, axis_secondary1, axis_secondary2);

            retVal.Add(new IndicesByCubeFace()
            {
                Face = face_pre,
                Indices = new[] { indices[ix, iy, iz] },
            });
        }
        private void GetCubeFaces_Axis_Standard(List<IndicesByCubeFace> retVal, VectorInt3[,,] indices, AxisFor axis_primary, AxisFor axis_secondary1, AxisFor axis_secondary2, int prim, int sec1, int sec2)
        {
            int ix = 0, iy = 0, iz = 0;
            axis_primary.Set3DValue(ref ix, ref iy, ref iz, prim);
            axis_secondary1.Set3DValue(ref ix, ref iy, ref iz, sec1);
            axis_secondary2.Set3DValue(ref ix, ref iy, ref iz, sec2);

            VectorInt3 index = new VectorInt3(indices[ix, iy, iz].X, indices[ix, iy, iz].Y, indices[ix, iy, iz].Z);

            CubeFace face = GetCubeFace_Max(index, axis_primary, axis_secondary1, axis_secondary2);

            var buffer = new List<VectorInt3>();
            buffer.Add(indices[ix, iy, iz]);       // this sits between current and next cell

            if (prim < axis_primary.Stop)
            {
                int offset_x = 0, offset_y = 0, offset_z = 0;
                axis_primary.Set3DValue(ref offset_x, ref offset_y, ref offset_z, 1);
                axis_secondary1.Set3DValue(ref offset_x, ref offset_y, ref offset_z, 0);
                axis_secondary2.Set3DValue(ref offset_x, ref offset_y, ref offset_z, 0);

                buffer.Add(indices[ix + offset_x, iy + offset_y, iz + offset_z]);       // if this is the last cell, then don't add the one to the right (because that's one too far)
            }

            retVal.Add(new IndicesByCubeFace()
            {
                Face = face,
                Indices = buffer.ToArray(),
            });
        }

        private CubeFace GetCubeFace_Max(VectorInt3 index, AxisFor axis_primary, AxisFor axis_secondary1, AxisFor axis_secondary2)
        {
            Point3D[] points = new Point3D[4];

            double prim = axis_primary.GetValue(index) * _cell_size + _cell_half;

            double sec1_center = axis_secondary1.GetValue(index) * _cell_size;
            double sec2_center = axis_secondary2.GetValue(index) * _cell_size;

            double x = 0, y = 0, z = 0;

            // - -
            axis_primary.Set3DValue(ref x, ref y, ref z, prim);
            axis_secondary1.Set3DValue(ref x, ref y, ref z, sec1_center - _cell_half);
            axis_secondary2.Set3DValue(ref x, ref y, ref z, sec2_center - _cell_half);
            points[0] = new Point3D(x, y, z);

            // + -
            axis_primary.Set3DValue(ref x, ref y, ref z, prim);
            axis_secondary1.Set3DValue(ref x, ref y, ref z, sec1_center + _cell_half);
            axis_secondary2.Set3DValue(ref x, ref y, ref z, sec2_center - _cell_half);
            points[1] = new Point3D(x, y, z);

            // + +
            axis_primary.Set3DValue(ref x, ref y, ref z, prim);
            axis_secondary1.Set3DValue(ref x, ref y, ref z, sec1_center + _cell_half);
            axis_secondary2.Set3DValue(ref x, ref y, ref z, sec2_center + _cell_half);
            points[2] = new Point3D(x, y, z);

            // - +
            axis_primary.Set3DValue(ref x, ref y, ref z, prim);
            axis_secondary1.Set3DValue(ref x, ref y, ref z, sec1_center - _cell_half);
            axis_secondary2.Set3DValue(ref x, ref y, ref z, sec2_center + _cell_half);
            points[3] = new Point3D(x, y, z);

            return new CubeFace()
            {
                Token = TokenGenerator.NextToken(),
                Triangles = new[]
                {
                    new TriangleIndexed_wpf(0, 1, 2, points),
                    new TriangleIndexed_wpf(2, 3, 0, points),
                },
            };
        }

        private void WalkStripe(ITriangle_wpf triangle, IndicesByCubeFace[] stripe, bool[,,] marked, VectorInt3 offset, bool forward)
        {
            // Helper functions
            var is_already_marked = new Func<VectorInt3, bool>(vi => marked[vi.X - offset.X, vi.Y - offset.Y, vi.Z - offset.Z]);

            var mark_index = new Action<VectorInt3>(vi =>
            {
                marked[vi.X - offset.X, vi.Y - offset.Y, vi.Z - offset.Z] = true;
            });

            var all_already_marked = new Func<VectorInt3[], bool>(vis =>
            {
                for (int i = 0; i < vis.Length; i++)
                    if (!is_already_marked(vis[i]))
                        return false;

                return true;
            });

            var ensure_all_marked = new Action<VectorInt3[]>(vis =>
            {
                for (int i = 0; i < vis.Length; i++)
                    mark_index(vis[i]);
            });

            // Walk the stripe, starting in the middle of the array, out to 0 or len-1
            bool had_marked = false;

            int half = stripe.Length / 2;

            AxisFor iterator = forward ?
                new AxisFor(Axis.X, half, stripe.Length - 1) :      // the axis doesn't matter, just using this for the from/to
                new AxisFor(Axis.X, half - 1, 0);

            foreach (int i in iterator.Iterate())
            {
                //NOTE: Calling all_already_marked, because there's no point checking geometry collisions if the affected cells
                //are already considered to be touching

                if (all_already_marked(stripe[i].Indices) || WalkStripe_IsColliding(triangle, stripe[i].Face))
                {
                    ensure_all_marked(stripe[i].Indices);
                    had_marked = true;
                }
                else if (had_marked)
                {
                    // Walking a stripe is like marching a ray.  Triangle is a convex polygon.
                    // So once going from inside the polygon to outside, the rest of the ray is guaranteed to not collide
                    break;
                }
            }
        }
        private static bool WalkStripe_IsColliding(ITriangle_wpf triangle, CubeFace face)
        {
            for (int i = 0; i < face.Triangles.Length; i++)
            {
                var points = Math3D.GetIntersection_Triangle_Triangle(triangle, face.Triangles[i]);
                if (points != null)
                    return true;
            }

            return false;
        }

        private static VectorInt3[] GetMarkedCells(bool[,,] marked, VectorInt3 offset)
        {
            var retVal = new List<VectorInt3>();

            for (int x = 0; x <= marked.GetUpperBound(0); x++)
            {
                for (int y = 0; y <= marked.GetUpperBound(1); y++)
                {
                    for (int z = 0; z <= marked.GetUpperBound(2); z++)
                    {
                        if (marked[x, y, z])
                            retVal.Add(new VectorInt3(x + offset.X, y + offset.Y, z + offset.Z));
                    }
                }
            }

            return retVal.ToArray();
        }

        private void ShowDebug_IndicesTriangle(ITriangle_wpf triangle, (Point3D min, Point3D max) aabb, VectorInt3[] corner_indices, VectorInt3[] marked_cells, bool is_single_cell)
        {
            var window = new Debug3DWindow()
            {
                Title = "Get Indices - Triangle",
            };

            window.AddTriangle(triangle, Colors.DarkKhaki);

            var sizes = Debug3DWindow.GetDrawSizes((aabb.max - aabb.min).Length / 2);

            window.AddLines(Polytopes.GetCubeLines(aabb.min, aabb.max), sizes.line * 2, Colors.DarkGray);

            var line_segments = GetDistinctLineSegments(corner_indices);
            window.AddLines(line_segments.index_pairs, line_segments.all_points_distinct, sizes.line / 2, Colors.White);

            if (is_single_cell)
            {
                window.Background = Brushes.Honeydew;
                window.Show();
                return;
            }

            line_segments = GetDistinctLineSegments(marked_cells);
            window.AddLines(line_segments.index_pairs, line_segments.all_points_distinct, sizes.line, UtilityWPF.ColorFromHex("7585BD"));
            window.Show();
        }

        #endregion
    }
}
