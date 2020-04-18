using BepuPhysics.Collidables;
using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameItems
{
    public static class UtilityBepu
    {
        /// <summary>
        /// This returns the radius of the sphere that surrounds the shape
        /// </summary>
        public static float GetRadius(IShape shape)
        {
            if (shape is Sphere sphere)
            {
                return sphere.Radius;
            }
            else if (shape is Box box)
            {
                return (float)Math.Sqrt(Math3D.LengthSquared(0, 0, 0, box.HalfHeight, box.HalfLength, box.HalfWidth));
            }
            else
            {
                throw new ArgumentException($"Unknown shape type: {shape.GetType()}");
            }
        }
    }
}
