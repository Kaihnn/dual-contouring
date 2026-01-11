using Unity.Entities;
using UnityEngine;

namespace DualContouring.MeshGeneration
{
    public partial class DualContouringMeshUpdateSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (vertexBuffer, triangleBuffer, meshRef) in SystemAPI.Query<
                         DynamicBuffer<DualContouringMeshVertex>,
                         DynamicBuffer<DualContouringMeshTriangle>,
                         RefRO<DualContouringMeshReference>>())
            {
                if (meshRef.ValueRO.Mesh.Value != null)
                {
                    UpdateMeshData(meshRef.ValueRO.Mesh.Value, vertexBuffer, triangleBuffer);
                }
            }
        }

        private void UpdateMeshData(
            Mesh mesh,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            DynamicBuffer<DualContouringMeshTriangle> triangleBuffer)
        {
            if (vertexBuffer.Length == 0 || triangleBuffer.Length == 0)
            {
                mesh.Clear();
                return;
            }

            mesh.Clear();

            var vertices = new Vector3[vertexBuffer.Length];
            var normals = new Vector3[vertexBuffer.Length];

            for (int i = 0; i < vertexBuffer.Length; i++)
            {
                vertices[i] = vertexBuffer[i].Position;
                normals[i] = vertexBuffer[i].Normal;
            }

            var triangles = new int[triangleBuffer.Length];
            for (int i = 0; i < triangleBuffer.Length; i++)
            {
                triangles[i] = triangleBuffer[i].Index;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }
    }
}
