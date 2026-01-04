using UnityEngine;

/// <summary>
///     Exemple de configuration pour tester le dual contouring avec génération de mesh
/// </summary>
public class DualContouringExample : MonoBehaviour
{
    [Header("Instructions")]
    [TextArea(3, 10)]
    public string instructions = 
        "Ce composant fournit des exemples de configurations de champ scalaire.\n" +
        "Ajoutez un ScalarFieldAuthoring au même GameObject et utilisez les valeurs d'exemple ci-dessous.";
    
    [Header("Exemple 1: Sphère")]
    [Tooltip("Valeurs pour une sphère au centre de la grille")]
    public sbyte[] sphereValues = new sbyte[]
    {
        // Couche Y=0
        -1, -1, -1,  // Z=0
        -1, 50, -1,  // Z=1
        -1, -1, -1,  // Z=2
        
        // Couche Y=1
        -1, 50, -1,  // Z=0
        50, 127, 50,  // Z=1
        -1, 50, -1,  // Z=2
        
        // Couche Y=2
        -1, -1, -1,  // Z=0
        -1, 50, -1,  // Z=1
        -1, -1, -1,  // Z=2
    };
    
    [Header("Exemple 2: Cube")]
    [Tooltip("Valeurs pour un cube au centre")]
    public sbyte[] cubeValues = new sbyte[]
    {
        // Couche Y=0
        -1, -1, -1,
        -1, -1, -1,
        -1, -1, -1,
        
        // Couche Y=1
        -1, -1, -1,
        -1, 127, -1,
        -1, -1, -1,
        
        // Couche Y=2
        -1, -1, -1,
        -1, -1, -1,
        -1, -1, -1,
    };
    
    [Header("Exemple 3: Plan horizontal")]
    [Tooltip("Valeurs pour un plan horizontal au milieu")]
    public sbyte[] planeValues = new sbyte[]
    {
        // Couche Y=0
        -1, -1, -1,
        -1, -1, -1,
        -1, -1, -1,
        
        // Couche Y=1
        50, 50, 50,
        50, 50, 50,
        50, 50, 50,
        
        // Couche Y=2
        127, 127, 127,
        127, 127, 127,
        127, 127, 127,
    };
    
    [Header("Exemple 4: Coin")]
    [Tooltip("Valeurs pour un coin")]
    public sbyte[] cornerValues = new sbyte[]
    {
        // Couche Y=0
        -1, -1, -1,
        -1, -1, -1,
        -1, -1, -1,
        
        // Couche Y=1
        -1, -1, -1,
        -1, 127, 127,
        -1, 127, 127,
        
        // Couche Y=2
        -1, -1, -1,
        -1, 127, 127,
        -1, 127, 127,
    };
    
    [Header("Appliquer les valeurs")]
    public ExampleType exampleToApply = ExampleType.Sphere;
    
    [ContextMenu("Appliquer l'exemple sélectionné")]
    public void ApplyExample()
    {
        ScalarFieldAuthoring authoring = GetComponent<ScalarFieldAuthoring>();
        if (authoring == null)
        {
            Debug.LogError("Aucun ScalarFieldAuthoring trouvé sur ce GameObject!");
            return;
        }
        
        switch (exampleToApply)
        {
            case ExampleType.Sphere:
                authoring.Values = (sbyte[])sphereValues.Clone();
                Debug.Log("Exemple Sphère appliqué");
                break;
            case ExampleType.Cube:
                authoring.Values = (sbyte[])cubeValues.Clone();
                Debug.Log("Exemple Cube appliqué");
                break;
            case ExampleType.Plane:
                authoring.Values = (sbyte[])planeValues.Clone();
                Debug.Log("Exemple Plan appliqué");
                break;
            case ExampleType.Corner:
                authoring.Values = (sbyte[])cornerValues.Clone();
                Debug.Log("Exemple Coin appliqué");
                break;
        }
        
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(authoring);
        #endif
    }
    
    public enum ExampleType
    {
        Sphere,
        Cube,
        Plane,
        Corner
    }
}

