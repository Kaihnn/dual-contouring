using System.Runtime.CompilerServices;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 WorldPositionToGridPosition(in float3 worldPosition, in float minNodeSize, in float3 octreeOffset)
        {
            float3 localPos = (worldPosition - octreeOffset) / minNodeSize;
            return new int3(
                (int)math.floor(localPos.x),
                (int)math.floor(localPos.y),
                (int)math.floor(localPos.z)
            );
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleOctree(in DynamicBuffer<OctreeNode> octreeBuffer, in OctreeInfos octreeInfos, in float3 worldPosition)
        {
            if (octreeBuffer.Length == 0)
            {
                return 0f;
            }

            float3 localPos = (worldPosition - octreeInfos.OctreeOffset) / octreeInfos.MinNodeSize;
            int3 baseCell = new int3(
                (int)math.floor(localPos.x),
                (int)math.floor(localPos.y),
                (int)math.floor(localPos.z)
            );

            float3 t = localPos - new float3(baseCell);
            int3 maxGrid = octreeInfos.GridSize - new int3(2, 2, 2);
            baseCell = math.clamp(baseCell, int3.zero, maxGrid);
            t = math.clamp(t, 0f, 1f);

            float v000 = GetValueAtPosition(octreeBuffer, octreeInfos, baseCell + new int3(0, 0, 0));
            float v100 = GetValueAtPosition(octreeBuffer, octreeInfos, baseCell + new int3(1, 0, 0));
            float v010 = GetValueAtPosition(octreeBuffer, octreeInfos, baseCell + new int3(0, 1, 0));
            float v110 = GetValueAtPosition(octreeBuffer, octreeInfos, baseCell + new int3(1, 1, 0));
            float v001 = GetValueAtPosition(octreeBuffer, octreeInfos, baseCell + new int3(0, 0, 1));
            float v101 = GetValueAtPosition(octreeBuffer, octreeInfos, baseCell + new int3(1, 0, 1));
            float v011 = GetValueAtPosition(octreeBuffer, octreeInfos, baseCell + new int3(0, 1, 1));
            float v111 = GetValueAtPosition(octreeBuffer, octreeInfos, baseCell + new int3(1, 1, 1));

            float v00 = math.lerp(v000, v100, t.x);
            float v01 = math.lerp(v001, v101, t.x);
            float v10 = math.lerp(v010, v110, t.x);
            float v11 = math.lerp(v011, v111, t.x);

            float v0 = math.lerp(v00, v10, t.y);
            float v1 = math.lerp(v01, v11, t.y);

            return math.lerp(v0, v1, t.z);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalculateNormalFromOctree(in DynamicBuffer<OctreeNode> octreeBuffer, in OctreeInfos octreeInfos, in float3 position, out float3 normal)
        {
            float epsilon = octreeInfos.MinNodeSize * 0.1f;

            float valueXPlus = SampleOctree(octreeBuffer, octreeInfos, position + new float3(epsilon, 0, 0));
            float valueXMinus = SampleOctree(octreeBuffer, octreeInfos, position - new float3(epsilon, 0, 0));
            float gradX = (valueXPlus - valueXMinus) / (2f * epsilon);

            float valueYPlus = SampleOctree(octreeBuffer, octreeInfos, position + new float3(0, epsilon, 0));
            float valueYMinus = SampleOctree(octreeBuffer, octreeInfos, position - new float3(0, epsilon, 0));
            float gradY = (valueYPlus - valueYMinus) / (2f * epsilon);

            float valueZPlus = SampleOctree(octreeBuffer, octreeInfos, position + new float3(0, 0, epsilon));
            float valueZMinus = SampleOctree(octreeBuffer, octreeInfos, position - new float3(0, 0, epsilon));
            float gradZ = (valueZPlus - valueZMinus) / (2f * epsilon);

            float3 gradient = new float3(gradX, gradY, gradZ);
            float length = math.length(gradient);

            if (length > 0.0001f)
            {
                normal = math.normalize(gradient);
                return;
            }

            normal = new float3(0, 1, 0);
        }

        [BurstCompile]
        public static float GetValueAtPosition(in DynamicBuffer<OctreeNode> octreeBuffer, in OctreeInfos octreeInfos, in int3 position, float defaultValue = -1f)
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