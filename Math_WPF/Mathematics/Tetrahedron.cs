using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Math_WPF.Mathematics
{
    #region enum: TetrahedronEdge

    public enum TetrahedronEdge
    {
        Edge_01,
        Edge_02,
        Edge_03,
        Edge_12,
        Edge_13,
        Edge_23,
    }

    #endregion
    #region enum: TetrahedronFace

    public enum TetrahedronFace
    {
        Face_012,
        Face_023,
        Face_031,
        Face_132,
    }

    #endregion
}
