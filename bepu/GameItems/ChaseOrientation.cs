using Game.Core;
using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;

namespace GameItems
{
    #region class: ChaseOrientation_Velocity

    /// <summary>
    /// This chases an orientation
    /// NOTE: Only a single vector is chased (not a double vector).  So the body can still spin about the axis of the vector being chased
    /// </summary>
    public class ChaseOrientation_Velocity
    {
        #region Declaration Section

        private readonly Vector3D _initialDirectionLocal;

        private Vector3D? _desiredOrientation = null;

        #endregion

        #region Constructor

        /// <param name="orientation">This is how the item is oriented initially (will probably just be identity)</param>
        /// <param name="directionWorld">
        /// This is the default direction.  Think of it like a lever.  When a different direction is passed into
        /// SetOrientation(), the orientation will be changed so that this direction aligns with the new direction
        /// </param>
        public ChaseOrientation_Velocity(Quaternion orientation, Vector3D directionWorld)
        {
            Orientation = orientation;

            _initialDirectionLocal = orientation.FromWorld(directionWorld);
        }

        #endregion

        #region Public Properties

        public Quaternion Orientation { get; set; }

        public double Multiplier { get; set; } = 6d;

        /// <summary>
        /// This is degrees per second
        /// </summary>
        public double? MaxVelocity { get; set; } //= .5d;

        /// <summary>
        /// This is a safety in case the processor hangs for a bit.  This will keep the item from exploding away
        /// during the next tick
        /// </summary>
        public double MaxElapsedSeconds { get; set; } = .25d;

        #endregion

        #region Public Methods

        public void SetOrientation(Vector3D orientation)
        {
            _desiredOrientation = orientation;
        }
        public void StopChasing()
        {
            _desiredOrientation = null;
        }

        public void Tick(double elapsedSeconds)
        {
            // See if there is anything to do
            if (_desiredOrientation == null)
                return;

            Vector3D desiredLocal = Orientation.FromWorld(_desiredOrientation.Value);

            Quaternion rotation = Math3D.GetRotation(_initialDirectionLocal, desiredLocal);
            if (rotation.IsIdentity)
                return;

            double angle = rotation.Angle * Multiplier;
            if (MaxVelocity != null && angle > MaxVelocity)
                angle = MaxVelocity.Value;

            angle *= Math.Min(elapsedSeconds, MaxElapsedSeconds);

            rotation = new Quaternion(rotation.Axis, angle);

            //Orientation *= rotation;
            Orientation = Orientation.RotateBy(rotation);       // this makes sure they are unit quaternions
        }

        #endregion
    }

    #endregion

    //TODO: Each tick, this should just calculate torque.  Hook to bepu and let the physics engine turn torque into angular velocity
    #region class: ChaseOrientation_Torques

    /// <summary>
    /// This chases an orientation
    /// </summary>
    public class ChaseOrientation_Torques
    {
        #region Declaration Section

        public readonly Vector3D _initialDirectionLocal;

        /// <summary>
        /// If they are using a spring, this is the point to move to
        /// </summary>
        public Vector3D? _desiredOrientation = null;

        #endregion

        #region Constructor

        /// <param name="orientation">This is how the item is oriented initially (will probably just be identity)</param>
        /// <param name="momentInertia">Inertia of the sphere (solid sphere is 2/5 mr^2)</param>
        /// <param name="directionWorld">
        /// This is the default direction.  Think of it like a lever.  When a different direction is passed into
        /// SetOrientation(), the orientation will be changed so that this direction aligns with the new direction
        /// </param>
        public ChaseOrientation_Torques(Quaternion orientation, double momentInertia, Vector3D directionWorld)
        {
            Orientation = orientation;
            MomentInertia = momentInertia;

            _initialDirectionLocal = orientation.FromWorld(directionWorld);
        }

        #endregion

        #region Public Properties

        public Quaternion Orientation { get; set; }

