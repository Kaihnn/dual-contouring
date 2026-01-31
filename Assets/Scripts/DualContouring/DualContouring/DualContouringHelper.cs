using System.Runtime.CompilerServices;
using DualContouring.ScalarField;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.DualContouring
{
    [BurstCompile]
    public static class DualContouringHelper
    {
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalculateVertexPositionAndNormal(
            in DynamicBuffer<ScalarFieldItem> scalarField,
            ref DynamicBuffer<DualContouringEdgeIntersection> edgeIntersections,
            in int3 cellIndex,
            in ScalarFieldInfos scalarFieldInfos,
            in float cellSize,
            out float3 vertexPosition,
            out float3 cellNormal)
        {
            int3 gridSize = scalarFieldInfos.GridSize;
            float baseCellSize = scalarFieldInfos.CellSize;
            float3 scalarFieldOffset = scalarFieldInfos.ScalarFieldOffset;

            vertexPosition = float3.zero;
            cellNormal = new float3(0, 1, 0);

            int3 cellGridSize = gridSize - new int3(1, 1, 1);
            int currentCellIndex = ScalarFieldUtility.CoordToIndex(cellIndex, cellGridSize);

            NativeArray<float3> positions = new NativeArray<float3>(12, Allocator.Temp);
            NativeArray<float3> normals = new NativeArray<float3>(12, Allocator.Temp);
            int count = 0;
            float3 massPoint = float3.zero;
            float3 normalSum = float3.zero;
            
            // Calculer le nombre de cellules de base que couvre cette cellule LOD
            int cellStride = (int)math.round(cellSize / baseCellSize);

            // Arêtes parallèles à X (4)
            for (int y = 0; y <= cellStride; y += cellStride)
            {
                for (int z = 0; z <= cellStride; z += cellStride)
                {
                    if (TryGetEdgeIntersection(scalarField,
                            cellIndex + new int3(0, y, z),
                            cellIndex + new int3(cellStride, y, z),
                            out float3 intersection,
                            out float3 normal,
                            scalarFieldInfos))
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

            // Arêtes parallèles à Y (4)
            for (int x = 0; x <= cellStride; x += cellStride)
            {
                for (int z = 0; z <= cellStride; z += cellStride)
                {
                    if (TryGetEdgeIntersection(scalarField,
                            cellIndex + new int3(x, 0, z),
                            cellIndex + new int3(x, cellStride, z),
                            out float3 intersection,
                            out float3 normal,
                            scalarFieldInfos))
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

            // Arêtes parallèles à Z (4)
            for (int x = 0; x <= cellStride; x += cellStride)
            {
                for (int y = 0; y <= cellStride; y += cellStride)
                {
                    if (TryGetEdgeIntersection(scalarField,
                            cellIndex + new int3(x, y, 0),
                            cellIndex + new int3(x, y, cellStride),
                            out float3 intersection,
                            out float3 normal,
                            scalarFieldInfos))
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

                ScalarFieldUtility.GetWorldPosition(cellIndex, baseCellSize, scalarFieldOffset, out float3 cellMin);
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

            int fallbackIndex = ScalarFieldUtility.CoordToIndex(cellIndex, gridSize);
            if (fallbackIndex >= 0 && fallbackIndex < scalarField.Length)
            {
                ScalarFieldUtility.GetWorldPosition(cellIndex, baseCellSize, scalarFieldOffset, out float3 cellMin);
                vertexPosition = cellMin + new float3(0.5f, 0.5f, 0.5f) * cellSize;
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetEdgeIntersection(
            in DynamicBuffer<ScalarFieldItem> scalarField,
            in int3 corner1Index,
            in int3 corner2Index,
            out float3 intersection,
            out float3 normal,
            in ScalarFieldInfos scalarFieldInfos)
        {
            int3 gridSize = scalarFieldInfos.GridSize;
            float cellSize = scalarFieldInfos.CellSize;
            float3 scalarFieldOffset = scalarFieldInfos.ScalarFieldOffset;

            intersection = float3.zero;
            normal = float3.zero;

            int idx1 = ScalarFieldUtility.CoordToIndex(corner1Index, gridSize);
            int idx2 = ScalarFieldUtility.CoordToIndex(corner2Index, gridSize);

            if (idx1 < 0 || idx1 >= scalarField.Length || idx2 < 0 || idx2 >= scalarField.Length)
            {
                return false;
            }

            ScalarFieldItem v1 = scalarField[idx1];
            ScalarFieldItem v2 = scalarField[idx2];

            if ((v1.Value < 0 && v2.Value >= 0) || (v1.Value >= 0 && v2.Value < 0))
            {
                float t = math.abs(v1.Value) / (math.abs(v1.Value) + math.abs(v2.Value));
                ScalarFieldUtility.GetWorldPosition(corner1Index, cellSize, scalarFieldOffset, out float3 pos1);
                ScalarFieldUtility.GetWorldPosition(corner2Index, cellSize, scalarFieldOffset, out float3 pos2);
                intersection = math.lerp(pos1, pos2, t);

                CalculateNormal(scalarField, intersection, scalarFieldInfos, out normal);

                return true;
            }

            return false;
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalculateNormal(in DynamicBuffer<ScalarFieldItem> scalarField, in float3 position, in ScalarFieldInfos scalarFieldInfos, out float3 normal)
        {
            float cellSize = scalarFieldInfos.CellSize;
            float epsilon = cellSize * 0.1f;

            float valueXPlus = SampleScalarField(scalarField, position + new float3(epsilon, 0, 0), scalarFieldInfos);
            float valueXMinus = SampleScalarField(scalarField, position - new float3(epsilon, 0, 0), scalarFieldInfos);
            float gradX = (valueXPlus - valueXMinus) / (2.0f * epsilon);

            float valueYPlus = SampleScalarField(scalarField, position + new float3(0, epsilon, 0), scalarFieldInfos);
            float valueYMinus = SampleScalarField(scalarField, position - new float3(0, epsilon, 0), scalarFieldInfos);
            float gradY = (valueYPlus - valueYMinus) / (2.0f * epsilon);

            float valueZPlus = SampleScalarField(scalarField, position + new float3(0, 0, epsilon), scalarFieldInfos);
            float valueZMinus = SampleScalarField(scalarField, position - new float3(0, 0, epsilon), scalarFieldInfos);
            float gradZ = (valueZPlus - valueZMinus) / (2.0f * epsilon);

            var gradient = new float3(gradX, gradY, gradZ);

            float length = math.length(gradient);
            if (length > 0.0001f)
            {
                normal = math.normalize(gradient);
                return;
            }

            normal = new float3(0, 1, 0);
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleScalarField(in DynamicBuffer<ScalarFieldItem> scalarField, in float3 position, in ScalarFieldInfos scalarFieldInfos)
        {
            if (scalarField.Length == 0)
            {
                return 0;
            }

            int3 gridSize = scalarFieldInfos.GridSize;
            float cellSize = scalarFieldInfos.CellSize;
            float3 scalarFieldOffset = scalarFieldInfos.ScalarFieldOffset;

            float3 gridPos = (position - scalarFieldOffset) / cellSize;

            var baseCell = new int3(
                (int)math.floor(gridPos.x),
                (int)math.floor(gridPos.y),
                (int)math.floor(gridPos.z)
            );

            float3 t = gridPos - new float3(baseCell);

            baseCell = math.clamp(baseCell, int3.zero, gridSize - new int3(2, 2, 2));
            t = math.clamp(t, 0.0f, 1.0f);

            float v000 = GetScalarValueAtCoord(scalarField, baseCell + new int3(0, 0, 0), gridSize);
            float v100 = GetScalarValueAtCoord(scalarField, baseCell + new int3(1, 0, 0), gridSize);
            float v010 = GetScalarValueAtCoord(scalarField, baseCell + new int3(0, 1, 0), gridSize);
            float v110 = GetScalarValueAtCoord(scalarField, baseCell + new int3(1, 1, 0), gridSize);
            float v001 = GetScalarValueAtCoord(scalarField, baseCell + new int3(0, 0, 1), gridSize);
            float v101 = GetScalarValueAtCoord(scalarField, baseCell + new int3(1, 0, 1), gridSize);
            float v011 = GetScalarValueAtCoord(scalarField, baseCell + new int3(0, 1, 1), gridSize);
            float v111 = GetScalarValueAtCoord(scalarField, baseCell + new int3(1, 1, 1), gridSize);

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
        public static float GetScalarValueAtCoord(in DynamicBuffer<ScalarFieldItem> scalarField, in int3 coord, in int3 gridSize)
        {
            int index = ScalarFieldUtility.CoordToIndex(coord, gridSize);
            if (index >= 0 && index < scalarField.Length)
            {
                return scalarField[index].Value;
            }

            return 0;
        }
    }
}

