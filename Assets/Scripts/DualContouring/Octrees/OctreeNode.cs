using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.Octrees
{
    public struct OctreeNode : IBufferElementData
    {
        public float Value;
        public int ChildIndex;
        public int3 Position;
        public int Depth;
    }
}

