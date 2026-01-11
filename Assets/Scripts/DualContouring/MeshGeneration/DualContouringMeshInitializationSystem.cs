using DualContouring.DualContouring;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace DualContouring.MeshGeneration
{
    public partial class DualContouringMeshInitializationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            RequireForUpdate<DualContouringMaterialReference>();
        }

        protected override void OnUpdate()
        {
            var materialRef = SystemAPI.GetSingleton<DualContouringMaterialReference>();
            
            var entitiesToInitialize = new NativeList<Entity>(Allocator.Temp);

            foreach (var (cellBuffer, vertexBuffer, triangleBuffer, entity) in SystemAPI.Query<
                         DynamicBuffer<DualContouringCell>,
                         DynamicBuffer<DualContouringMeshVertex>,
                         DynamicBuffer<DualContouringMeshTriangle>>()
                     .WithNone<DualContouringMeshReference>()
                     .WithEntityAccess())
            {
                if (cellBuffer.Length == 0 || vertexBuffer.Length == 0 || triangleBuffer.Length == 0)
                {
                    continue;
                }

                entitiesToInitialize.Add(entity);
            }

            foreach (var entity in entitiesToInitialize)
            {
                var mesh = new Mesh
                {
                    name = $"DualContouringMesh_{entity.Index}"
                };

                EntityManager.AddComponentData(entity, new DualContouringMeshReference
                {
                    Mesh = mesh
                });

                var renderMeshArray = new RenderMeshArray(new[] { materialRef.Material.Value }, new[] { mesh });
                var renderMeshDescription = new RenderMeshDescription(UnityEngine.Rendering.ShadowCastingMode.Off);

                RenderMeshUtility.AddComponents(
                    entity,
                    EntityManager,
                    in renderMeshDescription,
                    renderMeshArray,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            }
            
            entitiesToInitialize.Dispose();
        }


        protected override void OnDestroy()
        {
            base.OnDestroy();

            foreach (var meshRef in SystemAPI.Query<RefRO<DualContouringMeshReference>>())
            {
                if (meshRef.ValueRO.Mesh.Value != null)
                {
                    Object.Destroy(meshRef.ValueRO.Mesh.Value);
                }
            }
        }
    }
}

