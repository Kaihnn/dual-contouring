using System.Runtime.CompilerServices;
using DualContouring.DualContouring.Debug;
using DualContouring.Octrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.DualContouring
{
    struct OctreeTraversalNode
    {
        public int NodeIndex;
        public int Depth;
    }

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
            in OctreeInfos octreeInfos)
        {
            if (octreeBuffer.Length == 0)
            {
                return;
            }

            cellBuffer.Clear();
            edgeIntersectionBuffer.Clear();

            int3 cellGridSize = octreeInfos.GridSize - new int3(1, 1, 1);
            int maxDepth = octreeInfos.MaxDepth;

            var nodesToVisit = new NativeList<OctreeTraversalNode>(64, Allocator.Temp);
            nodesToVisit.Add(new OctreeTraversalNode { NodeIndex = 0, Depth = 0 });

            while (nodesToVisit.Length > 0)
            {
                int lastIndex = nodesToVisit.Length - 1;
                OctreeTraversalNode current = nodesToVisit[lastIndex];
                nodesToVisit.RemoveAtSwapBack(lastIndex);

                OctreeNode node = octreeBuffer[current.NodeIndex];

                if (node.ChildIndex < 0)
                {
                    // Leaf node: only process if at maxDepth (size 1 cells)
                    // Leaves before maxDepth are uniform regions with no surface
                    if (current.Depth >= maxDepth)
                    {
                        ProcessCell(octreeBuffer, cellBuffer, cellGridSize, node.Position, edgeIntersectionBuffer, octreeInfos);
                    }
                }
                else
                {
                    int childDepth = current.Depth + 1;
                    for (int i = 0; i < 8; i++)
                    {
                        nodesToVisit.Add(new OctreeTraversalNode
                        {
                            NodeIndex = node.ChildIndex + i,
                            Depth = childDepth
                        });
                    }
                }
            }

            nodesToVisit.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessCell(
            in DynamicBuffer<OctreeNode> octreeBuffer,
            DynamicBuffer<DualContouringCell> cellsBuffer,
            int3 cellGridSize,
            int3 cellPosition,
            DynamicBuffer<DualContouringEdgeIntersection> edgeIntersectionBuffer,
            OctreeInfos octreeInfos)
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
                DualContouringOctreeHelper.CalculateVertexPositionAndNormal(in octreeBuffer,
                    ref edgeIntersectionBuffer,
                    in cellPosition,
                    in octreeInfos,
                    out vertexPosition,
                    out cellNormal);

                cellsBuffer.Add(new DualContouringCell
                {
                    Position = worldPosition,
                    Size = cellSize,
                    HasVertex = true,
                    VertexPosition = vertexPosition,
                    Normal = cellNormal,
                    GridIndex = cellPosition
                });
            }
        }
    }
}