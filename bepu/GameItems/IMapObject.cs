using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace GameItems
{
    public interface IMapObject : IComparable<IMapObject>, IEquatable<IMapObject>        // see MapObjectUtil for compares
    {
        long Token { get; }

        bool IsDisposed { get; }

        int BodyHandle { get; }
        BodyReference Body { get; }

        Vector3 PositionWorld { get; }
        Vector3 VelocityWorld { get; }
        Vector3 AngularVelocityWorld { get; }

        /// <summary>
        /// This is the bounding sphere, or rough size of the object
        /// </summary>
        float Radius { get; }

        /// <summary>
        /// This can be helpful if there are too many objects, and old ones need to be cleared
        /// NOTE: This is DateTime.UtcNow
        /// </summary>
        /// <remarks>
        /// The physics world could be running at a different rate than real time, so this should only be used when
        /// calculating the age from the human's perspective
        /// </remarks>
        DateTime CreationTime { get; }
    }

    #region class: MapObjectUtil

    public static class MapObjectUtil
    {
        // These are helper methods for objects that implement IMapObject
        public static int CompareToT(IMapObject thisObject, IMapObject other)
        {
#if DEBUG
            if (thisObject == null)
            {
                throw new ArgumentException("thisObject should never be null");
            }
#endif

            if (other == null)
            {
                // Nonnull is greater than null
                return 1;
            }

            if (thisObject.Token < other.Token)
            {
                return -1;
            }
            else if (thisObject.Token > other.Token)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
        public static bool EqualsT(IMapObject thisObject, IMapObject other)
        {
#if DEBUG
            if (thisObject == null)
            {
                throw new ArgumentException("thisObject should never be null");
            }
#endif

            if (other == null)
            {
                // Nonnull is greater than null
                return false;
            }
            else
            {
                return thisObject.Token == other.Token;
            }
        }
        public static bool EqualsObj(IMapObject thisObject, object obj)
        {
#if DEBUG
            if (thisObject == null)
            {
                throw new ArgumentException("thisObject should never be null");
            }
#endif

            if (obj is IMapObject other)
            {
                return thisObject.Token == other.Token;
            }
            else
            {
                return false;
            }
        }
        public static int GetHashCode(IMapObject thisObject)
        {
#if DEBUG
            if (thisObject == null)
            {
                throw new ArgumentException("thisObject should never be null");
            }
#endif

            //http://blogs.msdn.com/b/ericlippert/archive/2011/02/28/guidelines-and-rules-for-gethashcode.aspx
            //http://stackoverflow.com/questions/13837774/gethashcode-and-buckets
            //http://stackoverflow.com/questions/858904/can-i-convert-long-to-int

            //return (int)thisObject.Token;
            //return unchecked((int)thisObject.Token);
            return thisObject.Token.GetHashCode();      //after much reading about GetHashCode, this most obvious solution should be the best :)
        }
    }

    #endregion
}
