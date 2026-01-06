using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring
{
    public struct DualContouringEdgeIntersection : IBufferElementData
    {
        public float3 Position;
        public float3 Normal;
        public int CellIndex;
    }
}