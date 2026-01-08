using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.ScalarField
{
    public struct ScalarFieldInfos : IComponentData
    {
        public int3 GridSize;
        public float CellSize;
        public float3 ScalarFieldOffset;
    }
}