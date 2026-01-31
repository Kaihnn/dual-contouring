using Unity.Entities;

namespace DualContouring.Octrees
{
    /// <summary>
    /// Component pour contrôler le niveau de détail (LOD) de l'octree.
    /// Un LOD de 0 signifie le plus haut niveau de détail (toutes les feuilles).
    /// Un LOD plus élevé réduit le nombre de cellules affichées en s'arrêtant à une profondeur plus faible.
    /// </summary>
    public struct OctreeLOD : IComponentData
    {
        /// <summary>
        /// Niveau de LOD actuel. 
        /// 0 = détail maximum (toutes les feuilles)
        /// 1+ = réduction progressive du détail
        /// </summary>
        public int Level;
    }
}
