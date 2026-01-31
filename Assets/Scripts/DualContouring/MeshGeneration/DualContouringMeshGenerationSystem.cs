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
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var generateMeshJob = new GenerateMeshJob
            {
                ECB = ecb.AsParallelWriter()
            };
            generateMeshJob.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct GenerateMeshJob : IJobEntity
    {
        public EntityCommandBuffer.ParallelWriter ECB;

        private void Execute(
            Entity entity,
            [ChunkIndexInQuery] int sortKey,
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

            // Merger les vertex proches AVANT de générer les faces
            NativeArray<int> vertexRemap = MergeCloseVertices(vertexBuffer, cellBuffer, cellGridToVertexIndex);
            
            // Mettre à jour le mapping cellule->vertex avec les vertex mergés
            UpdateCellGridMapping(cellGridToVertexIndex, vertexRemap);

            GenerateFacesFromCells(cellBuffer, cellGridToVertexIndex, vertexBuffer, triangleBuffer);

            cellGridToVertexIndex.Dispose();
            vertexRemap.Dispose();

            if (vertexBuffer.Length > 0)
            {
                var bounds = ComputeBounds(vertexBuffer);
                ECB.AddComponent(sortKey, entity, bounds);
                ECB.AddComponent<DualContouringMeshDirty>(sortKey, entity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private DualContouringMeshBounds ComputeBounds(DynamicBuffer<DualContouringMeshVertex> vertexBuffer)
        {
            float3 min = new float3(float.MaxValue);
            float3 max = new float3(float.MinValue);

            for (int i = 0; i < vertexBuffer.Length; i++)
            {
                float3 pos = vertexBuffer[i].Position;
                min = math.min(min, pos);
                max = math.max(max, pos);
            }

            return new DualContouringMeshBounds
            {
                Center = (min + max) * 0.5f,
                Size = max - min
            };
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

            bool has00 = cellGridToVertexIndex.TryGetValue(c00, out int v00);
            bool has10 = cellGridToVertexIndex.TryGetValue(c10, out int v10);
            bool has01 = cellGridToVertexIndex.TryGetValue(c01, out int v01);
            bool has11 = cellGridToVertexIndex.TryGetValue(c11, out int v11);

            // On a besoin des 4 vertex pour créer un quad
            if (!has00 || !has10 || !has01 || !has11)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckAndAddMissingCellsForQuad(
            int3 baseCoord,
            int3 tangent1,
            int3 tangent2,
            NativeHashMap<int3, int> cellGridToVertexIndex,
            NativeHashSet<int3> cellsToAdd,
            DynamicBuffer<DualContouringCell> cellBuffer)
        {
            int3 c00 = baseCoord;
            int3 c10 = baseCoord + tangent1;
            int3 c01 = baseCoord + tangent2;
            int3 c11 = baseCoord + tangent1 + tangent2;

            bool has00 = cellGridToVertexIndex.ContainsKey(c00);
            bool has10 = cellGridToVertexIndex.ContainsKey(c10);
            bool has01 = cellGridToVertexIndex.ContainsKey(c01);
            bool has11 = cellGridToVertexIndex.ContainsKey(c11);

            int count = (has00 ? 1 : 0) + (has10 ? 1 : 0) + (has01 ? 1 : 0) + (has11 ? 1 : 0);

            // Si on a exactement 3 cellules, vérifier si on doit ajouter la 4ème
            if (count == 3)
            {
                int3 missingCell = int3.zero;
                if (!has00) missingCell = c00;
                else if (!has10) missingCell = c10;
                else if (!has01) missingCell = c01;
                else if (!has11) missingCell = c11;

                // Vérifier que la cellule manquante existe dans le buffer (sans vertex)
                // Cela signifie qu'elle est proche de la surface
                bool existsInBuffer = false;
                for (int i = 0; i < cellBuffer.Length; i++)
                {
                    if (math.all(cellBuffer[i].GridIndex == missingCell))
                    {
                        existsInBuffer = true;
                        break;
                    }
                }

                // Seulement ajouter si la cellule existe déjà dans le buffer
                // (donc elle est proche de la surface, juste sans vertex)
                if (existsInBuffer)
                {
                    cellsToAdd.Add(missingCell);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 InterpolatePositionFromNeighbors(
            int3 cellPos,
            NativeHashMap<int3, int> cellGridToVertexIndex,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            DynamicBuffer<DualContouringCell> cellBuffer)
        {
            float3 avgPosition = float3.zero;
            int count = 0;

            // Chercher dans les 6 directions
            int3[] directions = new int3[]
            {
                new int3(1, 0, 0), new int3(-1, 0, 0),
                new int3(0, 1, 0), new int3(0, -1, 0),
                new int3(0, 0, 1), new int3(0, 0, -1)
            };

            for (int i = 0; i < 6; i++)
            {
                int3 neighborPos = cellPos + directions[i];
                if (cellGridToVertexIndex.TryGetValue(neighborPos, out int vertexIndex))
                {
                    avgPosition += vertexBuffer[vertexIndex].Position;
                    count++;
                }
            }

            if (count > 0)
            {
                return avgPosition / count;
            }

            // Fallback: utiliser la position du centre de la cellule
            for (int i = 0; i < cellBuffer.Length; i++)
            {
                if (math.all(cellBuffer[i].GridIndex == cellPos))
                {
                    return cellBuffer[i].Position + new float3(0.5f) * cellBuffer[i].Size;
                }
            }

            return float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 InterpolateNormalFromNeighbors(
            int3 cellPos,
            NativeHashMap<int3, int> cellGridToVertexIndex,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer)
        {
            float3 avgNormal = float3.zero;
            int count = 0;

            // Chercher dans les 6 directions
            int3[] directions = new int3[]
            {
                new int3(1, 0, 0), new int3(-1, 0, 0),
                new int3(0, 1, 0), new int3(0, -1, 0),
                new int3(0, 0, 1), new int3(0, 0, -1)
            };

            for (int i = 0; i < 6; i++)
            {
                int3 neighborPos = cellPos + directions[i];
                if (cellGridToVertexIndex.TryGetValue(neighborPos, out int vertexIndex))
                {
                    avgNormal += vertexBuffer[vertexIndex].Normal;
                    count++;
                }
            }

            if (count > 0)
            {
                return math.normalize(avgNormal);
            }

            return new float3(0, 1, 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NativeArray<int> MergeCloseVertices(
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            DynamicBuffer<DualContouringCell> cellBuffer,
            NativeHashMap<int3, int> cellGridToVertexIndex)
        {
            NativeArray<int> vertexRemap = new NativeArray<int>(vertexBuffer.Length * 2, Allocator.Temp);
            
            if (vertexBuffer.Length == 0)
            {
                return vertexRemap;
            }

            // Initialiser le remap (chaque vertex pointe vers lui-même)
            for (int i = 0; i < vertexBuffer.Length; i++)
            {
                vertexRemap[i] = i;
            }

            // Seuil de distance pour merger les vertex
            float mergeThreshold = 0.05f;
            float mergeThresholdSq = mergeThreshold * mergeThreshold;

            // Pour chaque vertex, vérifier s'il y a des vertex proches
            for (int i = 0; i < vertexBuffer.Length; i++)
            {
                if (vertexRemap[i] != i)
                {
                    continue;
                }

                DualContouringMeshVertex v1 = vertexBuffer[i];

                for (int j = i + 1; j < vertexBuffer.Length; j++)
                {
                    if (vertexRemap[j] != j)
                    {
                        continue;
                    }

                    DualContouringMeshVertex v2 = vertexBuffer[j];
                    float distSq = math.lengthsq(v1.Position - v2.Position);

                    if (distSq < mergeThresholdSq)
                    {
                        vertexRemap[j] = i;
                    }
                }
            }

            // Ajouter des cellules fictives pour les quads incomplets UNIQUEMENT à la surface
            AddSurfaceFillCells(cellBuffer, cellGridToVertexIndex, vertexBuffer, vertexRemap);

            return vertexRemap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddSurfaceFillCells(
            DynamicBuffer<DualContouringCell> cellBuffer,
            NativeHashMap<int3, int> cellGridToVertexIndex,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            NativeArray<int> vertexRemap)
        {
            NativeList<int3> cellsWithVertex = new NativeList<int3>(cellBuffer.Length, Allocator.Temp);
            for (int i = 0; i < cellBuffer.Length; i++)
            {
                if (cellBuffer[i].HasVertex)
                {
                    cellsWithVertex.Add(cellBuffer[i].GridIndex);
                }
            }

            NativeHashSet<int3> cellsToAdd = new NativeHashSet<int3>(32, Allocator.Temp);
            
            for (int i = 0; i < cellsWithVertex.Length; i++)
            {
                int3 cellPos = cellsWithVertex[i];
                
                CheckAndAddMissingCellsForQuad(cellPos, new int3(1, 0, 0), new int3(0, 1, 0), 
                    cellGridToVertexIndex, cellsToAdd, cellBuffer);
                CheckAndAddMissingCellsForQuad(cellPos, new int3(0, 1, 0), new int3(0, 0, 1), 
                    cellGridToVertexIndex, cellsToAdd, cellBuffer);
                CheckAndAddMissingCellsForQuad(cellPos, new int3(0, 0, 1), new int3(1, 0, 0), 
                    cellGridToVertexIndex, cellsToAdd, cellBuffer);
            }

            var cellsToAddArray = cellsToAdd.ToNativeArray(Allocator.Temp);
            for (int i = 0; i < cellsToAddArray.Length; i++)
            {
                int3 cellPos = cellsToAddArray[i];
                
                float3 position = InterpolatePositionFromNeighbors(cellPos, cellGridToVertexIndex, vertexBuffer, cellBuffer);
                float3 normal = InterpolateNormalFromNeighbors(cellPos, cellGridToVertexIndex, vertexBuffer);
                
                int vertexIndex = vertexBuffer.Length;
                
                // Étendre le remap si nécessaire
                if (vertexIndex >= vertexRemap.Length)
                {
                    continue;
                }
                
                vertexRemap[vertexIndex] = vertexIndex;
                cellGridToVertexIndex.Add(cellPos, vertexIndex);
                
                vertexBuffer.Add(new DualContouringMeshVertex
                {
                    Position = position,
                    Normal = normal
                });
            }

            cellsToAddArray.Dispose();
            cellsToAdd.Dispose();
            cellsWithVertex.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateCellGridMapping(
            NativeHashMap<int3, int> cellGridToVertexIndex,
            NativeArray<int> vertexRemap)
        {
            // Créer une liste temporaire des clés
            var keys = cellGridToVertexIndex.GetKeyArray(Allocator.Temp);
            
            // Mettre à jour chaque entrée avec l'index remappé
            for (int i = 0; i < keys.Length; i++)
            {
                int3 key = keys[i];
                int oldIndex = cellGridToVertexIndex[key];
                int newIndex = vertexRemap[oldIndex];
                cellGridToVertexIndex[key] = newIndex;
            }
            
            keys.Dispose();
        }
    }
}

