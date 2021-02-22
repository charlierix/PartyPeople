using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media.Media3D;

namespace Game.Bepu.Testers
{
    [Serializable]
    public class Vec3
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }

        public static Point3D ToPoint(Vec3 v)
        {
            return new Point3D(v.x, v.y, v.z);
        }
        public static Vector3D ToVector(Vec3 v)
        {
            return new Vector3D(v.x, v.y, v.z);
        }
    }

    [Serializable]
    public class Vec4
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
        public float w { get; set; }

        public static Quaternion ToQuat(Vec4 v)
        {
            return new Quaternion(v.x, v.y, v.z, v.w);
        }
    }

    [Serializable]
    public class Plane3
    {
        public Vec3 pos { get; set; }
        public Vec3 norm { get; set; }
    }
}
