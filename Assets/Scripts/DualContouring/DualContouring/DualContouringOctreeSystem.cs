using System.Runtime.CompilerServices;
using DualContouring.DualContouring.Debug;
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
                    ProcessLeafNode(octreeBuffer, scalarFieldBuffer, cellBuffer, edgeIntersectionBuffer, node, octreeInfos, scalarFieldInfos);
                }
            }

            nodesToProcess.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessLeafNode(
            in DynamicBuffer<OctreeNode> octreeBuffer,
            in DynamicBuffer<ScalarFieldItem> scalarField,
            DynamicBuffer<DualContouringCell> cells,
            DynamicBuffer<DualContouringEdgeIntersection> edgeIntersections,
            OctreeNode node,
            OctreeInfos octreeInfos,
            ScalarFieldInfos scalarFieldInfos)
        {
            int3 cellPosition = node.Position;
            int3 gridSize = scalarFieldInfos.GridSize;
            float cellSize = octreeInfos.MinNodeSize;
            float3 octreeOffset = octreeInfos.OctreeOffset;

            int3 cellGridSize = gridSize - new int3(1, 1, 1);
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
                DualContouringHelper.CalculateVertexPositionAndNormal(in scalarField,
                    ref edgeIntersections,
                    in cellPosition,
                    in scalarFieldInfos,
                    out vertexPosition,
                    out cellNormal);
            }

            cells.Add(new DualContouringCell
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