using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

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
            foreach (var (scalarFieldBuffer, cellBuffer, edgeIntersectionBuffer) in SystemAPI.Query<
                         DynamicBuffer<ScalarFieldValue>,
                         DynamicBuffer<DualContouringCell>,
                         DynamicBuffer<DualContouringEdgeIntersection>>())
            {
                cellBuffer.Clear();
                edgeIntersectionBuffer.Clear();

                // Pour dual contouring, on traite les cellules (gridSize - 1)^3
                // Avec une grille 3x3x3, on a 2x2x2 = 8 cellules possibles
                int3 cellGridSize = ScalarFieldUtility.DefaultGridSize - new int3(1, 1, 1);
                
                for (int y = 0; y < cellGridSize.y; y++)
                {
                    for (int z = 0; z < cellGridSize.z; z++)
                    {
                        for (int x = 0; x < cellGridSize.x; x++)
                        {
                            ProcessCell(scalarFieldBuffer, cellBuffer, edgeIntersectionBuffer, new int3(x, y, z));
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
            DynamicBuffer<DualContouringEdgeIntersection> edgeIntersections,
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
                int scalarIndex = ScalarFieldUtility.CoordToIndex(cornerIndex, ScalarFieldUtility.DefaultGridSize);

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
                vertexPosition = CalculateVertexPosition(scalarField, edgeIntersections, cellIndex, cellSize);
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
            DynamicBuffer<DualContouringEdgeIntersection> edgeIntersections,
            int3 cellIndex,
            float cellSize)
        {
            // Version simplifiée: on trouve les arêtes qui intersectent la surface
            // et on fait la moyenne des points d'intersection

            float3 sum = float3.zero;
            int count = 0;
            
            // Calculer l'index de la cellule pour le stocker dans les intersections
            int3 cellGridSize = ScalarFieldUtility.DefaultGridSize - new int3(1, 1, 1);
            int currentCellIndex = ScalarFieldUtility.CoordToIndex(cellIndex, cellGridSize);

            // Parcourir les 12 arêtes de la cellule
            // Arêtes parallèles à X (4)
            for (int y = 0; y < 2; y++)
            {
                for (int z = 0; z < 2; z++)
                {
                    if (TryGetEdgeIntersection(scalarField, cellIndex + new int3(0, y, z),
                            cellIndex + new int3(1, y, z), out float3 intersection, out float3 normal))
                    {
                        sum += intersection;
                        count++;
                        
                        // Ajouter l'intersection au buffer
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
            for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    if (TryGetEdgeIntersection(scalarField, cellIndex + new int3(x, 0, z),
                            cellIndex + new int3(x, 1, z), out float3 intersection, out float3 normal))
                    {
                        sum += intersection;
                        count++;
                        
                        // Ajouter l'intersection au buffer
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
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    if (TryGetEdgeIntersection(scalarField, cellIndex + new int3(x, y, 0),
                            cellIndex + new int3(x, y, 1), out float3 intersection, out float3 normal))
                    {
                        sum += intersection;
                        count++;
                        
                        // Ajouter l'intersection au buffer
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
                return sum / count;
            }

            // Fallback: centre de la cellule
            int scalarIndex = ScalarFieldUtility.CoordToIndex(cellIndex, ScalarFieldUtility.DefaultGridSize);
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
            out float3 intersection,
            out float3 normal)
        {
            intersection = float3.zero;
            normal = float3.zero;

            int idx1 = ScalarFieldUtility.CoordToIndex(corner1Index, ScalarFieldUtility.DefaultGridSize);
            int idx2 = ScalarFieldUtility.CoordToIndex(corner2Index, ScalarFieldUtility.DefaultGridSize);

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
                
                // Calculer la normale en utilisant le gradient (approximation par différence finie)
                normal = CalculateNormal(scalarField, intersection);
                
                return true;
            }

            return false;
        }
        
        /// <summary>
        ///     Calcule la normale au point d'intersection en utilisant le gradient du champ scalaire
        /// </summary>
        private float3 CalculateNormal(DynamicBuffer<ScalarFieldValue> scalarField, float3 position)
        {
            // Pour calculer la normale, on utilise le gradient du champ scalaire
            // La normale est le gradient normalisé
            // On utilise une différence finie centrée pour approximer le gradient
            
            float epsilon = 0.01f;
            
            // Gradient en X
            float valueXPlus = SampleScalarField(scalarField, position + new float3(epsilon, 0, 0));
            float valueXMinus = SampleScalarField(scalarField, position - new float3(epsilon, 0, 0));
            float gradX = (valueXPlus - valueXMinus) / (2.0f * epsilon);
            
            // Gradient en Y
            float valueYPlus = SampleScalarField(scalarField, position + new float3(0, epsilon, 0));
            float valueYMinus = SampleScalarField(scalarField, position - new float3(0, epsilon, 0));
            float gradY = (valueYPlus - valueYMinus) / (2.0f * epsilon);
            
            // Gradient en Z
            float valueZPlus = SampleScalarField(scalarField, position + new float3(0, 0, epsilon));
            float valueZMinus = SampleScalarField(scalarField, position - new float3(0, 0, epsilon));
            float gradZ = (valueZPlus - valueZMinus) / (2.0f * epsilon);
            
            float3 gradient = new float3(gradX, gradY, gradZ);
            
            // Normaliser le gradient pour obtenir la normale
            float length = math.length(gradient);
            if (length > 0.0001f)
            {
                return math.normalize(gradient);
            }
            
            // Si le gradient est trop petit, retourner une normale par défaut
            return new float3(0, 1, 0);
        }
        
        /// <summary>
        ///     Échantillonne le champ scalaire à une position donnée (interpolation trilinéaire)
        /// </summary>
        private float SampleScalarField(DynamicBuffer<ScalarFieldValue> scalarField, float3 position)
        {
            // Trouver le point le plus proche dans le champ scalaire
            // Pour simplifier, on utilise le nearest neighbor
            float minDist = float.MaxValue;
            float value = 0;
            
            for (int i = 0; i < scalarField.Length; i++)
            {
                float dist = math.distance(scalarField[i].Position, position);
                if (dist < minDist)
                {
                    minDist = dist;
                    value = scalarField[i].Value;
                }
            }
            
            return value;
        }
    }

