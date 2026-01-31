using Unity.Entities;
using UnityEngine;
using OctreeVisualizationSystem = DualContouring.Octrees.Debug.OctreeVisualizationSystem;

namespace DualContouring.Octrees
{
    /// <summary>
    ///     Composant pour ajouter le buffer OctreeNode et la visualisation de l'octree.
    ///     Doit être placé sur le même GameObject que ScalarFieldAuthoring.
    /// </summary>
    public class OctreeAuthoring : MonoBehaviour
    {
        [Header("Level of Detail")]
        [Tooltip("Niveau de LOD. 0 = détail maximum, valeurs plus élevées = moins de détails.\nEn Play mode, modifiez via l'Entity Inspector (fenêtre Entities > sélectionner l'entité > component OctreeLOD).")]
        [Range(0, 10)]
        public int LODLevel = 0;

        private void OnDrawGizmos()
        {
            // Visualiser les valeurs du champ scalaire et les cellules pendant le jeu
            if (Application.isPlaying && World.DefaultGameObjectInjectionWorld != null)
            {
                var octreeVisualizationSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<OctreeVisualizationSystem>();
                octreeVisualizationSystem?.DrawGizmos();
            }
        }

        private class Baker : Baker<OctreeAuthoring>
        {
            public override void Bake(OctreeAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddBuffer<OctreeNode>(entity);

                AddComponent(entity, new OctreeNodeInfos());
                AddComponent(entity, new OctreeLOD { Level = authoring.LODLevel });
            }
        }
    }
}