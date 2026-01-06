using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.ScalarField
{
    public struct ScalarFieldItem : IBufferElementData
    {
        public float Value;
        public float3 Position;
    }
}