        /// <summary>
        /// This assumes that the body is some kind of sphere (otherwise, you need an inertia tensor)
        /// </summary>
        /// <remarks>
        /// Solid Sphere: I = 2/5 * m * r^2
        /// Hollow Sphere: I = 2/3 * m * r^2
        /// </remarks>
        public double MomentInertia { get; set; }

        /// <summary>
        /// This is in degrees per second
        /// </summary>
        public Quaternion AngularVelocity { get; set; }

        // If these are populated, then a torque will get reduced if it will exceed one of these
        public double? MaxTorque { get; set; }
        public double? MaxAcceleration { get; set; }

        /// <summary>
        /// This is a safety in case the processor hangs for a bit.  This will keep the item from exploding away
        /// during the next tick
        /// </summary>
        public double MaxElapsedSeconds { get; set; } = .25d;

        /// <summary>
        /// This gives an option of only applying a percent of the full force
        /// </summary>
        /// <remarks>
        /// This way objects can be gradually ramped up to full force (good when first creating an object)
        /// </remarks>
        public double Percent { get; set; } = 1d;

        public ChaseTorque[] Torques { get; set; }

        #endregion

        #region Public Methods

        public void SetOrientation(Vector3D direction)
        {
            _desiredOrientation = direction;
        }
        public void StopChasing()
        {
            _desiredOrientation = null;
        }

        public void Tick(double elapsedSeconds)
        {
            //Quaternion deltaVelocity = GetDeltaVelocity(elapsedSeconds);


            Vector3D? torque = GetTorque();


            //TODO: Account to elapsed time
            // Adjust velocity
            //AngularVelocity *= deltaVelocity;
            //AngularVelocity = deltaVelocity * AngularVelocity;

            // Rotate by velocity
            Orientation = Quaternion.Slerp(Orientation, Orientation.RotateBy(AngularVelocity), Math.Min(elapsedSeconds, MaxElapsedSeconds));
        }

        #endregion

        #region Private Methods

        private Quaternion GetDeltaVelocity_TOLOCAL(double elapsedSeconds)
        {
            if (_desiredOrientation == null)
                return Quaternion.Identity;

            Vector3D desiredLocal = Orientation.FromWorld(_desiredOrientation.Value);

            Quaternion rotation = Math3D.GetRotation(_initialDirectionLocal, desiredLocal);
            if (rotation.IsIdentity)
                return Quaternion.Identity;

            var args = new ChaseOrientation_GetTorqueArgs(MomentInertia, AngularVelocity, rotation);

            Vector3D? torque = null;

            // Call each worker
            foreach (var worker in Torques)
            {
                Vector3D? localForce = worker.GetTorque(args);

                if (localForce == null)
                    continue;

                if (torque == null)
                {
                    torque = localForce;
                }
                else
                {
                    torque = torque.Value + localForce.Value;
                }
            }

            if (torque == null)
                return Quaternion.Identity;

            // Apply the torque

            // Limit if exceeds this.MaxForce
            if (MaxTorque != null && torque.Value.LengthSquared > MaxTorque.Value * MaxTorque.Value)
                torque = torque.Value.ToUnit() * MaxTorque.Value;

            double accel = torque.Value.Length / MomentInertia;

            // Limit acceleration
            if (MaxAcceleration != null && accel > MaxAcceleration.Value)
                accel = MaxAcceleration.Value;

            accel *= Percent;

            //e.Body.AddTorque(torque.Value);


            return new Quaternion(rotation.Axis, accel);
        }
        private Quaternion GetDeltaVelocity(double elapsedSeconds)
        {
            if (_desiredOrientation == null)
                return Quaternion.Identity;

            Quaternion rotation = Math3D.GetRotation(Orientation.ToWorld(_initialDirectionLocal), _desiredOrientation.Value);
            if (rotation.IsIdentity)
                return Quaternion.Identity;

            var args = new ChaseOrientation_GetTorqueArgs(MomentInertia, AngularVelocity, rotation);

            Vector3D? torque = null;

            // Call each worker
            foreach (var worker in Torques)
            {
                Vector3D? localForce = worker.GetTorque(args);

                if (localForce == null)
                    continue;

                if (torque == null)
                {
                    torque = localForce;
                }
                else
                {
                    torque = torque.Value + localForce.Value;
                }
            }

            if (torque == null)
                return Quaternion.Identity;

            // Apply the torque

            // Limit if exceeds this.MaxForce
            if (MaxTorque != null && torque.Value.LengthSquared > MaxTorque.Value * MaxTorque.Value)
                torque = torque.Value.ToUnit() * MaxTorque.Value;

            double accel = torque.Value.Length / MomentInertia;

            // Limit acceleration
            if (MaxAcceleration != null && accel > MaxAcceleration.Value)
                accel = MaxAcceleration.Value;

            accel *= Percent;

            //e.Body.AddTorque(torque.Value);


            // Shouldn't be using rotation's axis, that's just the rotation from current to desired
            // Need to account for the direction that torque is pointing
            //return new Quaternion(Orientation.FromWorld(rotation.Axis), accel);
            //return new Quaternion(Orientation.FromWorld(torque.Value), accel);
            return new Quaternion(torque.Value, accel);

        }

