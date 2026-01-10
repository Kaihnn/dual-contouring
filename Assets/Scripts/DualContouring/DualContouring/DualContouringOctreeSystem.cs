using DualContouring.Octrees;
using DualContouring.ScalarField;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.DualContouring
{
    [BurstCompile]
    public partial struct DualContouringOctreeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OctreeNode>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new DualContouringOctreeJob();
            job.ScheduleParallel();
        }
    }

    [BurstCompile]
    partial struct DualContouringOctreeJob : IJobEntity
    {
        void Execute(
            ref DynamicBuffer<DualContouringCell> cellBuffer,
            ref DynamicBuffer<DualContouringEdgeIntersection> edgeIntersectionBuffer,
            in DynamicBuffer<OctreeNode> octreeBuffer,
            in DynamicBuffer<ScalarFieldItem> scalarFieldBuffer,
            in OctreeNodeInfos octreeNodeInfos,
            in ScalarFieldInfos scalarFieldInfos)
        {
            cellBuffer.Clear();
            edgeIntersectionBuffer.Clear();

            if (octreeBuffer.Length == 0)
            {
                return;
            }

            var nodesToProcess = new NativeList<int>(math.max(64, octreeBuffer.Length / 8), Allocator.Temp);
            nodesToProcess.Add(0);

            while (nodesToProcess.Length > 0)
            {
                int lastIndex = nodesToProcess.Length - 1;
                int nodeIndex = nodesToProcess[lastIndex];
                nodesToProcess.RemoveAtSwapBack(lastIndex);

                if (nodeIndex < 0 || nodeIndex >= octreeBuffer.Length)
                {
                    continue;
                }

                OctreeNode node = octreeBuffer[nodeIndex];

                if (node.ChildIndex >= 0)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        nodesToProcess.Add(node.ChildIndex + i);
                    }
                }
                else
                {
                    ProcessLeafNode(scalarFieldBuffer, cellBuffer, edgeIntersectionBuffer, node, scalarFieldInfos);
                }
            }

            nodesToProcess.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ProcessLeafNode(
            in DynamicBuffer<ScalarFieldItem> scalarField,
            DynamicBuffer<DualContouringCell> cells,
            DynamicBuffer<DualContouringEdgeIntersection> edgeIntersections,
            OctreeNode node,
            ScalarFieldInfos scalarFieldInfos)
        {
            int3 cellIndex = node.Position;
            int3 gridSize = scalarFieldInfos.GridSize;
            float cellSize = scalarFieldInfos.CellSize;
            float3 scalarFieldOffset = scalarFieldInfos.ScalarFieldOffset;

            int config = 0;

            for (int i = 0; i < 8; i++)
            {
                var offset = new int3(
                    i & 1,
                    (i >> 1) & 1,
                    (i >> 2) & 1
                );

                int3 cornerIndex = cellIndex + offset;
                int scalarIndex = ScalarFieldUtility.CoordToIndex(cornerIndex, gridSize);

                if (scalarIndex >= 0 && scalarIndex < scalarField.Length)
                {
                    ScalarFieldItem value = scalarField[scalarIndex];

                    if (value.Value >= 0)
                    {
                        config |= 1 << i;
                    }
                }
            }

            bool hasVertex = config != 0 && config != 255;

            ScalarFieldUtility.GetWorldPosition(cellIndex, cellSize, scalarFieldOffset, out float3 cellPosition);
            float3 vertexPosition = cellPosition + new float3(0.5f, 0.5f, 0.5f) * cellSize;
            var cellNormal = new float3(0, 1, 0);

            if (hasVertex)
            {
                CalculateVertexPositionAndNormal(scalarField,
                    edgeIntersections,
                    cellIndex,
                    scalarFieldInfos,
                    out vertexPosition,
                    out cellNormal);
            }

            cells.Add(new DualContouringCell
            {
                Position = cellPosition,
                Size = cellSize,
                HasVertex = hasVertex,
                VertexPosition = vertexPosition,
                Normal = cellNormal,
                GridIndex = cellIndex
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CalculateVertexPositionAndNormal(
            in DynamicBuffer<ScalarFieldItem> scalarField,
            DynamicBuffer<DualContouringEdgeIntersection> edgeIntersections,
            int3 cellIndex,
            ScalarFieldInfos scalarFieldInfos,
            out float3 vertexPosition,
            out float3 cellNormal)
        {
            int3 gridSize = scalarFieldInfos.GridSize;
            float cellSize = scalarFieldInfos.CellSize;
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

            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    if (TryGetEdgeIntersection(scalarField,
                            cellIndex + new int3(0, y, z),
                            cellIndex + new int3(1, y, z),
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

            for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    if (TryGetEdgeIntersection(scalarField,
                            cellIndex + new int3(x, 0, z),
                            cellIndex + new int3(x, 1, z),
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

            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    if (TryGetEdgeIntersection(scalarField,
                            cellIndex + new int3(x, y, 0),
                            cellIndex + new int3(x, y, 1),
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

                float3 vertexPos = SolveQef(positions, normals, count, massPoint);

                int scalarIndex = ScalarFieldUtility.CoordToIndex(cellIndex, gridSize);
                if (scalarIndex >= 0 && scalarIndex < scalarField.Length)
                {
                    ScalarFieldUtility.GetWorldPosition(cellIndex, cellSize, scalarFieldOffset, out float3 cellMin);
                    float3 cellMax = cellMin + new float3(cellSize, cellSize, cellSize);
                    vertexPos = math.clamp(vertexPos, cellMin, cellMax);
                }

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
                ScalarFieldUtility.GetWorldPosition(cellIndex, cellSize, scalarFieldOffset, out float3 cellMin);
                vertexPosition = cellMin + new float3(0.5f, 0.5f, 0.5f) * cellSize;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float3 SolveQef(NativeArray<float3> positions, NativeArray<float3> normals, int count, float3 massPoint)
        {
            float3x3 ata = float3x3.zero;
            float3 atb = float3.zero;

            for (int i = 0; i < count; i++)
            {
                float3 n = normals[i];
                float3 p = positions[i];

                ata.c0 += n * n.x;
                ata.c1 += n * n.y;
                ata.c2 += n * n.z;

                float d = math.dot(n, p);
                atb += n * d;
            }

            float3 result = SolveLinearSystem3X3(ata, atb);

            if (math.any(math.isnan(result)) || math.any(math.isinf(result)))
            {
                return massPoint;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float3 SolveLinearSystem3X3(float3x3 a, float3 b)
        {
            float epsilon = 1e-10f;

            var row0 = new float3(a.c0.x, a.c1.x, a.c2.x);
            var row1 = new float3(a.c0.y, a.c1.y, a.c2.y);
            var row2 = new float3(a.c0.z, a.c1.z, a.c2.z);
            float3 rhs = b;

            if (math.abs(row0.x) < epsilon)
            {
                if (math.abs(row1.x) > math.abs(row0.x))
                {
                    float3 temp = row0;
                    row0 = row1;
                    row1 = temp;
                    float tempB = rhs.x;
                    rhs.x = rhs.y;
                    rhs.y = tempB;
                }

                if (math.abs(row2.x) > math.abs(row0.x))
                {
                    float3 temp = row0;
                    row0 = row2;
                    row2 = temp;
                    float tempB = rhs.x;
                    rhs.x = rhs.z;
                    rhs.z = tempB;
                }
            }

            if (math.abs(row0.x) > epsilon)
            {
                float factor1 = row1.x / row0.x;
                row1 -= row0 * factor1;
                rhs.y -= rhs.x * factor1;

                float factor2 = row2.x / row0.x;
                row2 -= row0 * factor2;
                rhs.z -= rhs.x * factor2;
            }

            if (math.abs(row1.y) < epsilon && math.abs(row2.y) > math.abs(row1.y))
            {
                float3 temp = row1;
                row1 = row2;
                row2 = temp;
                float tempB = rhs.y;
                rhs.y = rhs.z;
                rhs.z = tempB;
            }

            if (math.abs(row1.y) > epsilon)
            {
                float factor = row2.y / row1.y;
                row2 -= row1 * factor;
                rhs.z -= rhs.y * factor;
            }

            float3 result = float3.zero;

            if (math.abs(row2.z) > epsilon)
            {
                result.z = rhs.z / row2.z;
            }

            if (math.abs(row1.y) > epsilon)
            {
                result.y = (rhs.y - row1.z * result.z) / row1.y;
            }

            if (math.abs(row0.x) > epsilon)
            {
                result.x = (rhs.x - row0.y * result.y - row0.z * result.z) / row0.x;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool TryGetEdgeIntersection(
            in DynamicBuffer<ScalarFieldItem> scalarField,
            int3 corner1Index,
            int3 corner2Index,
            out float3 intersection,
            out float3 normal,
            ScalarFieldInfos scalarFieldInfos)
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

                normal = CalculateNormal(scalarField, intersection, scalarFieldInfos);

                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float3 CalculateNormal(in DynamicBuffer<ScalarFieldItem> scalarField, float3 position, ScalarFieldInfos scalarFieldInfos)
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
                return math.normalize(gradient);
            }

            return new float3(0, 1, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float SampleScalarField(in DynamicBuffer<ScalarFieldItem> scalarField, float3 position, ScalarFieldInfos scalarFieldInfos)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        float GetScalarValueAtCoord(in DynamicBuffer<ScalarFieldItem> scalarField, int3 coord, int3 gridSize)
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

