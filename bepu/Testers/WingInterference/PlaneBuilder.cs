using Game.Core;
using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media.Media3D;
using static BepuPhysics.Collidables.CompoundBuilder;

namespace Game.Bepu.Testers.WingInterference
{
    public static class PlaneBuilder
    {
        public static PlaneDefinition BuildPlane(PlaneDefinition def)
        {
            // Throw out items that are too small
            def = RemoveSmallDefinitions.ExaminePlane(def);

            //TODO: validate positions
            //  move engines out of the way
            //  apply modifiers to each wing things are too close together
            //      lift at -90 / 0 / 90
            //      drag at -90 / 0 / 90
            //      from/to

            //return new PlaneBuilderResults_Plane()
            //{
            //    Engine_0_Left = BuildEngine(def.Engine_0, mountpoints.Engine_0_Left, engine_prefab, false),
            //    Engine_0_Right = BuildEngine(def.Engine_0, mountpoints.Engine_0_Right, engine_prefab, true),

            //    Engine_1_Left = BuildEngine(def.Engine_1, mountpoints.Engine_1_Left, engine_prefab, false),
            //    Engine_1_Right = BuildEngine(def.Engine_1, mountpoints.Engine_1_Right, engine_prefab, true),

            //    Engine_2_Left = BuildEngine(def.Engine_2, mountpoints.Engine_2_Left, engine_prefab, false),
            //    Engine_2_Right = BuildEngine(def.Engine_2, mountpoints.Engine_2_Right, engine_prefab, true),

            //    Wing_0_Left = BuildWing(def.Wing_0, mountpoints.Wing_0_Left, wing_prefab, false),
            //    Wing_0_Right = BuildWing(def.Wing_0, mountpoints.Wing_0_Right, wing_prefab, true),

            //    Wing_1_Left = BuildWing(def.Wing_1, mountpoints.Wing_1_Left, wing_prefab, false),
            //    Wing_1_Right = BuildWing(def.Wing_1, mountpoints.Wing_1_Right, wing_prefab, true),

            //    Wing_2_Left = BuildWing(def.Wing_2, mountpoints.Wing_2_Left, wing_prefab, false),
            //    Wing_2_Right = BuildWing(def.Wing_2, mountpoints.Wing_2_Right, wing_prefab, true),

            //    //TODO: IF Mathf.Abs(def.Tail.Offset.x) > MIN THEN two tails ELSE one tail with offset.x = 0
            //    Tail = BuildTail(def.Tail, mountpoints.Tail, wing_prefab),
            //};

            return new PlaneDefinition()
            {
                Engine_0 = BuildEngine(def.Engine_0),
                Engine_1 = BuildEngine(def.Engine_1),
                Engine_2 = BuildEngine(def.Engine_2),

                Wing_0 = BuildWing(def.Wing_0),
                Wing_1 = BuildWing(def.Wing_1),
                Wing_2 = BuildWing(def.Wing_2),

                Tail = BuildTail(def.Tail),
            };
        }

        public static EngineDefinition BuildEngine(EngineDefinition def)
        {
            if (def == null)
                return null;

            float cylinder_half_height = EngineDefinition.STANDARD_HEIGHT * def.Size / 2;
            float radius = EngineDefinition.STANDARD_DIAMETER * def.Size / 2;

            // In unity, the height of a capsule is 2R+H.  So the endpoints are interior
            var direction_interior = new Vector3(0, 0, cylinder_half_height);
            direction_interior = def.Rotation.GetRotatedVector(direction_interior);

            // UtilityWPF works with tip points
            var direction_tip = new Vector3(0, 0, cylinder_half_height + radius);
            direction_tip = def.Rotation.GetRotatedVector(direction_tip);

            return def with
            {
                Meshes = new EngineDefinition_Meshes()
                {
                    Cylinder_From_Interior = def.Offset - direction_interior,
                    Cylinder_To_Interior = def.Offset + direction_interior,

                    Cylinder_From_Tip = def.Offset - direction_tip,
                    Cylinder_To_Tip = def.Offset + direction_tip,

                    Cylinder_Radius = radius,
                },
            };
        }

