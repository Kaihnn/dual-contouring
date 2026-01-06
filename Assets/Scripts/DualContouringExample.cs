using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Exemple de configuration pour tester le dual contouring avec génération de mesh
/// </summary>
public class DualContouringExample : MonoBehaviour
{
    public enum ExampleType
    {
        Sphere,
        Cube,
        Plane,
        Corner,
        ExtendedSphere,
        Cross
    }

    [Header("Instructions")]
    [TextArea(3, 10)]
    public string instructions =
        "Ce composant fournit des exemples de configurations de champ scalaire.\n" +
        "Les exemples sont générés dynamiquement selon la taille de grille configurée dans ScalarFieldAuthoring.\n" +
        "Sélectionnez un type d'exemple et utilisez 'Appliquer l'exemple sélectionné' ou les méthodes de génération procédurale.";

    [Header("Paramètres de génération procédurale")]
    [Tooltip("Valeur minimale du champ scalaire (extérieur/vide)")]
    public sbyte minValue = sbyte.MinValue;

    [Tooltip("Valeur maximale du champ scalaire (intérieur/plein)")]
    public sbyte maxValue = sbyte.MaxValue;

    [Tooltip("Rayon de la sphère en unités de grille (0.5 = moitié de la grille)")]
    [Range(0.1f, 2f)]
    public float sphereRadius = 0.3f;

    [ContextMenu("Générer une sphère procédurale")]
    public void GenerateProceduralSphere()
    {
        var authoring = GetComponent<ScalarFieldAuthoring>();
        if (authoring == null)
        {
            Debug.LogError("Aucun ScalarFieldAuthoring trouvé sur ce GameObject!");
            return;
        }

        int totalSize = authoring.GridSize.x * authoring.GridSize.y * authoring.GridSize.z;
        authoring.Values = new sbyte[totalSize];

        // Centre de la grille en coordonnées de grille
        var gridCenter = new float3(
            (authoring.GridSize.x - 1) * 0.5f,
            (authoring.GridSize.y - 1) * 0.5f,
            (authoring.GridSize.z - 1) * 0.5f
        );

        // Rayon de la sphère en unités de grille
        float minDimension = Mathf.Min(authoring.GridSize.x, Mathf.Min(authoring.GridSize.y, authoring.GridSize.z));
        float radius = minDimension * sphereRadius;

        int index = 0;
        for (int y = 0; y < authoring.GridSize.y; y++)
        {
            for (int z = 0; z < authoring.GridSize.z; z++)
            {
                for (int x = 0; x < authoring.GridSize.x; x++)
                {
                    var gridPos = new float3(x, y, z);
                    float distance = math.distance(gridPos, gridCenter);

                    // Distance signée: négative à l'extérieur, positive à l'intérieur
                    float signedDistance = radius - distance;

                    // Normaliser la distance pour mapper sur la plage [minValue, maxValue]
                    // signedDistance varie de -inf (loin) à +radius (au centre)
                    // On normalise entre -radius et +radius
                    float normalizedDistance = Mathf.Clamp(signedDistance / radius, -1f, 1f);

                    // Mapper sur la plage [minValue, maxValue]
                    float valueRange = maxValue - minValue;
                    float mappedValue = minValue + (normalizedDistance + 1f) * 0.5f * valueRange;

                    authoring.Values[index] = (sbyte)Mathf.Clamp(Mathf.RoundToInt(mappedValue), minValue, maxValue);

                    index++;
                }
            }
        }

        Debug.Log($"Sphère procédurale générée pour une grille {authoring.GridSize.x}x{authoring.GridSize.y}x{authoring.GridSize.z} " +
                  $"avec rayon={radius:F2} unités, valeurs=[{minValue}, {maxValue}]");

#if UNITY_EDITOR
        EditorUtility.SetDirty(authoring);
#endif
    }

    [ContextMenu("Générer un cube procédural")]
    public void GenerateProceduralCube()
    {
        var authoring = GetComponent<ScalarFieldAuthoring>();
        if (authoring == null)
        {
            Debug.LogError("Aucun ScalarFieldAuthoring trouvé sur ce GameObject!");
            return;
        }

        int totalSize = authoring.GridSize.x * authoring.GridSize.y * authoring.GridSize.z;
        authoring.Values = new sbyte[totalSize];

        // Centre de la grille en coordonnées de grille
        var gridCenter = new float3(
            (authoring.GridSize.x - 1) * 0.5f,
            (authoring.GridSize.y - 1) * 0.5f,
            (authoring.GridSize.z - 1) * 0.5f
        );

        // Taille du cube (environ la moitié de la plus petite dimension)
        float minDimension = Mathf.Min(authoring.GridSize.x, Mathf.Min(authoring.GridSize.y, authoring.GridSize.z));
        float halfSize = minDimension * sphereRadius; // Réutilise le paramètre de rayon

        int index = 0;
        for (int y = 0; y < authoring.GridSize.y; y++)
        {
            for (int z = 0; z < authoring.GridSize.z; z++)
            {
                for (int x = 0; x < authoring.GridSize.x; x++)
                {
                    var gridPos = new float3(x, y, z);
                    float3 offset = math.abs(gridPos - gridCenter);

                    // Distance au cube (distance maximale sur tous les axes)
                    float maxDist = Mathf.Max(offset.x, Mathf.Max(offset.y, offset.z));
                    float signedDistance = halfSize - maxDist;

                    // Normaliser et mapper sur [minValue, maxValue]
                    float normalizedDistance = Mathf.Clamp(signedDistance / halfSize, -1f, 1f);
                    float valueRange = maxValue - minValue;
                    float mappedValue = minValue + (normalizedDistance + 1f) * 0.5f * valueRange;

                    authoring.Values[index] = (sbyte)Mathf.Clamp(Mathf.RoundToInt(mappedValue), minValue, maxValue);

                    index++;
                }
            }
        }

        Debug.Log($"Cube procédural généré pour une grille {authoring.GridSize.x}x{authoring.GridSize.y}x{authoring.GridSize.z} " +
                  $"avec demi-taille={halfSize:F2} unités, valeurs=[{minValue}, {maxValue}]");

#if UNITY_EDITOR
        EditorUtility.SetDirty(authoring);
#endif
    }

