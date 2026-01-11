using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace DualContouring.MeshGeneration
{
    /// <summary>
    ///     Composant qui contient les données de vertex pour le mesh généré
    /// </summary>
    public struct DualContouringMeshVertex : IBufferElementData
    {
        public float3 Position;
        public float3 Normal;
    }

    /// <summary>
    ///     Composant qui contient les indices de triangles pour le mesh généré
    /// </summary>
    public struct DualContouringMeshTriangle : IBufferElementData
    {
        public int Index;
    }

    /// <summary>
    ///     Tag pour indiquer qu'un mesh doit être regénéré
    /// </summary>
    public struct DualContouringMeshDirty : IComponentData
    {
    }

    /// <summary>
    ///     Composant qui contient la référence au mesh Unity généré
    /// </summary>
    public struct DualContouringMeshReference : IComponentData
    {
        public UnityObjectRef<Mesh> Mesh;
    }

    /// <summary>
    ///     Composant qui contient les bounds précalculées du mesh
    /// </summary>
    public struct DualContouringMeshBounds : IComponentData
    {
        public float3 Center;
        public float3 Size;
    }
}