        public static WingDefinition BuildWing(WingDefinition def)
        {
            if (def == null)
                return null;

            // offset is the base of the wing
            // rotation will then set the direction from there

            Point3D start = def.Offset.ToPoint_wpf();
            Vector3D dir_span = def.Rotation.GetRotatedVector(new Vector3(1, 0, 0)).ToVector_wpf();
            Vector3D dir_chord = def.Rotation.GetRotatedVector(new Vector3(0, 0, 1)).ToVector_wpf();
            Vector3D dir_vert = def.Rotation.GetRotatedVector(new Vector3(0, 1, 0)).ToVector_wpf();

            //var endpoints     <- 0 to 2

            Vector3D[] points_global = GetPoints(start, start + (dir_span * def.Span), def.Inner_Segment_Count, def.Span_Power);
            //Vector3[] points_relative     <- this makes each point relative to the previous.  That's not needed for this wpf, since everything is abs positioned (unity cascades its objects relative to parent)

            double[] chords = GetValueAtEndpoint_Power(def.Chord_Base, def.Chord_Tip, def.Chord_Power, points_global, def.Span);
            double[] vert_stabalizers = GetValueAtEndpoint_Power(def.VerticalStabilizer_Base, def.VerticalStabilizer_Tip, def.VerticalStabilizer_Power, points_global, def.Span);

            var wing_horz = GetWingSegments(points_global, dir_chord, chords);
            var wing_vert = GetWingVertSegments(points_global, dir_chord, dir_vert, chords, vert_stabalizers, def.MIN_VERTICALSTABILIZER_HEIGHT);

            return def with
            {
                Meshes = new WingDefinition_Meshes()
                {
                    Triangles = wing_horz.
                        Concat(wing_vert).
                        ToArray(),
                },
            };
        }

        public static TailDefinition BuildTail(TailDefinition def)
        {
            if (def == null)
                return null;

            Point3D start = def.Offset.ToPoint_wpf();
            Vector3D dir_boom = def.Rotation.GetRotatedVector(new Vector3(0, 0, -1)).ToVector_wpf();
            Vector3D dir_span = def.Rotation.GetRotatedVector(new Vector3(1, 0, 0)).ToVector_wpf();
            Vector3D dir_vert = def.Rotation.GetRotatedVector(new Vector3(0, 1, 0)).ToVector_wpf();

            var defB = def.Boom;
            var defT = def.Tail;

            var tail_usage = GetTailUsage(defT);

            //Point3D start_tail = tail_usage.horz || tail_usage.vert ?
            //    start + (dir_boom * def.Boom.Length) :
            //    start;

            // Calculate all the positions

            Vector3D[] points_boom_global = GetPoints(start, start + (dir_boom * defB.Length), defB.Inner_Segment_Count, defB.Length_Power);

            double[] spans_boom = defB.Has_Span ?
                GetValueAtEndpoint_Bezier(defB.Span_Base, defB.Span_Mid, defB.Span_Tip, defB.Mid_Length, defB.Length, points_boom_global, defB.Bezier_PinchPercent) :
                null;

            double[] verts_boom = defB.Has_Vert ?
                GetValueAtEndpoint_Bezier(defB.Vert_Base, defB.Vert_Mid, defB.Vert_Tip, defB.Mid_Length, defB.Length, points_boom_global, defB.Bezier_PinchPercent) :
                null;

            var tail_values = GetTailValues(tail_usage.horz, tail_usage.vert, start, dir_boom, dir_span, dir_vert, defB.Length, defT);

            ITriangle_wpf[] boom_horz = defB.Has_Span ?
                GetWingSegments(points_boom_global, dir_span, spans_boom) :
                null;

            ITriangle_wpf[] boom_vert = defB.Has_Vert ?
                GetWingSegments(points_boom_global, dir_vert, verts_boom) :
                null;

            ITriangle_wpf[] tail_horz = tail_usage.horz ?
                GetWingSegments(tail_values.points_global, dir_span, tail_values.spans) :
                null;

            ITriangle_wpf[] tail_vert = tail_usage.vert ?
                GetWingSegments(tail_values.points_global, dir_vert, tail_values.verts) :
                null;

            return def with
            {
                Boom = def.Boom with
                {
                    Meshes = new TailDefinition_Boom_Meshes()
                    {
                        Triangles = UtilityCore.Iterate(boom_horz, boom_vert).ToArray(),
                    },
                },

                Tail = def.Tail != null ?
                    def.Tail with
                    {
                        Meshes = new TailDefinition_Tail_Meshes()
                        {
                            Triangles = UtilityCore.Iterate(tail_horz, tail_vert).ToArray(),
                        },
                    } :
                    null,
            };
        }

