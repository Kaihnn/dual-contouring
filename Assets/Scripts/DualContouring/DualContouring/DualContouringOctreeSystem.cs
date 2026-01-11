using System.Runtime.CompilerServices;
using DualContouring.DualContouring.Debug;
using DualContouring.Octrees;
using DualContouring.ScalarField;
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
            state.RequireForUpdate<DualContouringOptions>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out DualContouringOptions options) ||
                options.Type != DualContouringType.Octree)
            {
                return;
            }

            var job = new DualContouringOctreeJob();
            job.ScheduleParallel();
        }
    }

    [BurstCompile]
    internal partial struct DualContouringOctreeJob : IJobEntity
    {
        private void Execute(
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

            NativeList<int> nodesToProcess = new NativeList<int>(math.max(64, octreeBuffer.Length / 8), Allocator.Temp);
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
        private void ProcessLeafNode(
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
        private void CalculateVertexPositionAndNormal(
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

                // Utiliser QEF pour trouver la meilleure position
                QefSolver.SolveQef(positions, normals, count, massPoint, out float3 vertexPos);

                // Calculer les limites de la cellule
                ScalarFieldUtility.GetWorldPosition(cellIndex, cellSize, scalarFieldOffset, out float3 cellMin);
                float3 cellMax = cellMin + new float3(cellSize, cellSize, cellSize);

                // Vérifier si le vertex est trop loin du centre de masse des intersections
                float distanceToMass = math.length(vertexPos - massPoint);
                float maxDistance = cellSize * 2.0f; // Autoriser une certaine distance

                if (distanceToMass > maxDistance || math.any(math.isnan(vertexPos)) || math.any(math.isinf(vertexPos)))
                {
                    // Fallback vers le centre de masse si le QEF donne une solution aberrante
                    vertexPos = massPoint;
                }

                // Contraindre le vertex à l'intérieur de la cellule
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
                ScalarFieldUtility.GetWorldPosition(cellIndex, cellSize, scalarFieldOffset, out float3 cellMin);
                vertexPosition = cellMin + new float3(0.5f, 0.5f, 0.5f) * cellSize;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetEdgeIntersection(
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
        private float3 CalculateNormal(in DynamicBuffer<ScalarFieldItem> scalarField, float3 position, ScalarFieldInfos scalarFieldInfos)
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
        private float SampleScalarField(in DynamicBuffer<ScalarFieldItem> scalarField, float3 position, ScalarFieldInfos scalarFieldInfos)
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
        private float GetScalarValueAtCoord(in DynamicBuffer<ScalarFieldItem> scalarField, int3 coord, int3 gridSize)
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