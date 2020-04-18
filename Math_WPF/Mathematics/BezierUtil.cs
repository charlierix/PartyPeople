using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Math_WPF.Mathematics
{
    public static partial class BezierUtil
    {
        #region Private Methods

        private static IEnumerable<(int index0, int index1, int index2)> IterateTrianglePoints(int countHorz, int countVert)
        {
            for (int h = 0; h < countHorz - 1; h += 2)
            {
                int offsetH_0 = h * countVert;
                int offsetH_center = (h + 1) * countVert;
                int offsetH_1 = (h + 2) * countVert;

                for (int v = 0; v < countVert - 1; v += 2)
                {
                    // Left
                    yield return
                    (
                        offsetH_0 + v,
                        offsetH_0 + v + 2,
                        offsetH_center + v + 1
                    );

                    // Bottom
                    yield return
                    (
                        offsetH_0 + v + 2,
                        offsetH_1 + v + 2,
                        offsetH_center + v + 1
                    );

                    // Right
                    yield return
                    (
                        offsetH_1 + v + 2,
                        offsetH_1 + v,
                        offsetH_center + v + 1
                    );

                    // Top
                    yield return
                    (
                        offsetH_1 + v,
                        offsetH_0 + v,
                        offsetH_center + v + 1
                    );
                }
            }
        }

        #endregion
    }
}
