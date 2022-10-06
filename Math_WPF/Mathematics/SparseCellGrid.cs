using Game.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
        private readonly double _cell_size;
        public double CellSize => _cell_size;

        private readonly double _cell_half;

        public SparseCellGrid(double cell_size)
        {
            _cell_size = cell_size;
            _cell_half = cell_size / 2;
        }

        public VectorInt3 GetIndex_Point(Point3D point)
        {
            return new VectorInt3
            (
                ((point.X - _cell_half) / _cell_size).ToInt_Ceiling(),
                ((point.Y - _cell_half) / _cell_size).ToInt_Ceiling(),
                ((point.Z - _cell_half) / _cell_size).ToInt_Ceiling()
            );
        }
        public VectorInt3[] GetIndices_Triangle(ITriangle_wpf triangle)
        {
            throw new ApplicationException("finish this");
        }
        public VectorInt3[][] GetIndices_Rect2D(Rect rect, double z)
        {
            throw new ApplicationException("finish this");
        }

        public VectorInt3 Mark_Point(Point3D point)
        {
            throw new ApplicationException("finish this");
        }
        public VectorInt3[] Mark_Triangle(ITriangle_wpf triangle)
        {
            throw new ApplicationException("finish this");
        }
        public VectorInt3[][] Mark_Rect2D(Rect rect, double z)
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

        #region Private Methods

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
    }
}
