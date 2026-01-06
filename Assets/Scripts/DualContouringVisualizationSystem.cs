using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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
            foreach (var (cellBuffer, edgeIntersectionBuffer, selectedCell, gridSize) in SystemAPI.Query<
                         DynamicBuffer<DualContouringCell>,
                         DynamicBuffer<DualContouringEdgeIntersection>,
                         RefRO<SelectedCell>,
                         RefRO<ScalarFieldGridSize>>())
            {
                // Calculer l'index de la cellule sélectionnée
                int3 cellGridSize = gridSize.ValueRO.Value - new int3(1, 1, 1);
                int selectedIndex = ScalarFieldUtility.CoordToIndex(selectedCell.ValueRO.Value, cellGridSize);
                
                // Si l'index est invalide, dessiner toutes les cellules
                bool drawAllCells = selectedIndex < 0 || selectedIndex >= cellBuffer.Length;
                
                if (drawAllCells)
                {
                    // Dessiner toutes les cellules
                    DrawAllCells(cellBuffer);
                    
                    // Dessiner toutes les intersections d'arêtes
                    DrawAllEdgeIntersections(edgeIntersectionBuffer);
                }
                else
                {
                    // Dessiner uniquement la cellule sélectionnée
                    DrawCell(cellBuffer[selectedIndex]);
                    
                    // Dessiner les intersections d'arêtes pour la cellule sélectionnée uniquement
                    DrawEdgeIntersectionsForCell(edgeIntersectionBuffer, selectedIndex);
                }
            }
        }

    private void DrawWireCube(float3 center, float3 size)
    {
        Gizmos.DrawWireCube(center, size);
    }

    private void DrawAllCells(DynamicBuffer<DualContouringCell> cellBuffer)
    {
        foreach (var cell in cellBuffer)
        {
            DrawCell(cell);
        }
    }

    private void DrawCell(DualContouringCell cell)
    {
        if (cell.HasVertex)
        {
            // Dessiner la cellule en vert si elle a un vertex
            Gizmos.color = Color.green;
            DrawWireCube(cell.Position + new float3(0.5f, 0.5f, 0.5f) * cell.Size, 
                new float3(cell.Size, cell.Size, cell.Size));

            // Dessiner le vertex en jaune
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(cell.VertexPosition, cell.Size * 0.1f);
            
            // Dessiner la normale de la cellule en magenta
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(cell.VertexPosition, 
                cell.VertexPosition + cell.Normal * cell.Size * 0.5f);
        }
        else
        {
            // Dessiner la cellule en gris si elle n'a pas de vertex
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            DrawWireCube(cell.Position + new float3(0.5f, 0.5f, 0.5f) * cell.Size, 
                new float3(cell.Size, cell.Size, cell.Size));
        }
    }

    private void DrawAllEdgeIntersections(DynamicBuffer<DualContouringEdgeIntersection> edgeIntersectionBuffer)
    {
        foreach (var edgeIntersection in edgeIntersectionBuffer)
        {
            DrawEdgeIntersection(edgeIntersection);
        }
    }

    private void DrawEdgeIntersectionsForCell(DynamicBuffer<DualContouringEdgeIntersection> edgeIntersectionBuffer, int cellIndex)
    {
        foreach (var edgeIntersection in edgeIntersectionBuffer)
        {
            if (edgeIntersection.CellIndex == cellIndex)
            {
                DrawEdgeIntersection(edgeIntersection);
            }
        }
    }

    private void DrawEdgeIntersection(DualContouringEdgeIntersection edgeIntersection)
    {
        // Dessiner le point d'intersection en rouge
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(edgeIntersection.Position, 0.05f);
        
        // Dessiner la normale en cyan
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(edgeIntersection.Position, 
            edgeIntersection.Position + edgeIntersection.Normal * 0.2f);
    }
}
