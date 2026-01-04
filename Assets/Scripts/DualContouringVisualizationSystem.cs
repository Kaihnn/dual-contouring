using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DualContouring;

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
            foreach (var cellBuffer in SystemAPI.Query<DynamicBuffer<DualContouringCell>>())
            {
                foreach (var cell in cellBuffer)
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
                    }
                    else
                    {
                        // Dessiner la cellule en gris si elle n'a pas de vertex
                        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                        DrawWireCube(cell.Position + new float3(0.5f, 0.5f, 0.5f) * cell.Size, 
                            new float3(cell.Size, cell.Size, cell.Size));
                    }
                }
            }
        }

    private void DrawWireCube(float3 center, float3 size)
    {
        Gizmos.DrawWireCube(center, size);
    }
}

