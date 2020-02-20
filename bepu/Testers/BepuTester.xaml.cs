using BepuPhysics;
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
        #region class: SimpleThreadDispatcher

        // Copied from Demos
        private class SimpleThreadDispatcher : IThreadDispatcher, IDisposable
        {
            int threadCount;
            public int ThreadCount => threadCount;
            struct Worker
            {
                public Thread Thread;
                public AutoResetEvent Signal;
            }

            Worker[] workers;
            AutoResetEvent finished;

            BufferPool[] bufferPools;

            public SimpleThreadDispatcher(int threadCount)
            {
                this.threadCount = threadCount;
                workers = new Worker[threadCount - 1];
                for (int i = 0; i < workers.Length; ++i)
                {
                    workers[i] = new Worker { Thread = new Thread(WorkerLoop), Signal = new AutoResetEvent(false) };
                    workers[i].Thread.IsBackground = true;
                    workers[i].Thread.Start(workers[i].Signal);
                }
                finished = new AutoResetEvent(false);
                bufferPools = new BufferPool[threadCount];
                for (int i = 0; i < bufferPools.Length; ++i)
                {
                    bufferPools[i] = new BufferPool();
                }
            }

            void DispatchThread(int workerIndex)
            {
                //Debug.Assert(workerBody != null);
                workerBody(workerIndex);

                if (Interlocked.Increment(ref completedWorkerCounter) == threadCount)
                {
                    finished.Set();
                }
            }

            volatile Action<int> workerBody;
            int workerIndex;
            int completedWorkerCounter;

            void WorkerLoop(object untypedSignal)
            {
                var signal = (AutoResetEvent)untypedSignal;
                while (true)
                {
                    signal.WaitOne();
                    if (disposed)
                        return;
                    DispatchThread(Interlocked.Increment(ref workerIndex) - 1);
                }
            }

            void SignalThreads()
            {
                for (int i = 0; i < workers.Length; ++i)
                {
                    workers[i].Signal.Set();
                }
            }

            public void DispatchWorkers(Action<int> workerBody)
            {
                //Debug.Assert(this.workerBody == null);
                workerIndex = 1; //Just make the inline thread worker 0. While the other threads might start executing first, the user should never rely on the dispatch order.
                completedWorkerCounter = 0;
                this.workerBody = workerBody;
                SignalThreads();
                //Calling thread does work. No reason to spin up another worker and block this one!
                DispatchThread(0);
                finished.WaitOne();
                this.workerBody = null;
            }

            volatile bool disposed;
            public void Dispose()
            {
                if (!disposed)
                {
                    disposed = true;
                    SignalThreads();
                    for (int i = 0; i < bufferPools.Length; ++i)
                    {
                        bufferPools[i].Clear();
                    }
                    foreach (var worker in workers)
                    {
                        worker.Thread.Join();
                        worker.Signal.Dispose();
                    }
                }
            }

            public BufferPool GetThreadMemoryPool(int workerIndex)
            {
                return bufferPools[workerIndex];
            }
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
            }

            /// <summary>
            /// This gets called for each body, each frame
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void IntegrateVelocity(int bodyIndex, in RigidPose pose, in BodyInertia localInertia, int workerIndex, ref BodyVelocity velocity)
            {
                //velocity.Linear = (velocity.Linear + _gravityDt) * linearDampingDt;
                //velocity.Angular = velocity.Angular * angularDampingDt;

                // --- or ---

                //var offset = pose.Position - PlanetCenter;
                //var distance = offset.Length();
                //velocity.Linear -= _gravityDt * offset / MathF.Max(1f, distance * distance * distance);

            }
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

        #region class: PhysicsBody

        private class PhysicsBody
        {
            //TODO: Store the bepu object
            //TODO: Figure out what to do with sets of joined bodies

            public int BodyHandle { get; set; }

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

        private ExternalAccelCallbacks _externalAccel;
        private CollisionCallbacks _collision;

        private SortedList<int, PhysicsBody> _bodies = new SortedList<int, PhysicsBody>();
        private List<Visual3D> _visuals = new List<Visual3D>();

        /// <summary>
        /// Gets the thread dispatcher available for use by the simulation.
        /// </summary>
        private SimpleThreadDispatcher _threadDispatcher = null;
        private DispatcherTimer _timer = null;

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

            // Physics Simulation
            _collision = new CollisionCallbacks();
            _externalAccel = new ExternalAccelCallbacks();
            _bufferPool = new BufferPool();
            _simulation = Simulation.Create(_bufferPool, _collision, _externalAccel);


            //TODO: Figure out the game loop.  Should there be a game main tread that's separate from the gui main thread?
            //
            // for the first draft, run it from a timer that's managed by this window

            _threadDispatcher = new SimpleThreadDispatcher(Environment.ProcessorCount);

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(50);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        #endregion

        #region Event Listeners

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                #region simulation tick

                //In the demos, we use one time step per frame. We don't bother modifying the physics time step duration for different monitors so different refresh rates
                //change the rate of simulation. This doesn't actually change the result of the simulation, though, and the simplicity is a good fit for the demos.
                //In the context of a 'real' application, you could instead use a time accumulator to take time steps of fixed length as needed, or
                //fully decouple simulation and rendering rates across different threads.
                //(In either case, you'd also want to interpolate or extrapolate simulation results during rendering for smoothness.)
                //Note that taking steps of variable length can reduce stability. Gradual or one-off changes can work reasonably well.
                _simulation.Timestep(1 / 60f, _threadDispatcher);

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


                // Set breakpoints in this method chain.  Not sure whether to foreach through everything in simulation, or to foreach through a local copy and request items
                //ShapesExtractor:
                //  AddInstances -> AddBodyShape -> AddShape

                #region attempt - copy of demo

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





                #region attempt - my instances

                //Bodies bodies = _simulation.Bodies;


                //foreach (var body in _bodies)
                //{
                    //var simBody = _simulation.Bodies. [body.BodyHandle];


                //}

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
                if (!int.TryParse(txtSimpleCount.Text, out int count))
                {
                    MessageBox.Show("Couldn't parse the count", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var getBody = new Func<(BodyInertia inertia, CollidableDescription collidable)>(() =>
                {
                    var sphere = new Sphere(.5f);
                    sphere.ComputeInertia(1, out var inertia);
                    var collidable = new CollidableDescription(_simulation.Shapes.Add(sphere), 0.1f);

                    return (inertia, collidable);
                });

                var getGeometry = new Func<Geometry3D>(() => UtilityWPF.GetSphere_Ico(.5, 1, true));

                AddBody(count, getBody, getGeometry);
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
                if (!int.TryParse(txtSimpleCount.Text, out int count))
                {
                    MessageBox.Show("Couldn't parse the count", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                //TODO: Test box sizes to make sure x,y,z axiis are the same between bepu and wpf
                var getBody = new Func<(BodyInertia inertia, CollidableDescription collidable)>(() =>
                {
                    var box = new Box(1f, 1f, 1f);
                    box.ComputeInertia(1, out var inertia);
                    var collidable = new CollidableDescription(_simulation.Shapes.Add(box), .1f);

                    return (inertia, collidable);
                });

                var getGeometry = new Func<Geometry3D>(() => UtilityWPF.GetCube_IndependentFaces(new Point3D(-.5, -.5, -.5), new Point3D(.5, .5, .5)));

                AddBody(count, getBody, getGeometry);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear says it doesn't do anything that removes wouldn't, but the SimpleThreadDispatcher crashes with a null reference
                //_simulation.Bodies.Clear();

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

        private void AddBody(int cellCount, Func<(BodyInertia inertia, CollidableDescription collidable)> getBody, Func<Geometry3D> getGeometry)
        {
            Random rand = StaticRandom.GetRandomForThread();

            var cells = Math3D.GetCells_Cube(1, cellCount, .1, Math3D.GetRandomVector_Spherical(PLACEMENTRADIUS).ToPoint());

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

                int bodyHandle = _simulation.Bodies.Add(BodyDescription.CreateDynamic(pose, initialVel, physicsBody.inertia, physicsBody.collidable, new BodyActivityDescription(.01f)));


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
    }
}
