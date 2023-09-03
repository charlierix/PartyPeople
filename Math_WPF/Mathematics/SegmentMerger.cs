using Microsoft.VisualBasic.ApplicationServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;

namespace Game.Math_WPF.Mathematics
{
    /// <summary>
    /// This is a class that will merge colinear points as segments are added.  After you finish adding all the line segments, request the
    /// final line segments
    /// </summary>
    public class SegmentMerger
    {
        #region Declaration Section

        private readonly Point3D[] _points;

        private readonly SortedList<int, List<int>> _links = new SortedList<int, List<int>>();

        private (int i1, int i2)[] _final_index_pairs = null;
        private Point3D[] _final_points_distinct = null;

        #endregion

        #region Constructor

        public SegmentMerger(Point3D[] points)
        {
            _points = points;
        }

        #endregion

        public void AddSegment(int index0, int index1)
        {
            if (_final_index_pairs != null)
                throw new InvalidOperationException("Cannot add more segments after GetMergedSegments has been called");

            // See if this segment already exists or is colinear with existing segments
            var merge01 = TryMerge(index0, index1);
            if (merge01.already_exists)
                return;

            var merge10 = TryMerge(index1, index0);
            if (merge10.already_exists)
                return;

            if (merge01.merges.Length == 0 && merge10.merges.Length == 0)
            {
                // No merges, so add this segment
                AddPair(index0, index1);
                AddPair(index1, index0);
            }
            else
            {
                // This segment merged with others.  The sub segments have been removed, so add in the new larger segments
                foreach (var pair in merge01.merges.Concat(merge10.merges))
                {
                    AddSegment(pair.Item1, pair.Item2);     // recurse with the merged segment
                }
            }
        }

        public ((int i1, int i2)[] index_pairs, Point3D[] all_points_distinct) GetMergedSegments()
        {
            if (_final_index_pairs == null)
            {
                var final_pairs = GetFinalPairs();

                var reduced = RemoveUnusedPoints(final_pairs);

                _final_index_pairs = reduced.pairs;
                _final_points_distinct = reduced.points;
            }

            return (_final_index_pairs, _final_points_distinct);
        }

        #region Private Methods

        /// <summary>
        /// Looks in _links[index_primary] for segments that create a larger segment (other segments that are colinear
        /// with segment primary to secondary).  If there are merges, the other segment is removed and the larger segments
        /// are returned
        /// </summary>
        private (bool already_exists, (int, int)[] merges) TryMerge(int index_primary, int index_secondary)
        {
            var retVal = new List<(int, int)>();

            if (_links.TryGetValue(index_primary, out List<int> existing))
            {
                int index = 0;

                while (index < existing.Count)
                {
                    int index_existing = existing[index];

                    if (index_existing == index_secondary)
                        return (true, new (int, int)[0]);

                    if (IsColinear(index_secondary, index_primary, index_existing))
                    {
                        existing.RemoveAt(index);       // this is primary to existing
                        RemovePair(index_secondary, index_primary);
                        RemovePair(index_existing, index_primary);

                        //RemovePair(index_existing, index_secondary);      // these won't be there, because that is what the new merged line will be
                        //RemovePair(index_secondary, index_existing);

                        retVal.Add((index_secondary, index_existing));
                    }
                    else
                    {
                        index++;
                    }
                }
            }

            return (false, retVal.ToArray());
        }

        private void AddPair(int index_primary, int index_secondary)
        {
            if (_links.TryGetValue(index_primary, out List<int> existing))
                existing.Add(index_secondary);

            else
                _links.Add(index_primary, new List<int>() { index_secondary });
        }

        /// <remarks>
        /// _links holds a pair both directions:
        ///     _links[index_primary][n] == index_secondary AND
        ///     _links[index_secondary][n] == index_primary
        ///     
        /// This function only removes the first pair.  It's intended to be called when the other direction has already been
        /// discovered and removed
        /// </remarks>
        private void RemovePair(int index_primary, int index_secondary)
        {
            if (_links.TryGetValue(index_primary, out List<int> existing))
            {
                int index = 0;

                while (index < existing.Count)
                {
                    if (existing[index] == index_secondary)
                    {
                        existing.RemoveAt(index);
                        return;     // there should never be dupes, so no point searching farther
                    }
                    else
                    {
                        index++;
                    }
                }
            }
        }

        private bool IsColinear(int index0, int index_shared, int index1)
        {
            Vector3D segment0 = _points[index0] - _points[index_shared];
            Vector3D segment1 = _points[index1] - _points[index_shared];

            // Length of the cross product is the area of the parallelogram
            // So zero area means a straight line
            return Vector3D.CrossProduct(segment0, segment1).LengthSquared.IsNearZero();
        }

        private (int i1, int i2)[] GetFinalPairs()
        {
            var retVal = new List<(int i1, int i2)>();

            int[] primaries = _links.Keys.ToArray();

            foreach (int primary in primaries)
            {
                List<int> secondaries = _links[primary];

                while (secondaries.Count > 0)
                {
                    retVal.Add((primary, secondaries[0]));

                    RemovePair(secondaries[0], primary);

                    secondaries.RemoveAt(0);
                }
            }

            return retVal.ToArray();
        }

        private ((int i1, int i2)[] pairs, Point3D[] points) RemoveUnusedPoints((int i1, int i2)[] final_pairs)
        {
            // See which points are no longer referenced
            int[] unused_indices = Enumerable.Range(0, _points.Length).
                Except(final_pairs.
                    SelectMany(o => new[] { o.i1, o.i2 }).
                    Distinct()).
                OrderBy(o => o).        // should already be ordered, but just in case
                ToArray();

            // Index of map is the original index into _points
            // Value of map[n] is index into the reduced set of points
            int[] map = RemoveUnusedPoints_GetMap(unused_indices, _points.Length);

            (int, int)[] reduced_pairs = RemoveUnusedPoints_GetRemappedPairs(final_pairs, map);
            Point3D[] reduced_points = RemoveUnusedPoints_GetReducedPoints(_points, map);

            return (reduced_pairs, reduced_points);
        }
        private static int[] RemoveUnusedPoints_GetMap(int[] unused_indices, int length)
        {
            int[] retVal = new int[length];

            int unused_index = 0;

            for (int i = 0; i < length; i++)
            {
                if (unused_index < unused_indices.Length && unused_indices[unused_index] == i)
                {
                    unused_index++;
                    retVal[i] = -1;
                }
                else
                {
                    retVal[i] = i - unused_index;
                }
            }

            return retVal;
        }
        private static (int, int)[] RemoveUnusedPoints_GetRemappedPairs((int, int)[] pairs, int[] map)
        {
            var retVal = new (int, int)[pairs.Length];

            for (int i = 0; i < pairs.Length; i++)
            {
                retVal[i] =
                (
                    map[pairs[i].Item1],
                    map[pairs[i].Item2]
                );
            }

            return retVal;
        }
        private static Point3D[] RemoveUnusedPoints_GetReducedPoints(Point3D[] points, int[] map)
        {
            var retVal = new List<Point3D>();

            for (int i = 0; i < map.Length; i++)
            {
                if (map[i] >= 0)
                    retVal.Add(points[i]);      // the map was built for mapping old segment indices to the new reduced set of points.  So for rebuilding points, just skip anything that the map says is invalid
            }

            return retVal.ToArray();
        }

        #endregion
    }
}
