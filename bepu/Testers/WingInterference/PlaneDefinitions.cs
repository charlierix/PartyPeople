using Game.Math_WPF.Mathematics;
using System.Numerics;

namespace Game.Bepu.Testers.WingInterference
{
    // Plane Definition from Unity

    public record PlaneDefinition
    {
        public EngineDefinition Engine_0 { get; init; }
        public EngineDefinition Engine_1 { get; init; }
        public EngineDefinition Engine_2 { get; init; }

        public WingDefinition Wing_0 { get; init; }
        public WingDefinition Wing_1 { get; init; }
        public WingDefinition Wing_2 { get; init; }

        public WingModifier[] WingModifiers_0 { get; init; }
        public WingModifier[] WingModifiers_1 { get; init; }
        public WingModifier[] WingModifiers_2 { get; init; }

        public TailDefinition Tail { get; init; }

        // head canard

        // spine
        //  this would be a single hinge joint, or a small chain of segments

        //TODO: maybe some way to define mass
    }

    public record EngineDefinition
    {
        public const float STANDARD_HEIGHT = 0.4f;      // these are what the scale of the engine should be when size is "one"
        public const float STANDARD_RADIUS = 0.3f;

        public float THRUST_AT_HALF = 36;
        public float THRUST_AT_DOUBLE = 144;

        public Vector3 Offset { get; init; }        // this is for the right wing.  The left will be mirroed
        public Quaternion Rotation { get; init; }

        public float Size { get; init; } = 1;

        // ------------- for the tester -------------

        public EngineDefinition_Meshes Meshes { get; init; }
    }

    public record EngineDefinition_Meshes
    {
        // These are in world coords (they've already been transformed by offset and rotation)
        // From/To points are interior (full capsule height is 2R+H)
        public Vector3 Cylinder_From_Interior { get; init; }
        public Vector3 Cylinder_To_Interior { get; init; }

        public Vector3 Cylinder_From_Tip { get; init; }
        public Vector3 Cylinder_To_Tip { get; init; }

        public float Cylinder_Radius { get; init; }
    }

    public record WingDefinition
    {
        public Vector3 Offset { get; init; }        // this is for the right wing.  The left will be mirroed
        public Quaternion Rotation { get; init; }

        /// <summary>
        /// There will be a root point, this many interior points, tip point
        /// </summary>
        /// <remarks>
        /// This doesn't change the total length of the wing, just how much it is chopped up
        /// 
        /// Each * is an unscaled anchor point
        /// Each - is a wing segment (vertical stabilizers are tied to the anchor points)
        /// 
        /// 0:
        ///     *-*
        /// 
        /// 1:
        ///     *-*-*
        /// 
        /// 2:
        ///     *-*-*-*
        ///     
        /// 3:
        ///     *-*-*-*-*
        /// </remarks>
        public int Inner_Segment_Count { get; init; } = 2;

        /// <summary>
        /// Total length of the wing (wing span)
        /// X scale
        /// </summary>
        public float Span { get; init; } = 1f;
        /// <summary>
        /// How the wing segments are spaced apart
        /// </summary>
        /// <remarks>
        /// If power is 1, then they are spaced linearly:
        /// *     *     *     *
        /// 
        /// If power is .5, then it's sqrt
        /// *       *     *   *
        /// 
        /// If power is 2, then it's ^2 (this would be an unnatural way to make a wing)
        /// *   *     *       *
        /// </remarks>
        public float Span_Power { get; init; } = 1f;       // all inputs to the power function are scaled from 0 to 1.  You generally want to set power between 0.5 and 1

        /// <summary>
        /// The part the runs parallel to the fuselage
        /// Z scale
        /// </summary>
        public float Chord_Base { get; init; } = 0.4f;
        public float Chord_Tip { get; init; } = 0.4f;
        public float Chord_Power { get; init; } = 1f;