        #region Private Methods

        private static ITriangle_wpf[] GetWingSegments(Vector3D[] points_global, Vector3D direction, double[] lengths)
        {
            var retVal = new List<Triangle_wpf>();

            // The arrays passed in have entries for base and tip, but the last rectangle will be from points[^2] to points[^1]
            // NOTE: In unity, the sub wings are rectangles, but here they are calculated as trapazoids
            for (int i = 0; i < points_global.Length - 1; i++)
            {
                Vector3D half_chord_i = direction * (lengths[i] / 2);
                Vector3D half_chord_i1 = direction * (lengths[i + 1] / 2);

                Point3D top_left = (points_global[i] + half_chord_i).ToPoint();
                Point3D bot_left = (points_global[i] - half_chord_i).ToPoint();
                Point3D top_right = (points_global[i + 1] + half_chord_i1).ToPoint();
                Point3D bot_right = (points_global[i + 1] - half_chord_i1).ToPoint();

                retVal.Add(new Triangle_wpf(top_left, bot_left, bot_right));
                retVal.Add(new Triangle_wpf(bot_right, top_right, top_left));
            }

            return retVal.ToArray();
        }
        private static ITriangle_wpf[] GetWingVertSegments(Vector3D[] points_global, Vector3D dir_chord, Vector3D dir_vert, double[] chords, double[] vert_stabalizers, float min_height)
        {
            var retVal = new List<Triangle_wpf>();

            // NOTE: the wing segments go between elements of points_global (wing at 0 to 1...n-1 to n)
            // but the vertical stabalizers are at each points_global (stabalizer at 0, 1...n)
            for (int i = 0; i < points_global.Length; i++)
            {
                if (vert_stabalizers[i] < min_height)
                    continue;

                Vector3D half_chord = dir_chord * (chords[i] / 2);
                Vector3D half_vert = dir_vert * (vert_stabalizers[i] / 2);

                Point3D top_left = (points_global[i] - half_chord + half_vert).ToPoint();
                Point3D bot_left = (points_global[i] - half_chord - half_vert).ToPoint();
                Point3D top_right = (points_global[i] + half_chord + half_vert).ToPoint();
                Point3D bot_right = (points_global[i] + half_chord - half_vert).ToPoint();

                retVal.Add(new Triangle_wpf(top_left, bot_left, bot_right));
                retVal.Add(new Triangle_wpf(bot_right, top_right, top_left));
            }

            return retVal.ToArray();
        }

