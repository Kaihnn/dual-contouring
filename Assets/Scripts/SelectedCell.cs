using Unity.Entities;
using Unity.Mathematics;

/// <summary>
///     Composant pour stocker la cellule sélectionnée
/// </summary>
public struct SelectedCell : IComponentData
{
    public int3 Value;
}