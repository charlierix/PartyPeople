using Game.Core;
using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Game.Bepu.Testers.WingInterference
{
    public static class GetPlaneDefinition
    {
        //NOTE: Using unity's coords, so Y is up, Z is along fuselage

        public static PlaneDefinition GetDefaultPlane()
        {
            return new PlaneDefinition()
            {
                Engine_0 = GetDefaultEngine(),
                Wing_0 = GetDefaultWing(),
                Tail = GetDefaultTail(),
            };
        }
        public static PlaneDefinition GetRandomPlane()
        {
            int num_engines = StaticRandom.Next(1, 4);      // this will return 1 2 or 3
            int num_wings = StaticRandom.Next(1, 4);
            bool build_tail = StaticRandom.NextBool();

            return new PlaneDefinition()
            {
                Engine_0 = GetRandomEngine(),
                Engine_1 = num_engines >= 2 ? GetRandomEngine() : null,
                Engine_2 = num_engines >= 3 ? GetRandomEngine() : null,

                Wing_0 = GetRandomWing2(),
                Wing_1 = num_wings >= 2 ? GetRandomWing2() : null,
                Wing_2 = num_wings >= 3 ? GetRandomWing2() : null,

                Tail = build_tail ? GetRandomTail() : null,
            };
        }

        public static EngineDefinition GetDefaultEngine()
        {
            return new EngineDefinition()
            {
                Offset = new Vector3(0, 0, 1),
                Rotation = Quaternion.Identity,
            };
        }
        public static EngineDefinition GetRandomEngine()
        {
            Random rand = StaticRandom.GetRandomForThread();

            // x = 0 will be along the centerline
            // x = pos with push to the right (don't use negative)

            // y = neg will be below (positive will be above)

            // z = neg will push toward back of plane (postive will push forward)


            // don't want engine to point backward or straight down, but allow a good range between forward and up with a little
            // beyond (up and slight back, forward and slight down)
            double pitch = rand.NextDouble(-10, 100);

            return new EngineDefinition()
            {
                Size = (float)rand.NextDouble(0.4, 1.6),

                Offset = new Vector3
                (
                    (float)rand.NextDouble(0, 2.5),
                    (float)rand.NextDouble(-1, 1),
                    (float)rand.NextDouble(-0.5, 1.75)
                ),

                Rotation = GetRotation(pitch: pitch),
            };
        }

        public static WingDefinition GetDefaultWing()
        {
            return new WingDefinition()
            {
                Offset = new Vector3(0.25f, 0, 0.5f),
                Rotation = Quaternion.Identity,
            };
        }
        public static WingDefinition GetRandomWing2()
        {
            Random rand = StaticRandom.GetRandomForThread();

            // segment inner count
            //  0 to 4

            // span is the main one
            //  2 is a good value
            //  4 should probably be max

            // span power
            //  1 is linear
            //  < 1 has most of the wing being base cord
            //  > 1 has most being tip cord
            //  1/4 to 4 is a good range

            // cord base / tip
            //  don't let tip be > base
            //  0.2 to 0.8
            const double CHORD_MIN = 0.2;
            const double CHORD_MAX = 0.8;
            double chord_base = rand.NextDouble(CHORD_MIN, CHORD_MAX);
            double chord_tip = rand.NextDouble(CHORD_MIN, CHORD_MAX);
            UtilityMath.MinMax(ref chord_tip, ref chord_base);

            // cord power
            //  I'm not sure exactly what this is
            //  just do 1/4 to 4

            // lift base / tip
            //  0 to 1.25 ???

            // Vertical stabalizer base / tip
            // 0 to 0.5
            double vert_base_percent = rand.NextPow(12);        // very little chance of having vertical stabalizers at the base
            double vert_base_height = rand.NextDouble(0.15, 0.5);

            double vert_tip_percent = rand.NextPow(4);          // better chance of vert stabalizer at tip
            double vert_tip_height = rand.NextDouble(0.15, 0.5);

            // Vertical stabalizer power
            //  1/32 to 32


            // random pitch tilt should be fairly small
            // random sweep could be larger (yaw)
            // random rotation about chassis axis should be moderate (roll)
            double pitch = 10 * rand.NextPow(16, isPlusMinus: true);        // by running it through a power, there's a higher chance of it being zero (especially such a large power)
            double yaw = 30 * rand.NextPow(4, isPlusMinus: true);
            double roll = 20 * rand.NextPow(6, isPlusMinus: true);


            return new WingDefinition()
            {
                Inner_Segment_Count = rand.Next(0, 5),

                Span = (float)rand.NextDouble(0.5, 4),
                Span_Power = (float)rand.NextPercent(1, 4),     // 1/4 to 4, but even chance between (1/4 to 1) and (1 to 4)

                Chord_Base = (float)chord_base,
                Chord_Tip = (float)chord_tip,
                Chord_Power = (float)rand.NextPercent(1, 4),

                VerticalStabilizer_Base = (float)(vert_base_height * vert_base_percent),
                VerticalStabilizer_Tip = (float)(vert_tip_height * vert_tip_percent),
                VerticalStabilizer_Power = (float)rand.NextPercent(1, 32),      // at the extremes, this would pretty much just make one stabalizer (x^1/32: base, x^32: tip)

                Offset = new Vector3
                (
                    (float)rand.NextDouble(0.25, 1),
                    (float)rand.NextDouble(-1, 1),
                    (float)rand.NextDouble(-1.2, 1.2)
                ),

                Rotation = GetRotation(roll, pitch, yaw),
            };
        }

        public static TailDefinition GetDefaultTail()
        {
            return new TailDefinition()
            {
                Boom = new TailDefinition_Boom(),
                Tail = new TailDefinition_Tail(),

                Offset = new Vector3(0, 0, -0.25f),
                Rotation = Quaternion.Identity,
            };
        }
        public static TailDefinition GetRandomTail()
        {
            const double SPAN_MIN = 0.05;
            const double SPAN_MAX = 0.5;
            const double VERT_MIN = 0.05;
            const double VERT_MAX = 0.5;

            Random rand = StaticRandom.GetRandomForThread();

            // segment inner count
            //  0 to 6

            // length
            //  1.5 is standard
            //  3 is max
            double length = (float)rand.NextDouble(0.25, 3);

            // length power
            //  1/4 to 4

            // mid length
            //  0 would be pinched up near base
            //  length would be pinched at tail
            //  length/100 to 1-length/100

            double mid_length_margin = length / 100;
            double mid_length = rand.NextDouble(mid_length_margin, length - mid_length_margin);

            // span base,mid,tip
            //  how wide it is

            // vert base,mid,tip
            //  this should have the same range of values as mids

            // bezier pinch percent
            //  doesn't seem to do much
            //  comment says 0 to 0.4, just do that


            // make the tail optional

            // tail coord
            //  0.15 to 0.85

            // tail horz span
            //  0.25 to 2

            // tail vert height
            //  0.15 to 1.5



            // Offset
            //  X: 0 or 0.5 to 1.5
            //  Y: -1 to 1
            //  Z: 0 to -1.25


            // Rotataion
            //  X axis only, -10 to 10
            double pitch = 10 * rand.NextPow(16, isPlusMinus: true);        // by running it through a power, there's a higher chance of it being zero (especially such a large power)


            return new TailDefinition()
            {
                Boom = new TailDefinition_Boom()
                {
                    Inner_Segment_Count = rand.Next(0, 7),

                    Length = (float)length,
                    Length_Power = (float)rand.NextPercent(1, 4),

                    Mid_Length = (float)mid_length,

                    Span_Base = (float)rand.NextDouble(SPAN_MIN, SPAN_MAX),
                    Span_Mid = (float)rand.NextDouble(SPAN_MIN, SPAN_MAX),
                    Span_Tip = (float)rand.NextDouble(SPAN_MIN, SPAN_MAX),

                    Vert_Base = (float)rand.NextDouble(VERT_MIN, VERT_MAX),
                    Vert_Mid = (float)rand.NextDouble(VERT_MIN, VERT_MAX),
                    Vert_Tip = (float)rand.NextDouble(VERT_MIN, VERT_MAX),

                    Bezier_PinchPercent = (float)rand.NextDouble(0, 0.4),
                },

                Tail = rand.NextDouble() > 0.8 ?        // have a small chance of no tip to the tail (just the boom)
                    null :
                    new TailDefinition_Tail()
                    {
                        Chord = (float)rand.NextDouble(0.15, 0.85),
                        Horz_Span = (float)rand.NextDouble(0.25, 2),
                        Vert_Height = (float)rand.NextDouble(0.15, 1.5),
                    },

                Offset = new Vector3
                (
                    rand.NextDouble() > 0.8 ?
                        0f :
                        (float)rand.NextDouble(0.5, 1.5),
                    (float)rand.NextDouble(-1, 1),
                    (float)rand.NextDouble(-1.25, 0)
                ),

                Rotation = GetRotation(pitch: pitch),
            };
        }

        // Angles are in degrees
        private static Quaternion GetRotation(double? roll = null, double? pitch = null, double? yaw = null)
        {
            // Using this to match the way unity works
            var quat_numerics = System.Numerics.Quaternion.CreateFromYawPitchRoll((float)(yaw ?? 0), (float)(pitch ?? 0), (float)(roll ?? 0));

            return new Quaternion(quat_numerics.X, quat_numerics.Y, quat_numerics.Z, quat_numerics.W);
        }
    }
}
