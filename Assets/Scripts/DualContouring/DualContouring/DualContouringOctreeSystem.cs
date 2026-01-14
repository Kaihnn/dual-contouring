using System.Runtime.CompilerServices;
using DualContouring.DualContouring.Debug;
using DualContouring.Octrees;
using DualContouring.ScalarField;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.DualContouring
{
    [BurstCompile]
    public partial struct DualContouringOctreeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OctreeNode>();
            state.RequireForUpdate<DualContouringOptions>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out DualContouringOptions options) ||
                options.Type != DualContouringType.Octree)
            {
                return;
            }

            var job = new DualContouringOctreeJob();
            job.ScheduleParallel();
        }
    }

    [BurstCompile]
    internal partial struct DualContouringOctreeJob : IJobEntity
    {
        private void Execute(
            ref DynamicBuffer<DualContouringCell> cellBuffer,
            ref DynamicBuffer<DualContouringEdgeIntersection> edgeIntersectionBuffer,
            in DynamicBuffer<OctreeNode> octreeBuffer,
            in DynamicBuffer<ScalarFieldItem> scalarFieldBuffer,
            in OctreeInfos octreeInfos,
            in ScalarFieldInfos scalarFieldInfos)
        {
            if (octreeBuffer.Length == 0)
            {
                return;
            }

            cellBuffer.Clear();
            edgeIntersectionBuffer.Clear();

            int3 cellGridSize = scalarFieldInfos.GridSize - new int3(1, 1, 1);

            for (int y = 0; y < cellGridSize.y; y++)
            {
                for (int z = 0; z < cellGridSize.z; z++)
                {
                    for (int x = 0; x < cellGridSize.x; x++)
                    {
                        ProcessCell(octreeBuffer, scalarFieldBuffer, cellBuffer, cellGridSize, new int3(x, y, z), edgeIntersectionBuffer, octreeInfos, scalarFieldInfos);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessCell(
            in DynamicBuffer<OctreeNode> octreeBuffer,
            in DynamicBuffer<ScalarFieldItem> scalarFieldBuffer,
            DynamicBuffer<DualContouringCell> cellsBuffer,
            int3 cellGridSize,
            int3 cellPosition,
            DynamicBuffer<DualContouringEdgeIntersection> edgeIntersectionBuffer,
            OctreeInfos octreeInfos,
            ScalarFieldInfos scalarFieldInfos)
        {
            float cellSize = octreeInfos.MinNodeSize;
            float3 octreeOffset = octreeInfos.OctreeOffset;

            if (math.any(cellPosition < 0) || math.any(cellPosition >= cellGridSize))
            {
                return;
            }

            int config = 0;

            for (int i = 0; i < 8; i++)
            {
                var offset = new int3(
                    i & 1,
                    (i >> 1) & 1,
                    (i >> 2) & 1
                );

                int3 cornerPosition = cellPosition + offset;
                float valueAtPosition = OctreeUtils.GetValueAtPosition(octreeBuffer, octreeInfos, cornerPosition);
                if (valueAtPosition >= 0)
                {
                    config |= 1 << i;
                }
            }

            bool hasVertex = config != 0 && config != 255;

            OctreeUtils.GetWorldPositionFromPosition(cellPosition, cellSize, octreeOffset, out float3 worldPosition);
            float3 vertexPosition = worldPosition + new float3(0.5f, 0.5f, 0.5f) * cellSize;
            var cellNormal = new float3(0, 1, 0);

            if (hasVertex)
            {
                DualContouringHelper.CalculateVertexPositionAndNormal(in scalarFieldBuffer,
                    ref edgeIntersectionBuffer,
                    in cellPosition,
                    in scalarFieldInfos,
                    out vertexPosition,
                    out cellNormal);
            }

            cellsBuffer.Add(new DualContouringCell
            {
                Position = worldPosition,
                Size = cellSize,
                HasVertex = hasVertex,
                VertexPosition = vertexPosition,
                Normal = cellNormal,
                GridIndex = cellPosition
            });
        }
    }
}