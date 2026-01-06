using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.ScalarField
{
    public struct ScalarFieldGridSize : IComponentData
    {
        public int3 Value;
    }
}

