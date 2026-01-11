using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.ScalarField.Debug
{
    public struct ScalarFieldSelectedCell : IComponentData
    {
        public int3 Min;
        public int3 Max;
    }
}