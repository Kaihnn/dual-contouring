using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

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
        foreach (var (cellBuffer, vertexBuffer, triangleBuffer) in SystemAPI.Query<
                     DynamicBuffer<DualContouringCell>,
                     DynamicBuffer<DualContouringMeshVertex>,
                     DynamicBuffer<DualContouringMeshTriangle>>())
        {
            vertexBuffer.Clear();
            triangleBuffer.Clear();

            int3 cellGridSize = ScalarFieldUtility.DefaultGridSize - new int3(1, 1, 1);
            
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
                        Normal = float3.zero // Sera calculé plus tard
                    });
                }
            }
            
            // Deuxième passe: créer les faces entre les cellules adjacentes
            // Dans le dual contouring, on crée une face pour chaque arête de la grille qui traverse la surface
            // On parcourt toutes les arêtes possibles de la grille de cellules
            
            // Faces perpendiculaires à l'axe X (entre cellules le long de X) - inversé
            GenerateFacesAlongAxis(cellBuffer, cellToVertexIndex, vertexBuffer, triangleBuffer, 
                cellGridSize, new int3(1, 0, 0), new int3(0, 1, 0), new int3(0, 0, 1), true);
            
            // Faces perpendiculaires à l'axe Y (entre cellules le long de Y)
            GenerateFacesAlongAxis(cellBuffer, cellToVertexIndex, vertexBuffer, triangleBuffer, 
                cellGridSize, new int3(0, 1, 0), new int3(0, 0, 1), new int3(1, 0, 0), false);
            
            // Faces perpendiculaires à l'axe Z (entre cellules le long de Z) - inversé
            GenerateFacesAlongAxis(cellBuffer, cellToVertexIndex, vertexBuffer, triangleBuffer, 
                cellGridSize, new int3(0, 0, 1), new int3(1, 0, 0), new int3(0, 1, 0), true);
            
            // Recalculer les normales
            RecalculateNormals(vertexBuffer, triangleBuffer);
            
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
        int3 tangent2,     // Deuxième axe tangent à la face
        bool invertFaces)  // Inverser l'ordre des faces pour corriger les normales
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
                    // L'ordre des vertices détermine la direction de la normale
                    // On utilise la règle de la main droite
                    
                    if (invertFaces)
                    {
                        // Triangle 1: v00, v11, v10 (inversé)
                        triangleBuffer.Add(new DualContouringMeshTriangle { Index = v00 });
                        triangleBuffer.Add(new DualContouringMeshTriangle { Index = v11 });
                        triangleBuffer.Add(new DualContouringMeshTriangle { Index = v10 });
                        
                        // Triangle 2: v00, v01, v11 (inversé)
                        triangleBuffer.Add(new DualContouringMeshTriangle { Index = v00 });
                        triangleBuffer.Add(new DualContouringMeshTriangle { Index = v01 });
                        triangleBuffer.Add(new DualContouringMeshTriangle { Index = v11 });
                    }
                    else
                    {
                        // Triangle 1: v00, v10, v11
                        triangleBuffer.Add(new DualContouringMeshTriangle { Index = v00 });
                        triangleBuffer.Add(new DualContouringMeshTriangle { Index = v10 });
                        triangleBuffer.Add(new DualContouringMeshTriangle { Index = v11 });
                        
                        // Triangle 2: v00, v11, v01
                        triangleBuffer.Add(new DualContouringMeshTriangle { Index = v00 });
                        triangleBuffer.Add(new DualContouringMeshTriangle { Index = v11 });
                        triangleBuffer.Add(new DualContouringMeshTriangle { Index = v01 });
                    }
                }
            }
        }
    }
    
    /// <summary>
    ///     Recalcule les normales des vertices en moyennant les normales des faces
    /// </summary>
    private void RecalculateNormals(
        DynamicBuffer<DualContouringMeshVertex> vertexBuffer,
        DynamicBuffer<DualContouringMeshTriangle> triangleBuffer)
    {
        if (vertexBuffer.Length == 0 || triangleBuffer.Length < 3)
        {
            return;
        }
        
        // Initialiser toutes les normales à zéro
        NativeArray<float3> normals = new NativeArray<float3>(vertexBuffer.Length, Allocator.Temp);
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = float3.zero;
        }
        
        // Accumuler les normales de chaque face
        for (int i = 0; i < triangleBuffer.Length; i += 3)
        {
            int i0 = triangleBuffer[i + 0].Index;
            int i1 = triangleBuffer[i + 1].Index;
            int i2 = triangleBuffer[i + 2].Index;
            
            if (i0 >= 0 && i0 < vertexBuffer.Length &&
                i1 >= 0 && i1 < vertexBuffer.Length &&
                i2 >= 0 && i2 < vertexBuffer.Length)
            {
                float3 p0 = vertexBuffer[i0].Position;
                float3 p1 = vertexBuffer[i1].Position;
                float3 p2 = vertexBuffer[i2].Position;
                
                float3 edge1 = p1 - p0;
                float3 edge2 = p2 - p0;
                float3 faceNormal = math.normalize(math.cross(edge1, edge2));
                
                // Ajouter la normale de la face à chaque vertex
                normals[i0] += faceNormal;
                normals[i1] += faceNormal;
                normals[i2] += faceNormal;
            }
        }
        
        // Normaliser les normales accumulées
        for (int i = 0; i < vertexBuffer.Length; i++)
        {
            float3 normal = normals[i];
            float length = math.length(normal);
            if (length > 0.0001f)
            {
                normal = math.normalize(normal);
            }
            else
            {
                normal = new float3(0, 1, 0);
            }
            
            DualContouringMeshVertex vertex = vertexBuffer[i];
            vertex.Normal = normal;
            vertexBuffer[i] = vertex;
        }
        
        normals.Dispose();
    }
}

