using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using DualContouring.ScalarField;

namespace DualContouring.Tests.PlayMode
{
    public class ScalarFieldTests : EcsSystemTestBase
    {
        [Test]
        public void ScalarField_CreateEntity_HasCorrectComponents()
        {
            int3 gridSize = new int3(4, 4, 4);
            float cellSize = 1.0f;
            float3 offset = float3.zero;

            Entity entity = CreateScalarFieldEntity(gridSize, cellSize, offset);

            Assert.IsTrue(EntityManager.HasComponent<ScalarFieldInfos>(entity));
            Assert.IsTrue(EntityManager.HasBuffer<ScalarFieldItem>(entity));

            var infos = GetComponent<ScalarFieldInfos>(entity);
            Assert.AreEqual(gridSize.x, infos.GridSize.x);
            Assert.AreEqual(gridSize.y, infos.GridSize.y);
            Assert.AreEqual(gridSize.z, infos.GridSize.z);
            Assert.AreEqual(cellSize, infos.CellSize);
        }

        [Test]
        public void ScalarField_BufferSize_MatchesGridDimensions()
        {
            int3 gridSize = new int3(4, 5, 6);

            Entity entity = CreateScalarFieldEntity(gridSize, 1.0f, float3.zero);

            var buffer = GetBuffer<ScalarFieldItem>(entity);
            int expectedSize = gridSize.x * gridSize.y * gridSize.z;

            Assert.AreEqual(expectedSize, buffer.Length);
        }

        [Test]
        public void ScalarField_SetValues_RetrievesCorrectly()
        {
            int3 gridSize = new int3(3, 3, 3);

            Entity entity = CreateScalarFieldEntity(gridSize, 1.0f, float3.zero);
            var buffer = GetBuffer<ScalarFieldItem>(entity);

            int testIndex = ScalarFieldUtility.CoordToIndex(1, 1, 1, gridSize);
            buffer[testIndex] = new ScalarFieldItem { Value = 42.0f };

            Assert.AreEqual(42.0f, buffer[testIndex].Value);
        }

        [Test]
        public void ScalarField_SphereSDF_HasSurfaceCrossings()
        {
            int3 gridSize = new int3(8, 8, 8);
            float cellSize = 1.0f;
            float3 center = new float3(4, 4, 4);
            float radius = 2.5f;

            Entity entity = CreateScalarFieldEntity(gridSize, cellSize, float3.zero);
            var buffer = GetBuffer<ScalarFieldItem>(entity);

            FillWithSphereSDF(buffer, gridSize, center, radius);

            bool hasPositive = false;
            bool hasNegative = false;

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Value > 0) hasPositive = true;
                if (buffer[i].Value < 0) hasNegative = true;
            }

            Assert.IsTrue(hasPositive, "Sphere SDF should have positive values outside");
            Assert.IsTrue(hasNegative, "Sphere SDF should have negative values inside");
        }

        private Entity CreateScalarFieldEntity(int3 gridSize, float cellSize, float3 offset)
        {
            Entity entity = CreateEntity(
                typeof(ScalarFieldInfos)
            );

            AddComponent(entity, new ScalarFieldInfos
            {
                GridSize = gridSize,
                CellSize = cellSize,
                ScalarFieldOffset = offset
            });

            var buffer = AddBuffer<ScalarFieldItem>(entity);
            int totalSize = gridSize.x * gridSize.y * gridSize.z;
            buffer.ResizeUninitialized(totalSize);

            for (int i = 0; i < totalSize; i++)
            {
                buffer[i] = new ScalarFieldItem { Value = 0 };
            }

            return entity;
        }

        private void FillWithSphereSDF(DynamicBuffer<ScalarFieldItem> buffer, int3 gridSize, float3 center, float radius)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                for (int z = 0; z < gridSize.z; z++)
                {
                    for (int x = 0; x < gridSize.x; x++)
                    {
                        int index = ScalarFieldUtility.CoordToIndex(x, y, z, gridSize);
                        float3 position = new float3(x, y, z);
                        float distance = math.length(position - center) - radius;
                        buffer[index] = new ScalarFieldItem { Value = distance };
                    }
                }
            }
        }
    }
}
