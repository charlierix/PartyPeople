using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Bepu.Testers.WingInterference
{
    public class RemoveSmallDefinitions
    {
        private const float MIN_ENGINE = 0.1f;

        private const float MIN_WING_SPAN = 0.2f;
        private const float MIN_WING_CHORD = 0.08f;

        private const float MIN_TAIL_TOTAL = 0.1f;
        private const float MIN_TAIL_SPAN_VERT = 0.1f;

        public static PlaneDefinition ExaminePlane(PlaneDefinition def)
        {
            return new PlaneDefinition()
            {
                Engine_0 = ExamineEngine(def.Engine_0),
                Engine_1 = ExamineEngine(def.Engine_1),
                Engine_2 = ExamineEngine(def.Engine_2),

                Wing_0 = ExamineWing(def.Wing_0),
                Wing_1 = ExamineWing(def.Wing_1),
                Wing_2 = ExamineWing(def.Wing_2),

                Tail = ExamineTail(def.Tail),
            };
        }

        private static EngineDefinition ExamineEngine(EngineDefinition def)
        {
            if (def == null)
                return null;

            if (def.Size < MIN_ENGINE)
                return null;

            return def;
        }

        private static WingDefinition ExamineWing(WingDefinition def)
        {
            if (def == null)
                return null;

            if (def.Span < MIN_WING_SPAN)
                return null;

            if (def.Chord_Base < MIN_WING_CHORD)
                return null;

            if (def.Chord_Tip < MIN_WING_CHORD)
                return null;

            return def;
        }

        private static TailDefinition ExamineTail(TailDefinition def)
        {
            if (def == null)
                return null;

            if (def.Boom.Length + (def.Tail?.Chord ?? 0) < MIN_TAIL_TOTAL)
                return null;

            bool has_span = def.Boom.Span_Base >= MIN_TAIL_SPAN_VERT && def.Boom.Span_Mid >= MIN_TAIL_SPAN_VERT && def.Boom.Span_Tip >= MIN_TAIL_SPAN_VERT;
            bool has_vert = def.Boom.Vert_Base >= MIN_TAIL_SPAN_VERT && def.Boom.Vert_Mid >= MIN_TAIL_SPAN_VERT && def.Boom.Vert_Tip >= MIN_TAIL_SPAN_VERT;

            if (!has_span && !has_vert)
                return null;

            return def with
            {
                Boom = def.Boom with
                {
                    Has_Span = has_span,
                    Has_Vert = has_vert,
                },
            };
        }
    }
}
