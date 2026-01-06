using Unity.Entities;
using UnityEngine;

public class DualContouringMaterialAuthoring : MonoBehaviour
{
    [Tooltip("Matériau à utiliser pour le rendu du mesh de dual contouring")]
    public Material Material;

    class Baker : Baker<DualContouringMaterialAuthoring>
    {
        public override void Bake(DualContouringMaterialAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new DualContouringMaterialReference
            {
                Material = authoring.Material
            });
        }
    }
}