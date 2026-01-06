using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring
{
    public struct DualContouringCell : IBufferElementData
    {
        public float3 Position;
        public float Size;
        public bool HasVertex;
        public float3 VertexPosition;
        public float3 Normal;
    }
}