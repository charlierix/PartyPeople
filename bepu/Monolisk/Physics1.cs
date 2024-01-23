using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities.Memory;
using Game.Core;
using Game.Math_WPF.Mathematics;
using Game.Math_WPF.WPF;
using Game.Math_WPF.WPF.Controls3D;
using Game.Math_WPF.WPF.Viewers;
using GameItems;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace Game.Bepu.Monolisk
{
    public class Physics1 : IDisposable
    {
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
            public float GravityStrength;
            private Vector3 _gravityDt;

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
                _gravityDt = new Vector3(0, 0, -GravityStrength * dt);
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

                velocity.Linear += _gravityDt;      // gravity is already -z
            }
        }

        #endregion

        #region class: PhysicsBody

        private class PhysicsBody
        {
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

        /// <summary>
        /// Gets the thread dispatcher available for use by the simulation.
        /// </summary>
        private SimpleThreadDispatcher _threadDispatcher = null;
        private BufferPool _bufferPool = null;
        private Simulation _simulation = null;

        private CollisionCallbacks _collision;
        private ExternalAccelCallbacks _externalAccel;

        private DispatcherTimer _timer = null;

        private List<PhysicsBody> _debugBodies = new List<PhysicsBody>();

        #endregion

        #region Constructor

        public Physics1()
        {
            //TODO: Figure out the game loop.  Should there be a game main tread that's separate from the gui main thread?
            //
            // for the first draft, run it from a timer that's managed by this window
            _threadDispatcher = new SimpleThreadDispatcher(Environment.ProcessorCount);     // this might need to be created before the simulation.  The ExternalAccelCallbacks functions weren't getting called (but that might just be because the bodies weren't awake)

            _collision = new CollisionCallbacks();

            _externalAccel = new ExternalAccelCallbacks()
            {
                GravityStrength = 1f,
            };

            _bufferPool = new BufferPool();
            _simulation = Simulation.Create(_bufferPool, _collision, _externalAccel);


            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(10);
            _timer.Tick += Timer_Tick;
            _timer.Start();

        }

        #endregion

        #region IDisposable

        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _disposedValue = true;

                if (disposing)
                {
                    // dispose managed state (managed objects).
                    _timer.Stop();
                    _simulation.Dispose();
                    _bufferPool.Clear();
                    _threadDispatcher.Dispose();
                }

                // free unmanaged resources (unmanaged objects) and override a finalizer below.
                // set large fields to null.
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Physics1()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        #endregion

        #region Public Methods

        public void AddTerrain(Rect3D rect, bool lip_negX, bool lip_posX, bool lip_negY, bool lip_posY)
        {
            const double LIPWIDTHPERCENT = .02;
            const double LIPHEIGHTPERCENT = .1;

            var body = GetBoxBody(_simulation, rect);
            _simulation.Statics.Add(body.staticDesc.Value);

            double height = rect.SizeZ * LIPHEIGHTPERCENT;
            double sizeX = rect.SizeX * LIPWIDTHPERCENT;
            double halfX = sizeX * .5;
            double sizeY = rect.SizeY * LIPWIDTHPERCENT;
            double halfY = sizeY * .5;

            if (lip_negX)
            {
                Rect3D lip = new Rect3D(rect.X - halfX, rect.Y, rect.Z + rect.SizeZ, sizeX, rect.SizeY, height);

                body = GetBoxBody(_simulation, lip);
                _simulation.Statics.Add(body.staticDesc.Value);
            }

            if (lip_posX)
            {
                Rect3D lip = new Rect3D(rect.X + rect.SizeX - halfX, rect.Y, rect.Z + rect.SizeZ, sizeX, rect.SizeY, height);

                body = GetBoxBody(_simulation, lip);
                _simulation.Statics.Add(body.staticDesc.Value);
            }

            if (lip_negY)
            {
                Rect3D lip = new Rect3D(rect.X , rect.Y - halfY, rect.Z + rect.SizeZ, rect.SizeX, sizeY, height);

                body = GetBoxBody(_simulation, lip);
                _simulation.Statics.Add(body.staticDesc.Value);
            }

            if (lip_posY)
            {
                Rect3D lip = new Rect3D(rect.X, rect.Y + rect.SizeY - halfY, rect.Z + rect.SizeZ, rect.SizeX, sizeY, height);

                body = GetBoxBody(_simulation, lip);
                _simulation.Statics.Add(body.staticDesc.Value);
            }
        }

        /// <summary>
        /// This is just a test function to make sure the physics are working
        /// </summary>
        public void AddBall(Point3D position, double radius, Color color, Viewport3D viewport)
        {
            float mass = (float)((4d / 3d) * Math.PI * radius * radius * radius);

            var getBody = new Func<(BodyInertia inertia, CollidableDescription collidable, IShape shape)>(() =>
            {
                var sphere = new Sphere((float)radius);
                sphere.ComputeInertia(mass, out var inertia);
                var collidable = new CollidableDescription(_simulation.Shapes.Add(sphere), 0.1f);

                return (inertia, collidable, sphere);
            });

            var getGeometry = new Func<Geometry3D>(() => UtilityWPF.GetSphere_Ico(radius, 1, true));

            AddBody_TEST(1, radius * 2, position, color, viewport, getBody, getGeometry);
        }

        /// <summary>
        /// This completely removes all items from the physics simulation.  Call this before loading a new shard
        /// </summary>
        public void Clear()
        {
            _simulation.Bodies.Clear();
            _simulation.Statics.Clear();
        }

        #endregion

        #region Event Listeners

        private void Timer_Tick(object sender, EventArgs e)
        {
            //try
            //{

            if (_disposedValue)
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

            //TODO: Figure out where to fix orientation so that all characters stay upright

            //for (int i = 0; i < _simulation.Bodies.Sets.Length; ++i)        // NOTE: There is also a Statics list
            //{
            //    ref var set = ref _simulation.Bodies.Sets[i];

            //    if (set.Allocated) //Islands are stored noncontiguously; skip those which have been deallocated.
            //    {
            //        for (int bodyIndex = 0; bodyIndex < set.Count; bodyIndex++)
            //        {
            //            int handle = set.IndexToHandle[bodyIndex];

            //            //UpdateTransform(set.Poses[bodyIndex], _bodies[handle]);
            //        }
            //    }
            //}


            foreach (var body in _debugBodies)
            {
                //BodyReference bodyRef = _simulation.Bodies.GetBodyReference(body.BodyHandle);
                UpdateTransform(body.Body.Pose, body);
            }


            //}
            //catch (Exception)
            //{
            //    // Not sure what to do with errors
            //    //MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            //}
        }

        #endregion

        #region Private Methods

        private static (BodyInertia? inertia, CollidableDescription collidableDesc, StaticDescription? staticDesc, IShape shape) GetBoxBody(Simulation sim, Rect3D rect, double? density = null)
        {
            var box = new Box((float)rect.SizeX, (float)rect.SizeY, (float)rect.SizeZ);

            BodyInertia? inertiaFinal = null;
            if (density != null)
            {
                box.ComputeInertia((float)(rect.SizeX * rect.SizeY * rect.SizeZ * density.Value), out var inertia);
                inertiaFinal = inertia;
            }

            //TODO: May want to calculate the margin
            CollidableDescription collidableDesc = new CollidableDescription(sim.Shapes.Add(box), .1f);

            StaticDescription? staticDesc = null;
            if (inertiaFinal == null)
            {
                //staticDesc = new StaticDescription(new Vector3((float)rect.X, (float)rect.Y, (float)rect.Z), collidableDesc);
                staticDesc = new StaticDescription(new Vector3((float)rect.CenterX(), (float)rect.CenterY(), (float)rect.CenterZ()), collidableDesc);
            }

            return (inertiaFinal, collidableDesc, staticDesc, box);
        }

        //This was copied from BepuTester, and is just for crude testing
        private void AddBody_TEST(int cellCount, double cellSize, Point3D position, Color color, Viewport3D viewport, Func<(BodyInertia inertia, CollidableDescription collidable, IShape shape)> getBody, Func<Geometry3D> getGeometry)
        {
            //Random rand = StaticRandom.GetRandomForThread();

            var cells = Math3D.GetCells_Cube(cellSize, cellCount, cellSize * .1, position);

            //ColorHSV color = UtilityWPF.GetRandomColor(0, 275, 7, 7, _color);       // keep it out of the pinks

            Material material = Debug3DWindow.GetMaterial(true, color);
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

                _debugBodies.Add(body);
            }

            Visual3D visual = new ModelVisual3D
            {
                Content = modelGroup,
            };

            //_visuals.Add(visual);
            viewport.Children.Add(visual);
        }

        private static void UpdateTransform(in RigidPose pose, PhysicsBody graphic)
        {
            graphic.Translate.OffsetX = pose.Position.X;
            graphic.Translate.OffsetY = pose.Position.Y;
            graphic.Translate.OffsetZ = pose.Position.Z;

            graphic.Orientation.Quaternion = new System.Windows.Media.Media3D.Quaternion(pose.Orientation.X, pose.Orientation.Y, pose.Orientation.Z, pose.Orientation.W);
        }

        #endregion
    }
}
