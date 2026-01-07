using System;
using DualContouring.Debugs;
using Unity.Entities;
using UnityEngine;

namespace DualContouring.Octrees
{
    public class OctreeAuthoring : MonoBehaviour
    {
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

                DynamicBuffer<OctreeNode> buffer = AddBuffer<OctreeNode>(entity);
            }
        }
    }
}