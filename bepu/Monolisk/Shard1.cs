using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Game.Bepu.Monolisk
{
    // This first attempt will only be ports

    #region class: ShardMap1

    public class ShardMap1
    {
        public ShardBackgroundType1 Background { get; set; }

        // For this first version, just use a fixed size 2D array
        public ShardTile1[][] Tiles { get; set; }

        public IEnumerable<VectorInt2> EnumerateIndices()
        {
            for (int y = 0; y < Tiles.Length; y++)
            {
                for (int x = 0; x < Tiles[y].Length; x++)
                {
                    yield return new VectorInt2(x, y);
                }
            }
        }
    }

    #endregion
    #region class: ShardTile1

    public class ShardTile1
    {
        public ShardGroundType1 GroundType { get; set; }

        public ShardItem1 Item { get; set; }
    }

    #endregion
    #region class: ShardItem1

    public class ShardItem1
    {
        public ShardItemType1 ItemType { get; set; }
        public ShardAngle1 Angle { get; set; }
        public double AngleDbl
        {
            get
            {
                switch (Angle)
                {
                    case ShardAngle1._0:
                        return 0d;

                    case ShardAngle1._45:
                        return 45d;

                    case ShardAngle1._90:
                        return 90d;

                    case ShardAngle1._135:
                        return 135d;

                    case ShardAngle1._180:
                        return 180d;

                    case ShardAngle1._225:
                        return 225d;

                    case ShardAngle1._270:
                        return 270d;

                    case ShardAngle1._315:
                        return 315d;

                    default:
                        throw new ApplicationException($"Unknown {nameof(ShardAngle1)}: {Angle}");
                }
            }
        }
    }

    #endregion

    #region enum: ShardBackgroundType1

    // They all seem to be top-bottom linear gradient
    public enum ShardBackgroundType1
    {
        // Cemetery - green background
        // Cathedral
        // Abyss

        // Marsh - green background
        // Cavern
        // Zikkurat

        // Mountain
        // Army Camp
        // Ice Cave

        // Desert
        // Prison
        // Pyramid

        // Port - darkish greeen background
        // Lab - purple
        // Palace

        Port
    }

    #endregion

    #region enum: ShardGroundType1

    public enum ShardGroundType1
    {
        // do you use explicit ones for each map, or try to make them more generic?
        // explicit would give a chance to make a rainbow shard, but for v1, it's all port

        Cement
    }

    #endregion

    #region enum: ShardItemType1

    public enum ShardItemType1
    {
        StartLocation,
        EndGate,
    }

    #endregion
    #region enum: ShardAngle1

    public enum ShardAngle1
    {
        _0,
        _45,
        _90,
        _135,
        _180,
        _225,
        _270,
        _315,
    }

    #endregion
}
