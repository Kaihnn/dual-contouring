using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.Octrees
{
    public struct OctreeNode : IBufferElementData
    {
        public float Value;
        public float3 Position;
        public int ChildIndex;
    }
}

