using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring
{
    /// <summary>
    ///     Composant pour stocker la cellule sélectionnée
    /// </summary>
    public struct SelectedCell : IComponentData
    {
        public int3 Value;
    }
}