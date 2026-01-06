using Unity.Entities;
using Unity.Mathematics;

/// <summary>
///     Composant qui stocke la taille de la grille du champ scalaire
/// </summary>
public struct ScalarFieldGridSize : IComponentData
{
    public int3 Value;
    public float CellSize;
}

