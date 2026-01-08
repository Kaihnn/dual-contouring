using DualContouring.DualContouring;
using DualContouring.ScalarField;
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

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (cellBuffer, vertexBuffer, triangleBuffer, scalarFieldInfo) in SystemAPI.Query<
                         DynamicBuffer<DualContouringCell>,
                         DynamicBuffer<DualContouringMeshVertex>,
                         DynamicBuffer<DualContouringMeshTriangle>,
                         RefRO<ScalarFieldInfos>>())
            {
                vertexBuffer.Clear();
                triangleBuffer.Clear();

                int3 cellGridSize = scalarFieldInfo.ValueRO.GridSize - new int3(1, 1, 1);
            
                // Créer un mapping entre l'index de cellule et l'index de vertex
                NativeHashMap<int, int> cellToVertexIndex = new NativeHashMap<int, int>(cellBuffer.Length, Allocator.Temp);
            
                // Première passe: créer les vertices pour toutes les cellules qui en ont
                for (int i = 0; i < cellBuffer.Length; i++)
                {
                    DualContouringCell cell = cellBuffer[i];
                    if (cell.HasVertex)
                    {
                        int vertexIndex = vertexBuffer.Length;
                        cellToVertexIndex.Add(i, vertexIndex);
                    
                        vertexBuffer.Add(new DualContouringMeshVertex
                        {
                            Position = cell.VertexPosition,
                            Normal = cell.Normal // Utiliser la normale calculée de la cellule
                        });
                    }
                }
            
                // Deuxième passe: créer les faces entre les cellules adjacentes
                // Dans le dual contouring, on crée une face pour chaque arête de la grille qui traverse la surface
                // On parcourt toutes les arêtes possibles de la grille de cellules
            
                // Faces perpendiculaires à l'axe X (entre cellules le long de X)
                GenerateFacesAlongAxis(cellBuffer, cellToVertexIndex, vertexBuffer, triangleBuffer, 
                    cellGridSize, new int3(1, 0, 0), new int3(0, 1, 0), new int3(0, 0, 1));
            
                // Faces perpendiculaires à l'axe Y (entre cellules le long de Y)
                GenerateFacesAlongAxis(cellBuffer, cellToVertexIndex, vertexBuffer, triangleBuffer, 
                    cellGridSize, new int3(0, 1, 0), new int3(0, 0, 1), new int3(1, 0, 0));
            
                // Faces perpendiculaires à l'axe Z (entre cellules le long de Z)
                GenerateFacesAlongAxis(cellBuffer, cellToVertexIndex, vertexBuffer, triangleBuffer, 
                    cellGridSize, new int3(0, 0, 1), new int3(1, 0, 0), new int3(0, 1, 0));
            
                // Les normales sont déjà définies à partir des cellules, pas besoin de les recalculer
            
                cellToVertexIndex.Dispose();
            }
        }
    
        /// <summary>
        ///     Génère des faces le long d'un axe donné
        /// </summary>
        private void GenerateFacesAlongAxis(
            DynamicBuffer<DualContouringCell> cellBuffer,
            NativeHashMap<int, int> cellToVertexIndex,
            DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
            DynamicBuffer<DualContouringMeshTriangle> triangleBuffer,
            int3 cellGridSize,
            int3 axisDir,      // Direction de l'arête (direction normale à la face)
            int3 tangent1,     // Premier axe tangent à la face
            int3 tangent2)     // Deuxième axe tangent à la face
        {
            // Pour chaque position possible d'arête le long de cet axe
            int3 maxCoord = cellGridSize - axisDir;
        
            for (int i0 = 0; i0 <= maxCoord.x; i0++)
            {
                for (int i1 = 0; i1 <= maxCoord.y; i1++)
                {
                    for (int i2 = 0; i2 <= maxCoord.z; i2++)
                    {
                        int3 baseCoord = new int3(i0, i1, i2);
                    
                        // Les 4 cellules qui partagent cette arête
                        int3 c00 = baseCoord;
                        int3 c10 = baseCoord + tangent1;
                        int3 c01 = baseCoord + tangent2;
                        int3 c11 = baseCoord + tangent1 + tangent2;
                    
                        // Vérifier que toutes les cellules sont dans les limites
                        if (!ScalarFieldUtility.IsInBounds(c00, cellGridSize) ||
                            !ScalarFieldUtility.IsInBounds(c10, cellGridSize) ||
                            !ScalarFieldUtility.IsInBounds(c01, cellGridSize) ||
                            !ScalarFieldUtility.IsInBounds(c11, cellGridSize))
                        {
                            continue;
                        }
                    
                        int idx00 = ScalarFieldUtility.CoordToIndex(c00, cellGridSize);
                        int idx10 = ScalarFieldUtility.CoordToIndex(c10, cellGridSize);
                        int idx01 = ScalarFieldUtility.CoordToIndex(c01, cellGridSize);
                        int idx11 = ScalarFieldUtility.CoordToIndex(c11, cellGridSize);
                    
                        // Vérifier que toutes les cellules ont des vertices
                        if (!cellToVertexIndex.TryGetValue(idx00, out int v00) ||
                            !cellToVertexIndex.TryGetValue(idx10, out int v10) ||
                            !cellToVertexIndex.TryGetValue(idx01, out int v01) ||
                            !cellToVertexIndex.TryGetValue(idx11, out int v11))
                        {
                            continue;
                        }
                    
                        // Créer un quad (2 triangles) entre les 4 vertices
                        // L'ordre des vertices est déterminé par la normale de face (moyenne des normales des vertices)
                    
                        // Triangle 1: v00, v10, v11
                        AddTriangleWithCorrectWinding(triangleBuffer, vertexBuffer, v00, v10, v11);
                    
                        // Triangle 2: v00, v11, v01
                        AddTriangleWithCorrectWinding(triangleBuffer, vertexBuffer, v00, v11, v01);
                    }
                }
            }
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