        private static Vector3D[] GetPoints(Point3D from, Point3D to, int internal_points, double pow)
        {
            double[] x = GetPoints(from.X, to.X, internal_points, pow);
            double[] y = GetPoints(from.Y, to.Y, internal_points, pow);
            double[] z = GetPoints(from.Z, to.Z, internal_points, pow);

            Vector3D[] retVal = new Vector3D[x.Length];

            for (int i = 0; i < x.Length; i++)
            {
                retVal[i] = new Vector3D(x[i], y[i], z[i]);
            }

            return retVal;
        }
        private static double[] GetPoints(double from, double to, int internal_points, double pow)
        {
            double[] retVal = new double[internal_points + 2];

            double step = 1f / (retVal.Length - 1);
            double gap = to - from;

            for (int i = 0; i < retVal.Length; i++)
            {
                double percent = i * step;

                retVal[i] = from + (Math.Pow(percent, pow) * gap);
            }

            return retVal;
        }

        private static double[] GetValueAtEndpoint_Power(double val_base, double val_tip, double power, Vector3D[] points_global, double total_span)
        {
            var retVal = new double[points_global.Length];

            retVal[0] = val_base;
            retVal[^1] = val_tip;

            for (int i = 1; i < retVal.Length - 1; i++)
            {
                double percent = (points_global[i] - points_global[0]).Length / total_span;

                double mult = Math.Pow(percent, power);

                retVal[i] = UtilityMath.GetScaledValue(val_base, val_tip, 0, 1, mult);
            }

            return retVal;
        }
        private static double[] GetValueAtEndpoint_Bezier(double val_base, double val_mid, double val_tip, double mid_dist, double total_span, Vector3D[] points_global, double pinch_percent)
        {
            var retVal = new double[points_global.Length];

            retVal[0] = val_base;
            retVal[^1] = val_tip;

            // Only the Y is reported (X is used to help define the curve)
            var bezier = BezierUtil.GetBezierSegments(new[] { new Point3D(0, val_base, 0), new Point3D(mid_dist, val_mid, 0), new Point3D(total_span, val_tip, 0) }, pinch_percent);

            var get_x_at_dist = new Func<double, double>(perc => BezierUtil.GetPoint(perc, bezier).X);

            for (int i = 1; i < retVal.Length - 1; i++)
            {
                double dist_desired = (points_global[i] - points_global[0]).Length;

                double dist_actual = Math1D.GetInputForDesiredOutput_PosInput_PosCorrelation(dist_desired, 0.01, get_x_at_dist);

                Point3D bez_point = BezierUtil.GetPoint(dist_actual, bezier);

                retVal[i] = bez_point.Y;
            }

            return retVal;
        }

        private static (bool horz, bool vert) GetTailUsage(TailDefinition_Tail defT)
        {
            if (defT == null || defT.Chord <= defT.MIN_SIZE)
                return (false, false);

            bool horz = defT.Horz_Span >= defT.MIN_SIZE;
            bool vert = defT.Vert_Height >= defT.MIN_SIZE;

            return (horz, vert);
        }

        /// <summary>
        /// Return structures similar to what wing and boom use so that GetWingSegments can be reused
        /// </summary>
        /// <remarks>
        /// Wings and Booms can be split up into N segments, sub segments have their values defined by power curves / beziers.
        /// Tail either exists or doesn't and only has the two outer points
        /// </remarks>
        private static (Point3D start, Vector3D[] points_global, double[] spans, double[] verts) GetTailValues(bool used_horz, bool used_vert, Point3D start, Vector3D dir_boom, Vector3D dir_span, Vector3D dir_vert, double boom_length, TailDefinition_Tail defT)
        {
            if (!used_horz && !used_vert)
                return (start, new Vector3D[0], new double[0], new double[0]);

            Point3D start_tail = start + (dir_boom * boom_length);

            Vector3D[] points_global = new Vector3D[]
            {
                start_tail.ToVector(),
                (start_tail + (dir_boom * defT.Chord)).ToVector(),
            };

            double[] spans = new double[]
            {
                defT.Horz_Span,
                defT.Horz_Span,
            };

            double[] verts = new double[]
            {
                defT.Vert_Height,
                defT.Vert_Height,
            };

            return (start_tail, points_global, spans, verts);
        }

        #endregion
    }
}
