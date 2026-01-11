using Unity.Entities;

namespace DualContouring.DualContouring.Debug
{
    public struct DualContouringVisualizationOptions : IComponentData
    {
        public bool Enabled;
        public bool DrawEmptyCell;
        public bool DrawEdgeIntersections;
        public bool DrawMassPoint;
    }
}