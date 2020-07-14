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

            Random rand = StaticRandom.GetRandomForThread();

            var materials = GetTileMaterials(type);

            var pos = GetTilePos(index.X, index.Y);

            double tileZ = rand.NextPercent(TILE_Z, .12);

            Model3DGroup tileGroup = new Model3DGroup();

            // Top
            tileGroup.Children.Add(new GeometryModel3D
            {
                Material = materials.top,
                BackMaterial = materials.top,
                Geometry = UtilityWPF.GetSquare2D(pos.min, pos.max, tileZ),
            });

            // Sides
            tileGroup.Children.Add(new GeometryModel3D
            {
                Material = materials.sides,
                BackMaterial = materials.sides,
                Geometry = GetCubeSides(pos.min, pos.max, tileZ),
            });

            models[index.X, index.Y] = tileGroup;
            group.Children.Add(models[index.X, index.Y]);
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

        private static (Material top, Material sides) GetTileMaterials(ShardGroundType1 type)
        {
            //NOTE: the ground type enum is meant to be generic and will control more than just color (physics props, procedural decoration, etc)
            //specific colors would need to be a combination of map type and ground type (water would look different between port and swamp)

            //NOTE: Water's specular should be tied pretty closely with the map's sky color


            //TODO: Create baseline colors for most of these, then apply map type color settings over the defaults


            Random rand = StaticRandom.GetRandomForThread();

            ColorHSV diffuseTop, specTop, diffuseSide, specSide;
            double powTop, powSide;

            switch (type)
            {
                case ShardGroundType1.Cement:
                    //diffuseTop = new ColorHSV(0, 0, rand.Next(50 - 5, 50 + 5));
                    //specTop = new ColorHSV(36, rand.Next(278 - 5, 278 + 5), rand.Next(76 - 10, 76 + 10), rand.Next(52 - 5, 52 + 5));

                    //diffuseSide = new ColorHSV(0, 0, rand.Next(43 - 5, 43 + 5));
                    //specSide = new ColorHSV(24, rand.Next(278 - 5, 278 + 5), rand.Next(76 - 10, 76 + 10), rand.Next(52 - 5, 52 + 5));

                    diffuseTop = rand.ColorHSV("808080", 0, 0, 5);
                    //specTop = rand.ColorHSV("24602085", 5, 10, 5);
                    specTop = rand.ColorHSV("24817A85", 5, 10, 5);
                    powTop = 3;

                    diffuseSide = rand.ColorHSV("6E6E6E", 0, 0, 5);
                    //specSide = rand.ColorHSV("18602085", 5, 10, 5);
                    specSide = rand.ColorHSV("18817A85", 5, 10, 5);
                    powSide = 3;
                    break;

                case ShardGroundType1.Brick_large:
                    diffuseTop = rand.ColorHSV("707070", 0, 0, 5);
                    specTop = rand.ColorHSV("24817A85", 5, 10, 5);
                    powTop = 3;

                    diffuseSide = rand.ColorHSV("525252", 0, 0, 5);
                    specSide = rand.ColorHSV("18817A85", 5, 10, 5);
                    powSide = 3;
                    break;

                case ShardGroundType1.Brick_small:
                    diffuseTop = rand.ColorHSV("606060", 0, 0, 5);
                    specTop = rand.ColorHSV("24817A85", 5, 10, 5);
                    powTop = 3;

                    diffuseSide = rand.ColorHSV("444", 0, 0, 5);
                    specSide = rand.ColorHSV("18817A85", 5, 10, 5);
                    powSide = 3;
                    break;

                case ShardGroundType1.Water_deep:
                    diffuseTop = rand.ColorHSV("1E3A3B", 10, 15, 2);
                    specTop = rand.ColorHSV("40A3945B", 5, 10, 5);
                    powTop = 4;

                    diffuseSide = rand.ColorHSV("042326", 10, 20, 3);
                    specSide = rand.ColorHSV("307D7146", 5, 10, 5);
                    powSide = 4;
                    break;

                case ShardGroundType1.Water_shallow:
                    diffuseTop = rand.ColorHSV("1E313B", 10, 15, 2);
                    specTop = rand.ColorHSV("60F0ECDD", 5, 3, 1);
                    powTop = 2;

                    diffuseSide = rand.ColorHSV("324C4C", 10, 10, 2);
                    specSide = rand.ColorHSV("60ABA591", 7, 5, 8);
                    powSide = 6;
                    break;

                case ShardGroundType1.Dirt:
                    diffuseTop = rand.ColorHSV("4f4540", 15, 5, 2);
                    specTop = rand.ColorHSV("30404040", 0, 0, 5);
                    powTop = .5;

                    diffuseSide = rand.ColorHSV("3D3532", 10, 10, 2);
                    specSide = rand.ColorHSV("20404040", 0, 0, 3);
                    powSide = .5;
                    break;

                case ShardGroundType1.Sand:
                    diffuseTop = rand.ColorHSV("A1998F", 5, 4, 8);
                    specTop = rand.ColorHSV("40808080", 0, 0, 5);
                    powTop = 1;

                    diffuseSide = rand.ColorHSV("878078", 5, 4, 8);
                    specSide = rand.ColorHSV("38707070", 0, 0, 5);
                    powSide = .5;
                    break;

                case ShardGroundType1.Rocks:
                    diffuseTop = rand.ColorHSV("8C8984", 20, 4, 18);
                    specTop = rand.ColorHSV("A0706E5A", 20, 12, 3, 20);
                    powTop = 5;

                    diffuseSide = rand.ColorHSV("73706C", 20, 2, 4);
                    specSide = rand.ColorHSV("18807E69", 20, 5, 5);
                    powSide = 2;
                    break;

                case ShardGroundType1.Snow:
                    diffuseTop = rand.ColorHSV("F0F0F0", 0, 0, 3);
                    specTop = rand.ColorHSV("70FFFFFF", 0, 0, 12, 10);
                    powTop = 8;

                    diffuseSide = rand.ColorHSV("E8E8E8", 0, 1, 4);
                    specSide = rand.ColorHSV("90909090", 0, 0, 4);
                    powSide = 4;
                    break;

                case ShardGroundType1.Ice:
                    diffuseTop = rand.ColorHSV("C4D2DC", 0, 3, 5);
                    specTop = rand.ColorHSV("B0BED9DE", 5, 2, 3);
                    powTop = 12;

                    diffuseSide = rand.ColorHSV("BCCAD4", 0, 1, 4);
                    specSide = rand.ColorHSV("60BED9DE", 0, 0, 4);
                    powSide = 6;
                    break;

                case ShardGroundType1.Wood_tight:
                case ShardGroundType1.Wood_loose:
                    diffuseTop = rand.ColorHSV("9D987D", 2, 4, 5);
                    specTop = rand.ColorHSV("30262626", 0, 2, 3);
                    powTop = 2;

                    diffuseSide = rand.ColorHSV("8C8870", 2, 4, 5);
                    specSide = rand.ColorHSV("20262626", 0, 2, 3);
                    powSide = 1;
                    break;

                case ShardGroundType1.Tile:
                    diffuseTop = rand.ColorHSV("53455A", 6, 12, 3);
                    specTop = rand.ColorHSV("80B1AFC9", 0, 2, 3);
                    powTop = 15;

                    diffuseSide = rand.ColorHSV("4B3F52", 0, 0, 2);
                    specSide = rand.ColorHSV("80B1AFC9", 0, 2, 3);
                    powSide = 30;
                    break;

                default:
                    throw new ApplicationException($"Unknown {nameof(ShardGroundType1)}: {type}");
            }

            MaterialGroup materialTop = new MaterialGroup();
            materialTop.Children.Add(new DiffuseMaterial(new SolidColorBrush(diffuseTop.ToRGB())));
            materialTop.Children.Add(new SpecularMaterial(new SolidColorBrush(specTop.ToRGB()), powTop));

            MaterialGroup materialSide = new MaterialGroup();
            materialSide.Children.Add(new DiffuseMaterial(new SolidColorBrush(diffuseSide.ToRGB())));
            materialSide.Children.Add(new SpecularMaterial(new SolidColorBrush(specSide.ToRGB()), powSide));

            return (materialTop, materialSide);
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