    [ContextMenu("Générer un plan horizontal procédural")]
    public void GenerateProceduralPlane()
    {
        var authoring = GetComponent<ScalarFieldAuthoring>();
        if (authoring == null)
        {
            Debug.LogError("Aucun ScalarFieldAuthoring trouvé sur ce GameObject!");
            return;
        }

        int totalSize = authoring.GridSize.x * authoring.GridSize.y * authoring.GridSize.z;
        authoring.Values = new sbyte[totalSize];

        // Plan au milieu de la grille en Y
        float planeY = (authoring.GridSize.y - 1) * 0.5f;

        int index = 0;
        for (int y = 0; y < authoring.GridSize.y; y++)
        {
            for (int z = 0; z < authoring.GridSize.z; z++)
            {
                for (int x = 0; x < authoring.GridSize.x; x++)
                {
                    // Distance signée au plan (positif au-dessus, négatif en-dessous)
                    float signedDistance = planeY - y;

                    // Normaliser sur une petite épaisseur pour avoir une transition
                    float thickness = 1.5f; // Épaisseur de transition en unités de grille
                    float normalizedDistance = Mathf.Clamp(signedDistance / thickness, -1f, 1f);

                    // Mapper sur [minValue, maxValue]
                    float valueRange = maxValue - minValue;
                    float mappedValue = minValue + (normalizedDistance + 1f) * 0.5f * valueRange;

                    authoring.Values[index] = (sbyte)Mathf.Clamp(Mathf.RoundToInt(mappedValue), minValue, maxValue);

                    index++;
                }
            }
        }

        Debug.Log($"Plan horizontal procédural généré pour une grille {authoring.GridSize.x}x{authoring.GridSize.y}x{authoring.GridSize.z} " +
                  $"à Y={planeY:F2}, valeurs=[{minValue}, {maxValue}]");

#if UNITY_EDITOR
        EditorUtility.SetDirty(authoring);
#endif
    }

    [ContextMenu("Générer un tore procédural")]
    public void GenerateProceduralTorus()
    {
        var authoring = GetComponent<ScalarFieldAuthoring>();
        if (authoring == null)
        {
            Debug.LogError("Aucun ScalarFieldAuthoring trouvé sur ce GameObject!");
            return;
        }

        int totalSize = authoring.GridSize.x * authoring.GridSize.y * authoring.GridSize.z;
        authoring.Values = new sbyte[totalSize];

        // Centre de la grille
        var gridCenter = new float3(
            (authoring.GridSize.x - 1) * 0.5f,
            (authoring.GridSize.y - 1) * 0.5f,
            (authoring.GridSize.z - 1) * 0.5f
        );

        // Paramètres du tore
        float minDimension = Mathf.Min(authoring.GridSize.x, Mathf.Min(authoring.GridSize.y, authoring.GridSize.z));
        float majorRadius = minDimension * sphereRadius; // Rayon principal
        float minorRadius = majorRadius * 0.4f; // Rayon du tube

        int index = 0;
        for (int y = 0; y < authoring.GridSize.y; y++)
        {
            for (int z = 0; z < authoring.GridSize.z; z++)
            {
                for (int x = 0; x < authoring.GridSize.x; x++)
                {
                    var gridPos = new float3(x, y, z);
                    float3 offset = gridPos - gridCenter;

                    // Distance au tore dans le plan XZ
                    float distXZ = Mathf.Sqrt(offset.x * offset.x + offset.z * offset.z);
                    var torusPoint = new float2(distXZ - majorRadius, offset.y);
                    float torusDistance = math.length(torusPoint);

                    float signedDistance = minorRadius - torusDistance;

                    // Normaliser et mapper
                    float normalizedDistance = Mathf.Clamp(signedDistance / minorRadius, -1f, 1f);
                    float valueRange = maxValue - minValue;
                    float mappedValue = minValue + (normalizedDistance + 1f) * 0.5f * valueRange;

                    authoring.Values[index] = (sbyte)Mathf.Clamp(Mathf.RoundToInt(mappedValue), minValue, maxValue);

                    index++;
                }
            }
        }

        Debug.Log($"Tore procédural généré pour une grille {authoring.GridSize.x}x{authoring.GridSize.y}x{authoring.GridSize.z} " +
                  $"avec rayons={majorRadius:F2}/{minorRadius:F2}, valeurs=[{minValue}, {maxValue}]");

#if UNITY_EDITOR
        EditorUtility.SetDirty(authoring);
#endif
    }
}