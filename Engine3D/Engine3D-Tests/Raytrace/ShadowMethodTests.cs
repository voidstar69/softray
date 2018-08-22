using System;
using Engine3D.Raytrace;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using Scene = Engine3D.Scene;
using Vector = Engine3D.Vector;

namespace Engine3D_Tests.Raytrace
{
    [TestClass]
    public class ShadowMethodTests
    {
        private static readonly Random Random = new Random();

        [TestMethod]
        public void DynamicVsStaticShadowMethods()
        {
            IRayIntersectable geometry = new Sphere(Vector.Zero, 0.5);
            var scene = new Scene();
            const byte resolution = 10;
            string instanceKey = null;
            const int randomSeed = 12345;

            var context = new RenderContext(new Random(randomSeed));
            var dynamicShadowMethod = new ShadowMethod(geometry, scene, false, resolution, instanceKey, context);

            context = new RenderContext(new Random(randomSeed));
            var staticShadowMethod = new ShadowMethod(geometry, scene, true, resolution, instanceKey, context);

            const int numRays = 1000000;
            var numRaysHit = 0;

            for (var i = 0; i < numRays; i++)
            {
                var start = MakeRandomVector(-2, 2, -2, 2, -2, 2);
                var dir = MakeRandomVector(-1, 1, -1, 1, -1, 1);
                var info = dynamicShadowMethod.IntersectRay(start, dir, context);
                if (info != null)
                    numRaysHit++;

                var info2 = staticShadowMethod.IntersectRay(start, dir, context);

                // TODO: find a scenario where this fails, e.g. multi-threaded render; vary number of threads; cache shadows to disk; repeat rays
                Assert.AreEqual(info, info2);
            }

            //Assert.AreEqual(numRays, numRaysHit, "Num rays hit {0} should be the same as total rays {1}", numRaysHit, numRays);
            //Assert.IsTrue(numRays * 0.498 < numRaysHit && numRaysHit < numRays * 0.502, "Num rays hit {0} should be roughly half of total rays {1}", numRaysHit, numRays);
            Console.WriteLine("Num rays hit: {0} / {1}", numRaysHit, numRays);
        }

        private Vector MakeRandomVector(double minX, double maxX, double minY, double maxY, double minZ, double maxZ)
        {
            return new Vector((maxX - minX) * NextRandomDouble() + minX,
                (maxY - minY) * NextRandomDouble() + minY,
                (maxZ - minZ) * NextRandomDouble() + minZ);
        }

        private double NextRandomDouble()
        {
            return Random.NextDouble();
        }
    }
}