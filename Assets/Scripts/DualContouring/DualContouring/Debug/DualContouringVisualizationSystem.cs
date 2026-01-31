using DualContouring.ScalarField;
using DualContouring.ScalarField.Debug;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DualContouring.DualContouring.Debug
{
    public partial class DualContouringVisualizationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            EntityManager.CreateSingleton<DualContouringVisualizationOptions>();
        }

        protected override void OnUpdate()
        {
        }

        public void DrawGizmos()
        {
            // Récupérer le singleton et vérifier si la visualisation est activée
            var visualizationOptions = SystemAPI.GetSingleton<DualContouringVisualizationOptions>();
            if (!visualizationOptions.Enabled)
            {
                return;
            }

            foreach ((DynamicBuffer<DualContouringCell> cellBuffer, DynamicBuffer<DualContouringEdgeIntersection> edgeIntersectionBuffer,
                         RefRO<ScalarFieldSelectedCell> selectedCell, RefRO<ScalarFieldInfos> scalarFieldInfos, RefRO<LocalToWorld> localToWorld) in SystemAPI
                         .Query<DynamicBuffer<DualContouringCell>, DynamicBuffer<DualContouringEdgeIntersection>, RefRO<ScalarFieldSelectedCell>,
                             RefRO<ScalarFieldInfos>, RefRO<LocalToWorld>>().WithAll<ScalarFieldSelected>())
            {
                int3 cellGridSize = scalarFieldInfos.ValueRO.GridSize - new int3(1, 1, 1);
                int3 min = selectedCell.ValueRO.Min;
                int3 max = selectedCell.ValueRO.Max;
                
                // Vérifier si la plage est valide
                bool validRange = math.all(min >= 0) && math.all(max >= min) && math.all(max < cellGridSize);
                
                if (!validRange)
                {
                    // Si la plage est invalide, dessiner toutes les cellules
                    DrawAllCells(cellBuffer, localToWorld.ValueRO);

                    if (visualizationOptions.DrawEdgeIntersections)
                    {
                        DrawAllEdgeIntersections(edgeIntersectionBuffer, localToWorld.ValueRO);
                    }
                }
                else
                {
                    // Dessiner toutes les cellules dans la plage Min-Max
                    for (int i = 0; i < cellBuffer.Length; i++)
                    {
                        DualContouringCell cell = cellBuffer[i];
                        int3 cellGridIndex = cell.GridIndex;
                        
                        // Vérifier si la cellule est dans la plage Min-Max
                        if (math.all(cellGridIndex >= min) && math.all(cellGridIndex <= max))
                        {
                            DrawCell(cell, localToWorld.ValueRO, visualizationOptions.DrawEmptyCell);

                            // Dessiner les intersections d'arêtes pour cette cellule si activé
                            if (visualizationOptions.DrawEdgeIntersections)
                            {
                                DrawEdgeIntersectionsForCell(edgeIntersectionBuffer, i, localToWorld.ValueRO, visualizationOptions.DrawMassPoint, cellBuffer);
                            }
                        }
                    }
                }
            }
        }

        private void DrawWireCube(float3 center, float3 size)
        {
            Gizmos.DrawWireCube(center, size);
        }

        private void DrawAllCells(DynamicBuffer<DualContouringCell> cellBuffer, LocalToWorld localToWorld)
        {
            var visualizationOptions = SystemAPI.GetSingleton<DualContouringVisualizationOptions>();
            
            foreach (DualContouringCell cell in cellBuffer)
            {
                DrawCell(cell, localToWorld, visualizationOptions.DrawEmptyCell);
            }
        }

        private void DrawCell(DualContouringCell cell, LocalToWorld localToWorld, bool drawEmptyCell = false)
        {
            if (cell.HasVertex)
            {
                // Appliquer le transform à toutes les positions
                float3 cellCenter = math.transform(localToWorld.Value, cell.Position + new float3(0.5f, 0.5f, 0.5f) * cell.Size);
                float3 vertexPosition = math.transform(localToWorld.Value, cell.VertexPosition);

                // Dessiner la cellule en vert si elle a un vertex
                Gizmos.color = Color.green;
                DrawWireCube(cellCenter, new float3(cell.Size, cell.Size, cell.Size));

                // Dessiner le vertex en jaune
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(vertexPosition, cell.Size * 0.1f);

                // Dessiner la normale de la cellule en magenta
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(vertexPosition,
                    vertexPosition + cell.Normal * cell.Size * 0.5f);
            }
            else if (drawEmptyCell)
            {
                // Appliquer le transform
                float3 cellCenter = math.transform(localToWorld.Value, cell.Position + new float3(0.5f, 0.5f, 0.5f) * cell.Size);

                // Dessiner la cellule en gris si elle n'a pas de vertex
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                DrawWireCube(cellCenter, new float3(cell.Size, cell.Size, cell.Size));
            }
        }

        private void DrawAllEdgeIntersections(DynamicBuffer<DualContouringEdgeIntersection> edgeIntersectionBuffer, LocalToWorld localToWorld)
        {
            foreach (DualContouringEdgeIntersection edgeIntersection in edgeIntersectionBuffer)
            {
                DrawEdgeIntersection(edgeIntersection, localToWorld);
            }
        }

        private void DrawEdgeIntersectionsForCell(DynamicBuffer<DualContouringEdgeIntersection> edgeIntersectionBuffer, 
            int cellIndex, 
            LocalToWorld localToWorld, 
            bool drawMassPoint,
            DynamicBuffer<DualContouringCell> cellBuffer)
        {
            // Récupérer le GridIndex de la cellule
            if (cellIndex < 0 || cellIndex >= cellBuffer.Length)
                return;
                
            int3 cellGridIndex = cellBuffer[cellIndex].GridIndex;
            int cellStride = (int)math.round(cellBuffer[cellIndex].Size);
            
            float3 massPoint = float3.zero;
            int count = 0;

            foreach (DualContouringEdgeIntersection edgeIntersection in edgeIntersectionBuffer)
            {
                // Vérifier si l'edge appartient à cette cellule
                // Une edge appartient à une cellule si son start ou end est dans la cellule
                int3 edgeStart = edgeIntersection.Edge.Start;
                int3 edgeEnd = edgeIntersection.Edge.End;
                
                // Pour qu'une edge appartienne à la cellule, au moins un de ses points doit être dans la cellule
                int3 diffStart = edgeStart - cellGridIndex;
                int3 diffEnd = edgeEnd - cellGridIndex;
                
                // L'edge appartient si start ou end est dans [cellGridIndex, cellGridIndex + cellStride]
                bool startInCell = math.all(diffStart >= 0) && math.all(diffStart <= cellStride);
                bool endInCell = math.all(diffEnd >= 0) && math.all(diffEnd <= cellStride);
                bool belongsToCell = startInCell || endInCell;
                
                if (belongsToCell)
                {
                    DrawEdgeIntersection(edgeIntersection, localToWorld);
                    massPoint += edgeIntersection.Position;
                    count++;
                }
            }

            // Dessiner le centre de masse si activé et s'il y a des intersections
            if (drawMassPoint && count > 0)
            {
                massPoint /= count;
                float3 worldMassPoint = math.transform(localToWorld.Value, massPoint);

                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(worldMassPoint, 0.08f);

                // Dessiner des lignes vers les intersections
                Gizmos.color = new Color(0.5f, 0.5f, 1.0f, 0.5f);
                foreach (DualContouringEdgeIntersection edgeIntersection in edgeIntersectionBuffer)
                {
                    int3 edgeStart = edgeIntersection.Edge.Start;
                    int3 edgeEnd = edgeIntersection.Edge.End;
                    int3 diffStart = edgeStart - cellGridIndex;
                    int3 diffEnd = edgeEnd - cellGridIndex;
                    
                    bool startInCell = math.all(diffStart >= 0) && math.all(diffStart <= cellStride);
                    bool endInCell = math.all(diffEnd >= 0) && math.all(diffEnd <= cellStride);
                    bool belongsToCell = startInCell || endInCell;
                    
                    if (belongsToCell)
                    {
                        float3 worldPos = math.transform(localToWorld.Value, edgeIntersection.Position);
                        Gizmos.DrawLine(worldMassPoint, worldPos);
                    }
                }
            }
        }

        private void DrawEdgeIntersection(DualContouringEdgeIntersection edgeIntersection, LocalToWorld localToWorld)
        {
            // Appliquer le transform à la position
            float3 position = math.transform(localToWorld.Value, edgeIntersection.Position);

            // Dessiner le point d'intersection en rouge
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(position, 0.05f);

            // Dessiner la normale en cyan
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(position,
                position + edgeIntersection.Normal * 0.2f);
        }
    }
}