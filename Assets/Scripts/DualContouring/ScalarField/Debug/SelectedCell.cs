using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.ScalarField.Debug
{
    /// <summary>
    ///     Composant pour stocker la cellule sélectionnée
    /// </summary>
    public struct SelectedCell : IComponentData
    {
        public int3 Value;
    }
}