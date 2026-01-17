using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using DualContouring.DualContouring;

namespace DualContouring.Tests.EditMode
{
    public class QefSolverTests
    {
        [Test]
        public void SolveQef_TwoPerpendicularPlanes_FindsIntersection()
        {
            var positions = new NativeArray<float3>(2, Allocator.Temp);
            var normals = new NativeArray<float3>(2, Allocator.Temp);

            positions[0] = new float3(0.5f, 0, 0);
            normals[0] = new float3(1, 0, 0);

            positions[1] = new float3(0, 0.5f, 0);
            normals[1] = new float3(0, 1, 0);

            float3 massPoint = (positions[0] + positions[1]) * 0.5f;

            QefSolver.SolveQef(positions, normals, 2, massPoint, out float3 result);

            Assert.AreEqual(0.5f, result.x, 0.01f, "X coordinate should be 0.5");
            Assert.AreEqual(0.5f, result.y, 0.01f, "Y coordinate should be 0.5");

            positions.Dispose();
            normals.Dispose();
        }

        [Test]
        public void SolveQef_ThreePerpendicularPlanes_FindsCorner()
        {
            var positions = new NativeArray<float3>(3, Allocator.Temp);
            var normals = new NativeArray<float3>(3, Allocator.Temp);

            positions[0] = new float3(1, 0, 0);
            normals[0] = new float3(1, 0, 0);

            positions[1] = new float3(0, 1, 0);
            normals[1] = new float3(0, 1, 0);

            positions[2] = new float3(0, 0, 1);
            normals[2] = new float3(0, 0, 1);

            float3 massPoint = (positions[0] + positions[1] + positions[2]) / 3f;

            QefSolver.SolveQef(positions, normals, 3, massPoint, out float3 result);

            Assert.AreEqual(1f, result.x, 0.01f, "X coordinate should be 1");
            Assert.AreEqual(1f, result.y, 0.01f, "Y coordinate should be 1");
            Assert.AreEqual(1f, result.z, 0.01f, "Z coordinate should be 1");

            positions.Dispose();
            normals.Dispose();
        }

        [Test]
        public void SolveQef_ParallelPlanes_ReturnsMassPoint()
        {
            var positions = new NativeArray<float3>(2, Allocator.Temp);
            var normals = new NativeArray<float3>(2, Allocator.Temp);

            positions[0] = new float3(0, 0, 0);
            normals[0] = new float3(1, 0, 0);

            positions[1] = new float3(1, 0, 0);
            normals[1] = new float3(1, 0, 0);

            float3 massPoint = new float3(0.5f, 0.5f, 0.5f);

            QefSolver.SolveQef(positions, normals, 2, massPoint, out float3 result);

            Assert.IsFalse(float.IsNaN(result.x), "Result should not be NaN");
            Assert.IsFalse(float.IsNaN(result.y), "Result should not be NaN");
            Assert.IsFalse(float.IsNaN(result.z), "Result should not be NaN");

            positions.Dispose();
            normals.Dispose();
        }

        [Test]
        public void SolveQef_SinglePlane_ResultLiesOnPlane()
        {
            var positions = new NativeArray<float3>(1, Allocator.Temp);
            var normals = new NativeArray<float3>(1, Allocator.Temp);

            positions[0] = new float3(1, 0, 0);
            normals[0] = math.normalize(new float3(1, 0, 0));

            float3 massPoint = new float3(0.5f, 0.5f, 0.5f);

            QefSolver.SolveQef(positions, normals, 1, massPoint, out float3 result);

            float distanceToPlane = math.dot(result - positions[0], normals[0]);
            Assert.AreEqual(0f, distanceToPlane, 0.1f, "Result should lie near the plane");

            positions.Dispose();
            normals.Dispose();
        }

        [Test]
        public void SolveQef_EmptyInput_ReturnsMassPoint()
        {
            var positions = new NativeArray<float3>(0, Allocator.Temp);
            var normals = new NativeArray<float3>(0, Allocator.Temp);

            float3 massPoint = new float3(1, 2, 3);

            QefSolver.SolveQef(positions, normals, 0, massPoint, out float3 result);

            Assert.AreEqual(massPoint.x, result.x, 0.01f);
            Assert.AreEqual(massPoint.y, result.y, 0.01f);
            Assert.AreEqual(massPoint.z, result.z, 0.01f);

            positions.Dispose();
            normals.Dispose();
        }
    }
}
