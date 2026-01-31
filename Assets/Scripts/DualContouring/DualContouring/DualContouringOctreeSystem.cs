using System.Runtime.CompilerServices;
using DualContouring.Octrees;
using DualContouring.ScalarField;
using Unity.Burst;
using Unity.Collections;
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
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
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
            in OctreeNodeInfos octreeNodeInfos,
            in ScalarFieldInfos scalarFieldInfos)
        {
            cellBuffer.Clear();
            edgeIntersectionBuffer.Clear();

            if (octreeBuffer.Length == 0)
            {
                return;
            }

            NativeList<int> nodesToProcess = new NativeList<int>(math.max(64, octreeBuffer.Length / 8), Allocator.Temp);
            nodesToProcess.Add(0);

            while (nodesToProcess.Length > 0)
            {
                int lastIndex = nodesToProcess.Length - 1;
                int nodeIndex = nodesToProcess[lastIndex];
                nodesToProcess.RemoveAtSwapBack(lastIndex);

                if (nodeIndex < 0 || nodeIndex >= octreeBuffer.Length)
                {
                    continue;
                }

                OctreeNode node = octreeBuffer[nodeIndex];

                if (node.ChildIndex >= 0)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        nodesToProcess.Add(node.ChildIndex + i);
                    }
                }
                else
                {
                    ProcessLeafNode(scalarFieldBuffer, cellBuffer, edgeIntersectionBuffer, node, scalarFieldInfos);
                }
            }

            nodesToProcess.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessLeafNode(
            in DynamicBuffer<ScalarFieldItem> scalarField,
            DynamicBuffer<DualContouringCell> cells,
            DynamicBuffer<DualContouringEdgeIntersection> edgeIntersections,
            OctreeNode node,
            ScalarFieldInfos scalarFieldInfos)
        {
            int3 cellIndex = node.Position;
            int3 gridSize = scalarFieldInfos.GridSize;
            float cellSize = scalarFieldInfos.CellSize;
            float3 scalarFieldOffset = scalarFieldInfos.ScalarFieldOffset;

            int3 cellGridSize = gridSize - new int3(1, 1, 1);
            if (math.any(cellIndex < 0) || math.any(cellIndex >= cellGridSize))
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