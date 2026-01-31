using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.DualContouring
{
    /// <summary>
    /// Associe une edge à une cellule qui la partage.
    /// Utilisé pour trouver quelles cellules partagent une edge donnée.
    /// </summary>
    public struct EdgeToCell : IBufferElementData
    {
        /// <summary>
        /// Clé de l'edge
        /// </summary>
        public EdgeKey Edge;
        
        /// <summary>
        /// Index de la cellule dans le buffer DualContouringCell
        /// </summary>
        public int CellIndex;
        
        /// <summary>
        /// Index du vertex dans cette cellule (dans le buffer de mesh)
        /// </summary>
        public int VertexIndex;
    }
}
