using System.Runtime.CompilerServices;
using DualContouring.Octrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.DualContouring
{
    [BurstCompile]
    public static class DualContouringOctreeHelper
    {
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalculateVertexPositionAndNormal(
            in DynamicBuffer<OctreeNode> octreeBuffer,
            ref DynamicBuffer<DualContouringEdgeIntersection> edgeIntersections,
            in int3 cellIndex,
            in OctreeInfos octreeInfos,
            out float3 vertexPosition,
            out float3 cellNormal)
        {
            float cellSize = octreeInfos.MinNodeSize;
            float3 octreeOffset = octreeInfos.OctreeOffset;

            vertexPosition = float3.zero;
            cellNormal = new float3(0, 1, 0);

            int3 cellGridSize = octreeInfos.GridSize - new int3(1, 1, 1);
            int currentCellIndex = CoordToIndex(cellIndex, cellGridSize);

            NativeArray<float3> positions = new NativeArray<float3>(12, Allocator.Temp);
            NativeArray<float3> normals = new NativeArray<float3>(12, Allocator.Temp);
            int count = 0;
            float3 massPoint = float3.zero;
            float3 normalSum = float3.zero;

            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    if (TryGetEdgeIntersection(octreeBuffer,
                            cellIndex + new int3(0, y, z),
                            cellIndex + new int3(1, y, z),
                            out float3 intersection,
                            out float3 normal,
                            octreeInfos))
                    {
                        positions[count] = intersection;
                        normals[count] = normal;
                        massPoint += intersection;
                        normalSum += normal;
                        count++;

                        edgeIntersections.Add(new DualContouringEdgeIntersection
                        {
                            Position = intersection,
                            Normal = normal,
                            CellIndex = currentCellIndex
                        });
                    }
                }
            }

            for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    if (TryGetEdgeIntersection(octreeBuffer,
                            cellIndex + new int3(x, 0, z),
                            cellIndex + new int3(x, 1, z),
                            out float3 intersection,
                            out float3 normal,
                            octreeInfos))
                    {
                        positions[count] = intersection;
                        normals[count] = normal;
                        massPoint += intersection;
                        normalSum += normal;
                        count++;

                        edgeIntersections.Add(new DualContouringEdgeIntersection
                        {
                            Position = intersection,
                            Normal = normal,
                            CellIndex = currentCellIndex
                        });
                    }
                }
            }

            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    if (TryGetEdgeIntersection(octreeBuffer,
                            cellIndex + new int3(x, y, 0),
                            cellIndex + new int3(x, y, 1),
                            out float3 intersection,
                            out float3 normal,
                            octreeInfos))
                    {
                        positions[count] = intersection;
                        normals[count] = normal;
                        massPoint += intersection;
                        normalSum += normal;
                        count++;

                        edgeIntersections.Add(new DualContouringEdgeIntersection
                        {
                            Position = intersection,
                            Normal = normal,
                            CellIndex = currentCellIndex
                        });
                    }
                }
            }

            if (count > 0)
            {
                massPoint /= count;

                float normalLength = math.length(normalSum);
                if (normalLength > 0.0001f)
                {
                    cellNormal = -math.normalize(normalSum);
                }

                QefSolver.SolveQef(positions, normals, count, massPoint, out float3 vertexPos);

                OctreeUtils.GetWorldPositionFromPosition(cellIndex, cellSize, octreeOffset, out float3 cellMin);
                float3 cellMax = cellMin + new float3(cellSize, cellSize, cellSize);

                float distanceToMass = math.length(vertexPos - massPoint);
                float maxDistance = cellSize * 2.0f;

                if (distanceToMass > maxDistance || math.any(math.isnan(vertexPos)) || math.any(math.isinf(vertexPos)))
                {
                    vertexPos = massPoint;
                }

                vertexPos = math.clamp(vertexPos, cellMin, cellMax);

                vertexPosition = vertexPos;

                positions.Dispose();
                normals.Dispose();

                return;
            }

            positions.Dispose();
            normals.Dispose();

            OctreeUtils.GetWorldPositionFromPosition(cellIndex, cellSize, octreeOffset, out float3 fallbackCellMin);
            vertexPosition = fallbackCellMin + new float3(0.5f, 0.5f, 0.5f) * cellSize;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetEdgeIntersection(
            in DynamicBuffer<OctreeNode> octreeBuffer,
            in int3 corner1Index,
            in int3 corner2Index,
            out float3 intersection,
            out float3 normal,
            in OctreeInfos octreeInfos)
        {
            float cellSize = octreeInfos.MinNodeSize;
            float3 octreeOffset = octreeInfos.OctreeOffset;

            intersection = float3.zero;
            normal = float3.zero;

            float v1 = OctreeUtils.GetValueAtPosition(octreeBuffer, octreeInfos, corner1Index);
            float v2 = OctreeUtils.GetValueAtPosition(octreeBuffer, octreeInfos, corner2Index);

            if ((v1 < 0 && v2 >= 0) || (v1 >= 0 && v2 < 0))
            {
                float t = math.abs(v1) / (math.abs(v1) + math.abs(v2));
                OctreeUtils.GetWorldPositionFromPosition(corner1Index, cellSize, octreeOffset, out float3 pos1);
                OctreeUtils.GetWorldPositionFromPosition(corner2Index, cellSize, octreeOffset, out float3 pos2);
                intersection = math.lerp(pos1, pos2, t);

                OctreeUtils.CalculateNormalFromOctree(octreeBuffer, octreeInfos, intersection, out normal);

                return true;
            }

            return false;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CoordToIndex(in int3 coord, in int3 gridSize)
        {
            return coord.x + coord.y * gridSize.x + coord.z * gridSize.x * gridSize.y;
        }
    }
}
