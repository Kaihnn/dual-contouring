using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace DualContouring.MeshGeneration
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(DualContouringMeshGenerationSystem))]
    public partial struct DualContouringMeshUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (vertexBuffer, triangleBuffer, meshRef, bounds, entity) in SystemAPI.Query<
                         DynamicBuffer<DualContouringMeshVertex>,
                         DynamicBuffer<DualContouringMeshTriangle>,
                         RefRO<DualContouringMeshReference>,
                         RefRO<DualContouringMeshBounds>>()
                     .WithAll<DualContouringMeshDirty>()
                     .WithEntityAccess())
            {
                if (meshRef.ValueRO.Mesh.Value != null)
                {
                    UpdateMeshData(meshRef.ValueRO.Mesh.Value, vertexBuffer, triangleBuffer, bounds.ValueRO);
                }

                ecb.RemoveComponent<DualContouringMeshDirty>(entity);
            }
        }

        private void UpdateMeshData(
            Mesh mesh,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            DynamicBuffer<DualContouringMeshTriangle> triangleBuffer,
            DualContouringMeshBounds bounds)
        {
            mesh.Clear();

            if (vertexBuffer.Length == 0 || triangleBuffer.Length == 0)
            {
                return;
            }

            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            var meshData = meshDataArray[0];

            var vertexAttributes = new NativeArray<VertexAttributeDescriptor>(2, Allocator.Temp);
            vertexAttributes[0] = new VertexAttributeDescriptor(VertexAttribute.Position);
            vertexAttributes[1] = new VertexAttributeDescriptor(VertexAttribute.Normal);

            meshData.SetVertexBufferParams(vertexBuffer.Length, vertexAttributes);
            meshData.SetIndexBufferParams(triangleBuffer.Length, IndexFormat.UInt32);

            vertexAttributes.Dispose();

            var vertices = meshData.GetVertexData<VertexData>();
            for (int i = 0; i < vertexBuffer.Length; i++)
            {
                vertices[i] = new VertexData
                {
                    Position = vertexBuffer[i].Position,
                    Normal = vertexBuffer[i].Normal
                };
            }

            var indices = meshData.GetIndexData<uint>();
            for (int i = 0; i < triangleBuffer.Length; i++)
            {
                indices[i] = (uint)triangleBuffer[i].Index;
            }

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, triangleBuffer.Length));

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);

            mesh.bounds = new Bounds(bounds.Center, bounds.Size);
        }

        private struct VertexData
        {
            public float3 Position;
            public float3 Normal;
        }
    }
}
