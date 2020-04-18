using BepuPhysics;
using BepuPhysics.Collidables;
using Game.Core;
using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;

namespace GameItems.MapParts
{
    /// <summary>
    /// This is meant to be simple, to test integration between swarmbot logic and a bepu body
    /// </summary>
    public class SwarmBot2a : IMapObject, IPartUpdatable
    {
        #region Declaration Section

        private readonly Map _map;

        #endregion

        #region Constructor

        public SwarmBot2a(int bodyHandle, BodyReference body, IShape shape, Map map)
        {
            BodyHandle = bodyHandle;
            Body = body;

            _map = map;

            Token = TokenGenerator.NextToken();

            Radius = UtilityBepu.GetRadius(shape);

            CreationTime = DateTime.UtcNow;
        }

        #endregion

        #region IMapObject members

        public long Token { get; }

        public bool IsDisposed => false;

        public int BodyHandle { get; }
        public BodyReference Body { get; }

        public Vector3 PositionWorld => Body.Pose.Position;
        public Vector3 VelocityWorld => Body.Velocity.Linear;
        public Vector3 AngularVelocityWorld => Body.Velocity.Angular;

        public float Radius { get; }

        public DateTime CreationTime { get; }

        public int CompareTo([AllowNull] IMapObject other)
        {
            return MapObjectUtil.CompareToT(this, other);
        }
        public bool Equals([AllowNull] IMapObject other)
        {
            return MapObjectUtil.EqualsT(this, other);
        }
        public override bool Equals(object obj)
        {
            return MapObjectUtil.EqualsObj(this, obj);
        }
        public override int GetHashCode()
        {
            return MapObjectUtil.GetHashCode(this);
        }

        #endregion
        #region IPartUpdatable members

        public void Update_MainThread(double elapsedTime)
        {
        }
        public void Update_AnyThread(double elapsedTime)
        {
            // See what's around
            //MapOctree snapshot = _map.LatestSnapshot;
            //if (snapshot == null)
            //{
            //    _linearAccel = new Vector3();
            //    return;
            //}

            Vector3 position = PositionWorld;

            // Find stuff
            //TODO: This should also call the delegate that figures out friend or foe, which could change the number of actual neighbors to pay attention to:
            //      pay attention to any non friends within a certain radius
            //      only pay attention to the closest X friends within that same radius
            //var neighbors = GetNeighbors(snapshot, position);

            //if (neighbors.Length == 0)
            //{
            //    _linearAccel = GetSeekForce();
            //    return;
            //}

            //_linearAccel = GetSwarmForce(neighbors, position, VelocityWorld);
        }

        public int? IntervalSkips_MainThread => null;
        public int? IntervalSkips_AnyThread => 0;

        #endregion

        private volatile object _linearAccel = new Vector3();
        public Vector3 LinearAccel => (Vector3)_linearAccel;

        // some kind of delegate to classify neighbors
        //      chase vs avoid and by how much weight



    }
}
