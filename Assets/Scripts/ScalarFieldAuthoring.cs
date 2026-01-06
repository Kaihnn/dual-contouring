using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
///     Baker pour créer une entité avec un champ scalaire
/// </summary>
public class ScalarFieldAuthoring : MonoBehaviour
{
    [FormerlySerializedAs("origin")]
    [Header("Grid Settings")]
    public float3 Origin = float3.zero;
    public float CellSize = 1f;
    [Tooltip("Taille de la grille (nombre de points dans chaque dimension)")]
    public int3 GridSize = new int3(3, 3, 3);
    public sbyte[] Values;

    [Header("Visualization")]
    public bool ShowGizmos = true;
    public float GizmoSize = 0.1f;
    public int3 SelectedCell = new int3(0, 0, 0);

    private void OnDrawGizmos()
    {
        if (!ShowGizmos)
        {
            return;
        }

        // Visualiser les valeurs du champ scalaire
        int index = 0;
        for (int y = 0; y < GridSize.y; y++)
        {
            for (int z = 0; z < GridSize.z; z++)
            {
                for (int x = 0; x < GridSize.x; x++)
                {
                    float3 position = Origin + new float3(x, y, z) * CellSize;
                    float value = Values != null && index < Values.Length ? Values[index] : 0f;

                    // < 0 = noir, 0 = rouge, sbyte.MaxValue (127) = vert
                    Color color;
                    if (value < 0)
                    {
                        color = Color.black;
                    }
                    else
                    {
                        // Normaliser de [0, sbyte.MaxValue] vers [0, 1]
                        float t = value / sbyte.MaxValue;
                        // Lerp entre rouge (0) et vert (sbyte.MaxValue)
                        color = Color.Lerp(Color.red, Color.green, t);
                    }

                    Gizmos.color = color;
                    Gizmos.DrawSphere(position, GizmoSize);
                    index++;
                }
            }
        }

        // Visualiser les cellules de dual contouring pendant le jeu
        if (Application.isPlaying && World.DefaultGameObjectInjectionWorld != null)
        {
            var visualizationSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<DualContouringVisualizationSystem>();
            visualizationSystem?.DrawGizmos();
        }
    }

    private class Baker : Baker<ScalarFieldAuthoring>
    {
        public override void Bake(ScalarFieldAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity,
                new SelectedCell
                {
                    Value = authoring.SelectedCell
                });
            AddComponent(entity,
                new ScalarFieldGridSize
                {
                    Value = authoring.GridSize,
                    CellSize = authoring.CellSize
                });

            AddBuffer<DualContouringCell>(entity);
            AddBuffer<DualContouringEdgeIntersection>(entity);
            AddBuffer<DualContouringMeshVertex>(entity);
            AddBuffer<DualContouringMeshTriangle>(entity);
            DynamicBuffer<ScalarFieldValue> buffer = AddBuffer<ScalarFieldValue>(entity);

            // Générer les valeurs scalaires selon le type
            int index = 0;
            for (int y = 0; y < authoring.GridSize.y; y++)
            {
                for (int z = 0; z < authoring.GridSize.z; z++)
                {
                    for (int x = 0; x < authoring.GridSize.x; x++)
                    {
                        float3 position = authoring.Origin + new float3(x, y, z) * authoring.CellSize;
                        float value = authoring.Values != null && index < authoring.Values.Length ? authoring.Values[index] : 0f;
                        buffer.Add(new ScalarFieldValue { Position = position, Value = value });
                        index++;
                    }
                }
            }
        }
    }
}