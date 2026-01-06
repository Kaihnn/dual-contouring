using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring
{
    public struct ScalarFieldGridSize : IComponentData
    {
        public int3 Value;
    }
}

