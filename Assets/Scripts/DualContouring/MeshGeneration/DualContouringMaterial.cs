using Unity.Entities;
using UnityEngine;

namespace DualContouring.MeshGeneration
{
    /// <summary>
    ///     Composant qui contient la référence au matériau pour le mesh de dual contouring
    /// </summary>
    public struct DualContouringMaterialReference : IComponentData
    {
        public UnityObjectRef<Material> Material;
    }
}