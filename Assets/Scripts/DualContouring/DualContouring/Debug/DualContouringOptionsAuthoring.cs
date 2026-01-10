using Unity.Entities;
using UnityEngine;

namespace DualContouring.DualContouring.Debug
{
    public class DualContouringOptionsAuthoring : MonoBehaviour
    {
        public DualContouringType DualContouringType;
    }

    public class DualContouringBaker : Baker<DualContouringOptionsAuthoring>
    {
        public override void Bake(DualContouringOptionsAuthoring optionsAuthoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity,
                new DualContouringOptions
                {
                    Type = optionsAuthoring.DualContouringType
                });
        }
    }
}