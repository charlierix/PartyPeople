using Game.Core;
using Game.Math_WPF.Mathematics;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Game.ML
{
    /// <summary>
    /// Keeping this internal so that consumers of this dll don't need extra references to ml.net, accord, etc
    /// </summary>
    internal static class MathML
    {
        #region struct: IndexPointer

        private struct IndexPointer
        {
            public IndexPointer(int localIndex, int globalIndex)
            {
                LocalIndex = localIndex;
                GlobalIndex = globalIndex;
            }

            public int LocalIndex { get; set; }
            public int GlobalIndex { get; set; }

            public override string ToString()
            {
                return $"local: {LocalIndex} | global: {GlobalIndex}";
            }
        }

        #endregion
        #region struct: IndexPointer2

        private struct IndexPointer2
        {
            public IndexPointer2(int localIndex_Left, int localIndex_Right, int globalIndex)
            {
                LocalIndex_Left = localIndex_Left;
                LocalIndex_Right = localIndex_Right;
                GlobalIndex = globalIndex;
            }

            public int LocalIndex_Left { get; set; }
            public int LocalIndex_Right { get; set; }
            public int GlobalIndex { get; set; }

            public override string ToString()
            {
                return $"loc left: {LocalIndex_Left} | loc right: {LocalIndex_Right} | global: {GlobalIndex}";
            }
        }

        #endregion
        #region struct: SparseIndices

        private struct SparseIndices
        {
            public SparseIndices(IndexPointer[] leftOnly, IndexPointer2[] common, IndexPointer[] rightOnly)
            {
                LeftOnly = leftOnly;
                Common = common;
                RightOnly = rightOnly;
            }

            public IndexPointer[] LeftOnly { get; set; }
            public IndexPointer2[] Common { get; set; }
            public IndexPointer[] RightOnly { get; set; }

            public int SumIndices => (LeftOnly?.Length ?? 0) + (Common?.Length ?? 0) + (RightOnly?.Length ?? 0);

            public override string ToString()
            {
                return string.Format("left: {0} | common: {1} | right: {2} | total: {3}",
                    LeftOnly?.Length.ToString() ?? "<null>",
                    Common?.Length.ToString() ?? "<null>",
                    RightOnly?.Length.ToString() ?? "<null>",
                    SumIndices);
            }
        }

        #endregion
        #region class: IndexPointerComparer

        private class IndexPointerComparer : IEqualityComparer<IndexPointer>
        {
            public bool Equals(IndexPointer x, IndexPointer y)
            {
                return x.GlobalIndex == y.GlobalIndex;
            }

            public int GetHashCode(IndexPointer obj)
            {
                return obj.GlobalIndex.GetHashCode();
            }
        }

        #endregion
        #region struct: SparseVector

        private struct SparseVector
        {
            public SparseVector(int[] indices, float[] values, int totalSize)
            {
                if (indices == null)
                {
                    throw new ArgumentNullException(nameof(indices));
                }
                else if (values == null)
                {
                    throw new ArgumentNullException(nameof(values));
                }
                else if (indices.Length != values.Length)
                {
                    throw new ArgumentException($"The arrays must be the same size");
                }
                else if (indices.Length > totalSize)
                {
                    throw new ArgumentException($"TotalSize can't be less than the number of explicit items.  items: {indices.Length}  totalSize: {totalSize}");
                }

                Indices = indices;
                Values = values;
                TotalSize = totalSize;
            }

            public int[] Indices { get; set; }
            public float[] Values { get; set; }
            public int TotalSize { get; set; }

            public override string ToString()
            {
                return $"size: {TotalSize} | explicit values: {Indices?.Length.ToString() ?? "<null>"}";
            }
        }

        #endregion

        #region Declaration Section

        public const int SPARSETHRESHOLD_COUNT = 24;        // anything smaller than this should just be dense
        public const double SPARSETHRESHOLD_PERCENT = .33333;       // if nonzero/length is greater than this percent, it should be dense

        #endregion

        /// <summary>
        /// Subtracts the specified vector from another specified vector (return v1 - v2)
        /// </summary>
        /// <param name="vector1">The vector from which vector2 is subtracted</param>
        /// <param name="vector2">The vector to subtract from vector1</param>
        /// <returns>The difference between vector1 and vector2</returns>
        public static VBuffer<float> Subtract(VBuffer<float> vector1, VBuffer<float> vector2)
        {
            if (vector1.Length != vector2.Length)
            {
                throw new ArgumentException(string.Format("Vectors must have the same dimensionality.  v1={0}, v2={1}", vector1.Length, vector2.Length));
            }

            var val1 = vector1.GetValues();
            var val2 = vector2.GetValues();

            if (vector1.IsDense && vector2.IsDense)
            {
                return Subtract_BothDense(val1, val2);
            }
            else if (!vector1.IsDense && !vector2.IsDense)
            {
                return Subtract_BothSparse(vector1, vector2, val1, val2);
            }
            else if (vector1.IsDense && !vector2.IsDense)
            {
                return Subract_DenseSparse(vector2, val1, val2);
            }
            else// if(!vector1.IsDense && vector2.IsDense)
            {
                return Subtract_SparseDense(vector1, val1, val2);
            }
        }

        public static VBuffer<float> Add(VBuffer<float> vector1, VBuffer<float> vector2)
        {
            if (vector1.Length != vector2.Length)
            {
                throw new ArgumentException(string.Format("Vectors must have the same dimensionality.  v1={0}, v2={1}", vector1.Length, vector2.Length));
            }

            var val1 = vector1.GetValues();
            var val2 = vector2.GetValues();

            if (vector1.IsDense && vector2.IsDense)
            {
                return Add_BothDense(val1, val2);
            }
            else if (!vector1.IsDense && !vector2.IsDense)
            {
                return Add_BothSparse(vector1, vector2, val1, val2);
            }
            else if (vector1.IsDense && !vector2.IsDense)
            {
                return Add_DenseSparse(vector2, val1, val2);
            }
            else// if(!vector1.IsDense && vector2.IsDense)
            {
                return Add_DenseSparse(vector1, val2, val1);        // reusing the desnse:sparse function instead of making a sparse:dense function by alternating params (subtract had to have a separate function, but add doesn't matter)
            }
        }

        public static float Length(VBuffer<float> vector)
        {
            return Convert.ToSingle(Math.Sqrt(LengthSquared(vector)));
        }
        public static float LengthSquared(VBuffer<float> vector)
        {
            // It doesn't matter if the vector is dense or sparse.  Values of zero don't add to the magnitude of the vector
            var values = vector.GetValues();

            float retVal = 0;

            for (int cntr = 0; cntr < values.Length; cntr++)
            {
                retVal += values[cntr] * values[cntr];
            }

            return retVal;
        }

        #region Private Methods

        private static VBuffer<float> Subtract_BothDense(ReadOnlySpan<float> val1, ReadOnlySpan<float> val2)
        {
            float[] values = new float[val1.Length];

            for (int cntr = 0; cntr < val1.Length; cntr++)
            {
                values[cntr] = val1[cntr] - val2[cntr];
            }

            return ToVBuffer(values);
        }
        private static VBuffer<float> Subtract_BothSparse(VBuffer<float> vector1, VBuffer<float> vector2, ReadOnlySpan<float> val1, ReadOnlySpan<float> val2)
        {
            var ind1 = vector1.GetIndices();
            var ind2 = vector2.GetIndices();

            SparseIndices breakdown = GetIndices(ind1, ind2);

            if (ShouldBeSparse(breakdown, vector1.Length))
            {
                return Subtract_BothSparse_Sparse(breakdown, val1, val2, vector1.Length);
            }
            else
            {
                return Subtract_BothSparse_Dense(breakdown, val1, val2, vector1.Length);
            }
        }
        private static VBuffer<float> Subtract_BothSparse_Sparse(SparseIndices breakdown, ReadOnlySpan<float> val1, ReadOnlySpan<float> val2, int count)
        {
            int sumIndices = breakdown.SumIndices;

            int[] indices = new int[sumIndices];
            float[] values = new float[sumIndices];

            // LeftOnly
            for (int cntr = 0; cntr < breakdown.LeftOnly.Length; cntr++)
            {
                indices[cntr] = breakdown.LeftOnly[cntr].GlobalIndex;
                values[cntr] = val1[breakdown.LeftOnly[cntr].LocalIndex];
            }

            // Common
            int offset = breakdown.LeftOnly.Length;

            for (int cntr = 0; cntr < breakdown.Common.Length; cntr++)
            {
                indices[offset + cntr] = breakdown.Common[cntr].GlobalIndex;
                values[offset + cntr] = val1[breakdown.Common[cntr].LocalIndex_Left] - val2[breakdown.Common[cntr].LocalIndex_Right];
            }

            // Right Only
            offset += breakdown.Common.Length;

            for (int cntr = 0; cntr < breakdown.RightOnly.Length; cntr++)
            {
                indices[offset + cntr] = breakdown.RightOnly[cntr].GlobalIndex;
                values[offset + cntr] = -val2[breakdown.RightOnly[cntr].LocalIndex];
            }

            SparseVector retVal = new SparseVector(indices, values, count);
            retVal = RemoveZerosFromSparse(retVal);
            //retVal = SortIndices(retVal);     //TODO: See if it's necessary to sort these

            return new VBuffer<float>(count, retVal.Indices.Length, retVal.Values, retVal.Indices);
        }
        private static VBuffer<float> Subtract_BothSparse_Dense(SparseIndices breakdown, ReadOnlySpan<float> val1, ReadOnlySpan<float> val2, int count)
        {
            float[] values = new float[count];

            // LeftOnly
            for (int cntr = 0; cntr < breakdown.LeftOnly.Length; cntr++)
            {
                values[breakdown.LeftOnly[cntr].GlobalIndex] = val1[breakdown.LeftOnly[cntr].LocalIndex];
            }

            // Common
            for (int cntr = 0; cntr < breakdown.Common.Length; cntr++)
            {
                values[breakdown.Common[cntr].GlobalIndex] = val1[breakdown.Common[cntr].LocalIndex_Left] - val2[breakdown.Common[cntr].LocalIndex_Right];
            }

            // Right Only
            for (int cntr = 0; cntr < breakdown.RightOnly.Length; cntr++)
            {
                values[breakdown.RightOnly[cntr].GlobalIndex] = -val2[breakdown.RightOnly[cntr].LocalIndex];
            }

            return ToVBuffer(values);
        }
        private static VBuffer<float> Subract_DenseSparse(VBuffer<float> vector2, ReadOnlySpan<float> val1, ReadOnlySpan<float> val2)
        {
            float[] values = val1.ToArray();

            var ind2 = vector2.GetIndices();

            for (int cntr = 0; cntr < ind2.Length; cntr++)
            {
                values[ind2[cntr]] -= val2[cntr];
            }

            return ToVBuffer(values);
        }
        private static VBuffer<float> Subtract_SparseDense(VBuffer<float> vector1, ReadOnlySpan<float> val1, ReadOnlySpan<float> val2)
        {
            float[] values = val2.
                ToArray().
                Select(o => -o).        // 0 minus val2 would be -val2
                ToArray();

            var ind1 = vector1.GetIndices();

            for (int cntr = 0; cntr < ind1.Length; cntr++)
            {
                values[ind1[cntr]] += val1[cntr];       // since they all started as -val2, add val1's.  This is equivalent to taking val1 - val2
            }

            return ToVBuffer(values);
        }

        //NOTE: For optimization reasons, add is a tweaked copy of subtract
        private static VBuffer<float> Add_BothDense(ReadOnlySpan<float> val1, ReadOnlySpan<float> val2)
        {
            float[] values = new float[val1.Length];

            for (int cntr = 0; cntr < val1.Length; cntr++)
            {
                values[cntr] = val1[cntr] + val2[cntr];
            }

            return ToVBuffer(values);
        }
        private static VBuffer<float> Add_BothSparse(VBuffer<float> vector1, VBuffer<float> vector2, ReadOnlySpan<float> val1, ReadOnlySpan<float> val2)
        {
            var ind1 = vector1.GetIndices();
            var ind2 = vector2.GetIndices();

            SparseIndices breakdown = GetIndices(ind1, ind2);

            if (ShouldBeSparse(breakdown, vector1.Length))
            {
                return Add_BothSparse_Sparse(breakdown, val1, val2, vector1.Length);
            }
            else
            {
                return Add_BothSparse_Dense(breakdown, val1, val2, vector1.Length);
            }
        }
        private static VBuffer<float> Add_BothSparse_Sparse(SparseIndices breakdown, ReadOnlySpan<float> val1, ReadOnlySpan<float> val2, int count)
        {
            int sumIndices = breakdown.SumIndices;

            int[] indices = new int[sumIndices];
            float[] values = new float[sumIndices];

            // LeftOnly
            for (int cntr = 0; cntr < breakdown.LeftOnly.Length; cntr++)
            {
                indices[cntr] = breakdown.LeftOnly[cntr].GlobalIndex;
                values[cntr] = val1[breakdown.LeftOnly[cntr].LocalIndex];
            }

            // Common
            int offset = breakdown.LeftOnly.Length;

            for (int cntr = 0; cntr < breakdown.Common.Length; cntr++)
            {
                indices[offset + cntr] = breakdown.Common[cntr].GlobalIndex;
                values[offset + cntr] = val1[breakdown.Common[cntr].LocalIndex_Left] + val2[breakdown.Common[cntr].LocalIndex_Right];
            }

            // Right Only
            offset += breakdown.Common.Length;

            for (int cntr = 0; cntr < breakdown.RightOnly.Length; cntr++)
            {
                indices[offset + cntr] = breakdown.RightOnly[cntr].GlobalIndex;
                values[offset + cntr] = val2[breakdown.RightOnly[cntr].LocalIndex];
            }

            SparseVector retVal = new SparseVector(indices, values, count);
            retVal = RemoveZerosFromSparse(retVal);
            //retVal = SortIndices(retVal);     //TODO: See if it's necessary to sort these

            return new VBuffer<float>(count, retVal.Indices.Length, retVal.Values, retVal.Indices);
        }
        private static VBuffer<float> Add_BothSparse_Dense(SparseIndices breakdown, ReadOnlySpan<float> val1, ReadOnlySpan<float> val2, int count)
        {
            float[] values = new float[count];

            // LeftOnly
            for (int cntr = 0; cntr < breakdown.LeftOnly.Length; cntr++)
            {
                values[breakdown.LeftOnly[cntr].GlobalIndex] = val1[breakdown.LeftOnly[cntr].LocalIndex];
            }

            // Common
            for (int cntr = 0; cntr < breakdown.Common.Length; cntr++)
            {
                values[breakdown.Common[cntr].GlobalIndex] = val1[breakdown.Common[cntr].LocalIndex_Left] + val2[breakdown.Common[cntr].LocalIndex_Right];
            }

            // Right Only
            for (int cntr = 0; cntr < breakdown.RightOnly.Length; cntr++)
            {
                values[breakdown.RightOnly[cntr].GlobalIndex] = -val2[breakdown.RightOnly[cntr].LocalIndex];
            }

            return ToVBuffer(values);
        }
        private static VBuffer<float> Add_DenseSparse(VBuffer<float> vector2, ReadOnlySpan<float> val1, ReadOnlySpan<float> val2)
        {
            float[] values = val1.ToArray();

            var ind2 = vector2.GetIndices();

            for (int cntr = 0; cntr < ind2.Length; cntr++)
            {
                values[ind2[cntr]] += val2[cntr];
            }

            return ToVBuffer(values);
        }

        private static VBuffer<float> ToVBuffer(float[] values, double? partialCheckPercent = .05)
        {
            if (ShouldBeSparse(values))
            {
                SparseVector retVal = RemoveZerosFromDense(values);
                return new VBuffer<float>(values.Length, retVal.Indices.Length, retVal.Values, retVal.Indices);
            }
            else
            {
                return new VBuffer<float>(values.Length, values);
            }
        }

        private static SparseIndices GetIndices(ReadOnlySpan<int> left, ReadOnlySpan<int> right)
        {
            return GetIndices(ToIndexPointers(left), ToIndexPointers(right));
        }
        private static SparseIndices GetIndices(IndexPointer[] left, IndexPointer[] right)
        {
            var comparer = new IndexPointerComparer();

            return new SparseIndices
            (
                left.
                    Except(right, comparer).
                    ToArray(),

                left.
                    Intersect(right, comparer).
                    Select(o => new IndexPointer2(o.LocalIndex, right.First(p => p.GlobalIndex == o.GlobalIndex).LocalIndex, o.GlobalIndex)).
                    ToArray(),

                right.
                    Except(left, comparer).
                    ToArray()
            );
        }

        /// <summary>
        /// This overload takes stats of a sparse vector
        /// </summary>
        private static bool ShouldBeSparse(SparseIndices indices, int totalLength)
        {
            if (totalLength < SPARSETHRESHOLD_COUNT)
            {
                // Small vectors can just stay dense
                return false;
            }

            return Convert.ToDouble(indices.SumIndices) / Convert.ToDouble(totalLength) <= SPARSETHRESHOLD_PERCENT;
        }
        /// <summary>
        /// This overload looks at a dense vector's values
        /// </summary>
        private static bool ShouldBeSparse(float[] values)
        {
            const int NUMNONZEROS = 4;
            const int SAMPLESIZE = 12;       // (NUMNONZEROS / SPARSETHRESHOLD_PERCENT).ToInt_Round();

            if (values.Length < SPARSETHRESHOLD_COUNT)
            {
                // Small vectors can just stay dense
                return false;
            }

            int numNonZeros = 0;

            // Instead of looking at all values, only look at a small sample
            //int sampleSize = (NUMNONZEROS / SPARSETHRESHOLD_PERCENT).ToInt_Round();
            if (values.Length > SAMPLESIZE * 3)
            {
                foreach (int index in UtilityCore.RandomRange(0, values.Length, SAMPLESIZE))
                {
                    if (!Math1D.IsNearZero(values[index]))
                    {
                        numNonZeros++;
                        if (numNonZeros >= NUMNONZEROS)
                        {
                            // Found too many non zeros in this sample.  Keep it dense
                            return false;
                        }
                    }
                }

                // Didn't find enough non zeros in the sample, it should be sparse
                return true;
            }
            else
            {
                // The vector isn't big enough to justify the cost of calling RandomRange.  Just scan the whole thing
                for (int cntr = 0; cntr < values.Length; cntr++)
                {
                    if (Math1D.IsNearZero(values[cntr]))
                    {
                        numNonZeros++;
                    }
                }

                return Convert.ToDouble(numNonZeros) / Convert.ToDouble(values.Length) <= SPARSETHRESHOLD_PERCENT;
            }
        }

        private static IndexPointer[] ToIndexPointers(ReadOnlySpan<int> span)
        {
            IndexPointer[] retVal = new IndexPointer[span.Length];

            for (int cntr = 0; cntr < span.Length; cntr++)
            {
                retVal[cntr] = new IndexPointer(cntr, span[cntr]);
            }

            return retVal;

            // Can't use ReadOnlySpan with a yield return, it gives a compiler error
            //private static IEnumerable<T> AsEnumerable<T>(ReadOnlySpan<T> span)
            //{
            //    for (int cntr = 0; cntr < span.Length; cntr++)
            //    {
            //        yield return span[cntr];
            //    }
            //}
        }

        private static SparseVector RemoveZerosFromSparse(SparseVector vector)
        {
            // The chances are good that there aren't any zeros, so only take the expense of building new lists when the first zero value is found
            for (int cntr = 0; cntr < vector.Values.Length; cntr++)
            {
                if (Math1D.IsNearZero(vector.Values[cntr]))
                {
                    return RemoveZerosFromSparse_Rebuild(vector, cntr);
                }
            }

            // No zeros found, return what was passed in
            return vector;
        }
        private static SparseVector RemoveZerosFromSparse_Rebuild(SparseVector vector, int firstZeroIndex)
        {
            List<int> indices = new List<int>();
            List<float> values = new List<float>();

            // Copy everything up to the first zero
            if (firstZeroIndex > 0)
            {
                for (int cntr = 0; cntr < firstZeroIndex; cntr++)
                {
                    indices.Add(vector.Indices[cntr]);
                    values.Add(vector.Values[cntr]);
                }
            }

            // Add other non zero values
            for (int cntr = firstZeroIndex + 1; cntr < vector.Values.Length; cntr++)
            {
                if (!Math1D.IsNearZero(vector.Values[cntr]))
                {
                    indices.Add(vector.Indices[cntr]);
                    values.Add(vector.Values[cntr]);
                }
            }

            return new SparseVector(indices.ToArray(), values.ToArray(), vector.TotalSize);
        }

        private static SparseVector RemoveZerosFromDense(float[] denseValues)
        {
            List<int> indices = new List<int>();
            List<float> values = new List<float>();

            for (int cntr = 0; cntr < denseValues.Length; cntr++)
            {
                if (!Math1D.IsNearZero(denseValues[cntr]))
                {
                    indices.Add(cntr);
                    values.Add(denseValues[cntr]);
                }
            }

            return new SparseVector(indices.ToArray(), values.ToArray(), denseValues.Length);
        }

        #endregion
    }
}
