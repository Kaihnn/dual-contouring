using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring
{
    public struct ScalarFieldItem : IBufferElementData
    {
        public float Value;
        public float3 Position;
    }
}