        private Vector3D? GetTorque()
        {
            if (_desiredOrientation == null)
                return null;

            Quaternion rotation = Math3D.GetRotation(Orientation.ToWorld(_initialDirectionLocal), _desiredOrientation.Value);
            if (rotation.IsIdentity)
                return null;

            var args = new ChaseOrientation_GetTorqueArgs(MomentInertia, AngularVelocity, rotation);

            Vector3D? torque = null;

            // Call each worker
            foreach (var worker in Torques)
            {
                Vector3D? localForce = worker.GetTorque(args);

                if (localForce == null)
                    continue;

                if (torque == null)
                {
                    torque = localForce;
                }
                else
                {
                    torque = torque.Value + localForce.Value;
                }
            }

            return torque;
        }


        private static Vector3D? GetDeltaVelocity(Vector3D? torque)
        {
            if (torque == null)
                return null;

            #region work, power

            // I is moment of inertia of body
            // w is angular velocity

            // E rotational = 1/2Iω^2

            // Work = torque*theta

            // Power = torque*angularvelocity

            #endregion

            #region angular momentum

            // angular_momentum = moment_inertial * angular_velocity

            #endregion

            #region angular acceleration

            // torque = moment_inertial * angular_accel
            // angular_accel = torque / moment_inertial

            #endregion

            //https://www.youtube.com/watch?v=Bq9cc7sSm_g

            //https://courses.lumenlearning.com/boundless-physics/chapter/torque-and-angular-acceleration/

            //https://courses.lumenlearning.com/boundless-physics/chapter/vector-nature-of-rotational-kinematics/

            //https://courses.lumenlearning.com/boundless-physics/chapter/problem-solving-2/

            return null;
        }


        #endregion


        /// <summary>
        /// This returns a definition that is pretty good
        /// </summary>
        public static ChaseTorque[] GetStandard()
        {
            var retVal = new List<ChaseTorque>();

            double mult = 6;  //300; //600;

            // Attraction
            GradientEntry[] gradient = new[]
            {
                new GradientEntry(0d, 0d),     // distance, %
                new GradientEntry(10d, 1d),
            };
            retVal.Add(new ChaseTorque(ChaseDirectionType.Attract_Direction, .4 * mult, gradient: gradient));

            // Drag
            gradient = new[]        // this gradient is needed, because there needs to be no drag along the desired axis (otherwise, this drag will fight with the user's desire to rotate the ship)
            {
                new GradientEntry(0d, 0d),     // distance, %
                new GradientEntry(5d, 1d),
            };
            //retVal.Add(new ChaseTorque(ChaseDirectionType.Drag_Velocity_Orth, .0739 * mult, gradient: gradient));

            retVal.Add(new ChaseTorque(ChaseDirectionType.Drag_Velocity_AlongIfVelocityAway, .0408 * mult));

            return retVal.ToArray();
        }