        /// <summary>
        /// How much lift the wing generates
        /// </summary>
        /// <remarks>
        /// 0 is no extra lift, 1 is high lift/high drag
        /// 
        /// The way lift works is between about -20 to 20 degrees angle of attack, there is extra force applied along the wing's normal
        /// But that comes at a cost of extra drag and less effective at higher angles of attack
        /// </remarks>
        public float Lift_Base { get; init; } = 0.7f;
        public float Lift_Tip { get; init; } = 0.2f;
        public float Lift_Power { get; init; } = 1f;

        public float MIN_VERTICALSTABILIZER_HEIGHT { get; init; } = 0.1f;

        /// <summary>
        /// These are vertical pieces (lift is set to zero)
        /// </summary>
        /// <remarks>
        /// You would generally want a single stabalizer at the tip of the wing.  To accomplish that, use a high power
        /// (the height at each segment is near zero, then height approaches max close to the tip)
        /// </remarks>
        public float VerticalStabilizer_Base { get; init; } = 0;       // NOTE: a vertical stabilizer won't be created for a segment if it's less than MIN_VERTICALSTABILIZER_HEIGHT
        public float VerticalStabilizer_Tip { get; init; } = 0;
        public float VerticalStabilizer_Power { get; init; } = 16;

        // ------------- for the tester -------------
        
        public WingDefinition_Meshes Meshes { get; init; }
    }

    public record WingDefinition_Meshes
    {
        //TODO: these need to be broken out by wing part to help with analyzing marked cells
        public ITriangle_wpf[] Triangles { get; init; }
    }

    public record WingModifier
    {
        public MultAtAngle Lift { get; init; }
        public MultAtAngle Drag { get; init; }

        public float From { get; init; }
        public float To { get; init; }
    }

    public record MultAtAngle
    {
        public float Neg_90 { get; init; } = 1f;
        public float Zero { get; init; } = 1f;
        public float Pos_90 { get; init; } = 1f;
    }

    public record TailDefinition
    {
        public Vector3 Offset { get; init; }        //NOTE: if offset contains abs(X) > MIN, there will be two tails
        public Quaternion Rotation { get; init; }

        public TailDefinition_Boom Boom { get; init; }
        public TailDefinition_Tail Tail { get; init; }
    }

    public record TailDefinition_Boom
    {
        public int Inner_Segment_Count { get; init; } = 2;

        public float Length { get; init; } = 1f;
        public float Length_Power { get; init; } = 1f;

        public float Mid_Length { get; init; } = 0.7f;

        public float Span_Base { get; init; } = 0.5f;
        public float Span_Mid { get; init; } = 0.2f;
        public float Span_Tip { get; init; } = 0.3f;
        public bool Has_Span { get; init; }     // this gets populated by RemoveSmallDefinitions.ExamineTail

        public float Vert_Base { get; init; } = 0.5f;
        public float Vert_Mid { get; init; } = 0.2f;
        public float Vert_Tip { get; init; } = 0.3f;
        public bool Has_Vert { get; init; }     // this gets populated by RemoveSmallDefinitions.ExamineTail

        public float Bezier_PinchPercent { get; init; } = 0.2f;       // 0 to 0.4 (affects the curviness of the bezier, 0 is linear)

        // ------------- for the tester -------------

        public TailDefinition_Boom_Meshes Meshes { get; init; }
    }

    public record TailDefinition_Boom_Meshes
    {
        public ITriangle_wpf[] Triangles { get; init; }
    }

    public record TailDefinition_Tail
    {
        public float MIN_SIZE { get; init; } = 0.1f;

        public float Chord { get; init; } = 0.33f;        // if < MIN_TAIL_SIZE, there is no tail section
        public float Horz_Span { get; init; } = 0.5f;     // if < MIN_TAIL_SIZE, there is no horizontal wing
        public float Vert_Height { get; init; } = 0.5f;       // if < MIN_TAIL_SIZE, there is no vertical wing

        // ------------- for the tester -------------

        public TailDefinition_Tail_Meshes Meshes { get; init; } 
    }

    public record TailDefinition_Tail_Meshes
    {
        public ITriangle_wpf[] Triangles { get; init; }
    }
}