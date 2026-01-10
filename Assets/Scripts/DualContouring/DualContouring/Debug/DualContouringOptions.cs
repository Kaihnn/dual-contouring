using Unity.Entities;

namespace DualContouring.DualContouring.Debug
{
    public struct DualContouringOptions : IComponentData
    {
        public DualContouringType Type;
    }

    public enum DualContouringType
    {
        ScalarField,
        Octree,
    }
}