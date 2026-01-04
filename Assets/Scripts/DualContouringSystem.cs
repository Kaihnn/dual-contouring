using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DualContouring;
    /// <summary>
    ///     Système qui remplit le buffer DualContouringCell à partir du ScalarField
    /// </summary>
    [BurstCompile]
    public partial struct DualContouringSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScalarFieldValue>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (scalarFieldBuffer, cellBuffer) in SystemAPI.Query<
                         DynamicBuffer<ScalarFieldValue>,
                         DynamicBuffer<DualContouringCell>>())
            {
                cellBuffer.Clear();

                // Pour dual contouring, on traite les cellules 2x2x2
                // Avec une grille 3x3x3, on a 2x2x2 = 8 cellules possibles
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        for (int x = 0; x < 2; x++)
                        {
                            ProcessCell(scalarFieldBuffer, cellBuffer, new int3(x, y, z));
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Traite une cellule du dual contouring
        /// </summary>
        private void ProcessCell(
            DynamicBuffer<ScalarFieldValue> scalarField,
            DynamicBuffer<DualContouringCell> cells,
            int3 cellIndex)
        {
            // Récupérer les 8 coins de la cellule
            float3 corner0Pos = float3.zero;
            float cellSize = 0;
            bool firstCorner = true;

            // Calculer l'index de configuration (8 bits, un par coin)
            int config = 0;

            // Les 8 coins d'une cellule en ordre: (0,0,0), (1,0,0), (0,1,0), (1,1,0), (0,0,1), (1,0,1), (0,1,1), (1,1,1)
            for (int i = 0; i < 8; i++)
            {
                int3 offset = new int3(
                    i & 1,           // bit 0
                    (i >> 1) & 1,    // bit 1
                    (i >> 2) & 1     // bit 2
                );

                int3 cornerIndex = cellIndex + offset;
                int scalarIndex = GetScalarFieldIndex(cornerIndex, new int3(3, 3, 3));

                if (scalarIndex >= 0 && scalarIndex < scalarField.Length)
                {
                    ScalarFieldValue value = scalarField[scalarIndex];

                    if (firstCorner)
                    {
                        corner0Pos = value.Position;
                        firstCorner = false;
                    }
                    else if (cellSize == 0)
                    {
                        // Calculer la taille de cellule à partir de la distance entre coins
                        cellSize = math.distance(corner0Pos, value.Position);
                    }

                    // Si la valeur est positive (à l'intérieur de la surface), mettre le bit à 1
                    if (value.Value >= 0)
                    {
                        config |= 1 << i;
                    }
                }
            }

            // Si la configuration n'est ni 0 (tout dehors) ni 255 (tout dedans), il y a une surface
            bool hasVertex = config != 0 && config != 255;

            // Calculer la position du vertex (pour l'instant, on utilise le centre de la cellule)
            // Dans un vrai dual contouring, on utiliserait QEF (Quadratic Error Function)
            float3 cellPosition = corner0Pos;
            float3 vertexPosition = cellPosition + new float3(0.5f, 0.5f, 0.5f) * cellSize;

            // Si on a une intersection, calculer une meilleure position du vertex
            if (hasVertex)
            {
                vertexPosition = CalculateVertexPosition(scalarField, cellIndex, cellSize);
            }

            // Ajouter la cellule au buffer
            cells.Add(new DualContouringCell
            {
                Position = cellPosition,
                Size = cellSize,
                HasVertex = hasVertex,
                VertexPosition = vertexPosition
            });
        }

        /// <summary>
        ///     Calcule la position du vertex dans une cellule (version simplifiée)
        /// </summary>
        private float3 CalculateVertexPosition(
            DynamicBuffer<ScalarFieldValue> scalarField,
            int3 cellIndex,
            float cellSize)
        {
            // Version simplifiée: on trouve les arêtes qui intersectent la surface
            // et on fait la moyenne des points d'intersection

            float3 sum = float3.zero;
            int count = 0;

            // Parcourir les 12 arêtes de la cellule
            // Arêtes parallèles à X (4)
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    if (TryGetEdgeIntersection(scalarField, cellIndex + new int3(0, y, z),
                            cellIndex + new int3(1, y, z), out float3 intersection))
                    {
                        sum += intersection;
                        count++;
                    }
                }
            }

            // Arêtes parallèles à Y (4)
            for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    if (TryGetEdgeIntersection(scalarField, cellIndex + new int3(x, 0, z),
                            cellIndex + new int3(x, 1, z), out float3 intersection))
                    {
                        sum += intersection;
                        count++;
                    }
                }
            }

            // Arêtes parallèles à Z (4)
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    if (TryGetEdgeIntersection(scalarField, cellIndex + new int3(x, y, 0),
                            cellIndex + new int3(x, y, 1), out float3 intersection))
                    {
                        sum += intersection;
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                return sum / count;
            }

            // Fallback: centre de la cellule
            int scalarIndex = GetScalarFieldIndex(cellIndex, new int3(3, 3, 3));
            if (scalarIndex >= 0 && scalarIndex < scalarField.Length)
            {
                return scalarField[scalarIndex].Position + new float3(0.5f, 0.5f, 0.5f) * cellSize;
            }

            return float3.zero;
        }

        /// <summary>
        ///     Essaie de trouver l'intersection entre une arête et la surface (iso-surface à 0)
        /// </summary>
        private bool TryGetEdgeIntersection(
            DynamicBuffer<ScalarFieldValue> scalarField,
            int3 corner1Index,
            int3 corner2Index,
            out float3 intersection)
        {
            intersection = float3.zero;

            int idx1 = GetScalarFieldIndex(corner1Index, new int3(3, 3, 3));
            int idx2 = GetScalarFieldIndex(corner2Index, new int3(3, 3, 3));

            if (idx1 < 0 || idx1 >= scalarField.Length || idx2 < 0 || idx2 >= scalarField.Length)
            {
                return false;
            }

            ScalarFieldValue v1 = scalarField[idx1];
            ScalarFieldValue v2 = scalarField[idx2];

            // Vérifier si l'arête traverse la surface (changement de signe)
            if ((v1.Value < 0 && v2.Value >= 0) || (v1.Value >= 0 && v2.Value < 0))
            {
                // Interpolation linéaire pour trouver le point d'intersection
                float t = math.abs(v1.Value) / (math.abs(v1.Value) + math.abs(v2.Value));
                intersection = math.lerp(v1.Position, v2.Position, t);
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Convertit un index 3D en index 1D dans le buffer
        /// </summary>
        private int GetScalarFieldIndex(int3 index, int3 gridSize)
        {
            if (index.x < 0 || index.x >= gridSize.x ||
                index.y < 0 || index.y >= gridSize.y ||
                index.z < 0 || index.z >= gridSize.z)
            {
                return -1;
            }

            return index.x + index.y * gridSize.x * gridSize.z + index.z * gridSize.x;
        }
    }

