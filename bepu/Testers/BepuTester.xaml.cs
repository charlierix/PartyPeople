﻿using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
//using BepuPhysics.Collidables;
using BepuUtilities.Memory;
using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using Game.Math_WPF.WPF.Viewers;
using GameItems;
using GameItems.MapParts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Game.Bepu.Testers
{
    public partial class BepuTester : Window
    {
        #region class: SetVelocityArgs

        private class SetVelocityArgs
        {
            public bool RandomEach { get; set; }
            public bool RandomSame { get; set; }
            public bool Center { get; set; }
            public bool X { get; set; }
            public bool Y { get; set; }
            public bool Z { get; set; }

            public bool IsPositive { get; set; }

            public bool Translate { get; set; }
            public bool Rotate { get; set; }

            public float Speed_Translate { get; set; }
            public float Speed_Rotate { get; set; }

            public float Speed_Translate_Final => IsPositive ? Speed_Translate : -Speed_Translate;
            public float Speed_Rotate_Final => IsPositive ? Speed_Rotate : -Speed_Rotate;

            public bool Overwrite { get; set; }
        }

        #endregion
        #region class: VectorFieldArgs

        private class VectorFieldArgs
        {
            public bool HasField => Inward || Swirl || ZPlane || OuterShell;

            public bool Inward { get; set; }
            public bool Swirl { get; set; }
            public bool ZPlane { get; set; }

            private bool _outerShell = false;
            public bool OuterShell
            {
                get
                {
                    return _outerShell;
                }
                set
                {
                    _outerShell = value;
                    ShellChanged();
                }
            }

            private float _outerShellRadius = 1;
            public float OuterShellRadius
            {
                get
                {
                    return _outerShellRadius;
                }
                set
                {
                    _outerShellRadius = value;
                    ShellChanged();
                }
            }

            public double OuterShellExponent => 2d;
            public double EquationConstant { get; private set; }

            private float _strength = 0;
            public float Strength
            {
                get
                {
                    return _strength;
                }
                set
                {
                    _strength = value;
                    ShellChanged();
                }
            }

            public bool IsForce { get; set; }

            private void ShellChanged()
            {
                // See Game.Newt.v2.GameItems GravityFieldSpace.BoundryField.GetForce()
                double boundryStop = OuterShellRadius * 1.6;
                EquationConstant = Strength / Math.Pow((boundryStop - OuterShellRadius) * .5d, OuterShellExponent);
            }
        }

        #endregion
        #region class: DragArgs

        private class DragArgs
        {
            public bool ApplyLinear { get; set; }
            public bool ApplyAngular { get; set; }

            /// <summary>
            /// If this is .1 then it will reduce the speed by .1 every "time"
            /// </summary>
            public float PercentSpeed_Linear { get; set; }
            public float PercentSpeed_Angular { get; set; }
        }

        #endregion
        #region class: BodyBodyArgs

        private enum BodyBodyType
        {
            None,
            Constant,
            Spring,
            Gravity,
        }

        private class BodyBodyArgs
        {
            public BodyBodyType Type { get; set; }

            public bool Toward { get; set; }

            public float Strength { get; set; }

            public Simulation Sim { get; set; }
        }

        #endregion

        #region struct: CollisionCallbacks

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>
        /// I believe broad phase refers to rough collision detection (bounding box or sphere).  Narrow phase is exact collision detection
        /// 
        /// 
        /// </remarks>
        private struct CollisionCallbacks : INarrowPhaseCallbacks
        {
            private SpringSettings _contactSpringiness;

            public void Initialize(Simulation simulation)
            {
                _contactSpringiness = new SpringSettings(30, 1);
            }
            public void Dispose()
            {
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowContactGeneration(int workerIndex, CollidableReference a, CollidableReference b)
            {
                // Don't bother if they're both static (aparently, Kinematic is a special type (infinite mass), only useful if you want a trigger, not physics)
                return a.Mobility == CollidableMobility.Dynamic || b.Mobility == CollidableMobility.Dynamic;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool AllowContactGeneration(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB)
            {
                // This seems to be a dependent call of the other overload (only called if that returned true)
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
            {
                //TODO: The coefficient should depend on the types of bodies that are colliding
                pairMaterial.FrictionCoefficient = 1f;
                pairMaterial.MaximumRecoveryVelocity = 2f;
                pairMaterial.SpringSettings = _contactSpringiness;

                return true;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool ConfigureContactManifold(int workerIndex, CollidablePair pair, int childIndexA, int childIndexB, ref ConvexContactManifold manifold)
            {
                return true;
            }

            #region FROM DEMOS

            //public SpringSettings ContactSpringiness;
            //public void Initialize(Simulation simulation)
            //{
            //    //Use a default if the springiness value wasn't initialized.
            //    if (ContactSpringiness.AngularFrequency == 0 && ContactSpringiness.TwiceDampingRatio == 0)
            //        ContactSpringiness = new SpringSettings(30, 1);
            //}

            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            //public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
            //{
            //    pairMaterial.FrictionCoefficient = 1f;
            //    pairMaterial.MaximumRecoveryVelocity = 2f;
            //    pairMaterial.SpringSettings = ContactSpringiness;
            //    return true;
            //}

            //// -------------- car demo
            //public BodyProperty<CarBodyProperties> Properties;
            //public void Initialize(Simulation simulation)
            //{
            //    Properties.Initialize(simulation.Bodies);
            //}

            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            //public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
            //{
            //    pairMaterial.FrictionCoefficient = Properties[pair.A.Handle].Friction;
            //    if (pair.B.Mobility != CollidableMobility.Static)
            //    {
            //        //If two bodies collide, just average the friction.
            //        pairMaterial.FrictionCoefficient = (pairMaterial.FrictionCoefficient + Properties[pair.B.Handle].Friction) * 0.5f;
            //    }
            //    pairMaterial.MaximumRecoveryVelocity = 2f;
            //    pairMaterial.SpringSettings = new SpringSettings(30, 1);
            //    return true;
            //}


            //// ----------------- character
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            //public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
            //{
            //    pairMaterial = new PairMaterialProperties { FrictionCoefficient = 1, MaximumRecoveryVelocity = 2, SpringSettings = new SpringSettings(30, 1) };
            //    Characters.TryReportContacts(pair, ref manifold, workerIndex, ref pairMaterial);        // this sets friction to zero if it's a character - I think so the character can slide along the ground
            //    return true;
            //}


            //// ---------------- cloth demo
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            //public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
            //{
            //    pairMaterial.FrictionCoefficient = 0.25f;
            //    pairMaterial.MaximumRecoveryVelocity = 2f;
            //    pairMaterial.SpringSettings = new SpringSettings(30, 1);
            //    return true;
            //}


            //// ----------------- contact events
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            //public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
            //{
            //    pairMaterial.FrictionCoefficient = 1f;
            //    pairMaterial.MaximumRecoveryVelocity = 2f;
            //    pairMaterial.SpringSettings = new SpringSettings(30, 1);
            //    events.HandleManifold(workerIndex, pair, ref manifold);
            //    return true;
            //}


            //// ---------------------- newt
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            //public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
            //{
            //    pairMaterial.FrictionCoefficient = 1;
            //    pairMaterial.MaximumRecoveryVelocity = 2f;
            //    pairMaterial.SpringSettings = new SpringSettings(30, 1);
            //    return true;
            //}

            //// ---------------- ragdoll
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            //public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
            //{
            //    pairMaterial.FrictionCoefficient = 1;
            //    pairMaterial.MaximumRecoveryVelocity = 2f;
            //    pairMaterial.SpringSettings = new SpringSettings(30, 1);
            //    return true;
            //}


            //// ---------------- self contained
            ///// <summary>
            ///// Provides a notification that a manifold has been created for a pair. Offers an opportunity to change the manifold's details. 
            ///// </summary>
            ///// <param name="workerIndex">Index of the worker thread that created this manifold.</param>
            ///// <param name="pair">Pair of collidables that the manifold was detected between.</param>
            ///// <param name="manifold">Set of contacts detected between the collidables.</param>
            ///// <param name="pairMaterial">Material properties of the manifold.</param>
            ///// <returns>True if a constraint should be created for the manifold, false otherwise.</returns>
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            //public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
            //{
            //    //The IContactManifold parameter includes functions for accessing contact data regardless of what the underlying type of the manifold is.
            //    //If you want to have direct access to the underlying type, you can use the manifold.Convex property and a cast like Unsafe.As<TManifold, ConvexContactManifold or NonconvexContactManifold>(ref manifold).

            //    //The engine does not define any per-body material properties. Instead, all material lookup and blending operations are handled by the callbacks.
            //    //For the purposes of this demo, we'll use the same settings for all pairs.
            //    //(Note that there's no bounciness property! See here for more details: https://github.com/bepu/bepuphysics2/issues/3)
            //    pairMaterial.FrictionCoefficient = 1f;
            //    pairMaterial.MaximumRecoveryVelocity = 2f;
            //    pairMaterial.SpringSettings = new SpringSettings(30, 1);
            //    //For the purposes of the demo, contact constraints are always generated.
            //    return true;
            //}


            //// ---------------- tank
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            //public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
            //{
            //    //Different tank parts have different friction values. Wheels tend to stick more than the body of the tank.
            //    ref var propertiesA = ref Properties[pair.A.Handle];
            //    pairMaterial.FrictionCoefficient = propertiesA.Friction;
            //    if (pair.B.Mobility != CollidableMobility.Static)
            //    {
            //        //If two bodies collide, just average the friction. Other options include min(a, b) or a * b.
            //        ref var propertiesB = ref Properties[pair.B.Handle];
            //        pairMaterial.FrictionCoefficient = (pairMaterial.FrictionCoefficient + propertiesB.Friction) * 0.5f;
            //    }
            //    //These are just some nice standard values. Higher maximum velocities can result in more energy being introduced during deep contact.
            //    //Finite spring stiffness helps the solver converge to a solution in difficult cases. Try to keep the spring frequency at around half of the timestep frequency or less.
            //    pairMaterial.MaximumRecoveryVelocity = 2f;
            //    pairMaterial.SpringSettings = new SpringSettings(30, 1);

            //    if (propertiesA.Projectile || (pair.B.Mobility != CollidableMobility.Static && Properties[pair.B.Handle].Projectile))
            //    {
            //        for (int i = 0; i < manifold.Count; ++i)
            //        {
            //            //This probably looks a bit odd. You can't return refs to the this instance in structs, and interfaces can't require static functions...
            //            //so we use this redundant construction to get a direct reference to a contact's depth with near zero overhead.
            //            //There's a more typical out parameter overload for contact properties too. And there's always the option of using the manifold pointers directly.
            //            //Note the use of a nonzero negative threshold: speculative contacts will bring incoming objects to a stop at the surface, but in some cases integrator/numerical issues can mean that they don't quite reach.
            //            //In most cases, this isn't a problem at all, but tank projectiles are moving very quickly and a single missed frame might be enough to not trigger an explosion.
            //            //A nonzero epsilon helps catch those cases.
            //            //(An alternative would be to check each projectile's contact constraints and cause an explosion if any contact has nonzero penetration impulse.)
            //            if (manifold.GetDepth(ref manifold, i) >= -1e-3f)
            //            {
            //                //An actual collision was found. 
            //                if (propertiesA.Projectile)
            //                {
            //                    TryAddProjectileImpact(pair.A.Handle, pair.B);
            //                }
            //                if (pair.B.Mobility != CollidableMobility.Static && Properties[pair.B.Handle].Projectile)
            //                {
            //                    //Could technically combine the locks in the case that both bodies are projectiles, but that's not exactly common.
            //                    TryAddProjectileImpact(pair.B.Handle, pair.A);
            //                }
            //                break;
            //            }
            //        }
            //    }
            //    return true;
            //}


            //// ----------- compound collision
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            //public unsafe bool ConfigureContactManifold<TManifold>(int workerIndex, CollidablePair pair, ref TManifold manifold, out PairMaterialProperties pairMaterial) where TManifold : struct, IContactManifold<TManifold>
            //{
            //    if (manifold.Count > 0)
            //    {
            //        if (manifold.Convex)
            //        {
            //            Console.WriteLine($"CONVEX PAIR: {pair.A} versus {pair.B}");
            //        }
            //        else
            //        {
            //            Console.WriteLine($"NONCONVEX PAIR: {pair.A} versus {pair.B}");
            //        }
            //    }
            //    pairMaterial.FrictionCoefficient = 1f;
            //    pairMaterial.MaximumRecoveryVelocity = 2f;
            //    pairMaterial.SpringSettings = new SpringSettings(30, 1);
            //    return true;
            //}

            #endregion
        }

        #endregion
        #region struct: ExternalForcesCallbacks

        /// <summary>
        /// This adds accelerations onto body's velocities.  It's how bebu implemented concepts like my IGravityField
        /// </summary>
        /// <remarks>
        /// This deals with accelerations (adding directly to velocity), not forces
        /// </remarks>
        private struct ExternalAccelCallbacks : IPoseIntegratorCallbacks
        {
            #region Declaration Section

            public SortedList<int, PhysicsBody> Bodies;

            // Can't reference simulation, because simulation references this and makes a copy.  Need to have a list of bodies, or maybe a delegate to simulation?
            //public Simulation Simulation;

            // The values need to be stored in an object, because directly setting bools and floats as properties of this struct don't carry over (the values stay default because the struct is copied)
            public VectorFieldArgs Field;
            private float _fieldStrengthDt;
            private System.Numerics.Quaternion? _swirlQuat;

            public DragArgs Drag;
            private float _dragLinearDt;
            private float _dragAngularDt;

            public BodyBodyArgs BodyBody;
            private float _bodybodyStrengthDt;

            private SortedList<int, Vector3> _bodybodyAccelerations;

            #endregion

            public AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;

            /// <summary>
            /// Gets called each frame before all the bodies are queried.  Basically, just multiply the current acceleration values times
            /// delta and store that to be used during each call to IntegrateVelocity.  At a minimum, store delta to be used by each
            /// call to IntegrateVelocity
            /// </summary>
            /// <remarks>
            /// Things can only be cached here if they are the same for each body.  For example if gravity isn't position dependant, then
            /// set _gravityFrame = Gravity * dt each time this method is called
            /// </remarks>
            public void PrepareForIntegration(float dt)
            {
                _fieldStrengthDt = Field.Strength * dt;

                if (_swirlQuat == null)
                {
                    _swirlQuat = System.Numerics.Quaternion.CreateFromAxisAngle(new Vector3(0, 0, 1), Math1D.DegreesToRadians(10f));
                }

                _dragLinearDt = Drag.PercentSpeed_Linear * dt;
                _dragAngularDt = Drag.PercentSpeed_Angular * dt;

                _bodybodyStrengthDt = BodyBody.Strength * dt;

                _bodybodyAccelerations?.Clear();

                if (BodyBody.Type != BodyBodyType.None)
                {
                    //TODO: In a real app, something like gravity should be done in a different thread.  That way the main thread can just apply the current force felt to each body
                    CalculateBodyBody(dt);
                }
            }

            /// <summary>
            /// This gets called for each body, each frame
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void IntegrateVelocity(int bodyIndex, in RigidPose pose, in BodyInertia localInertia, int workerIndex, ref BodyVelocity velocity)
            {
                // Ignore kinematics (static bodies)
                if (localInertia.InverseMass <= 0)
                {
                    return;
                }

                if (Field.HasField)
                {
                    #region vector field

                    Vector3 direction = new Vector3();

                    Vector3 positionUnit = (Field.Inward || Field.Swirl || Field.OuterShell) ?
                        pose.Position.ToUnit() :
                        new Vector3();


                    if (Field.Inward)
                    {
                        direction += positionUnit * _fieldStrengthDt;
                    }

                    if (Field.Swirl)
                    {
                        direction += Vector3.Transform(positionUnit * _fieldStrengthDt, _swirlQuat.Value);
                    }

                    if (Field.ZPlane)
                    {
                        float z = pose.Position.Z.IsNearZero() ? 0f :
                            pose.Position.Z > 0 ? 1f :
                            -1f;

                        direction += new Vector3(0, 0, z * _fieldStrengthDt);
                    }

                    if (Field.OuterShell)
                    {
                        // See Game.Newt.v2.GameItems GravityFieldSpace.BoundryField.GetForce()
                        float shellDistance = pose.Position.Length() - Field.OuterShellRadius;
                        if (shellDistance > 0f)
                        {
                            double force = Field.EquationConstant * Math.Pow(shellDistance, Field.OuterShellExponent);
                            force *= _fieldStrengthDt;

                            direction += positionUnit * (float)force;
                        }
                    }

                    if (Field.IsForce)
                    {
                        //f=ma, so a=f/m
                        direction *= localInertia.InverseMass;
                    }

                    velocity.Linear -= direction;

                    #endregion
                }

                if (BodyBody.Type != BodyBodyType.None)
                {
                    velocity.Linear += _bodybodyAccelerations[BodyBody.Sim.Bodies.ActiveSet.IndexToHandle[bodyIndex]];       //WARNING: bodyIndex is not the same as bodyHandle.  It's the index into the active set
                }

                if (Drag.ApplyLinear)
                {
                    velocity.Linear -= velocity.Linear * _dragLinearDt;
                }

                if (Drag.ApplyAngular)
                {
                    velocity.Angular -= velocity.Angular * _dragAngularDt;
                }


                //velocity.Linear = (velocity.Linear + _gravityDt) * linearDampingDt;
                //velocity.Angular = velocity.Angular * angularDampingDt;

                // --- or ---

                //var offset = pose.Position - PlanetCenter;
                //var distance = offset.Length();
                //velocity.Linear -= _gravityDt * offset / MathF.Max(1f, distance * distance * distance);

            }

            #region Private Methods

            private void CalculateBodyBody(float dt)
            {
                // Init the forces list
                var forces = Bodies.Values.
                    Select(o => (handle: o.BodyHandle, body: o.Body, force: new Vector3(), mass: 1f / o.Body.LocalInertia.InverseMass)).
                    ToArray();

                // Calculate Forces
                for (int outer = 0; outer < Bodies.Values.Count - 1; outer++)
                {
                    Vector3 positionOuter = forces[outer].body.Pose.Position;

                    for (int inner = outer + 1; inner < Bodies.Values.Count; inner++)
                    {
                        Vector3 positionInner = forces[inner].body.Pose.Position;

                        Vector3 gravityLink = positionOuter - positionInner;

                        var force = BodyBody.Type switch
                        {
                            BodyBodyType.Constant => BodyBody.Strength,
                            BodyBodyType.Spring => BodyBody.Strength * gravityLink.Length(),
                            BodyBodyType.Gravity => BodyBody.Strength * (forces[outer].mass * forces[inner].mass) / gravityLink.LengthSquared(),
                            _ => throw new ApplicationException($"Unexpected {nameof(BodyBodyType)}: {BodyBody.Type}"),
                        };

                        if (!BodyBody.Toward)
                        {
                            force *= -1;
                        }

                        if (!force.IsInvalid())
                        {
                            gravityLink = gravityLink.ToUnit() * force;

                            forces[inner].force += gravityLink;
                            forces[outer].force -= gravityLink;
                        }
                    }
                }

                // Store them
                if (_bodybodyAccelerations == null)
                {
                    _bodybodyAccelerations = new SortedList<int, Vector3>();
                }
                _bodybodyAccelerations.Clear();

                foreach (var force in forces)
                {
                    _bodybodyAccelerations.Add(force.handle, force.force * force.body.LocalInertia.InverseMass * dt);        // turn force into an acceleration
                }
            }

            #endregion
        }

        #endregion

        #region class: PhysicsBody

        private class PhysicsBody
        {
            //TODO: Figure out what to do with sets of joined bodies

            public int BodyHandle { get; set; }
            public BodyReference Body { get; set; }
            public IShape Shape { get; set; }       // theoretically, you can get at the shape here: sim.Shapes[body.Collidable.Shape.Index];  But good luck making sense of that object.  It's all buffers, memory pools

            public Model3D Model { get; set; }
            public TranslateTransform3D Translate { get; set; }
            //public RotateTransform3D Rotate { get; set; }
            public QuaternionRotation3D Orientation { get; set; }
        }

        #endregion

        #region Declaration Section

        const double PLACEMENTRADIUS = 30;

        private readonly EquivalentColor _color = new EquivalentColor(new ColorHSV(StaticRandom.NextDouble(0, 360), StaticRandom.NextDouble(60, 70), StaticRandom.NextDouble(67, 72)));

        private TrackBallRoam _trackball = null;

        private BufferPool _bufferPool = null;
        private Simulation _simulation = null;

        private readonly VectorFieldArgs _field;
        private readonly DragArgs _drag;
        private readonly BodyBodyArgs _bodybody;
        private CollisionCallbacks _collision;
        private ExternalAccelCallbacks _externalAccel;

        private SortedList<int, PhysicsBody> _bodies = new SortedList<int, PhysicsBody>();
        private List<Visual3D> _visuals = new List<Visual3D>();

        /// <summary>
        /// Gets the thread dispatcher available for use by the simulation.
        /// </summary>
        private SimpleThreadDispatcher _threadDispatcher = null;
        private DispatcherTimer _timer = null;

        private bool _initialized = false;
        private bool _isDisposing = false;

        #endregion

        #region Constructor

        public BepuTester()
        {
            InitializeComponent();

            // Trackball
            _trackball = new TrackBallRoam(_camera);
            _trackball.EventSource = grdViewPort;       //NOTE:  If this control doesn't have a background color set, the trackball won't see events (I think transparent is ok, just not null)
            _trackball.AllowZoomOnMouseWheel = true;
            _trackball.Mappings.AddRange(TrackBallMapping.GetPrebuilt(TrackBallMapping.PrebuiltMapping.MouseComplete));
            _trackball.ShouldHitTestOnOrbit = false;

            //TODO: Figure out the game loop.  Should there be a game main tread that's separate from the gui main thread?
            //
            // for the first draft, run it from a timer that's managed by this window
            _threadDispatcher = new SimpleThreadDispatcher(Environment.ProcessorCount);     // this might need to be created before the simulation.  The ExternalAccelCallbacks functions weren't getting called (but that might just be because the bodies weren't awake)

            _field = new VectorFieldArgs();
            _drag = new DragArgs();

            // Physics Simulation
            _collision = new CollisionCallbacks();
            _bodybody = new BodyBodyArgs()
            {
                Type = BodyBodyType.None,
                Strength = 0,
                Toward = true,
            };
            _externalAccel = new ExternalAccelCallbacks()
            {
                Bodies = _bodies,
                Field = _field,
                Drag = _drag,
                BodyBody = _bodybody,
            };
            _bufferPool = new BufferPool();
            _simulation = Simulation.Create(_bufferPool, _collision, _externalAccel);

            //_externalAccel.Simulation = _simulation;      // This doesn't work, because the simulation makes a copy of the struct
            _bodybody.Sim = _simulation;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(10);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            _initialized = true;
        }

        #endregion

        #region Event Listeners

        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                _isDisposing = true;
                _timer.Stop();

                _simulation.Dispose();
                _bufferPool.Clear();
                _threadDispatcher.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_isDisposing)
                {
                    return;
                }

                #region simulation tick

                //In the demos, we use one time step per frame. We don't bother modifying the physics time step duration for different monitors so different refresh rates
                //change the rate of simulation. This doesn't actually change the result of the simulation, though, and the simplicity is a good fit for the demos.
                //In the context of a 'real' application, you could instead use a time accumulator to take time steps of fixed length as needed, or
                //fully decouple simulation and rendering rates across different threads.
                //(In either case, you'd also want to interpolate or extrapolate simulation results during rendering for smoothness.)
                //Note that taking steps of variable length can reduce stability. Gradual or one-off changes can work reasonably well.
                float dt = 1 / 60f;
                _simulation.Timestep(dt, _threadDispatcher);

                ////Here's an example of how it would look to use more frequent updates, but still with a fixed amount of time simulated per update call:
                //const float timeToSimulate = 1 / 60f;
                //const int timestepsPerUpdate = 2;
                //const float timePerTimestep = timeToSimulate / timestepsPerUpdate;
                //for (int i = 0; i < timestepsPerUpdate; ++i)
                //{
                //    _simulation.Timestep(timePerTimestep, ThreadDispatcher);
                //}

                ////And here's an example of how to use an accumulator to take a number of timesteps of fixed length in response to variable update dt:
                //timeAccumulator += dt;
                //var targetTimestepDuration = 1 / 120f;
                //while (timeAccumulator >= targetTimestepDuration)
                //{
                //    Simulation.Timestep(targetTimestepDuration, ThreadDispatcher);
                //    timeAccumulator -= targetTimestepDuration;
                //}
                ////If you wanted to smooth out the positions of rendered objects to avoid the 'jitter' that an unpredictable number of time steps per update would cause,
                ////you can just interpolate the previous and current states using a weight based on the time remaining in the accumulator:
                //var interpolationWeight = timeAccumulator / targetTimestepDuration;

                #endregion

                if (_map != null)
                {
                    foreach (SwarmBot2a bot in _map.GetAll())
                    {
                        bot.Update_MainThread(dt);
                        bot.Update_AnyThread(dt);
                    }
                }


                // Set breakpoints in this method chain.  Not sure whether to foreach through everything in simulation, or to foreach through a local copy and request items
                //ShapesExtractor:
                //  AddInstances -> AddBodyShape -> AddShape

                #region attempt - copy of demo

                //NOTE: A potentially cleaner way of iterating bodies is: (this whole loop was written before I just stored the body reference directly)
                //foreach (var body in _customBodyList)
                    //BodyReference bodyRef = _simulation.Bodies.GetBodyReference(body.BodyHandle);


                    // from ShapesExtractor.AddInstances()

                    for (int i = 0; i < _simulation.Bodies.Sets.Length; ++i)
                {
                    ref var set = ref _simulation.Bodies.Sets[i];

                    if (set.Allocated) //Islands are stored noncontiguously; skip those which have been deallocated.
                    {
                        for (int bodyIndex = 0; bodyIndex < set.Count; bodyIndex++)
                        {
                            int handle = set.IndexToHandle[bodyIndex];

                            UpdateTransform(set.Poses[bodyIndex], _bodies[handle]);
                        }
                    }
                }

                //TODO: Figure out how to handle statics.  Can dynamic and static body handles duplicate with each other?  If so, statics will need
                //to be stored in a different list
                for (int i = 0; i < _simulation.Statics.Count; i++)
                {
                    //AddStaticShape(_simulation.Shapes, _simulation.Statics, i);
                }


                #endregion



            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Sphere_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(txtSimple_Count.Text, out int count))
                {
                    MessageBox.Show("Couldn't parse the count", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                double diameter = chkSimple_RandomSize.IsChecked.Value ?
                    StaticRandom.NextDrift(1.3, 1) :
                    1;

                float radius = (float)(diameter / 2d);
                float mass = (float)((4d / 3d) * Math.PI * radius * radius * radius);

                var getBody = new Func<(BodyInertia inertia, CollidableDescription collidable, IShape shape)>(() =>
                {
                    var sphere = new Sphere(radius);
                    sphere.ComputeInertia(mass, out var inertia);
                    var collidable = new CollidableDescription(_simulation.Shapes.Add(sphere), 0.1f);

                    return (inertia, collidable, sphere);
                });

                var getGeometry = new Func<Geometry3D>(() => UtilityWPF.GetSphere_Ico(radius, 1, true));

                AddBody(count, diameter, getBody, getGeometry);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Box_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!int.TryParse(txtSimple_Count.Text, out int count))
                {
                    MessageBox.Show("Couldn't parse the count", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                float[] sizes = Enumerable.Range(0, 3).
                    Select(o => chkSimple_RandomSize.IsChecked.Value ? StaticRandom.NextDrift(1.3, 1) : 1).
                    Select(o => (float)o).
                    ToArray();

                //TODO: Test box sizes to make sure x,y,z axiis are the same between bepu and wpf
                var getBody = new Func<(BodyInertia inertia, CollidableDescription collidable, IShape shape)>(() =>
                {
                    var box = new Box(sizes[0], sizes[1], sizes[2]);
                    box.ComputeInertia(sizes[0] * sizes[1] * sizes[2], out var inertia);
                    var collidable = new CollidableDescription(_simulation.Shapes.Add(box), .1f);

                    return (inertia, collidable, box);
                });

                var getGeometry = new Func<Geometry3D>(() => UtilityWPF.GetCube_IndependentFaces(new Point3D(-sizes[0] / 2d, -sizes[1] / 2d, -sizes[2] / 2d), new Point3D(sizes[0] / 2d, sizes[1] / 2d, sizes[2] / 2d)));

                AddBody(count, sizes.Max(), getBody, getGeometry);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RadioVelocity_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_initialized)
                {
                    return;
                }

                if (radVel_Center.IsChecked.Value)
                {
                    chkVel_Positive.Content = "Away";
                    chkVel_Positive.Visibility = Visibility.Visible;
                }
                else if (radVel_X.IsChecked.Value || radVel_Y.IsChecked.Value || radVel_Z.IsChecked.Value)
                {
                    chkVel_Positive.Content = "Positive";
                    chkVel_Positive.Visibility = Visibility.Visible;
                }
                else
                {
                    chkVel_Positive.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SetVelocity_Translate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetVelocityArgs args = GetVelocityArgs();

                args.Translate = true;

                SetVelocity(args);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SetVelocity_Rotate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetVelocityArgs args = GetVelocityArgs();

                args.Rotate = true;

                SetVelocity(args);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SetVelocity_TranslateRotate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetVelocityArgs args = GetVelocityArgs();

                args.Translate = true;
                args.Rotate = true;

                SetVelocity(args);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SetVelocity_StopAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Vector3 stopped = new Vector3();

                foreach (PhysicsBody bodyWrapper in _bodies.Values)
                {
                    BodyReference body = _simulation.Bodies.GetBodyReference(bodyWrapper.BodyHandle);

                    body.Velocity.Linear = stopped;
                    body.Velocity.Angular = stopped;

                    // no need to change the awake state, because it's transitioning to stopped
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RadioField_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_initialized)
                {
                    return;
                }

                UpdateVectorFieldProps();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void trkField_Strength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (!_initialized)
                {
                    return;
                }

                UpdateVectorFieldProps();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void chkDrag_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_initialized)
                {
                    return;
                }

                UpdateDragProps();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void trkDrag_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (!_initialized)
                {
                    return;
                }

                UpdateDragProps();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RadioBodyBody_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_initialized)
                {
                    return;
                }

                UpdateBodyBodyProps();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void trkBodyBody_Strength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (!_initialized)
                {
                    return;
                }

                UpdateBodyBodyProps();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void chkSwarm_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (chkSwarm.IsChecked.Value)
                {
                    InitializeSwarm();
                }
                else
                {
                    ClearSwarm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void trkSwarm_Strength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear says it doesn't do anything that removes wouldn't, but the SimpleThreadDispatcher crashes with a null reference
                //_simulation.Bodies.Clear();

                ClearSwarm();

                foreach (var body in _bodies.Values)
                {
                    _simulation.Bodies.Remove(body.BodyHandle);
                }

                _bodies.Clear();

                _viewport.Children.RemoveAll(_visuals);
                _visuals.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private void AddBody(int cellCount, double cellSize, Func<(BodyInertia inertia, CollidableDescription collidable, IShape shape)> getBody, Func<Geometry3D> getGeometry)
        {
            Random rand = StaticRandom.GetRandomForThread();

            var cells = Math3D.GetCells_Cube(cellSize, cellCount, cellSize * .1, Math3D.GetRandomVector_Spherical(PLACEMENTRADIUS).ToPoint());

            ColorHSV color = UtilityWPF.GetRandomColor(0, 275, 7, 7, _color);       // keep it out of the pinks

            Material material = Debug3DWindow.GetMaterial(true, color.ToRGB());
            Model3DGroup modelGroup = new Model3DGroup();

            foreach (var cell in cells.Take(cellCount))
            {
                // See Demos.Demos PlanetDemo.Initialize
                //BepuPhysics.Simulation looks like the equivalent of world

                // types in BepuPhysics.Collidables seems to be the equivalent of newton's body

                var physicsBody = getBody();

                var pose = new RigidPose()
                {
                    Position = cell.center.ToVector3(),
                    Orientation = BepuUtilities.Quaternion.Identity,
                };

                var initialVel = new BodyVelocity()
                {
                    //Linear = Math3D.GetRandomVector_Spherical(3).ToVector3(),
                    //Angular = Math3D.GetRandomVector_Spherical(12).ToVector3(),
                };

                //int bodyHandle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(pose, initialVel, physicsBody.inertia, physicsBody.collidable, new BodyActivityDescription(.01f)));
                int bodyHandle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(pose, initialVel, physicsBody.inertia, physicsBody.collidable, new BodyActivityDescription(-1)));     // passing -1 so it won't go to sleep


                //TODO: Add red,green,blue lines to help tell the axiis apart - or use different colors fo the faces
                //Visual3D visual = Debug3DWindow.GetMesh(UtilityWPF.GetCube_IndependentFaces(new Point3D(-.5, -.5, -.5), new Point3D(.5, .5, .5)), Colors.Coral);


                GeometryModel3D model = new GeometryModel3D();
                model.Material = material;
                model.BackMaterial = material;

                model.Geometry = getGeometry();

                modelGroup.Children.Add(model);

                Transform3DGroup transform = new Transform3DGroup();

                QuaternionRotation3D quat = new QuaternionRotation3D();
                transform.Children.Add(new RotateTransform3D(quat));

                TranslateTransform3D translate = new TranslateTransform3D();
                transform.Children.Add(translate);

                model.Transform = transform;


                PhysicsBody body = new PhysicsBody()
                {
                    BodyHandle = bodyHandle,
                    Body = _simulation.Bodies.GetBodyReference(bodyHandle),
                    Shape = physicsBody.shape,
                    Model = model,
                    Translate = translate,
                    Orientation = quat,
                };

                UpdateTransform(pose, body);

                _bodies.Add(body.BodyHandle, body);
            }

            Visual3D visual = new ModelVisual3D
            {
                Content = modelGroup,
            };

            _visuals.Add(visual);
            _viewport.Children.Add(visual);
        }

        private void SetVelocity(SetVelocityArgs e)
        {
            // Some options need to the same velocity for all.  Set these here
            Vector3 translate = new Vector3();
            Vector3 rotate = new Vector3();

            if (e.RandomSame)
            {
                translate = Math3D.GetRandomVector_Spherical(e.Speed_Translate).ToVector3();
                rotate = Math3D.GetRandomVector_Spherical(e.Speed_Rotate).ToVector3();
            }
            else if (e.X)
            {
                translate = new Vector3(e.Speed_Translate_Final, 0, 0);
                rotate = new Vector3(e.Speed_Rotate_Final, 0, 0);
            }
            else if (e.Y)
            {
                translate = new Vector3(0, e.Speed_Translate_Final, 0);
                rotate = new Vector3(0, e.Speed_Rotate_Final, 0);
            }
            else if (e.Z)
            {
                translate = new Vector3(0, 0, e.Speed_Translate_Final);
                rotate = new Vector3(0, 0, e.Speed_Rotate_Final);
            }

            foreach (PhysicsBody bodyWrapper in _bodies.Values)
            {
                BodyReference body = _simulation.Bodies.GetBodyReference(bodyWrapper.BodyHandle);

                // Other options need to change for every body
                if (e.RandomEach)
                {
                    translate = Math3D.GetRandomVector_Spherical(e.Speed_Translate).ToVector3();
                    rotate = Math3D.GetRandomVector_Spherical(e.Speed_Rotate).ToVector3();
                }
                else if (e.Center)
                {
                    translate = body.Pose.Position.ToUnit() * e.Speed_Translate_Final;
                    rotate = Math3D.GetRandomVector_Spherical(e.Speed_Rotate).ToVector3();
                }

                // these are probably forces
                //body.ApplyLinearImpulse(translate);       
                //body.ApplyAngularImpulse(rotate);

                //TODO: Every once in a while, an item won't be affected by the velocity.  If I were to guess, it's because the awake is staying false
                //May need to wait until some event fires to set the velocity

                if (e.Translate)
                {
                    if (e.Overwrite)
                    {
                        body.Velocity.Linear = translate;
                    }
                    else
                    {
                        body.Velocity.Linear += translate;
                    }

                }

                if (e.Rotate)
                {
                    if (e.Overwrite)
                    {
                        body.Velocity.Angular = rotate;
                    }
                    else
                    {
                        body.Velocity.Angular += rotate;
                    }
                }

                body.Awake = true;      // it won't move if this stays false
            }
        }
        private SetVelocityArgs GetVelocityArgs()
        {
            return new SetVelocityArgs()
            {
                RandomEach = radVel_RandomEach.IsChecked.Value,
                RandomSame = radVel_RandomSame.IsChecked.Value,
                Center = radVel_Center.IsChecked.Value,
                X = radVel_X.IsChecked.Value,
                Y = radVel_Y.IsChecked.Value,
                Z = radVel_Z.IsChecked.Value,
                IsPositive = chkVel_Positive.IsChecked.Value,
                Speed_Translate = (float)UtilityMath.GetScaledValue_Capped(.1d, 50d, trkVel_Speed.Minimum, trkVel_Speed.Maximum, trkVel_Speed.Value),
                Speed_Rotate = (float)UtilityMath.GetScaledValue_Capped(.1d, 20d, trkVel_Speed.Minimum, trkVel_Speed.Maximum, trkVel_Speed.Value),
                Overwrite = radVel_Overwrite.IsChecked.Value,
            };
        }

        private void UpdateVectorFieldProps()
        {
            var strength = radField_Force.IsChecked.Value ? (.01d, 12d) : (.01d, 24d);

            _field.Strength = (float)UtilityMath.GetScaledValue_Capped(strength.Item1, strength.Item2, trkField_Strength.Minimum, trkField_Strength.Maximum, trkField_Strength.Value);

            _field.IsForce = radField_Force.IsChecked.Value;

            _field.Inward = chkField_Inward.IsChecked.Value;
            _field.Swirl = chkField_Swirl.IsChecked.Value;
            _field.ZPlane = chkField_ZPlane.IsChecked.Value;
            _field.OuterShell = chkField_OuterShell.IsChecked.Value;

            _field.OuterShellRadius = (float)trkField_OuterRadius.Value;
        }
        private void UpdateBodyBodyProps()
        {
            _bodybody.Strength = (float)UtilityMath.GetScaledValue_Capped(.01d, 3d, trkBodyBody_Strength.Minimum, trkBodyBody_Strength.Maximum, trkBodyBody_Strength.Value);

            _bodybody.Type = radBodyBody_None.IsChecked.Value ? BodyBodyType.None :
                radBodyBody_Constant.IsChecked.Value ? BodyBodyType.Constant :
                radBodyBody_Spring.IsChecked.Value ? BodyBodyType.Spring :
                radBodyBody_Gravity.IsChecked.Value ? BodyBodyType.Gravity :
                throw new ApplicationException("Unknown Body/Body force type");

            _bodybody.Toward = radBodyBody_Toward.IsChecked.Value;
        }
        private void UpdateDragProps()
        {
            double MAXDRAG = .8;

            _drag.ApplyLinear = chkDrag_Linear.IsChecked.Value;
            _drag.ApplyAngular = chkDrag_Angular.IsChecked.Value;

            _drag.PercentSpeed_Linear = (float)UtilityMath.GetScaledValue_Capped(0, MAXDRAG, trkDrag_Linear.Minimum, trkDrag_Linear.Maximum, trkDrag_Linear.Value);
            _drag.PercentSpeed_Angular = (float)UtilityMath.GetScaledValue_Capped(0, MAXDRAG, trkDrag_Angular.Minimum, trkDrag_Angular.Maximum, trkDrag_Angular.Value);
        }

        private static void UpdateTransform(in RigidPose pose, PhysicsBody graphic)
        {
            graphic.Translate.OffsetX = pose.Position.X;
            graphic.Translate.OffsetY = pose.Position.Y;
            graphic.Translate.OffsetZ = pose.Position.Z;

            graphic.Orientation.Quaternion = new System.Windows.Media.Media3D.Quaternion(pose.Orientation.X, pose.Orientation.Y, pose.Orientation.Z, pose.Orientation.W);
        }

        #endregion

        #region from demo - ShapesExtractor (drawing)

        //// Intermediate layer
        //private unsafe void AddCompoundChildren(ref Buffer<CompoundChild> children, Shapes shapes, in RigidPose pose, in Vector3 color)
        //{
        //    for (int i = 0; i < children.Length; ++i)
        //    {
        //        ref var child = ref children[i];
        //        Compound.GetWorldPose(child.LocalPose, pose, out var childPose);
        //        AddShape(shapes, child.ShapeIndex, ref childPose, color);
        //    }
        //}
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //void AddBodyShape(Shapes shapes, Bodies bodies, int setIndex, int indexInSet)
        //{
        //    ref var set = ref bodies.Sets[setIndex];
        //    var handle = set.IndexToHandle[indexInSet];

        //    #region shades of pink

        //    ////Body color is based on three factors:
        //    ////1) Handle as a hash seed that is unpacked into a color
        //    ////2) Dynamics vs kinematic state
        //    ////3) Activity state
        //    ////The handle is hashed to get variation.
        //    //ref var activity = ref set.Activity[indexInSet];
        //    //ref var inertia = ref set.LocalInertias[indexInSet];
        //    //Vector3 color;
        //    //Helpers.UnpackColor((uint)HashHelper.Rehash(handle), out Vector3 colorVariation);
        //    //if (Bodies.IsKinematic(inertia))
        //    //{
        //    //    var kinematicBase = new Vector3(0, 0.609f, 0.37f);
        //    //    var kinematicVariationSpan = new Vector3(0.1f, 0.1f, 0.1f);
        //    //    color = kinematicBase + kinematicVariationSpan * colorVariation;
        //    //}
        //    //else
        //    //{
        //    //    var dynamicBase = new Vector3(0.8f, 0.1f, 0.566f);
        //    //    var dynamicVariationSpan = new Vector3(0.2f, 0.2f, 0.2f);
        //    //    color = dynamicBase + dynamicVariationSpan * colorVariation;
        //    //}

        //    //if (setIndex == 0)
        //    //{
        //    //    if (activity.SleepCandidate)
        //    //    {
        //    //        var sleepCandidateTint = new Vector3(0.35f, 0.35f, 0.7f);
        //    //        color *= sleepCandidateTint;
        //    //    }
        //    //}
        //    //else
        //    //{
        //    //    var sleepTint = new Vector3(0.2f, 0.2f, 0.4f);
        //    //    color *= sleepTint;
        //    //}

        //    #endregion

        //    AddShape(shapes, set.Collidables[indexInSet].Shape, ref set.Poses[indexInSet], color);
        //}
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //void AddStaticShape(Shapes shapes, Statics statics, int index)
        //{
        //    var handle = statics.IndexToHandle[index];

        //    #region shades of brown

        //    ////Statics don't have any activity states. Just some simple variation on a central static color.
        //    //Helpers.UnpackColor((uint)HashHelper.Rehash(handle), out Vector3 colorVariation);
        //    //var staticBase = new Vector3(0.1f, 0.057f, 0.014f);
        //    //var staticVariationSpan = new Vector3(0.07f, 0.07f, 0.03f);
        //    //var color = staticBase + staticVariationSpan * colorVariation;

        //    #endregion

        //    AddShape(shapes, statics.Collidables[index].Shape, ref statics.Poses[index], color);
        //}

        //// final draw
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        //public unsafe void AddShape(void* shapeData, int shapeType, Shapes shapes, ref RigidPose pose, in Vector3 color)
        //{
        //    //TODO: This should likely be swapped over to a registration-based virtualized table approach to more easily support custom shape extractors-
        //    //generic terrain windows and examples like voxel grids would benefit.
        //    switch (shapeType)
        //    {
        //        case Sphere.Id:
        //            {
        //                SphereInstance instance;
        //                instance.Position = pose.Position;
        //                instance.Radius = Unsafe.AsRef<Sphere>(shapeData).Radius;
        //                Helpers.PackOrientation(pose.Orientation, out instance.PackedOrientation);
        //                instance.PackedColor = Helpers.PackColor(color);
        //                spheres.Add(instance, pool);
        //            }
        //            break;
        //        case Capsule.Id:
        //            {
        //                CapsuleInstance instance;
        //                instance.Position = pose.Position;
        //                ref var capsule = ref Unsafe.AsRef<Capsule>(shapeData);
        //                instance.Radius = capsule.Radius;
        //                instance.HalfLength = capsule.HalfLength;
        //                instance.PackedOrientation = Helpers.PackOrientationU64(ref pose.Orientation);
        //                instance.PackedColor = Helpers.PackColor(color);
        //                capsules.Add(instance, pool);
        //            }
        //            break;
        //        case Box.Id:
        //            {
        //                BoxInstance instance;
        //                instance.Position = pose.Position;
        //                ref var box = ref Unsafe.AsRef<Box>(shapeData);
        //                instance.PackedColor = Helpers.PackColor(color);
        //                instance.Orientation = pose.Orientation;
        //                instance.HalfWidth = box.HalfWidth;
        //                instance.HalfHeight = box.HalfHeight;
        //                instance.HalfLength = box.HalfLength;
        //                boxes.Add(instance, pool);
        //            }
        //            break;
        //        case Triangle.Id:
        //            {
        //                ref var triangle = ref Unsafe.AsRef<Triangle>(shapeData);
        //                TriangleInstance instance;
        //                instance.A = triangle.A;
        //                instance.PackedColor = Helpers.PackColor(color);
        //                instance.B = triangle.B;
        //                instance.C = triangle.C;
        //                instance.PackedOrientation = Helpers.PackOrientationU64(ref pose.Orientation);
        //                instance.X = pose.Position.X;
        //                instance.Y = pose.Position.Y;
        //                instance.Z = pose.Position.Z;
        //                triangles.Add(instance, pool);
        //            }
        //            break;
        //        case Cylinder.Id:
        //            {
        //                CylinderInstance instance;
        //                instance.Position = pose.Position;
        //                ref var cylinder = ref Unsafe.AsRef<Cylinder>(shapeData);
        //                instance.Radius = cylinder.Radius;
        //                instance.HalfLength = cylinder.HalfLength;
        //                instance.PackedOrientation = Helpers.PackOrientationU64(ref pose.Orientation);
        //                instance.PackedColor = Helpers.PackColor(color);
        //                cylinders.Add(instance, pool);
        //            }
        //            break;
        //        case ConvexHull.Id:
        //            {
        //                ref var hull = ref Unsafe.AsRef<ConvexHull>(shapeData);
        //                MeshInstance instance;
        //                instance.Position = pose.Position;
        //                instance.PackedColor = Helpers.PackColor(color);
        //                instance.PackedOrientation = Helpers.PackOrientationU64(ref pose.Orientation);
        //                instance.Scale = Vector3.One;
        //                var id = (ulong)hull.Points.Memory ^ (ulong)hull.Points.Length;
        //                if (!MeshCache.TryGetExistingMesh(id, out instance.VertexStart, out var vertices))
        //                {
        //                    int triangleCount = 0;
        //                    for (int i = 0; i < hull.FaceToVertexIndicesStart.Length; ++i)
        //                    {
        //                        hull.GetVertexIndicesForFace(i, out var faceVertexIndices);
        //                        triangleCount += faceVertexIndices.Length - 2;
        //                    }
        //                    instance.VertexCount = triangleCount * 3;
        //                    MeshCache.Allocate(id, instance.VertexCount, out instance.VertexStart, out vertices);
        //                    //This is a fresh allocation, so we need to upload vertex data.
        //                    int targetVertexIndex = 0;
        //                    for (int i = 0; i < hull.FaceToVertexIndicesStart.Length; ++i)
        //                    {
        //                        hull.GetVertexIndicesForFace(i, out var faceVertexIndices);
        //                        hull.GetPoint(faceVertexIndices[0], out var faceOrigin);
        //                        hull.GetPoint(faceVertexIndices[1], out var previousEdgeEnd);
        //                        for (int j = 2; j < faceVertexIndices.Length; ++j)
        //                        {
        //                            vertices[targetVertexIndex++] = faceOrigin;
        //                            vertices[targetVertexIndex++] = previousEdgeEnd;
        //                            hull.GetPoint(faceVertexIndices[j], out previousEdgeEnd);
        //                            vertices[targetVertexIndex++] = previousEdgeEnd;

        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    instance.VertexCount = vertices.Length;
        //                }
        //                meshes.Add(instance, pool);
        //            }
        //            break;
        //        case Compound.Id:
        //            {
        //                AddCompoundChildren(ref Unsafe.AsRef<Compound>(shapeData).Children, shapes, pose, color);
        //            }
        //            break;
        //        case BigCompound.Id:
        //            {
        //                AddCompoundChildren(ref Unsafe.AsRef<BigCompound>(shapeData).Children, shapes, pose, color);
        //            }
        //            break;
        //        case Mesh.Id:
        //            {
        //                ref var mesh = ref Unsafe.AsRef<Mesh>(shapeData);
        //                MeshInstance instance;
        //                instance.Position = pose.Position;
        //                instance.PackedColor = Helpers.PackColor(color);
        //                instance.PackedOrientation = Helpers.PackOrientationU64(ref pose.Orientation);
        //                instance.Scale = mesh.Scale;
        //                var id = (ulong)mesh.Triangles.Memory ^ (ulong)mesh.Triangles.Length;
        //                instance.VertexCount = mesh.Triangles.Length * 3;
        //                if (MeshCache.Allocate(id, instance.VertexCount, out instance.VertexStart, out var vertices))
        //                {
        //                    //This is a fresh allocation, so we need to upload vertex data.
        //                    for (int i = 0; i < mesh.Triangles.Length; ++i)
        //                    {
        //                        ref var triangle = ref mesh.Triangles[i];
        //                        var baseVertexIndex = i * 3;
        //                        //Note winding flip for rendering.
        //                        vertices[baseVertexIndex] = triangle.A;
        //                        vertices[baseVertexIndex + 1] = triangle.C;
        //                        vertices[baseVertexIndex + 2] = triangle.B;
        //                    }
        //                }
        //                meshes.Add(instance, pool);
        //            }
        //            break;
        //    }
        //}

        #endregion

        private Map _map = null;

        private void InitializeSwarm()
        {
            //TODO: Have a timer on a separate thread to update the swarmbots
            //TODO: Add a hook between _externalAccel and swarmbots so it can update the force





            // Makre sure everything is wiped out
            ClearSwarm();

            _map = new Map();

            foreach (PhysicsBody body in _bodies.Values)
            {
                SwarmBot2a swarmbot = new SwarmBot2a(body.BodyHandle, body.Body, body.Shape, _map);

                _map.AddItem(swarmbot);
            }






        }
        private void ClearSwarm()
        {
            //TODO: Map may need a dispose
            _map = null;
        }
    }
}
