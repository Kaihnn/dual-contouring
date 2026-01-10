using DualContouring.DualContouring;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.MeshGeneration
{
    /// <summary>
    ///     Système qui génère un mesh à partir des cellules de dual contouring
    ///     Dans le dual contouring, on crée des quads entre les vertices des cellules adjacentes
    ///     qui partagent une arête traversant la surface
    /// </summary>
    [BurstCompile]
    public partial struct DualContouringMeshGenerationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DualContouringCell>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (cellBuffer, vertexBuffer, triangleBuffer) in SystemAPI.Query<
                         DynamicBuffer<DualContouringCell>,
                         DynamicBuffer<DualContouringMeshVertex>,
                         DynamicBuffer<DualContouringMeshTriangle>>())
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
            
                // Deuxième passe: créer les faces entre les cellules adjacentes
                // Dans le dual contouring, on crée une face pour chaque arête de la grille qui traverse la surface
                // On parcourt toutes les arêtes possibles de la grille de cellules

                GenerateFacesFromCells(cellBuffer, cellGridToVertexIndex, vertexBuffer, triangleBuffer);

                cellGridToVertexIndex.Dispose();
            }
        }
    
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
    
        /// <summary>
        ///     Ajoute un triangle en déterminant automatiquement l'ordre correct des vertices
        ///     en utilisant la normale de face (moyenne des normales des vertices)
        /// </summary>
        private void AddTriangleWithCorrectWinding(
            DynamicBuffer<DualContouringMeshTriangle> triangleBuffer,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            int i0, int i1, int i2)
        {
            // Récupérer les vertices
            DualContouringMeshVertex v0 = vertexBuffer[i0];
            DualContouringMeshVertex v1 = vertexBuffer[i1];
            DualContouringMeshVertex v2 = vertexBuffer[i2];
        
            // Calculer la normale de face : moyenne des normales des 3 vertices
            float3 faceNormal = math.normalize(v0.Normal + v1.Normal + v2.Normal);
        
            // Calculer la normale géométrique du triangle (ordre v0, v1, v2)
            float3 edge1 = v1.Position - v0.Position;
            float3 edge2 = v2.Position - v0.Position;
            float3 geometricNormal = math.normalize(math.cross(edge1, edge2));
        
            // Vérifier si la normale géométrique est dans la même direction que la normale de face
            float dot = math.dot(geometricNormal, faceNormal);
        
            if (dot > 0)
            {
                // Les normales sont dans la même direction, ordre normal
                triangleBuffer.Add(new DualContouringMeshTriangle { Index = i0 });
                triangleBuffer.Add(new DualContouringMeshTriangle { Index = i1 });
                triangleBuffer.Add(new DualContouringMeshTriangle { Index = i2 });
            }
            else
            {
                // Les normales sont opposées, inverser l'ordre
                triangleBuffer.Add(new DualContouringMeshTriangle { Index = i0 });
                triangleBuffer.Add(new DualContouringMeshTriangle { Index = i2 });
                triangleBuffer.Add(new DualContouringMeshTriangle { Index = i1 });
            }
        }
    }
}

