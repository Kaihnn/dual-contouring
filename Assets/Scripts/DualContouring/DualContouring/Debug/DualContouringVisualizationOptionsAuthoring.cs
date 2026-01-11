using Unity.Entities;
using UnityEngine;

namespace DualContouring.DualContouring.Debug
{
    public class DualContouringVisualizationOptionsAuthoring : MonoBehaviour
    {
        public bool Enabled = true;
        public bool DrawEmptyCell = false;
        public bool DrawEdgeIntersections = true;
        public bool DrawMassPoint = true;
    }

    public class DualContouringVisualizationBaker : Baker<DualContouringVisualizationOptionsAuthoring>
    {
        public override void Bake(DualContouringVisualizationOptionsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity,
                new DualContouringVisualizationOptions
                {
                    Enabled = authoring.Enabled,
                    DrawEmptyCell = authoring.DrawEmptyCell,
                    DrawEdgeIntersections = authoring.DrawEdgeIntersections,
                    DrawMassPoint = authoring.DrawMassPoint
                });
        }
    }
}

