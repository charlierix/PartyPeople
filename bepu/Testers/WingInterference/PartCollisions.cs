using Game.Math_WPF.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Bepu.Testers.WingInterference
{
    public static class PartCollisions
    {
        #region enum: PartSlot

        public enum PartSlot
        {
            Engine_0,
            Engine_1,
            Engine_2,

            Wing_0,
            Wing_1,
            Wing_2,

            Tail,
        }

        #endregion

        #region records: PartDefinition

        private record PartDefinition_Engine
        {
            public PartSlot Slot { get; init; }
            public EngineDefinition_Meshes Mesh { get; init; }
        }

        private record PartDefinition_WingTail
        {
            public PartSlot Slot { get; init; }
            public ITriangle_wpf[] Triangles { get; init; }
        }

        #endregion

        public static (PartSlot, PartSlot)[] FindCollisions(PlaneDefinition def)
        {
            // Extract into easier to use lists
            var (engines, wingtails) = GetUsedPartDefinitions(def);

            var retVal = new List<(PartSlot, PartSlot)>();

            // Engine - Engine
            retVal.AddRange(Engine_Engine(engines));

            // Engine - Wing/Tail

            // Wing/Tail - Wing/Tail

            return retVal.ToArray();
        }

        #region Private Methods

        private static (PartDefinition_Engine[] engines, PartDefinition_WingTail[] wingtail) GetUsedPartDefinitions(PlaneDefinition def)
        {
            var engines = new List<PartDefinition_Engine>();
            var wingtail = new List<PartDefinition_WingTail>();

            // Engines
            if (def.Engine_0 != null)
                engines.Add(new PartDefinition_Engine() { Slot = PartSlot.Engine_0, Mesh = def.Engine_0.Meshes });

            if (def.Engine_1 != null)
                engines.Add(new PartDefinition_Engine() { Slot = PartSlot.Engine_1, Mesh = def.Engine_1.Meshes });

            if (def.Engine_2 != null)
                engines.Add(new PartDefinition_Engine() { Slot = PartSlot.Engine_2, Mesh = def.Engine_2.Meshes });

            // Wings
            if (def.Wing_0 != null)
                wingtail.Add(new PartDefinition_WingTail() { Slot = PartSlot.Wing_0, Triangles = def.Wing_0.Meshes.Triangles });

            if (def.Wing_1 != null)
                wingtail.Add(new PartDefinition_WingTail() { Slot = PartSlot.Wing_1, Triangles = def.Wing_1.Meshes.Triangles });

            if (def.Wing_2 != null)
                wingtail.Add(new PartDefinition_WingTail() { Slot = PartSlot.Wing_2, Triangles = def.Wing_2.Meshes.Triangles });

            // Tail
            if (def.Tail != null)
            {
                var triangles = new List<ITriangle_wpf>();

                triangles.AddRange(def.Tail.Boom.Meshes.Triangles);

                if (def.Tail.Tail != null)
                    triangles.AddRange(def.Tail.Tail.Meshes.Triangles);

                wingtail.Add(new PartDefinition_WingTail() { Slot = PartSlot.Tail, Triangles = triangles.ToArray() });
            }

            return (engines.ToArray(), wingtail.ToArray());
        }

        private static (PartSlot, PartSlot)[] Engine_Engine(PartDefinition_Engine[] engines)
        {
            var retVal = new List<(PartSlot, PartSlot)>();

            for (int i = 0; i < engines.Length - 1; i++)
            {
                for (int j = 0; j < engines.Length; j++) 
                {
                    //Math3D.GetIntersection_Triangle_Triangle();



                }
            }

            return retVal.ToArray();
        }

        #endregion

        #region move to math3d

        //private static bool IsIntersecting_Capsule_Capsule(Capsule_wpf capsule1, Capsule_wpf capsule2)
        //{
        //    capsule1 = capsule1.IsInterior ?
        //        capsule1 :
        //        capsule1.ToInterior();

        //    capsule2 = capsule2.IsInterior ?
        //        capsule2 :
        //        capsule2.ToInterior();

        //    Math3D.GetClosestPoints_Line_LineSegment




        //}

        //private static bool IsIntersecting_Triangle_Capsule()
        //{

        //}


        #endregion
    }
}