        //private void PhysicsBody_ApplyForceAndTorque(object sender, BodyApplyForceAndTorqueArgs e)
        //{
        //    // See if there is anything to do
        //    if (_desiredOrientation == null)
        //    {
        //        return;
        //    }

        //    //TODO: Offset
        //    Vector3D current = e.Body.DirectionToWorld(new Vector3D(0, 0, 1));
        //    Quaternion rotation = Math3D.GetRotation(current, _desiredOrientation.Value);

        //    if (rotation.IsIdentity)
        //    {
        //        // Don't set anything.  If they are rotating along the allowed axis, then no problem.  If they try
        //        // to rotate off that axis, another iteration of this method will rotate back
        //        //e.Body.AngularVelocity = new Vector3D(0, 0, 0);
        //        return;
        //    }

        //    ChaseOrientation_GetTorqueArgs args = new ChaseOrientation_GetTorqueArgs(this.Item, rotation);

        //    Vector3D? torque = null;

        //    // Call each worker
        //    foreach (var worker in this.Torques)
        //    {
        //        Vector3D? localForce = worker.GetTorque(args);

        //        if (localForce != null)
        //        {
        //            if (torque == null)
        //            {
        //                torque = localForce;
        //            }
        //            else
        //            {
        //                torque = torque.Value + localForce.Value;
        //            }
        //        }
        //    }

        //    // Apply the torque
        //    if (torque != null)
        //    {
        //        // Limit if exceeds this.MaxForce
        //        if (this.MaxTorque != null && torque.Value.LengthSquared > this.MaxTorque.Value * this.MaxTorque.Value)
        //        {
        //            torque = torque.Value.ToUnit() * this.MaxTorque.Value;
        //        }

        //        // Limit acceleration
        //        if (this.MaxAcceleration != null)
        //        {
        //            double mass = Item.PhysicsBody.Mass;

        //            //f=ma
        //            double accel = torque.Value.Length / mass;

        //            if (accel > this.MaxAcceleration.Value)
        //            {
        //                torque = torque.Value.ToUnit() * (this.MaxAcceleration.Value * mass);
        //            }
        //        }

        //        torque = torque.Value * this.Percent;

        //        e.Body.AddTorque(torque.Value);
        //    }
        //}

    }

    #endregion
    #region class: ChaseTorque

    public class ChaseTorque
    {
        #region Constructor

        public ChaseTorque(ChaseDirectionType direction, double value, bool isAccel = true, bool isSpring = false, GradientEntry[] gradient = null)
        {
            if (gradient != null && gradient.Length == 1)
            {
                throw new ArgumentException("Gradient must have at least two items if it is populated");
            }

            Direction = direction;
            Value = value;
            IsAccel = isAccel;
            IsSpring = isSpring;

            if (gradient == null || gradient.Length == 0)
            {
                Gradient = null;
            }
            else
            {
                Gradient = gradient;
            }
        }

        #endregion

        #region Public Properties

        public readonly ChaseDirectionType Direction;

        public bool IsDrag => Direction != ChaseDirectionType.Attract_Direction;        // Direction is attract, all else is drag

        /// <summary>
        /// True: The value is an acceleration
        /// False: The value is a force
        /// </summary>
        public readonly bool IsAccel;
        /// <summary>
        /// True: The value is multiplied by distance (f=kx)
        /// False: The value is it (f=k)
        /// NOTE: Distance is degrees, so any value from 0 to 180
        /// </summary>
        /// <remarks>
        /// Nothing in this class will prevent you from having this true and a gradient at the same time, but that
        /// would give pretty strange results
        /// </remarks>
        public readonly bool IsSpring;

        public readonly double Value;

