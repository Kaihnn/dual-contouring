using Unity.Burst;
using Unity.Entities;
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

        [BurstCompile]
        public static float GetValueAtPosition(in DynamicBuffer<OctreeNode> octreeBuffer, in OctreeNodeInfos octreeInfos, in int3 position, float defaultValue = 0f)
        {
            if (octreeBuffer.Length == 0)
            {
                return defaultValue;
            }

            int currentNodeIndex = 0;
            int currentDepth = 0;

            while (currentNodeIndex >= 0 && currentNodeIndex < octreeBuffer.Length)
            {
                OctreeNode node = octreeBuffer[currentNodeIndex];

                int nodeGridSize = 1 << (octreeInfos.MaxDepth - currentDepth);
                
                int3 relativePos = position - node.Position;

                if (math.any(relativePos < 0) || math.any(relativePos >= nodeGridSize))
                {
                    return defaultValue;
                }

                if (node.ChildIndex < 0)
                {
                    return node.Value;
                }

                int halfSize = nodeGridSize >> 1;
                
                int childOffset = 0;
                if (relativePos.x >= halfSize) childOffset |= 1;
                if (relativePos.y >= halfSize) childOffset |= 2;
                if (relativePos.z >= halfSize) childOffset |= 4;

                currentNodeIndex = node.ChildIndex + childOffset;
                currentDepth++;
            }

            return defaultValue;
        }
    }
}