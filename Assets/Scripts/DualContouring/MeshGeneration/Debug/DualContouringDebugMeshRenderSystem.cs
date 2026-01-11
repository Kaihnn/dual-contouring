#if UNITY_EDITOR
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace DualContouring.MeshGeneration.Debug
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(DualContouringMeshUpdateSystem))]
    public partial struct DualContouringDebugMeshRenderSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DualContouringMaterialReference>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SceneView.lastActiveSceneView == null || !SceneView.lastActiveSceneView.hasFocus)
            {
                return;
            }

            var materialRef = SystemAPI.GetSingleton<DualContouringMaterialReference>();

            if (materialRef.Material.Value == null)
            {
                return;
            }

            foreach ((RefRO<DualContouringMeshReference> meshRef, RefRO<LocalToWorld> localToWorld) in SystemAPI.Query<
                         RefRO<DualContouringMeshReference>,
                         RefRO<LocalToWorld>>())
            {
                if (meshRef.ValueRO.Mesh.Value != null)
                {
                    Graphics.DrawMesh(
                        meshRef.ValueRO.Mesh.Value,
                        localToWorld.ValueRO.Value,
                        materialRef.Material.Value,
                        0);
                }
            }
        }
    }
}
#endif
