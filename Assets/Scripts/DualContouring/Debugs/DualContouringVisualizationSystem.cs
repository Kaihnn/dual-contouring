using DualContouring.DualContouring;
using DualContouring.ScalarField;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DualContouring.Debugs
{
    /// <summary>
    ///     Système de visualisation des cellules de dual contouring dans l'éditeur
    /// </summary>
    public partial class DualContouringVisualizationSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Ce système ne fait rien pendant le jeu, seulement pour le debug dans l'éditeur
        }

        public void DrawGizmos()
        {
            foreach (var (cellBuffer, edgeIntersectionBuffer, selectedCell, gridSize, localToWorld) in SystemAPI.Query<
                         DynamicBuffer<DualContouringCell>,
                         DynamicBuffer<DualContouringEdgeIntersection>,
                         RefRO<SelectedCell>,
                         RefRO<ScalarFieldGridSize>,
                         RefRO<LocalToWorld>>().WithAll<ScalarFieldSelected>())
            {
                // Calculer l'index de la cellule sélectionnée
                int3 cellGridSize = gridSize.ValueRO.Value - new int3(1, 1, 1);
                int selectedIndex = ScalarFieldUtility.CoordToIndex(selectedCell.ValueRO.Value, cellGridSize);
                
                // Si l'index est invalide, dessiner toutes les cellules
                bool drawAllCells = selectedIndex < 0 || selectedIndex >= cellBuffer.Length;
                
                if (drawAllCells)
                {
                    // Dessiner toutes les cellules
                    DrawAllCells(cellBuffer, localToWorld.ValueRO);
                    
                    // Dessiner toutes les intersections d'arêtes
                    DrawAllEdgeIntersections(edgeIntersectionBuffer, localToWorld.ValueRO);
                }
                else
                {
                    // Dessiner uniquement la cellule sélectionnée
                    DrawCell(cellBuffer[selectedIndex], localToWorld.ValueRO);
                    
                    // Dessiner les intersections d'arêtes pour la cellule sélectionnée uniquement
                    DrawEdgeIntersectionsForCell(edgeIntersectionBuffer, selectedIndex, localToWorld.ValueRO);
                }
            }
        }

        private void DrawWireCube(float3 center, float3 size)
        {
            Gizmos.DrawWireCube(center, size);
        }

        private void DrawAllCells(DynamicBuffer<DualContouringCell> cellBuffer, LocalToWorld localToWorld)
        {
            foreach (var cell in cellBuffer)
            {
                DrawCell(cell, localToWorld);
            }
        }

        private void DrawCell(DualContouringCell cell, LocalToWorld localToWorld)
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
            else
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
            foreach (var edgeIntersection in edgeIntersectionBuffer)
            {
                DrawEdgeIntersection(edgeIntersection, localToWorld);
            }
        }

        private void DrawEdgeIntersectionsForCell(DynamicBuffer<DualContouringEdgeIntersection> edgeIntersectionBuffer, int cellIndex, LocalToWorld localToWorld)
        {
            foreach (var edgeIntersection in edgeIntersectionBuffer)
            {
                if (edgeIntersection.CellIndex == cellIndex)
                {
                    DrawEdgeIntersection(edgeIntersection, localToWorld);
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
