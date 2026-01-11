using System.Runtime.CompilerServices;
using DualContouring.DualContouring.Debug;
using DualContouring.ScalarField;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.DualContouring
{
    [BurstCompile]
    public partial struct DualContouringSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScalarFieldItem>();
            state.RequireForUpdate<DualContouringOptions>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out DualContouringOptions options) ||
                options.Type != DualContouringType.ScalarField)
            {
                return;
            }

            var job = new DualContouringJob();
            job.ScheduleParallel();
        }
    }

    [BurstCompile]
    internal partial struct DualContouringJob : IJobEntity
    {
        private void Execute(
            ref DynamicBuffer<DualContouringCell> cellBuffer,
            ref DynamicBuffer<DualContouringEdgeIntersection> edgeIntersectionBuffer,
            in DynamicBuffer<ScalarFieldItem> scalarFieldBuffer,
            in ScalarFieldInfos scalarFieldInfos)
        {
            cellBuffer.Clear();
            edgeIntersectionBuffer.Clear();

            int3 cellGridSize = scalarFieldInfos.GridSize - new int3(1, 1, 1);

            for (int y = 0; y < cellGridSize.y; y++)
            {
                for (int z = 0; z < cellGridSize.z; z++)
                {
                    for (int x = 0; x < cellGridSize.x; x++)
                    {
                        ProcessCell(scalarFieldBuffer, cellBuffer, edgeIntersectionBuffer, new int3(x, y, z), scalarFieldInfos);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessCell(
            in DynamicBuffer<ScalarFieldItem> scalarField,
            DynamicBuffer<DualContouringCell> cells,
            DynamicBuffer<DualContouringEdgeIntersection> edgeIntersections,
            int3 cellIndex,
            ScalarFieldInfos scalarFieldInfos)
        {
            int3 gridSize = scalarFieldInfos.GridSize;
            float cellSize = scalarFieldInfos.CellSize;
            float3 scalarFieldOffset = scalarFieldInfos.ScalarFieldOffset;

            int config = 0;

            for (int i = 0; i < 8; i++)
            {
                var offset = new int3(
                    i & 1,
                    (i >> 1) & 1,
                    (i >> 2) & 1
                );

                int3 cornerIndex = cellIndex + offset;
                int scalarIndex = ScalarFieldUtility.CoordToIndex(cornerIndex, gridSize);

                if (scalarIndex >= 0 && scalarIndex < scalarField.Length)
                {
                    ScalarFieldItem value = scalarField[scalarIndex];

                    if (value.Value >= 0)
                    {
                        config |= 1 << i;
                    }
                }
            }

            bool hasVertex = config != 0 && config != 255;

            ScalarFieldUtility.GetWorldPosition(cellIndex, cellSize, scalarFieldOffset, out float3 cellPosition);
            float3 vertexPosition = cellPosition + new float3(0.5f, 0.5f, 0.5f) * cellSize;
            var cellNormal = new float3(0, 1, 0);

            if (hasVertex)
            {
                DualContouringHelper.CalculateVertexPositionAndNormal(in scalarField,
                    ref edgeIntersections,
                    in cellIndex,
                    in scalarFieldInfos,
                    out vertexPosition,
                    out cellNormal);
            }

            cells.Add(new DualContouringCell
            {
                Position = cellPosition,
                Size = cellSize,
                HasVertex = hasVertex,
                VertexPosition = vertexPosition,
                Normal = cellNormal,
                GridIndex = cellIndex
            });
        }
    }
}