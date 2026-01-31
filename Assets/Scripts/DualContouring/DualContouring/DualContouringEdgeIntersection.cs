using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.DualContouring
{
    /// <summary>
    /// Représente une intersection entre une edge de la grille et la surface.
    /// Dans le dual contouring, chaque edge qui traverse la surface génère une intersection.
    /// </summary>
    public struct DualContouringEdgeIntersection : IBufferElementData
    {
        /// <summary>
        /// Position de l'intersection dans l'espace monde
        /// </summary>
        public float3 Position;
        
        /// <summary>
        /// Normale de la surface à l'intersection
        /// </summary>
        public float3 Normal;
        
        /// <summary>
        /// Clé unique identifiant l'edge dans la grille
        /// </summary>
        public EdgeKey Edge;
        
        /// <summary>
        /// Index de la cellule qui contient cette edge (pour référence)
        /// </summary>
        public int CellIndex;
    }
}