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

            // Calculer l'index de configuration (8 bits, un par coin)
            int config = 0;

            // Les 8 coins d'une cellule en ordre: (0,0,0), (1,0,0), (0,1,0), (1,1,0), (0,0,1), (1,0,1), (0,1,1), (1,1,1)
            for (int i = 0; i < 8; i++)
            {
                var offset = new int3(
                    i & 1, // bit 0
                    (i >> 1) & 1, // bit 1
                    (i >> 2) & 1 // bit 2
                );

                int3 cornerIndex = cellIndex + offset;
                int scalarIndex = ScalarFieldUtility.CoordToIndex(cornerIndex, gridSize);

                if (scalarIndex >= 0 && scalarIndex < scalarField.Length)
                {
                    ScalarFieldItem value = scalarField[scalarIndex];

                    // Si la valeur est positive (à l'intérieur de la surface), mettre le bit à 1
                    if (value.Value >= 0)
                    {
                        config |= 1 << i;
                    }
                }
            }

            // Si la configuration n'est ni 0 (tout dehors) ni 255 (tout dedans), il y a une surface
            bool hasVertex = config != 0 && config != 255;

            // Calculer la position du vertex (pour l'instant, on utilise le centre de la cellule)
            ScalarFieldUtility.GetWorldPosition(cellIndex, cellSize, scalarFieldOffset, out float3 cellPosition);
            float3 vertexPosition = cellPosition + new float3(0.5f, 0.5f, 0.5f) * cellSize;
            var cellNormal = new float3(0, 1, 0);

            // Si on a une intersection, calculer une meilleure position du vertex et la normale
            if (hasVertex)
            {
                DualContouringHelper.CalculateVertexPositionAndNormal(in scalarField,
                    ref edgeIntersections,
                    in cellIndex,
                    in scalarFieldInfos,
                    out vertexPosition,
                    out cellNormal);
            }

            // Ajouter la cellule au buffer
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