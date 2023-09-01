using Accord.Math;
using Game.Core;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using Octree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.Mathematics
{
    //TODO: Most usage of this class will be GetMarked_Sphere, so store in a structure that's efficient (probably octree)
    //https://github.com/mcserep/NetOctree

    /// <summary>
    /// This divides the world into an infinite set of cells.  Add shapes, then query for cells that touched one of those
    /// shapes
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
        #region record: GeometryBounds

        private record GeometryBounds
        {
            public Point3D aabb_min_3D { get; init; }
            public Point3D aabb_max_3D { get; init; }

            public VectorInt3[] corner_indices { get; init; }

            public VectorInt3 aabb_min { get; init; }
            public VectorInt3 aabb_max { get; init; }
        }

        #endregion
        #region record: TriangleToProcess

        private record TriangleToProcess
        {
            public string key { get; init; }
            public GeometryBounds bounds { get; init; }
            public ITriangle_wpf triangle { get; init; }
        }

        #endregion
        #region record: MarkedStorage

        private record MarkedStorage
        {
            public List<VectorInt3> List { get; init; }
            public PointOctree<VectorInt3> Octree_Points { get; init; }
            public BoundsOctree<VectorInt3> Octree_Cells { get; init; }
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

        private const string NULL_KEY = "SparseCellGrid NULL 7710b37a-4291-4ee0-bde7-7907dbf42520";

        private readonly double _cell_size;
        public double CellSize => _cell_size;

        private readonly double _cell_half;

        private readonly bool _supportsearch_sphere;
        private readonly bool _supportsearch_aabb;

        private readonly Dictionary<string, MarkedStorage> _marked = new Dictionary<string, MarkedStorage>();

        private readonly List<TriangleToProcess> _pending_triangles = new List<TriangleToProcess>();

        #endregion

        #region Constructor

        /// <param name="supportsearch_sphere">Required if using GetMarked_Sphere (cell centers will also be stored in an octree)</param>
        /// <param name="supportsearch_aabb">Required if using GetMarked_AABB (cells will also be stored in an octree)</param>
        public SparseCellGrid(double cell_size, bool supportsearch_sphere = false, bool supportsearch_aabb = false)
        {
            _cell_size = cell_size;
            _cell_half = cell_size / 2;

            _supportsearch_sphere = supportsearch_sphere;
            _supportsearch_aabb = supportsearch_aabb;
        }

        #endregion

        public VectorInt3 GetIndex_Point(Point3D point)
        {
            return new VectorInt3
            (
                GetIndex_1D(point.X),
                GetIndex_1D(point.Y),
                GetIndex_1D(point.Z)
            );
        }
        /// <summary>
        /// Returns all cells that touch this triangle
        /// </summary>
        public VectorInt3[] GetIndices_Triangle(ITriangle_wpf triangle, bool show_debug = false)
        {
            var bounds = GetBounds_Triangle(triangle);

            // If all three vertices have the same index, then the entire triangle is inside a single cell
            //if (corner_indices.Skip(1).All(o => o.Equals(corner_indices[0])))
            if (bounds.aabb_min == bounds.aabb_max)
            {
                if (show_debug)
                    ShowDebug_IndicesTriangle(triangle, bounds, null, true);

                return new[] { bounds.aabb_min };
            }

            VectorInt3[,,] indices = GetIndexBlock(bounds.aabb_min, bounds.aabb_max);

            // Go through all the cells and get the faces that sit betwen the cells
            var face_stripes = GetCubeFaceStripes_ForAABBIndices(indices);

            // To map from indices to this array, take the index - marked_offset
            bool[,,] marked = new bool[indices.GetUpperBound(0) + 1, indices.GetUpperBound(1) + 1, indices.GetUpperBound(2) + 1];
            VectorInt3 marked_offset = bounds.aabb_min;

            // Intersect the cube's faces with triangle (this won't detect a triangle completely inside a singe cell, but that check was accounted for earlier)
            foreach (var stripe in face_stripes)
            {
                WalkStripe(triangle, stripe, marked, marked_offset, true);
                WalkStripe(triangle, stripe, marked, marked_offset, false);
            }

            VectorInt3[] marked_cells = GetMarkedCells(marked, marked_offset);

            if (show_debug)
                ShowDebug_IndicesTriangle(triangle, bounds, marked_cells, false);

            return marked_cells;
        }
        /// <summary>
        /// Returns all cells that touch this rectangle
        /// </summary>
        public VectorInt3[,] GetIndices_Rect2D(Rect rect, double z)
        {
            var bounds = GetBounds_Rect2D(rect, z);

            VectorInt3[,,] indices = GetIndexBlock(bounds.aabb_min, bounds.aabb_max);

            if (indices.GetUpperBound(2) != 0)
                throw new ApplicationException($"3D indices should always have a size of 1 for this function.  rect: {rect}, z: {z}, indices_0: {indices.GetUpperBound(0)}, indices_1: {indices.GetUpperBound(1)}, indices_2: {indices.GetUpperBound(2)}");

            int size_x = indices.GetUpperBound(0) + 1;
            int size_y = indices.GetUpperBound(1) + 1;

            VectorInt3[,] retVal = new VectorInt3[size_x, size_y];

            for (int x = 0; x < size_x; x++)
            {
                for (int y = 0; y < size_y; y++)
                {
                    retVal[x, y] = indices[x, y, 0];
                }
            }

            return retVal;
        }
        public VectorInt3[] GetIndices_Sphere(Point3D center, double radius, bool is_hollow)
        {
            var retVal = new List<VectorInt3>();

            double radius_sqr = radius * radius;

            var aabb = GetAABB_Sphere(center, radius);

            for (int x = aabb.min.X; x <= aabb.max.X; x++)
            {
                for (int y = aabb.min.Y; y <= aabb.max.Y; y++)
                {
                    for (int z = aabb.min.Z; z <= aabb.max.Z; z++)
                    {
                        var index = new VectorInt3(x, y, z);
                        var cell = GetCell(index);

                        if (AnyInside_Sphere(center, cell.rect, radius_sqr))
                        {
                            if (is_hollow && AnyOutside_Sphere(center, cell.rect, radius_sqr))      // if all of the cell's points are inside the sphere, then that's an interior cell, so only look for cells that the surface goes through
                                retVal.Add(index);

                            else if (!is_hollow)        // solid filled sphere, so include any cells inside or touching the sphere
                                retVal.Add(index);
                        }
                    }
                }
            }

            return retVal.ToArray();
        }
        public VectorInt3[] GetIndices_Capsule(Point3D point0, Point3D point1, double radius, bool is_hollow)
        {
            var retVal = new List<VectorInt3>();

            double radius_sqr = radius * radius;

            var aabb = GetAABB_Capsule(point0, point1, radius);

            for (int x = aabb.min.X; x <= aabb.max.X; x++)
            {
                for (int y = aabb.min.Y; y <= aabb.max.Y; y++)
                {
                    for (int z = aabb.min.Z; z <= aabb.max.Z; z++)
                    {
                        var index = new VectorInt3(x, y, z);
                        var cell = GetCell(index);

                        double[] distances_sqr = cell.rect.GetPointsArray().
                            Select(o =>
                            {
                                Point3D nearest_point = Math3D.GetClosestPoint_LineSegment_Point(point0, point1, o);
                                return (o - nearest_point).LengthSquared;
                            }).
                            ToArray();


                        if (distances_sqr.Any(o => o <= radius_sqr))
                        {
                            if (is_hollow && distances_sqr.Any(o => o > radius_sqr))      // if all of the cell's points are inside the capsule, then that's an interior cell, so only look for cells that the surface goes through
                                retVal.Add(index);

                            else if (!is_hollow)        // solid filled capsule, so include any cells inside or touching the capsule
                                retVal.Add(index);
                        }
                    }
                }
            }

            return retVal.ToArray();
        }
        public VectorInt3[] GetIndices_Cylinder(Point3D point0, Point3D point1, double radius, bool is_hollow)
        {
            if (point0.IsNearValue(point1))
                return new VectorInt3[0];

            var retVal = new List<VectorInt3>();

            double radius_sqr = radius * radius;
            Vector3D normal_unit = (point1 - point0).ToUnit();

            var aabb = GetAABB_Capsule(point0, point1, radius);

            for (int x = aabb.min.X; x <= aabb.max.X; x++)
            {
                for (int y = aabb.min.Y; y <= aabb.max.Y; y++)
                {
                    for (int z = aabb.min.Z; z <= aabb.max.Z; z++)
                    {
                        var index = new VectorInt3(x, y, z);
                        var cell = GetCell(index);

                        var nearest = cell.rect.GetPointsArray().
                            Select(o =>
                            {
                                var nearest_point = Math3D.GetClosestPoint_LineSegment_Point_verbose(point0, point1, o);
                                return (rect_point: o, nearest_point.point_on_line, nearest_point.where_on_line);
                            }).
                            ToArray();

                        var middle_distances_sqr = nearest.
                            Where(o => o.where_on_line == Math3D.LocationOnLineSegment.Middle).
                            Select(o => (o.rect_point - o.point_on_line).LengthSquared).
                            ToArray();

                        if (middle_distances_sqr.Any(o => o <= radius_sqr))
                        {
                            if (is_hollow && middle_distances_sqr.Any(o => o > radius_sqr))      // if all of the cell's points are inside the capsule, then that's an interior cell, so only look for cells that the surface goes through
                                retVal.Add(index);

                            else if (!is_hollow)        // solid filled capsule, so include any cells inside or touching the capsule
                                retVal.Add(index);
                        }
                        else if (is_hollow)     // if it's solid, the end caps are already filled.  But hollow would just be a tube without the special logic in this else
                        {
                            var end_distances = nearest.
                                Where(o => o.where_on_line != Math3D.LocationOnLineSegment.Middle).
                                Select(o => new
                                {
                                    //o.rect_point,
                                    //o.point_on_line,
                                    //o.where_on_line,
                                    dist_sqr = (o.rect_point - o.point_on_line).LengthSquared,
                                    plane_dist = Math.Abs(Math3D.DistanceFromPlane(o.point_on_line, normal_unit, o.rect_point)),
                                }).
                                Where(o => o.dist_sqr <= radius_sqr && o.plane_dist <= CellSize * 0.55).     // a full cell size distance from the cap's plane is a bit much.  Choosing something just over half
                                ToArray();

                            if (end_distances.Length > 0)
                                retVal.Add(index);
                        }
                    }
                }
            }

            return retVal.ToArray();
        }

        public MarkResult Mark_Point(Point3D point, string key = null)
        {
            key = key ?? NULL_KEY;

            VectorInt3 index = GetIndex_Point(point);

            VectorInt3[] index_arr = new[] { index };

            MarkCells(index_arr, key);

            return new MarkResult()
            {
                AABB_Min = index,
                AABB_Max = index,
                MarkedCells = index_arr,
            };
        }
        public MarkResult Mark_Triangle(ITriangle_wpf triangle, string key = null, bool return_marked_cells = false)
        {
            key = key ?? NULL_KEY;

            var bounds = GetBounds_Triangle(triangle);

            VectorInt3[] touching_cells = null;
            if (return_marked_cells)
            {
                touching_cells = GetIndices_Triangle(triangle, false);
                MarkCells(touching_cells, key);
            }
            else
            {
                // Store this for later
                _pending_triangles.Add(new TriangleToProcess()
                {
                    key = key,
                    bounds = bounds,
                    triangle = new Triangle_wpf(triangle.Point0, triangle.Point1, triangle.Point2),     // clone in case the triangle changes
                });
            }

            return new MarkResult()
            {
                AABB_Min = bounds.aabb_min,
                AABB_Max = bounds.aabb_max,

                MarkedCells = touching_cells,
            };
        }
        public MarkResult Mark_Rect2D(Rect rect, double z, string key = null)
        {
            key = key ?? NULL_KEY;

            var indices = GetIndices_Rect2D(rect, z);

            VectorInt3[] flattened = new VectorInt3[indices.Length];

            int k = 0;
            for (int i = 0; i <= indices.GetUpperBound(0); i++)
            {
                for (int j = 0; j <= indices.GetUpperBound(1); j++)
                {
                    flattened[k++] = indices[i, j];
                }
            }

            MarkCells(flattened, key);

            return new MarkResult()
            {
                AABB_Min = indices[0, 0],
                AABB_Max = indices[indices.GetUpperBound(0), indices.GetUpperBound(1)],
                MarkedCells = flattened,
            };
        }
        public MarkResult Mark_Sphere(Point3D center, double radius, bool is_hollow, string key = null)
        {
            key = key ?? NULL_KEY;

            var aabb = GetAABB_Sphere(center, radius);

            VectorInt3[] indices = GetIndices_Sphere(center, radius, is_hollow);

            MarkCells(indices, key);

            return new MarkResult()
            {
                AABB_Min = aabb.min,
                AABB_Max = aabb.max,
                MarkedCells = indices,
            };
        }
        public MarkResult Mark_Capsule(Point3D point0, Point3D point1, double radius, bool is_hollow, string key = null)
        {
            key = key ?? NULL_KEY;

            var aabb = GetAABB_Capsule(point0, point1, radius);

            VectorInt3[] indices = GetIndices_Capsule(point0, point1, radius, is_hollow);

            MarkCells(indices, key);

            return new MarkResult()
            {
                AABB_Min = aabb.min,
                AABB_Max = aabb.max,
                MarkedCells = indices,
            };
        }
        public MarkResult Mark_Cylinder(Point3D point0, Point3D point1, double radius, bool is_hollow, string key = null)
        {
            key = key ?? NULL_KEY;

            // capsule should be good enough
            var aabb = GetAABB_Capsule(point0, point1, radius);

            VectorInt3[] indices = GetIndices_Cylinder(point0, point1, radius, is_hollow);

            MarkCells(indices, key);

            return new MarkResult()
            {
                AABB_Min = aabb.min,
                AABB_Max = aabb.max,
                MarkedCells = indices,
            };
        }

        public VectorInt3[] GetMarked_All(string[] include_keys = null, string[] except_keys = null)
        {
            // Make sure all pending are processed
            foreach (var pending in _pending_triangles)
                MarkPending(pending);

            _pending_triangles.Clear();

            return IterateMarkedStorages(include_keys, except_keys).
                SelectMany(o => o.List).
                ToArray();
        }
        /// <summary>
        /// Returns cells that are within a search sphere
        /// NOTE: supportsearch_sphere must be true in constructor
        /// NOTE: This only considers the center point of the cells.  So cells that touch, but the center is outside the radius won't be returned
        /// WARNING: This is NOT threadsafe.  A node's bounds get increased, the check is done, then the bounds get put back.  Multiple threads would make a mess of that logic
        /// </summary>
        public VectorInt3[] GetMarked_Sphere(Point3D center, double radius, string[] include_keys = null, string[] except_keys = null)
        {
            if (!_supportsearch_sphere)
                throw new InvalidOperationException("GetMarked_Sphere() requires supportsearch_sphere to be true in the constructor");

            ProcessPending_Touching(center, radius);

            return IterateMarkedStorages(include_keys, except_keys).
                SelectMany(o => o.Octree_Points.GetNearby(center.ToVector3(), (float)radius)).
                ToArray();
        }
        /// <summary>
        /// Returns cells that touch the search box
        /// NOTE: supportsearch_aabb must be true in constructor
        /// NOTE: This looks like it's threadsafe (assuming you're done populating the tree before doing searches)
        /// </summary>
        public VectorInt3[] GetMarked_AABB(Point3D min, Point3D max, string[] include_keys = null, string[] except_keys = null)
        {
            if (!_supportsearch_aabb)
                throw new InvalidOperationException("GetMarked_AABB() requires _supportsearch_aabb to be true in the constructor");

            ProcessPending_Touching(min, max);

            var aabb = Math3D.GetAABB(new[] { min, max });      // use this to make sure min and max are correct

            Point3D search_center = Math3D.GetCenter(aabb.min, aabb.max);
            Vector3D search_size = aabb.max - aabb.min;

            var search_box = new BoundingBox(search_center.ToVector3(), search_size.ToVector3());

            return IterateMarkedStorages(include_keys, except_keys).
                SelectMany(o => o.Octree_Cells.GetColliding(search_box)).
                ToArray();
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

        #region Private Methods

        private static bool AnyInside_Sphere(Point3D center, Rect3D rect, double radius_sqr)
        {
            foreach (Point3D point in rect.GetPointsArray())
            {
                if ((center - point).LengthSquared <= radius_sqr)
                    return true;
            }

            return false;
        }
        private static bool AnyOutside_Sphere(Point3D center, Rect3D rect, double radius_sqr)
        {
            foreach (Point3D point in rect.GetPointsArray())
            {
                if ((center - point).LengthSquared > radius_sqr)
                    return true;
            }

            return false;
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

        private GeometryBounds GetBounds_Triangle(ITriangle_wpf triangle)
        {
            return GetBounds_Points(new Point3D[] { triangle.Point0, triangle.Point1, triangle.Point2 });
        }
        private GeometryBounds GetBounds_Rect2D(Rect rect, double z)
        {
            var rect_points = new[]
            {
                new Point3D(rect.X, rect.Y, z),
                new Point3D(rect.X + rect.Width, rect.Y, z),
                new Point3D(rect.X + rect.Width, rect.Y + rect.Height, z),
                new Point3D(rect.X, rect.Y + rect.Height, z),
            };

            return GetBounds_Points(rect_points);
        }
        private GeometryBounds GetBounds_Points(IEnumerable<Point3D> points)
        {
            var aabb = Math3D.GetAABB(points);

            // Get index of each corner of aabb
            VectorInt3[] corner_indices = Polytopes.GetCubePoints(aabb.min, aabb.max).
                Select(o => GetIndex_Point(o)).
                ToArray();

            return new GeometryBounds()
            {
                aabb_min_3D = aabb.min,
                aabb_max_3D = aabb.max,

                corner_indices = corner_indices,

                aabb_min = new VectorInt3
                (
                    corner_indices.Min(o => o.X),
                    corner_indices.Min(o => o.Y),
                    corner_indices.Min(o => o.Z)
                ),

                aabb_max = new VectorInt3
                (
                    corner_indices.Max(o => o.X),
                    corner_indices.Max(o => o.Y),
                    corner_indices.Max(o => o.Z)
                ),
            };
        }

        /// <summary>
        /// This finds the min/max and returns a 3D block of indices
        /// </summary>
        private static VectorInt3[,,] GetIndexBlock(VectorInt3 min, VectorInt3 max)
        {
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

        private void ShowDebug_IndicesTriangle(ITriangle_wpf triangle, GeometryBounds bounds, VectorInt3[] marked_cells, bool is_single_cell)
        {
            var window = new Debug3DWindow()
            {
                Title = "Get Indices - Triangle",
            };

            window.AddTriangle(triangle, Colors.DarkKhaki);

            var sizes = Debug3DWindow.GetDrawSizes((bounds.aabb_max_3D - bounds.aabb_min_3D).Length / 2);

            window.AddLines(Polytopes.GetCubeLines(bounds.aabb_min_3D, bounds.aabb_max_3D), sizes.line * 2, Colors.DarkGray);

            var line_segments = GetDistinctLineSegments(bounds.corner_indices);
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
        #region Private Methods - marking

        private void ProcessPending_Touching(Point3D center, double radius)
        {
            int index = 0;

            while (index < _pending_triangles.Count)
            {
                if (Math3D.IsIntersecting_AABB_Sphere(_pending_triangles[index].bounds.aabb_min_3D, _pending_triangles[index].bounds.aabb_max_3D, center, radius))
                {
                    MarkPending(_pending_triangles[index]);
                    _pending_triangles.RemoveAt(index);
                }
                else
                {
                    index++;
                }
            }
        }
        private void ProcessPending_Touching(Point3D min, Point3D max)
        {
            int index = 0;

            while (index < _pending_triangles.Count)
            {
                if (Math3D.IsIntersecting_AABB_AABB(min, max, _pending_triangles[index].bounds.aabb_min_3D, _pending_triangles[index].bounds.aabb_max_3D))
                {
                    MarkPending(_pending_triangles[index]);
                    _pending_triangles.RemoveAt(index);
                }
                else
                {
                    index++;
                }
            }
        }

        private void MarkPending(TriangleToProcess triangle)
        {
            VectorInt3[] touching_cells = GetIndices_Triangle(triangle.triangle, false);
            MarkCells(touching_cells, triangle.key);
        }

        private void MarkCells(VectorInt3[] cells, string key)
        {
            EnsureKeyExists(key);

            // Figure out which cells will actually be added
            VectorInt3[] adding_cells = cells.
                Except(_marked[key].List).
                ToArray();

            // Always populate this list
            _marked[key].List.AddRange(adding_cells);

            // Specialized octrees
            if (_supportsearch_sphere || _supportsearch_aabb)
            {
                foreach (VectorInt3 index in adding_cells)
                {
                    var cell = GetCell(index);

                    if (_supportsearch_sphere)
                        _marked[key].Octree_Points.Add(index, cell.center.ToVector3());

                    if (_supportsearch_aabb)
                        _marked[key].Octree_Cells.Add(index, new BoundingBox(cell.center.ToVector3(), cell.rect.Size.ToVector3()));
                }
            }
        }

        private void EnsureKeyExists(string key)
        {
            if (_marked.ContainsKey(key))
                return;

            // Look here for explanations of the values
            // https://github.com/mcserep/NetOctree

            float initial_size = (float)(_cell_size * 24);
            float min_size = (float)(_cell_size * 6);

            var storage = new MarkedStorage()
            {
                List = new List<VectorInt3>(),

                Octree_Points = _supportsearch_sphere ?
                    new PointOctree<VectorInt3>(initial_size, new System.Numerics.Vector3(), min_size) :
                    null,

                Octree_Cells = _supportsearch_aabb ?
                    new BoundsOctree<VectorInt3>(initial_size, new System.Numerics.Vector3(), min_size, 1.25f) :
                    null,
            };

            _marked.Add(key, storage);
        }

        private IEnumerable<MarkedStorage> IterateMarkedStorages(string[] include_keys, string[] except_keys)
        {
            // Need to convert null keys into the string constant
            // It could be argued that cells under the null key should always be returned.  But it's easy enough to just add null to the include list
            include_keys = include_keys == null ?
                null :
                include_keys.
                    Select(o => o ?? NULL_KEY).
                    ToArray();

            except_keys = except_keys == null ?
                null :
                except_keys.
                    Select(o => o ?? NULL_KEY).
                    ToArray();

            foreach (string key in _marked.Keys)
            {
                if (include_keys != null && !include_keys.Contains(key))
                    continue;

                if (except_keys != null && except_keys.Contains(key))
                    continue;

                yield return _marked[key];
            }
        }

        private (VectorInt3 min, VectorInt3 max) GetAABB_Sphere(Point3D center, double radius)
        {
            int min_x = GetIndex_1D(center.X - radius);
            int max_x = GetIndex_1D(center.X + radius);

            int min_y = GetIndex_1D(center.Y - radius);
            int max_y = GetIndex_1D(center.Y + radius);

            int min_z = GetIndex_1D(center.Z - radius);
            int max_z = GetIndex_1D(center.Z + radius);

            return
            (
                new VectorInt3(min_x, min_y, min_z),
                new VectorInt3(max_x, max_y, max_z)
            );
        }
        private (VectorInt3 min, VectorInt3 max) GetAABB_Capsule(Point3D point0, Point3D point1, double radius)
        {
            var aabb0 = GetAABB_Sphere(point0, radius);
            var aabb1 = GetAABB_Sphere(point1, radius);

            VectorInt3 aabb_min = new VectorInt3(Math.Min(aabb0.min.X, aabb1.min.X), Math.Min(aabb0.min.Y, aabb1.min.Y), Math.Min(aabb0.min.Z, aabb1.min.Z));
            VectorInt3 aabb_max = new VectorInt3(Math.Max(aabb0.max.X, aabb1.max.X), Math.Max(aabb0.max.Y, aabb1.max.Y), Math.Max(aabb0.max.Z, aabb1.max.Z));

            return (aabb_min, aabb_max);
        }

        private int GetIndex_1D(double point)
        {
            return ((point - _cell_half) / _cell_size).ToInt_Ceiling();
        }

        private static bool IsInsideAABB(VectorInt3 test, VectorInt3 min, VectorInt3 max)
        {
            if (test.X < min.X || test.X > max.X)
                return false;

            else if (test.Y < min.Y || test.Y > max.Y)
                return false;

            else if (test.Z < min.Z || test.Z > max.Z)
                return false;

            return true;
        }

        private static double GetDistanceSquared(VectorInt3 a, VectorInt3 b)
        {
            return Math3D.LengthSquared((double)a.X, a.Y, a.Z, b.X, b.Y, b.Z);
        }

        #endregion
    }
}
