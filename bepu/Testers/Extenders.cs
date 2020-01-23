using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Bepu.Testers
{
    //TODO: Put this in the lowest dll that references bepu
    public static class Extenders
    {
        #region Quaternion - sys.numerics

        public static BepuUtilities.Quaternion ToQuat_bepu(this System.Numerics.Quaternion quaternion)
        {
            return new BepuUtilities.Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
        }

        #endregion

        #region Quaternion - wpf

        public static BepuUtilities.Quaternion ToQuat_bepu(this System.Windows.Media.Media3D.Quaternion quaternion)
        {
            return new BepuUtilities.Quaternion((float)quaternion.X, (float)quaternion.Y, (float)quaternion.Z, (float)quaternion.W);
        }

        #endregion

        #region Quaternion - bepu

        public static System.Windows.Media.Media3D.Quaternion ToQuaternion_wpf(this BepuUtilities.Quaternion quaternion)
        {
            return new System.Windows.Media.Media3D.Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
        }

        public static System.Numerics.Quaternion ToQuat_numerics(this BepuUtilities.Quaternion quaternion)
        {
            return new System.Numerics.Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
        }

        #endregion
    }
}
