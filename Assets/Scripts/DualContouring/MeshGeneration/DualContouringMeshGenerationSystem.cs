using System.Runtime.CompilerServices;
using DualContouring.DualContouring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.MeshGeneration
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DualContouring.DualContouringOctreeSystem))]
    public partial struct DualContouringMeshGenerationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
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
            DynamicBuffer<DualContouringEdgeIntersection> edgeIntersectionBuffer,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            DynamicBuffer<DualContouringMeshTriangle> triangleBuffer)
        {
            vertexBuffer.Clear();
            triangleBuffer.Clear();
            
            if (cellBuffer.Length == 0)
            {
                return;
            }

            // Étape 1: Créer un mapping cellule->vertex pour les cellules avec vertex
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
            
            if (vertexBuffer.Length == 0)
            {
                cellGridToVertexIndex.Dispose();
                return;
            }

            // Étape 2: Créer un mapping edge->cellules pour identifier quelles cellules partagent une edge
            NativeParallelMultiHashMap<EdgeKey, int> edgeToCells = BuildEdgeToCellMapping(edgeIntersectionBuffer, cellBuffer, cellGridToVertexIndex);

            // Étape 3: Générer les quads à partir des edges partagées
            GenerateFacesFromEdges(edgeToCells, cellGridToVertexIndex, vertexBuffer, triangleBuffer);

            edgeToCells.Dispose();
            cellGridToVertexIndex.Dispose();

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

        /// <summary>
        /// Construit un mapping de chaque edge vers les cellules qui la touchent.
        /// Utilise directement les edges générées par les cellules, sans recalcul géométrique.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NativeParallelMultiHashMap<EdgeKey, int> BuildEdgeToCellMapping(
            DynamicBuffer<DualContouringEdgeIntersection> edgeIntersections,
            DynamicBuffer<DualContouringCell> cells,
            NativeHashMap<int3, int> cellGridToVertexIndex)
        {
            // Allouer avec une capacité suffisante (chaque edge peut être partagée par ~4 cellules)
            var edgeToCells = new NativeParallelMultiHashMap<EdgeKey, int>(edgeIntersections.Length * 4, Allocator.Temp);

            // Pour chaque intersection d'edge, l'ajouter au mapping avec sa cellule
            for (int i = 0; i < edgeIntersections.Length; i++)
            {
                var edgeIntersection = edgeIntersections[i];
                EdgeKey edge = edgeIntersection.Edge;
                
                // Trouver quelle cellule a généré cette edge
                // Les edges sont générées par DualContouringHelper pour chaque cellule
                // On doit trouver toutes les cellules qui ont un vertex et qui touchent cette edge
                
                // Pour cela, on parcourt toutes les cellules et on vérifie si l'edge est dans leurs bounds
                for (int cellIdx = 0; cellIdx < cells.Length; cellIdx++)
                {
                    var cell = cells[cellIdx];
                    if (!cell.HasVertex)
                        continue;
                    
                    if (!cellGridToVertexIndex.TryGetValue(cell.GridIndex, out int vertexIndex))
                        continue;
                    
                    // Vérifier si l'edge est dans les bounds de cette cellule
                    // Une edge appartient à une cellule si ses deux points sont dans/sur les bounds de la cellule
                    if (IsEdgeInCellBounds(edge, cell))
                    {
                        edgeToCells.Add(edge, vertexIndex);
                    }
                }
            }

            return edgeToCells;
        }
        
        /// <summary>
        /// Vérifie si une edge appartient à une cellule (si l'edge est sur le bord ou à l'intérieur)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsEdgeInCellBounds(EdgeKey edge, DualContouringCell cell)
        {
            int3 cellMin = cell.GridIndex;
            // Calculer cellStride à partir de la taille de la cellule
            // Supposons que les cellules de base ont une taille donnée par le plus petit Size
            // Pour LOD, cellStride = Size / baseSize
            // On peut approximer avec Size (si baseSize = 1)
            int cellStride = (int)math.round(cell.Size);
            if (cellStride < 1) cellStride = 1;
            
            int3 cellMax = cellMin + new int3(cellStride);
            
            int3 edgeMin = edge.Start;
            int3 edgeMax = edge.End;
            
            // L'edge appartient à la cellule si ses deux extrémités sont dans [cellMin, cellMax]
            bool startInBounds = math.all(edgeMin >= cellMin) && math.all(edgeMin <= cellMax);
            bool endInBounds = math.all(edgeMax >= cellMin) && math.all(edgeMax <= cellMax);
            
            return startInBounds && endInBounds;
        }



        /// <summary>
        /// Génère les faces du mesh à partir des edges partagées entre cellules.
        /// Chaque edge avec 4 cellules adjacentes génère un quad (2 triangles).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GenerateFacesFromEdges(
            NativeParallelMultiHashMap<EdgeKey, int> edgeToCells,
            NativeHashMap<int3, int> cellGridToVertexIndex,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            DynamicBuffer<DualContouringMeshTriangle> triangleBuffer)
        {
            var processedEdges = new NativeHashSet<EdgeKey>(edgeToCells.Count(), Allocator.Temp);
            var keys = edgeToCells.GetKeyArray(Allocator.Temp);
            
            for (int i = 0; i < keys.Length; i++)
            {
                EdgeKey edge = keys[i];
                
                if (processedEdges.Contains(edge))
                    continue;
                    
                processedEdges.Add(edge);
                
                // Récupérer tous les vertex des cellules adjacentes à cette edge
                var adjacentVertices = new NativeList<int>(4, Allocator.Temp);
                
                if (edgeToCells.TryGetFirstValue(edge, out int vertexIdx, out var iterator))
                {
                    adjacentVertices.Add(vertexIdx);
                    
                    while (edgeToCells.TryGetNextValue(out vertexIdx, ref iterator))
                    {
                        adjacentVertices.Add(vertexIdx);
                    }
                }
                
                // Générer un quad si on a 4 vertex (ou au moins 3)
                if (adjacentVertices.Length >= 3)
                {
                    GenerateQuadFromVertices(adjacentVertices, edge, vertexBuffer, triangleBuffer);
                }
                
                adjacentVertices.Dispose();
            }
            
            keys.Dispose();
            processedEdges.Dispose();
        }

        /// <summary>
        /// Génère un quad (2 triangles) à partir d'une liste de vertex autour d'une edge.
        /// Les vertex sont ordonnés pour former un quad cohérent.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void GenerateQuadFromVertices(
            NativeList<int> vertexIndices,
            EdgeKey edge,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            DynamicBuffer<DualContouringMeshTriangle> triangleBuffer)
        {
            if (vertexIndices.Length < 3)
                return;
            
            // Trier les vertex pour former un quad planaire cohérent
            // basé sur leur position relative à l'edge
            SortVerticesAroundEdge(vertexIndices, edge, vertexBuffer);
            
            // Créer les triangles
            if (vertexIndices.Length == 3)
            {
                // Triangle simple
                AddTriangleWithCorrectWinding(triangleBuffer, vertexBuffer,
                    vertexIndices[0], vertexIndices[1], vertexIndices[2]);
            }
            else if (vertexIndices.Length == 4)
            {
                // Quad complet (2 triangles)
                AddTriangleWithCorrectWinding(triangleBuffer, vertexBuffer,
                    vertexIndices[0], vertexIndices[1], vertexIndices[2]);
                AddTriangleWithCorrectWinding(triangleBuffer, vertexBuffer,
                    vertexIndices[0], vertexIndices[2], vertexIndices[3]);
            }
            else if (vertexIndices.Length > 4)
            {
                // Cas rare: plus de 4 vertex, créer un fan de triangles
                for (int i = 1; i < vertexIndices.Length - 1; i++)
                {
                    AddTriangleWithCorrectWinding(triangleBuffer, vertexBuffer,
                        vertexIndices[0], vertexIndices[i], vertexIndices[i + 1]);
                }
            }
        }

        /// <summary>
        /// Trie les vertex autour d'une edge pour former un quad cohérent.
        /// Utilise les angles dans le plan perpendiculaire à l'edge.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SortVerticesAroundEdge(
            NativeList<int> vertexIndices,
            EdgeKey edge,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer)
        {
            if (vertexIndices.Length <= 2)
                return;
            
            // Centre de l'edge
            float3 edgeCenter = edge.GetCenter();
            
            // Direction de l'edge
            float3 edgeDir = math.normalize(new float3(edge.End - edge.Start));
            
            // Créer un système de coordonnées perpendiculaire à l'edge
            float3 perpAxis1 = math.abs(edgeDir.y) < 0.9f ? 
                math.normalize(math.cross(edgeDir, new float3(0, 1, 0))) :
                math.normalize(math.cross(edgeDir, new float3(1, 0, 0)));
            
            // Calculer les angles de chaque vertex autour de l'edge
            var angles = new NativeArray<float>(vertexIndices.Length, Allocator.Temp);
            
            for (int i = 0; i < vertexIndices.Length; i++)
            {
                float3 vertexPos = vertexBuffer[vertexIndices[i]].Position;
                float3 toVertex = vertexPos - edgeCenter;
                
                // Projeter sur le plan perpendiculaire
                toVertex = toVertex - edgeDir * math.dot(toVertex, edgeDir);
                
                if (math.lengthsq(toVertex) > 0.0001f)
                {
                    toVertex = math.normalize(toVertex);
                    angles[i] = math.atan2(math.dot(toVertex, math.cross(edgeDir, perpAxis1)), 
                                           math.dot(toVertex, perpAxis1));
                }
                else
                {
                    angles[i] = 0;
                }
            }
            
            // Tri par insertion simple (peu de vertex)
            for (int i = 1; i < vertexIndices.Length; i++)
            {
                int tempIdx = vertexIndices[i];
                float tempAngle = angles[i];
                int j = i - 1;
                
                while (j >= 0 && angles[j] > tempAngle)
                {
                    vertexIndices[j + 1] = vertexIndices[j];
                    angles[j + 1] = angles[j];
                    j--;
                }
                
                vertexIndices[j + 1] = tempIdx;
                angles[j + 1] = tempAngle;
            }
            
            angles.Dispose();
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

