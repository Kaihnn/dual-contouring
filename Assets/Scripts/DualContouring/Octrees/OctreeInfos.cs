using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.Octrees
{
    public struct OctreeInfos : IComponentData
    {
        public float3 OctreeOffset;
        public int MaxDepth;
        public float MaxNodeSize;
        public float MinNodeSize;
        public int3 GridSize;
    }
}