        /// <summary>
        /// This specifies varying percents based on distance to target
        /// Item1: Distance (in degrees from 0 to 180)
        /// Item2: Percent
        /// </summary>
        /// <remarks>
        /// If a distance is less than what is specified, then the lowest value gradient stop will be used (same with larger distances)
        /// So you could set up a crude s curve (though I don't know why you would):
        ///     at 5: 25%
        ///     at 20: 75%
        /// 
        /// I think the most common use of the gradient would be to set up a dead spot near 0:
        ///     at 0: 0%
        ///     at 10: 100%
        /// 
        /// Or maybe set up a crude 1/x repulsive force near the destination point:
        ///     at 0: 100%
        ///     at 2: 27%
        ///     at 4: 12%
        ///     at 6: 5.7%
        ///     at 8: 2%
        ///     at 10: 0%
        /// </remarks>
        public readonly GradientEntry[] Gradient;

        #endregion

        #region Public Methods

        public Vector3D? GetTorque(ChaseOrientation_GetTorqueArgs e)
        {
            GetDesiredVector(out Vector3D unit, out double length, e, Direction);
            if (Math3D.IsNearZero(unit))
                return null;

            double torque = Value;
            if (IsAccel)
            {
                // f=ma
                torque *= e.MomentInertia;
            }

            if (IsSpring)
            {
                torque *= e.Rotation.Angle;
            }

            if (IsDrag)
            {
                torque *= -length;       // negative, because it needs to be a drag force
            }

            // Gradient %
            if (Gradient != null)
            {
                torque *= GradientEntry.GetGradientPercent(e.Rotation.Angle, Gradient);
            }

            return unit * torque;
        }

        #endregion

        #region Private Methods

        private static void GetDesiredVector(out Vector3D unit, out double length, ChaseOrientation_GetTorqueArgs e, ChaseDirectionType direction)
        {
            switch (direction)
            {
                case ChaseDirectionType.Drag_Velocity_Along:
                case ChaseDirectionType.Drag_Velocity_AlongIfVelocityAway:
                case ChaseDirectionType.Drag_Velocity_AlongIfVelocityToward:
                    unit = e.AngVelocityAlongUnit;
                    length = e.AngVelocityAlongLength;
                    break;

                case ChaseDirectionType.Attract_Direction:
                    unit = e.Rotation.Axis;
                    length = e.Rotation.Angle;
                    break;

                case ChaseDirectionType.Drag_Velocity_Any:
                    unit = e.AngVelocityUnit;
                    length = e.AngVelocityLength;
                    break;

                case ChaseDirectionType.Drag_Velocity_Orth:
                    unit = e.AngVelocityOrthUnit;
                    length = e.AngVelocityOrthLength;
                    break;

                default:
                    throw new ApplicationException($"Unknown DirectionType: {direction}");
            }
        }

        #endregion
    }

    #endregion

    #region class: ChaseOrientation_GetTorqueArgs

    public class ChaseOrientation_GetTorqueArgs
    {
        public ChaseOrientation_GetTorqueArgs(double momentInertia, Quaternion angularVelocity, Quaternion rotation)
        {
            MomentInertia = momentInertia;

            Rotation = rotation;
            Vector3D direction = rotation.Axis;

            // Angular Velocity
            AngVelocityLength = angularVelocity.Angle;
            AngVelocityUnit = angularVelocity.Axis.ToUnit();

            // Along
            Vector3D velocityAlong = (AngVelocityUnit * AngVelocityLength).GetProjectedVector(direction);
            AngVelocityAlongLength = velocityAlong.Length;
            AngVelocityAlongUnit = velocityAlong.ToUnit();
            IsAngVelocityAlongTowards = Vector3D.DotProduct(direction, AngVelocityUnit) > 0d;

            // Orth
            Vector3D orth = Vector3D.CrossProduct(direction, AngVelocityUnit);       // the first cross is orth to both (outside the plane)
            orth = Vector3D.CrossProduct(orth, direction);       // the second cross is in the plane, but orth to distance
            Vector3D velocityOrth = (AngVelocityUnit * AngVelocityLength).GetProjectedVector(orth);

            AngVelocityOrthLength = velocityOrth.Length;
            AngVelocityOrthUnit = velocityOrth.ToUnit();
        }

