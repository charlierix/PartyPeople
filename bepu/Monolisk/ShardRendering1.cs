using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Monolisk
{
    public static class ShardRendering1
    {
        #region Declaration Section

        public const int SIZE = 48;
        public const int HALFSIZE = SIZE / 2;

        private const double TILE_Z = .06;

        #endregion

        public static (Point min, Point max, Point center) GetTilePos(int x, int y)
        {
            int offsetX = x - ShardRendering1.HALFSIZE;
            int offsetY = y - ShardRendering1.HALFSIZE;

            return
            (
                new Point(offsetX, offsetY),
                new Point(offsetX + 1, offsetY + 1),
                new Point(offsetX + .5, offsetY + .5)
            );
        }
        public static VectorInt2 GetTileIndex(Point pos)
        {
            int x = (pos.X + ShardRendering1.HALFSIZE).ToInt_Floor();
            int y = (pos.Y + ShardRendering1.HALFSIZE).ToInt_Floor();

            return new VectorInt2(x, y);
        }

        public static ShardVisuals1 LoadShard(ShardMap1 shard)
        {
            ShardVisuals1 retVal = new ShardVisuals1()
            {
                Shard = shard,
                TileGroup = new Model3DGroup(),
                Tiles = new Model3D[shard.Tiles.Length, shard.Tiles.Length],        // it's square
                ItemsGroup = new Model3DGroup(),
                Items = new Model3D[shard.Tiles.Length, shard.Tiles.Length],        // it's square
            };

            for (int x = 0; x < shard.Tiles.Length; x++)
            {
                for (int y = 0; y < shard.Tiles.Length; y++)
                {
                    if (shard.Tiles[y][x] != null)
                    {
                        var index = new VectorInt2(x, y);

                        AddTileGraphic(index, shard.Tiles[y][x].GroundType, retVal.TileGroup, retVal.Tiles);

                        if (shard.Tiles[y][x].Item != null)
                        {
                            AddItemGraphic(index, shard.Tiles[y][x].Item, retVal.ItemsGroup, retVal.Items);
                        }
                    }
                }
            }

            Model3DGroup finalGroup = new Model3DGroup();
            finalGroup.Children.Add(retVal.TileGroup);
            finalGroup.Children.Add(retVal.ItemsGroup);

            retVal.Visual = new ModelVisual3D
            {
                Content = finalGroup,
            };

            return retVal;
        }

        public static void RemoveTileGraphic(VectorInt2 index, Model3DGroup group, Model3D[,] models)
        {
            group.Children.Remove(models[index.X, index.Y]);
            models[index.X, index.Y] = null;
        }
        public static void RemoveItemGraphic(VectorInt2 index, Model3DGroup group, Model3D[,] models)
        {
            group.Children.Remove(models[index.X, index.Y]);
            models[index.X, index.Y] = null;
        }

        public static void AddTileGraphic(VectorInt2 index, ShardGroundType1 type, Model3DGroup group, Model3D[,] models)
        {
            if (models[index.X, index.Y] != null)
            {
                RemoveTileGraphic(index, group, models);
            }

            switch (type)
            {
                case ShardGroundType1.Cement:
                    AddTileGraphic_Cement(index, group, models);
                    break;

                default:
                    throw new ApplicationException($"Unknown {nameof(ShardGroundType1)}: {type}");
            }
        }
        public static void AddItemGraphic(VectorInt2 index, ShardItem1 item, Model3DGroup group, Model3D[,] models)
        {
            if (models[index.X, index.Y] != null)
            {
                RemoveItemGraphic(index, group, models);
            }

            //TODO: For start and end location, remove any other instance of that
            //TODO: For other item types, only add if count isn't exceeded

            switch (item.ItemType)
            {
                case ShardItemType1.StartLocation:
                    AddItemGraphic_StartLocation(index, group, models);
                    break;

                case ShardItemType1.EndGate:
                    AddItemGraphic_EndGate(index, group, models);
                    break;

                default:
                    throw new ApplicationException($"Unknown {nameof(ShardItemType1)}: {item.ItemType}");
            }


            //TODO: Apply rotation


        }

        #region Private Methods

        private static void AddTileGraphic_Cement(VectorInt2 index, Model3DGroup group, Model3D[,] models)
        {
            // diffuse: 777
            // specular: 30602085, 3

            var pos = GetTilePos(index.X, index.Y);

            Random rand = StaticRandom.GetRandomForThread();

            double tileZ = rand.NextPercent(TILE_Z, .12);

            Model3DGroup tileGroup = new Model3DGroup();

            #region top

            ColorHSV diffuse = new ColorHSV(0, 0, rand.Next(50 - 5, 50 + 5));
            ColorHSV spec = new ColorHSV(36, rand.Next(278 - 5, 278 + 5), rand.Next(76 - 10, 76 + 10), rand.Next(52 - 5, 52 + 5));

            MaterialGroup material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(diffuse.ToRGB())));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(spec.ToRGB()), 3));

            tileGroup.Children.Add(new GeometryModel3D
            {
                Material = material,
                BackMaterial = material,
                Geometry = UtilityWPF.GetSquare2D(pos.min, pos.max, tileZ),
            });

            #endregion

            #region sides

            diffuse = new ColorHSV(0, 0, rand.Next(43 - 5, 43 + 5));
            spec = new ColorHSV(24, rand.Next(278 - 5, 278 + 5), rand.Next(76 - 10, 76 + 10), rand.Next(52 - 5, 52 + 5));

            material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(diffuse.ToRGB())));
            material.Children.Add(new SpecularMaterial(new SolidColorBrush(spec.ToRGB()), 3));

            tileGroup.Children.Add(new GeometryModel3D
            {
                Material = material,
                BackMaterial = material,
                Geometry = GetCubeSides(pos.min, pos.max, tileZ),
            });

            #endregion

            models[index.X, index.Y] = tileGroup;
            group.Children.Add(models[index.X, index.Y]);
        }

        private static void AddItemGraphic_StartLocation(VectorInt2 index, Model3DGroup group, Model3D[,] models)
        {
            var pos = GetTilePos(index.X, index.Y);

            MaterialGroup material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(UtilityWPF.BrushFromHex("555")));
            material.Children.Add(new SpecularMaterial(UtilityWPF.BrushFromHex("40EEEEEE"), 1.5));

            GeometryModel3D model = new GeometryModel3D
            {
                Material = material,
                BackMaterial = material,
                Geometry = UtilityWPF.GetCylinder_AlongX(8, .4, TILE_Z, new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), -90d))),
                Transform = new TranslateTransform3D(pos.center.X, pos.center.Y, TILE_Z * 1.5),     //TODO: expose a porition of this transform so the item can be rotated
            };

            models[index.X, index.Y] = model;
            group.Children.Add(models[index.X, index.Y]);
        }
        private static void AddItemGraphic_EndGate(VectorInt2 index, Model3DGroup group, Model3D[,] models)
        {
            // This is a good start, but the endcaps cover the entire face
            //UtilityWPF.GetMultiRingedTube

            // This works, but an ellipse is needed
            //Debug3DWindow.GetCircle


            // Also, the interior of the ring should come to an edge instead of flat.  So what is needed is a squared ring that's black,
            // an inner ring that's a triangle (like st louis arch), and if you want to get fancy, an exterior ring


            var pos = GetTilePos(index.X, index.Y);

            MaterialGroup material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(UtilityWPF.BrushFromHex("4C8FF5")));
            material.Children.Add(new SpecularMaterial(UtilityWPF.BrushFromHex("A08B49EB"), 1));
            material.Children.Add(new EmissiveMaterial(UtilityWPF.BrushFromHex("4049B4EB")));

            var rings = new List<TubeRingBase>();
            rings.Add(new TubeRingRegularPolygon(0, false, .7, 1.2, true));
            rings.Add(new TubeRingRegularPolygon(.1, false, .7, 1.2, true));

            Transform3DGroup transform = new Transform3DGroup();
            transform.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), 90)));
            transform.Children.Add(new TranslateTransform3D(pos.center.X, pos.center.Y, 1));

            GeometryModel3D model = new GeometryModel3D
            {
                Material = material,
                BackMaterial = material,

                Geometry = UtilityWPF.GetMultiRingedTube(24, rings, true, true, transform),
            };

            models[index.X, index.Y] = model;
            group.Children.Add(models[index.X, index.Y]);
        }

        private static MeshGeometry3D GetCubeSides(Point min, Point max, double topZ)
        {
            // Copied from UtilityWPF.GetCube_IndependentFaces

            // Define 3D mesh object
            MeshGeometry3D retVal = new MeshGeometry3D();

            // Front face
            //retVal.Positions.Add(new Point3D(min.X, min.Y, topZ - 1));		// 0
            //retVal.Positions.Add(new Point3D(max.X, min.Y, topZ - 1));		// 1
            //retVal.Positions.Add(new Point3D(max.X, max.Y, topZ - 1));		// 2
            //retVal.Positions.Add(new Point3D(min.X, max.Y, topZ - 1));		// 3
            //retVal.TriangleIndices.Add(0);
            //retVal.TriangleIndices.Add(1);
            //retVal.TriangleIndices.Add(2);
            //retVal.TriangleIndices.Add(2);
            //retVal.TriangleIndices.Add(3);
            //retVal.TriangleIndices.Add(0);

            // Back face
            //retVal.Positions.Add(new Point3D(min.X, min.Y, topZ));		// 4
            //retVal.Positions.Add(new Point3D(max.X, min.Y, topZ));		// 5
            //retVal.Positions.Add(new Point3D(max.X, max.Y, topZ));		// 6
            //retVal.Positions.Add(new Point3D(min.X, max.Y, topZ));		// 7
            //retVal.TriangleIndices.Add(6);
            //retVal.TriangleIndices.Add(5);
            //retVal.TriangleIndices.Add(4);
            //retVal.TriangleIndices.Add(4);
            //retVal.TriangleIndices.Add(7);
            //retVal.TriangleIndices.Add(6);

            // Right face
            retVal.Positions.Add(new Point3D(max.X, min.Y, topZ - 1));		// 1-8
            retVal.Positions.Add(new Point3D(max.X, max.Y, topZ - 1));		// 2-9
            retVal.Positions.Add(new Point3D(max.X, min.Y, topZ));		// 5-10
            retVal.Positions.Add(new Point3D(max.X, max.Y, topZ));		// 6-11
            retVal.TriangleIndices.Add(8 - 8);		// 1
            retVal.TriangleIndices.Add(10 - 8);		// 5
            retVal.TriangleIndices.Add(9 - 8);		// 2
            retVal.TriangleIndices.Add(10 - 8);		// 5
            retVal.TriangleIndices.Add(11 - 8);		// 6
            retVal.TriangleIndices.Add(9 - 8);		// 2

            // Top face
            retVal.Positions.Add(new Point3D(max.X, max.Y, topZ - 1));		// 2-12
            retVal.Positions.Add(new Point3D(min.X, max.Y, topZ - 1));		// 3-13
            retVal.Positions.Add(new Point3D(max.X, max.Y, topZ));		// 6-14
            retVal.Positions.Add(new Point3D(min.X, max.Y, topZ));		// 7-15
            retVal.TriangleIndices.Add(12 - 8);		// 2
            retVal.TriangleIndices.Add(14 - 8);		// 6
            retVal.TriangleIndices.Add(13 - 8);		// 3
            retVal.TriangleIndices.Add(13 - 8);		// 3
            retVal.TriangleIndices.Add(14 - 8);		// 6
            retVal.TriangleIndices.Add(15 - 8);		// 7

            // Bottom face
            retVal.Positions.Add(new Point3D(min.X, min.Y, topZ - 1));		// 0-16
            retVal.Positions.Add(new Point3D(max.X, min.Y, topZ - 1));		// 1-17
            retVal.Positions.Add(new Point3D(min.X, min.Y, topZ));		// 4-18
            retVal.Positions.Add(new Point3D(max.X, min.Y, topZ));		// 5-19
            retVal.TriangleIndices.Add(19 - 8);		// 5
            retVal.TriangleIndices.Add(17 - 8);		// 1
            retVal.TriangleIndices.Add(16 - 8);		// 0
            retVal.TriangleIndices.Add(16 - 8);		// 0
            retVal.TriangleIndices.Add(18 - 8);		// 4
            retVal.TriangleIndices.Add(19 - 8);		// 5

            // Right face
            retVal.Positions.Add(new Point3D(min.X, min.Y, topZ - 1));		// 0-20
            retVal.Positions.Add(new Point3D(min.X, max.Y, topZ - 1));		// 3-21
            retVal.Positions.Add(new Point3D(min.X, min.Y, topZ));		// 4-22
            retVal.Positions.Add(new Point3D(min.X, max.Y, topZ));		// 7-23
            retVal.TriangleIndices.Add(22 - 8);		// 4
            retVal.TriangleIndices.Add(20 - 8);		// 0
            retVal.TriangleIndices.Add(21 - 8);		// 3
            retVal.TriangleIndices.Add(21 - 8);		// 3
            retVal.TriangleIndices.Add(23 - 8);		// 7
            retVal.TriangleIndices.Add(22 - 8);		// 4

            // shouldn't I set normals?
            //retVal.Normals

            //retVal.Freeze();
            return retVal;
        }

        #endregion
    }

    #region class: ShardVisuals

    public class ShardVisuals1
    {
        public ShardMap1 Shard { get; set; }

        public Model3DGroup TileGroup { get; set; }
        public Model3D[,] Tiles { get; set; }

        public Model3DGroup ItemsGroup { get; set; }
        public Model3D[,] Items { get; set; }

        public Visual3D Visual { get; set; }
    }

    #endregion
}
