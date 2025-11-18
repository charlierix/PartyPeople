using Accord.MachineLearning.Boosting;
using Game.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace Game.Math_WPF.Mathematics
{
    public static class KMeansClusterer
    {
        #region class: Sample

        public class Sample<T>
        {
            public float[] Vector { get; set; }
            public T Source { get; set; }
        }

        #endregion
        #region class: Cluster

        public class Cluster<T>
        {
            public float[] Center { get; set; }

            // these arrays are the same size
            public Sample<T>[] Items { get; set; }
            public float[] Item_DistSqr_FromCenter { get; set; }
        }

        private class Cluster_Building<T>
        {
            public float[] Center { get; set; }
            public List<(Sample<T> item, float distSqr)> Items { get; } = new List<(Sample<T>, float)>();
        }

        #endregion
        #region class: ElbowRunStats

        public class ElbowRunStats<T>
        {
            public Cluster<T>[] Result => Runs[BestIndex].clusters;

            // ------ everything below is for debugging and visualization ------

            public int MinK { get; set; }
            public int MaxK { get; set; }

            public int BestIndex { get; set; }

            public ElbowRunStats_Run<T>[] Runs { get; set; }
        }
        public class ElbowRunStats_Run<T>
        {
            public int k { get; set; }      // how many clusters
            public float sse { get; set; }      // sum distances from center
            public float distance { get; set; }     // distance from trend line between runs[0] to runs[^1] (x=k,y=sse)
            public Cluster<T>[] clusters { get; set; }
        }

        #endregion


        /// <summary>
        /// This does multiple kmeans and uses elbow method to return the one with the best number of clusters
        /// </summary>
        /// <remarks>
        /// NOTE: Can't stop the first time distance stops increasing.  There could be better distances later
        /// </remarks>
        /// <param name="data">The inputs.  Only vector is looked at by this function, source is for the caller to have an easy time getting at the object that the vector represents</param>
        /// <param name="weights">Use this if some of the dimensions of vector should count more or less than others.  The values used don't matter too much, it's how they relate to each other</param>
        /// <param name="keep_all_clusters">
        /// True: use if doing analysis/visualization of all the data
        /// False: only the optimal cluster is left populated.  this is more memory efficient and is what should be used in production
        /// </param>
        public static Cluster<T>[] DoClustering<T>(IList<Sample<T>> data, float[] weights = null)
        {
            return DoClustering_private(data, weights, false).Result;
        }
        public static ElbowRunStats<T> DoClustering_Debug<T>(IList<Sample<T>> data, float[] weights = null)
        {
            return DoClustering_private(data, weights, true);
        }

        public static Cluster<T>[] DoClustering<T>(IList<Sample<T>> data, int num_clusters, float[] weights = null)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            if (data.Count < num_clusters)
                num_clusters = data.Count;

            if (num_clusters <= 0)
                throw new ArgumentException($"Not enough clusters: {num_clusters}");

            if (weights == null)
                weights = Enumerable.Range(0, data[0].Vector.Length).
                    Select(o => 1f).
                    ToArray();

            if (weights.Length != data[0].Vector.Length)
                throw new InvalidOperationException($"weights isn't same length as first sample's vector length.  weights: {weights.Length}, vector: {data[0].Vector.Length}");

            // Initialize cluster centers with random samples
            var retVal = GetInitialClusters(data, num_clusters);

            // Keep shuffling until each cluster's item is closer to its center than other node centers
            while (true)
                if (!Cluster_Step(retVal, data, num_clusters, weights))       // keep refining until the cluster centers stop moving
                    break;

            return BuildFinalReturn(retVal);
        }

        /// <summary>
        /// Returns the index of the cluster that this point is closest to
        /// </summary>
        public static int GetClusterIndex<T>(Cluster<T>[] clusters, float[] vector, float[] weights)
        {
            int nearest_index = 0;
            float min_distSqr = DistanceSqr(vector, clusters[0].Center, weights);

            for (int i = 1; i < clusters.Length; i++)
            {
                float distSqr = DistanceSqr(vector, clusters[i].Center, weights);
                if (distSqr < min_distSqr)
                {
                    min_distSqr = distSqr;
                    nearest_index = i;
                }
            }

            return nearest_index;
        }

        /// <summary>
        /// Makes new clusters, but with the items sorted so that index 0 is closest to center
        /// </summary>
        public static Cluster<T>[] SortItemsByDistFromCenters<T>(Cluster<T>[] result)
        {
            var retVal = new Cluster<T>[result.Length];

            for (int i = 0; i < result.Length; i++)
                retVal[i] = SortCluster(result[i]);

            return retVal;
        }

        #region Private Methods - elbow

        private static ElbowRunStats<T> DoClustering_private<T>(IList<Sample<T>> data, float[] weights = null, bool keep_all_clusters = false)
        {
            if (data == null || data.Count == 0)
                return new ElbowRunStats<T>
                {
                    BestIndex = -1,
                    MinK = 0,
                    MaxK = 0,
                    Runs = [],
                };

            if (weights == null)
                weights = Enumerable.Range(0, data[0].Vector.Length).
                    Select(o => 1f).
                    ToArray();

            if (weights.Length != data[0].Vector.Length)
                throw new InvalidOperationException($"weights isn't same length as first sample's vector length.  weights: {weights.Length}, vector: {data[0].Vector.Length}");

            // Determine the maximum number of clusters to test
            int maxK = Math.Max(2, (int)Math.Sqrt(data.Count));

            // Store (k, sse) pairs for all tested cluster counts
            //var kSSEList = new List<(int k, float sse, Cluster<T>[] clusters)>();
            var runs = new ElbowRunStats_Run<T>[maxK];

            runs[0] = GetElbowRun_Initial(1, data, weights);
            runs[^1] = GetElbowRun_Initial(maxK, data, weights);

            // Extract first and last (k, sse) points for line reference
            var first = runs[0];
            var last = runs[^1];
            int optimalIndex = 0;
            float maxDistance = 0f;

            // Loop through all points except the first and last to find the maximum perpendicular distance
            for (int i = 1; i < runs.Length - 1; i++)
            {
                runs[i] = GetElbowRun_Initial(i + 1, data, weights);

                runs[i].distance = GetElbowDist(runs[i].k, runs[i].sse, first.k, first.sse, last.k, last.sse);

                if (runs[i].distance > maxDistance)
                {
                    maxDistance = runs[i].distance;
                    optimalIndex = i;

                    if (!keep_all_clusters)
                        runs[i - 1].clusters = null;        // (i starts at one, so i-1 is safe) it might already be null, but it also might have been the prev winner
                }
                else
                {
                    if (!keep_all_clusters)
                        runs[i].clusters = null;
                }
            }

            // Fallback: if no elbow is found, use the last k (lowest SSE)
            if (maxDistance == 0)
            {
                optimalIndex = runs.Length - 1;

                if (!keep_all_clusters)
                    runs[^2].clusters = null;
            }
            else
            {
                if (!keep_all_clusters)
                    runs[^1].clusters = null;
            }

            // Return final clustering with the chosen optimal number of clusters
            return new ElbowRunStats<T>
            {
                MinK = first.k,
                MaxK = last.k,
                BestIndex = optimalIndex,
                Runs = runs.ToArray(),
            };
        }

        private static ElbowRunStats_Run<T> GetElbowRun_Initial<T>(int k, IList<Sample<T>> data, float[] weights)
        {
            // Perform clustering for k clusters (Assume a method DoClustering is available)
            Cluster<T>[] clusters = DoClustering(data, k, weights);

            // Compute the total within-cluster sum of squares (WSS)
            float totalSSE = 0f;

            foreach (var cluster in clusters)
                for (int i = 0; i < cluster.Item_DistSqr_FromCenter.Length; i++)
                    totalSSE += cluster.Item_DistSqr_FromCenter[i];

            return new ElbowRunStats_Run<T>
            {
                k = k,
                sse = totalSSE,
                clusters = clusters,
            };
        }


        private static float GetElbowDist(int num_clusters, float sumSquares, int first_numclusters, float first_sumsqr, int last_numclusters, float last_sumsqr)
        {
            //var point = kSSEList[i];
            float x = num_clusters;
            float y = sumSquares;

            float x1 = first_numclusters;
            float y1 = first_sumsqr;

            float x2 = last_numclusters;
            float y2 = last_sumsqr;

            // Calculate perpendicular distance using line equation
            float numerator = (y2 - y1) * x - (x2 - x1) * y + (x2 * y1 - y2 * x1);
            float distance = Math.Abs(numerator);

            return distance;
        }

        #endregion
        #region Private Methods - kmeans

        private static Cluster_Building<T>[] GetInitialClusters<T>(IList<Sample<T>> data, int num_clusters)
        {
            var retVal = new Cluster_Building<T>[num_clusters];

            int[] sample_indices = UtilityCore.RandomRange(0, data.Count, num_clusters).ToArray();      // this return unique random indices.  DoClustering already made sure num_clusters <= data.Count

            for (int i = 0; i < num_clusters; i++)
            {
                retVal[i] = new Cluster_Building<T>
                {
                    Center = data[sample_indices[i]].Vector,
                };
            }

            return retVal.ToArray();
        }

        private static bool Cluster_Step<T>(Cluster_Building<T>[] clusters, IList<Sample<T>> data, int num_clusters, float[] weights)
        {
            var clusters_step = new Cluster_Building<T>[num_clusters];

            for (int i = 0; i < num_clusters; i++)
                clusters_step[i] = new Cluster_Building<T>
                {
                    Center = clusters[i].Center,
                };

            // Assign each data point to the nearest cluster
            foreach (var sample in data)
                AddToNearestCluster(sample, clusters_step, num_clusters, weights);

            // Update clusters's centers and items according to what's in clusters_step
            bool changed = false;
            for (int i = 0; i < num_clusters; i++)
                changed |= UpdateClusterCenters(clusters, clusters_step, i, weights);

            return changed;
        }

        /// <summary>
        /// Adds each sample to the best node in clusters_step
        /// </summary>
        private static void AddToNearestCluster<T>(Sample<T> sample, Cluster_Building<T>[] clusters_step, int num_clusters, float[] weights)
        {
            int nearest_index = 0;
            float min_distSqr = DistanceSqr(sample.Vector, clusters_step[0].Center, weights);

            for (int i = 1; i < num_clusters; i++)
            {
                float distSqr = DistanceSqr(sample.Vector, clusters_step[i].Center, weights);
                if (distSqr < min_distSqr)
                {
                    min_distSqr = distSqr;
                    nearest_index = i;
                }
            }

            clusters_step[nearest_index].Items.Add((sample, min_distSqr));
        }

        /// <summary>
        /// This sets clusters[index].center and makes items same as clusters_step[index].item
        /// also returns true if center moved
        /// </summary>
        private static bool UpdateClusterCenters<T>(Cluster_Building<T>[] clusters, Cluster_Building<T>[] clusters_step, int index, float[] weights)
        {
            clusters[index].Items.Clear();
            clusters[index].Items.AddRange(clusters_step[index].Items);

            if (clusters_step[index].Items.Count == 0)
                return false;

            // Figure out the center of this cluster
            float[] new_center = new float[clusters[index].Center.Length];
            for (int i = 0; i < new_center.Length; i++)
            {
                new_center[i] = 0f;
                foreach (var sample in clusters_step[index].Items)
                    //new_center[i] += sample.item.Vector[i] * weights[i];
                    new_center[i] += sample.item.Vector[i];     // the weight is already considered when building this dimension of vector, so no need to apply it here

                new_center[i] /= clusters_step[index].Items.Count;
            }

            // See if that cluster differs from the existing
            if (!ArraysEqual(new_center, clusters[index].Center))
            {
                clusters[index].Center = new_center;
                return true;
            }

            return false;
        }

        private static Cluster<T>[] BuildFinalReturn<T>(Cluster_Building<T>[] clusters)
        {
            var retVal = new Cluster<T>[clusters.Length];

            for (int i = 0; i < clusters.Length; i++)
            {
                Sample<T>[] items = new Sample<T>[clusters[i].Items.Count];
                float[] item_distSqr_fromcenter = new float[clusters[i].Items.Count];

                for (int j = 0; j < items.Length; j++)
                {
                    items[j] = clusters[i].Items[j].item;
                    item_distSqr_fromcenter[j] = clusters[i].Items[j].distSqr;
                }

                retVal[i] = new Cluster<T>
                {
                    Center = clusters[i].Center,
                    Items = items,
                    Item_DistSqr_FromCenter = item_distSqr_fromcenter,
                };
            }

            return retVal;
        }

        private static Cluster<T> SortCluster<T>(Cluster<T> cluster)
        {
            // Ensure the arrays are valid
            if (cluster.Items == null || cluster.Item_DistSqr_FromCenter == null || cluster.Items.Length != cluster.Item_DistSqr_FromCenter.Length)
                throw new InvalidOperationException("Items and distances must be non-null and the same length");

            var retVal = new Cluster<T>()
            {
                Center = cluster.Center,
            };

            int n = cluster.Items.Length;

            // Generate array of indices
            int[] indices = new int[n];
            for (int i = 0; i < n; i++)
                indices[i] = i;

            // Sort indices based on the corresponding distance values
            Array.Sort(indices, (a, b) => cluster.Item_DistSqr_FromCenter[a].CompareTo(cluster.Item_DistSqr_FromCenter[b]));

            // Create new sorted arrays
            Sample<T>[] sortedItems = new Sample<T>[n];
            float[] sortedDistances = new float[n];

            for (int i = 0; i < n; i++)
            {
                sortedItems[i] = cluster.Items[indices[i]];
                sortedDistances[i] = cluster.Item_DistSqr_FromCenter[indices[i]];
            }

            // Store the sorted arrays
            retVal.Items = sortedItems;
            retVal.Item_DistSqr_FromCenter = sortedDistances;

            return retVal;
        }

        private static float DistanceSqr(float[] a, float[] b, float[] weights)
        {
            float sum = 0f;

            for (int i = 0; i < a.Length; i++)
                sum += (a[i] - b[i]) * (a[i] - b[i]) * weights[i]; // Apply weight to each dimension

            return sum;
        }

        private static bool ArraysEqual(float[] a, float[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
                if (!a[i].IsNearValue(b[i]))
                    return false;

            return true;
        }

        #endregion
    }
}
