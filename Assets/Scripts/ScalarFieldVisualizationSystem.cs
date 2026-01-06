using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Système de visualisation des valeurs du champ scalaire dans l'éditeur
/// </summary>
public partial class ScalarFieldVisualizationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Ce système ne fait rien pendant le jeu, seulement pour le debug dans l'éditeur
    }

    public void DrawGizmos()
    {
        foreach (var (scalarFieldBuffer, gridSize, localToWorld) in SystemAPI.Query<
                     DynamicBuffer<ScalarFieldItem>,
                     RefRO<ScalarFieldGridSize>,
                     RefRO<LocalToWorld>>())
        {
            DrawScalarFieldValues(scalarFieldBuffer, gridSize.ValueRO, localToWorld.ValueRO);
        }
    }

    private void DrawScalarFieldValues(DynamicBuffer<ScalarFieldItem> scalarFieldBuffer, ScalarFieldGridSize gridSize, LocalToWorld localToWorld)
    {
        int index = 0;
        for (int y = 0; y < gridSize.Value.y; y++)
        {
            for (int z = 0; z < gridSize.Value.z; z++)
            {
                for (int x = 0; x < gridSize.Value.x; x++)
                {
                    if (index >= scalarFieldBuffer.Length)
                    {
                        return;
                    }

                    var scalarValue = scalarFieldBuffer[index];
                    float3 position = math.transform(localToWorld.Value, scalarValue.Position);
                    float value = scalarValue.Value;

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
                    Gizmos.DrawSphere(position, 0.1f); // Utiliser une taille fixe de 0.1f

                    // Afficher l'index en texte
#if UNITY_EDITOR
                    // Calculer un offset qui s'adapte à la distance de la caméra
                    Vector3 worldPos = position;
                    Vector3 offset = Vector3.up * (HandleUtility.GetHandleSize(worldPos) * 0.3f);
                    Handles.Label(worldPos + offset, index.ToString());
#endif

                    index++;
                }
            }
        }
    }
}

