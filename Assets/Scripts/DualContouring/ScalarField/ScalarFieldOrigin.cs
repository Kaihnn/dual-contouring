using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.ScalarField
{
    /// <summary>
    /// Composant contenant l'origine et la taille des cellules du champ scalaire
    /// </summary>
    public struct ScalarFieldOrigin : IComponentData
    {
        public float3 Origin;
        public float CellSize;
    }
}

