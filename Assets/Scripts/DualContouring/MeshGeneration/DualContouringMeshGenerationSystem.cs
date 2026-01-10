using System.Runtime.CompilerServices;
using DualContouring.DualContouring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.MeshGeneration
{
    [BurstCompile]
    public partial struct DualContouringMeshGenerationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DualContouringCell>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var generateMeshJob = new GenerateMeshJob();
            generateMeshJob.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct GenerateMeshJob : IJobEntity
    {
        private void Execute(
            DynamicBuffer<DualContouringCell> cellBuffer,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            DynamicBuffer<DualContouringMeshTriangle> triangleBuffer)
        {
            vertexBuffer.Clear();
            triangleBuffer.Clear();

            NativeHashMap<int3, int> cellGridToVertexIndex = new NativeHashMap<int3, int>(cellBuffer.Length, Allocator.Temp);

            for (int i = 0; i < cellBuffer.Length; i++)
            {
                DualContouringCell cell = cellBuffer[i];
                if (cell.HasVertex)
                {
                    int vertexIndex = vertexBuffer.Length;
                    cellGridToVertexIndex.Add(cell.GridIndex, vertexIndex);

                    vertexBuffer.Add(new DualContouringMeshVertex
                    {
                        Position = cell.VertexPosition,
                        Normal = cell.Normal
                    });
                }
            }

            GenerateFacesFromCells(cellBuffer, cellGridToVertexIndex, vertexBuffer, triangleBuffer);

            cellGridToVertexIndex.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GenerateFacesFromCells(
            DynamicBuffer<DualContouringCell> cellBuffer,
            NativeHashMap<int3, int> cellGridToVertexIndex,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            DynamicBuffer<DualContouringMeshTriangle> triangleBuffer)
        {
            NativeHashSet<int3> processedEdges = new NativeHashSet<int3>(cellBuffer.Length * 3, Allocator.Temp);

            for (int cellIdx = 0; cellIdx < cellBuffer.Length; cellIdx++)
            {
                DualContouringCell cell = cellBuffer[cellIdx];
                if (!cell.HasVertex)
                {
                    continue;
                }

                int3 cellPos = cell.GridIndex;

                GenerateFacesForEdge(cellPos, new int3(1, 0, 0), new int3(0, 1, 0), new int3(0, 0, 1),
                    cellGridToVertexIndex, vertexBuffer, triangleBuffer, processedEdges);

                GenerateFacesForEdge(cellPos, new int3(0, 1, 0), new int3(0, 0, 1), new int3(1, 0, 0),
                    cellGridToVertexIndex, vertexBuffer, triangleBuffer, processedEdges);

                GenerateFacesForEdge(cellPos, new int3(0, 0, 1), new int3(1, 0, 0), new int3(0, 1, 0),
                    cellGridToVertexIndex, vertexBuffer, triangleBuffer, processedEdges);
            }

            processedEdges.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GenerateFacesForEdge(
            int3 baseCoord,
            int3 axisDir,
            int3 tangent1,
            int3 tangent2,
            NativeHashMap<int3, int> cellGridToVertexIndex,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            DynamicBuffer<DualContouringMeshTriangle> triangleBuffer,
            NativeHashSet<int3> processedEdges)
        {
            int3 edgeKey = baseCoord * 8 + axisDir * 4;
            if (processedEdges.Contains(edgeKey))
            {
                return;
            }
            processedEdges.Add(edgeKey);

            int3 c00 = baseCoord;
            int3 c10 = baseCoord + tangent1;
            int3 c01 = baseCoord + tangent2;
            int3 c11 = baseCoord + tangent1 + tangent2;

            if (!cellGridToVertexIndex.TryGetValue(c00, out int v00) ||
                !cellGridToVertexIndex.TryGetValue(c10, out int v10) ||
                !cellGridToVertexIndex.TryGetValue(c01, out int v01) ||
                !cellGridToVertexIndex.TryGetValue(c11, out int v11))
            {
                return;
            }

            AddTriangleWithCorrectWinding(triangleBuffer, vertexBuffer, v00, v10, v11);
            AddTriangleWithCorrectWinding(triangleBuffer, vertexBuffer, v00, v11, v01);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddTriangleWithCorrectWinding(
            DynamicBuffer<DualContouringMeshTriangle> triangleBuffer,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            int i0, int i1, int i2)
        {
            DualContouringMeshVertex v0 = vertexBuffer[i0];
            DualContouringMeshVertex v1 = vertexBuffer[i1];
            DualContouringMeshVertex v2 = vertexBuffer[i2];

            float3 faceNormal = math.normalize(v0.Normal + v1.Normal + v2.Normal);

            float3 edge1 = v1.Position - v0.Position;
            float3 edge2 = v2.Position - v0.Position;
            float3 geometricNormal = math.normalize(math.cross(edge1, edge2));

            float dot = math.dot(geometricNormal, faceNormal);

            if (dot > 0)
            {
                triangleBuffer.Add(new DualContouringMeshTriangle { Index = i0 });
                triangleBuffer.Add(new DualContouringMeshTriangle { Index = i1 });
                triangleBuffer.Add(new DualContouringMeshTriangle { Index = i2 });
            }
            else
            {
                triangleBuffer.Add(new DualContouringMeshTriangle { Index = i0 });
                triangleBuffer.Add(new DualContouringMeshTriangle { Index = i2 });
                triangleBuffer.Add(new DualContouringMeshTriangle { Index = i1 });
            }
        }
    }
}

