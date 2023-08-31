using Game.Core;
using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace Game.Bepu.Testers.WingInterference
{
    public static class PlaneBuilder
    {
        private const float ENGINE_STANDARD_HEIGHT = 0.4f;      // these are what the scale of the engine should be when size is "one"
        private const float ENGINE_STANDARD_RADIUS = 0.3f;

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
            return def;
        }

        public static WingDefinition BuildWing(WingDefinition def)
        {
            return def;
        }

        public static TailDefinition BuildTail(TailDefinition def)
        {
            return def;
        }
    }
}
