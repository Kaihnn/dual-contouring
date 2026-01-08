using Unity.Burst;
using Unity.Mathematics;

namespace DualContouring.Octrees
{
    [BurstCompile]
    public static class OctreeUtils
    {
        [BurstCompile]
        public static void GetSizeFromDepth(in int maxDepth, in int depth, in float minNodeSize, out float size)
        {
            size = (1 << (maxDepth - depth)) * minNodeSize;
        }

        [BurstCompile]
        public static void GetWorldPositionFromPosition(in int3 position, in float minNodeSize, in float3 octreeOffset, out float3 worldPosition)
        {
            worldPosition = octreeOffset + new float3(position.x * minNodeSize, position.y * minNodeSize, position.z * minNodeSize);
        }
    }
}