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
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct DualContouringOctreeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OctreeNode>();
            state.RequireForUpdate<OctreeLOD>();
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
            in OctreeLOD octreeLOD)
        {
            cellBuffer.Clear();
            edgeIntersectionBuffer.Clear();

            if (octreeBuffer.Length == 0)
            {
                return;
            }

            // Calculer la profondeur cible en fonction du LOD
            // LOD 0 = profondeur max (toutes les feuilles)
            // LOD 1+ = profondeur max - LOD
            int targetDepth = math.max(0, octreeNodeInfos.MaxDepth - octreeLOD.Level);

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

                // Si le nœud a des enfants ET qu'on n'a pas atteint la profondeur cible
                if (node.ChildIndex >= 0 && node.Depth < targetDepth)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        nodesToProcess.Add(node.ChildIndex + i);
                    }
                }
                else
                {
                    // Sinon, traiter ce nœud comme une cellule (que ce soit une feuille ou un nœud à la profondeur cible)
                    ProcessLeafNode(scalarFieldBuffer, cellBuffer, edgeIntersectionBuffer, node, octreeNodeInfos);
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
            OctreeNodeInfos octreeNodeInfos)
        {
            int3 cellIndex = node.Position;
            int3 gridSize = octreeNodeInfos.GridSize;
            
            // Calculer la taille de la cellule en fonction de la profondeur du nœud
            float baseCellSize = octreeNodeInfos.MinNodeSize;
            OctreeUtils.GetSizeFromDepth(in octreeNodeInfos.MaxDepth, in node.Depth, in baseCellSize, out float cellSize);
            
            float3 scalarFieldOffset = octreeNodeInfos.OctreeOffset;
            
            // Calculer le stride (nombre de cellules de base que couvre cette cellule LOD)
            int cellStride = (int)math.round(cellSize / baseCellSize);

            // Vérifier que tous les coins de cette cellule LOD sont dans les limites
            int3 maxCorner = cellIndex + new int3(cellStride);
            if (math.any(cellIndex < 0) || math.any(maxCorner >= gridSize))
            {
                return;
            }

            int config = 0;

            for (int i = 0; i < 8; i++)
            {
                var offset = new int3(
                    (i & 1) * cellStride,
                    ((i >> 1) & 1) * cellStride,
                    ((i >> 2) & 1) * cellStride
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

            // Utiliser baseCellSize pour calculer la position car cellIndex est en unités de grille de base
            ScalarFieldUtility.GetWorldPosition(cellIndex, baseCellSize, scalarFieldOffset, out float3 cellPosition);
            float3 vertexPosition = cellPosition + new float3(0.5f, 0.5f, 0.5f) * cellSize;
            var cellNormal = new float3(0, 1, 0);

            if (hasVertex)
            {
                // Créer un ScalarFieldInfos temporaire pour DualContouringHelper
                var scalarFieldInfos = new ScalarFieldInfos
                {
                    GridSize = octreeNodeInfos.GridSize,
                    CellSize = octreeNodeInfos.MinNodeSize,
                    ScalarFieldOffset = octreeNodeInfos.OctreeOffset
                };
                
                DualContouringHelper.CalculateVertexPositionAndNormal(in scalarField,
                    ref edgeIntersections,
                    in cellIndex,
                    in scalarFieldInfos,
                    in cellSize,
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