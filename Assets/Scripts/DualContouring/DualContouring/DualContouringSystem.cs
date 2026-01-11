using System.Runtime.CompilerServices;
using DualContouring.DualContouring.Debug;
using DualContouring.ScalarField;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.DualContouring
{
    [BurstCompile]
    public partial struct DualContouringSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScalarFieldItem>();
            state.RequireForUpdate<DualContouringOptions>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out DualContouringOptions options) ||
                options.Type != DualContouringType.ScalarField)
            {
                return;
            }

            var job = new DualContouringJob();
            job.ScheduleParallel();
        }
    }

    [BurstCompile]
    internal partial struct DualContouringJob : IJobEntity
    {
        private void Execute(
            ref DynamicBuffer<DualContouringCell> cellBuffer,
            ref DynamicBuffer<DualContouringEdgeIntersection> edgeIntersectionBuffer,
            in DynamicBuffer<ScalarFieldItem> scalarFieldBuffer,
            in ScalarFieldInfos scalarFieldInfos)
        {
            cellBuffer.Clear();
            edgeIntersectionBuffer.Clear();

            int3 cellGridSize = scalarFieldInfos.GridSize - new int3(1, 1, 1);

            for (int y = 0; y < cellGridSize.y; y++)
            {
                for (int z = 0; z < cellGridSize.z; z++)
                {
                    for (int x = 0; x < cellGridSize.x; x++)
                    {
                        ProcessCell(scalarFieldBuffer, cellBuffer, edgeIntersectionBuffer, new int3(x, y, z), scalarFieldInfos);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ProcessCell(
            in DynamicBuffer<ScalarFieldItem> scalarField,
            DynamicBuffer<DualContouringCell> cells,
            DynamicBuffer<DualContouringEdgeIntersection> edgeIntersections,
            int3 cellIndex,
            ScalarFieldInfos scalarFieldInfos)
        {
            int3 gridSize = scalarFieldInfos.GridSize;
            float cellSize = scalarFieldInfos.CellSize;
            float3 scalarFieldOffset = scalarFieldInfos.ScalarFieldOffset;

            // Calculer l'index de configuration (8 bits, un par coin)
            int config = 0;

            // Les 8 coins d'une cellule en ordre: (0,0,0), (1,0,0), (0,1,0), (1,1,0), (0,0,1), (1,0,1), (0,1,1), (1,1,1)
            for (int i = 0; i < 8; i++)
            {
                var offset = new int3(
                    i & 1, // bit 0
                    (i >> 1) & 1, // bit 1
                    (i >> 2) & 1 // bit 2
                );

                int3 cornerIndex = cellIndex + offset;
                int scalarIndex = ScalarFieldUtility.CoordToIndex(cornerIndex, gridSize);

                if (scalarIndex >= 0 && scalarIndex < scalarField.Length)
                {
                    ScalarFieldItem value = scalarField[scalarIndex];

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
            ScalarFieldUtility.GetWorldPosition(cellIndex, cellSize, scalarFieldOffset, out float3 cellPosition);
            float3 vertexPosition = cellPosition + new float3(0.5f, 0.5f, 0.5f) * cellSize;
            var cellNormal = new float3(0, 1, 0);

            // Si on a une intersection, calculer une meilleure position du vertex et la normale
            if (hasVertex)
            {
                CalculateVertexPositionAndNormal(scalarField,
                    edgeIntersections,
                    cellIndex,
                    scalarFieldInfos,
                    out vertexPosition,
                    out cellNormal);
            }

            // Ajouter la cellule au buffer
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
            // Valeurs par défaut
            vertexPosition = float3.zero;
            cellNormal = new float3(0, 1, 0);

            // Collecter toutes les intersections et normales
            int3 cellGridSize = gridSize - new int3(1, 1, 1);
            int currentCellIndex = ScalarFieldUtility.CoordToIndex(cellIndex, cellGridSize);

            // Utiliser NativeArray alloué sur la stack pour éviter les allocations managées dans Burst
            // Max 12 arêtes par cellule
            NativeArray<float3> positions = new NativeArray<float3>(12, Allocator.Temp);
            NativeArray<float3> normals = new NativeArray<float3>(12, Allocator.Temp);
            int count = 0;
            float3 massPoint = float3.zero; // Centre de masse des intersections
            float3 normalSum = float3.zero; // Somme des normales

            // Parcourir les 12 arêtes de la cellule
            // Arêtes parallèles à X (4)
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

            // Arêtes parallèles à Y (4)
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

            // Arêtes parallèles à Z (4)
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

                // Calculer la normale de la cellule : somme des normales, normalisée et inversée
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

                // Contraindre le vertex à l'intérieur de la cellule avec une petite marge
                vertexPos = math.clamp(vertexPos, cellMin, cellMax);

                vertexPosition = vertexPos;

                // Nettoyer les NativeArray
                positions.Dispose();
                normals.Dispose();

                return;
            }

            // Nettoyer les NativeArray même si count == 0
            positions.Dispose();
            normals.Dispose();

            // Fallback: centre de la cellule
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

            // Vérifier si l'arête traverse la surface (changement de signe)
            if ((v1.Value < 0 && v2.Value >= 0) || (v1.Value >= 0 && v2.Value < 0))
            {
                // Interpolation linéaire pour trouver le point d'intersection
                float t = math.abs(v1.Value) / (math.abs(v1.Value) + math.abs(v2.Value));
                ScalarFieldUtility.GetWorldPosition(corner1Index, cellSize, scalarFieldOffset, out float3 pos1);
                ScalarFieldUtility.GetWorldPosition(corner2Index, cellSize, scalarFieldOffset, out float3 pos2);
                intersection = math.lerp(pos1, pos2, t);

                // Calculer la normale en utilisant le gradient (approximation par différence finie)
                normal = CalculateNormal(scalarField, intersection, scalarFieldInfos);

                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 CalculateNormal(in DynamicBuffer<ScalarFieldItem> scalarField, float3 position, ScalarFieldInfos scalarFieldInfos)
        {
            float cellSize = scalarFieldInfos.CellSize;

            // Utiliser un epsilon proportionnel à la taille de cellule
            float epsilon = cellSize * 0.1f;

            // Gradient en X
            float valueXPlus = SampleScalarField(scalarField, position + new float3(epsilon, 0, 0), scalarFieldInfos);
            float valueXMinus = SampleScalarField(scalarField, position - new float3(epsilon, 0, 0), scalarFieldInfos);
            float gradX = (valueXPlus - valueXMinus) / (2.0f * epsilon);

            // Gradient en Y
            float valueYPlus = SampleScalarField(scalarField, position + new float3(0, epsilon, 0), scalarFieldInfos);
            float valueYMinus = SampleScalarField(scalarField, position - new float3(0, epsilon, 0), scalarFieldInfos);
            float gradY = (valueYPlus - valueYMinus) / (2.0f * epsilon);

            // Gradient en Z
            float valueZPlus = SampleScalarField(scalarField, position + new float3(0, 0, epsilon), scalarFieldInfos);
            float valueZMinus = SampleScalarField(scalarField, position - new float3(0, 0, epsilon), scalarFieldInfos);
            float gradZ = (valueZPlus - valueZMinus) / (2.0f * epsilon);

            var gradient = new float3(gradX, gradY, gradZ);

            // Normaliser le gradient pour obtenir la normale
            float length = math.length(gradient);
            if (length > 0.0001f)
            {
                return math.normalize(gradient);
            }

            // Si le gradient est trop petit, retourner une normale par défaut
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

            // Convertir la position en coordonnées de grille (flottantes)
            float3 gridPos = (position - scalarFieldOffset) / cellSize;

            // Trouver la cellule de base (coin inférieur)
            var baseCell = new int3(
                (int)math.floor(gridPos.x),
                (int)math.floor(gridPos.y),
                (int)math.floor(gridPos.z)
            );

            // Coordonnées locales dans la cellule (0-1)
            float3 t = gridPos - new float3(baseCell);

            // Clamp pour éviter les dépassements
            baseCell = math.clamp(baseCell, int3.zero, gridSize - new int3(2, 2, 2));
            t = math.clamp(t, 0.0f, 1.0f);

            // Interpolation trilinéaire
            // Récupérer les 8 valeurs aux coins de la cellule
            float v000 = GetScalarValueAtCoord(scalarField, baseCell + new int3(0, 0, 0), gridSize);
            float v100 = GetScalarValueAtCoord(scalarField, baseCell + new int3(1, 0, 0), gridSize);
            float v010 = GetScalarValueAtCoord(scalarField, baseCell + new int3(0, 1, 0), gridSize);
            float v110 = GetScalarValueAtCoord(scalarField, baseCell + new int3(1, 1, 0), gridSize);
            float v001 = GetScalarValueAtCoord(scalarField, baseCell + new int3(0, 0, 1), gridSize);
            float v101 = GetScalarValueAtCoord(scalarField, baseCell + new int3(1, 0, 1), gridSize);
            float v011 = GetScalarValueAtCoord(scalarField, baseCell + new int3(0, 1, 1), gridSize);
            float v111 = GetScalarValueAtCoord(scalarField, baseCell + new int3(1, 1, 1), gridSize);

            // Interpolation selon X
            float v00 = math.lerp(v000, v100, t.x);
            float v01 = math.lerp(v001, v101, t.x);
            float v10 = math.lerp(v010, v110, t.x);
            float v11 = math.lerp(v011, v111, t.x);

            // Interpolation selon Y
            float v0 = math.lerp(v00, v10, t.y);
            float v1 = math.lerp(v01, v11, t.y);

            // Interpolation selon Z
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