        public readonly double MomentInertia;

        public readonly Quaternion Rotation;

        public readonly Vector3D AngVelocityUnit;
        public readonly double AngVelocityLength;

        public readonly bool IsAngVelocityAlongTowards;
        public readonly Vector3D AngVelocityAlongUnit;
        public readonly double AngVelocityAlongLength;

        public readonly Vector3D AngVelocityOrthUnit;
        public readonly double AngVelocityOrthLength;
    }

    #endregion

    #region enum: ChaseDirectionType

    public enum ChaseDirectionType
    {
        //------ this is an attraction force

        /// <summary>
        /// The force is along the direction vector
        /// </summary>
        Attract_Direction,

        //------ everything below is a drag force

        /// <summary>
        /// Drag is applied to the entire velocity
        /// </summary>
        Drag_Velocity_Any,
        /// <summary>
        /// Drag is only applied along the part of the velocity that is along the direction to the chase point
        /// </summary>
        Drag_Velocity_Along,
        /// <summary>
        /// Drag is only applied along the part of the velocity that is along the direction to the chase point.
        /// But only if that velocity is toward the chase point
        /// </summary>
        Drag_Velocity_AlongIfVelocityToward,
        /// <summary>
        /// Drag is only applied along the part of the velocity that is along the direction to the chase point.
        /// But only if that velocity is away from chase point
        /// </summary>
        Drag_Velocity_AlongIfVelocityAway,
        /// <summary>
        /// Drag is only applied along the part of the velocity that is othrogonal to the direction to the chase point
        /// </summary>
        Drag_Velocity_Orth
    }

    #endregion

    #region class: GradientEntry

    public class GradientEntry
    {
        public GradientEntry(double distance, double percent)
        {
            Distance = distance;
            Percent = percent;
        }

        public readonly double Distance;
        public readonly double Percent;

        public static double GetGradientPercent(double distance, GradientEntry[] gradient)
        {
            // See if they are outside the gradient (if so, use that cap's %)
            if (distance <= gradient[0].Distance)
            {
                return gradient[0].Percent;
            }
            else if (distance >= gradient[gradient.Length - 1].Distance)
            {
                return gradient[gradient.Length - 1].Percent;
            }

            //  It is inside the gradient.  Find the two stops that are on either side
            for (int cntr = 0; cntr < gradient.Length - 1; cntr++)
            {
                if (distance > gradient[cntr].Distance && distance <= gradient[cntr + 1].Distance)
                {
                    // LERP between the from % and to %
                    return UtilityMath.GetScaledValue(gradient[cntr].Percent, gradient[cntr + 1].Percent, gradient[cntr].Distance, gradient[cntr + 1].Distance, distance);        //NOTE: Not calling the capped overload, because max could be smaller than min (and capped would fail)
                }
            }

            throw new ApplicationException("Execution should never get here");
        }
    }

    #endregion

    #region TODOs

    //TODO: AngularDrag
    //  This would need to be a torque modifier (call body.addtorque instead of body.addforce) - or
    //  directly modify angular velocity
    //
    //  This shouldn't act on world coords.  Instead pass in a vector every frame (when drag is a plane,
    //  world works, but when it's a cylinder, the vector needs to be tangent to the cylinder)

    /// <summary>
    /// This applies torque if the angular velocity isn't right
    /// </summary>
    //internal class MapObject_ChaseAngVel_Forces
    //{
    //TODO: Finish this:
    //  Instead of SetPosition, have SetAngularVelocity - this would be useful for keeping things spinning forever
    //  Or have a way of clamping to only one plane of rotation
    //}

    #endregion
}
