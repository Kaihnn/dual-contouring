using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring
{
    public struct ScalarFieldValue : IBufferElementData
    {
        public float Value;
        public float3 Position;
    }
}

