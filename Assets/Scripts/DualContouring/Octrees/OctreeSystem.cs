using DualContouring.ScalarField;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.Octrees
{
    /// <summary>
    /// Système qui construit un octree à partir du ScalarField
    /// </summary>
    [BurstCompile]
    public partial struct OctreeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScalarFieldItem>();
            state.RequireForUpdate<OctreeNode>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (scalarFieldBuffer, octreeBuffer, gridSize) in SystemAPI.Query<
                         DynamicBuffer<ScalarFieldItem>,
                         DynamicBuffer<OctreeNode>,
                         RefRO<ScalarFieldGridSize>>())
            {
                octreeBuffer.Clear();

                if (scalarFieldBuffer.Length == 0)
                    continue;

                // Calculer les limites du champ scalaire
                float3 minBounds = scalarFieldBuffer[0].Position;
                float3 maxBounds = scalarFieldBuffer[0].Position;

                for (int i = 1; i < scalarFieldBuffer.Length; i++)
                {
                    minBounds = math.min(minBounds, scalarFieldBuffer[i].Position);
                    maxBounds = math.max(maxBounds, scalarFieldBuffer[i].Position);
                }

                float3 center = (minBounds + maxBounds) / 2f;
                float size = math.cmax(maxBounds - minBounds);

                // Créer le nœud racine
                int rootIndex = octreeBuffer.Length;
                octreeBuffer.Add(new OctreeNode
                {
                    Position = center,
                    Value = SampleScalarField(scalarFieldBuffer, gridSize.ValueRO.Value, center),
                    ChildIndex = -1 // Pas d'enfants initialement
                });

                // Subdiviser récursivement l'octree
                int maxDepth = 4; // Profondeur maximale de l'octree
                SubdivideNode(octreeBuffer, scalarFieldBuffer, gridSize.ValueRO.Value, rootIndex, center, size, 0, maxDepth);
            }
        }

        /// <summary>
        /// Subdivise récursivement un nœud de l'octree
        /// </summary>
        private void SubdivideNode(
            DynamicBuffer<OctreeNode> octreeBuffer,
            DynamicBuffer<ScalarFieldItem> scalarField,
            int3 gridSize,
            int nodeIndex,
            float3 center,
            float size,
            int depth,
            int maxDepth)
        {
            // Condition d'arrêt: profondeur maximale atteinte
            if (depth >= maxDepth)
                return;

            // Vérifier si le nœud traverse la surface (changement de signe)
            bool shouldSubdivide = ShouldSubdivideNode(scalarField, gridSize, center, size);

            if (!shouldSubdivide)
                return;

            // Créer les 8 enfants
            float childSize = size / 2f;
            float offset = childSize / 2f;
            int firstChildIndex = octreeBuffer.Length;

            // Mettre à jour le ChildIndex du parent
            OctreeNode parentNode = octreeBuffer[nodeIndex];
            parentNode.ChildIndex = firstChildIndex;
            octreeBuffer[nodeIndex] = parentNode;

            // Créer les 8 enfants (ordre: 000, 001, 010, 011, 100, 101, 110, 111)
            for (int i = 0; i < 8; i++)
            {
                float3 childOffset = new float3(
                    (i & 1) == 0 ? -offset : offset,
                    ((i >> 1) & 1) == 0 ? -offset : offset,
                    ((i >> 2) & 1) == 0 ? -offset : offset
                );

                float3 childCenter = center + childOffset;
                float childValue = SampleScalarField(scalarField, gridSize, childCenter);

                int childIndex = octreeBuffer.Length;
                octreeBuffer.Add(new OctreeNode
                {
                    Position = childCenter,
                    Value = childValue,
                    ChildIndex = -1
                });

                // Subdiviser récursivement l'enfant
                SubdivideNode(octreeBuffer, scalarField, gridSize, childIndex, childCenter, childSize, depth + 1, maxDepth);
            }
        }

        /// <summary>
        /// Détermine si un nœud doit être subdivisé (si la surface traverse ce nœud)
        /// </summary>
        private bool ShouldSubdivideNode(
            DynamicBuffer<ScalarFieldItem> scalarField,
            int3 gridSize,
            float3 center,
            float size)
        {
            // Échantillonner aux 8 coins du nœud
            float halfSize = size / 2f;
            bool hasPositive = false;
            bool hasNegative = false;

            for (int i = 0; i < 8; i++)
            {
                float3 offset = new float3(
                    (i & 1) == 0 ? -halfSize : halfSize,
                    ((i >> 1) & 1) == 0 ? -halfSize : halfSize,
                    ((i >> 2) & 1) == 0 ? -halfSize : halfSize
                );

                float3 samplePos = center + offset;
                float value = SampleScalarField(scalarField, gridSize, samplePos);

                if (value >= 0)
                    hasPositive = true;
                else
                    hasNegative = true;

                // Si on a les deux signes, la surface traverse ce nœud
                if (hasPositive && hasNegative)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Échantillonne le champ scalaire à une position donnée (interpolation trilinéaire)
        /// </summary>
        private float SampleScalarField(
            DynamicBuffer<ScalarFieldItem> scalarField,
            int3 gridSize,
            float3 position)
        {
            if (scalarField.Length == 0)
                return 0f;

            // Trouver le point le plus proche dans la grille
            float3 firstPos = scalarField[0].Position;
            float cellSize = 1f;
            
            // Calculer la taille de cellule si possible
            if (scalarField.Length > 1)
            {
                cellSize = math.distance(firstPos, scalarField[1].Position);
            }

            // Convertir la position en coordonnées de grille
            float3 gridPos = (position - firstPos) / cellSize;
            int3 gridIndex = (int3)math.floor(gridPos);

            // Clamper aux limites de la grille
            gridIndex = math.clamp(gridIndex, int3.zero, gridSize - 1);

            // Pour simplification, retourner la valeur du point le plus proche
            // (une interpolation trilinéaire complète pourrait être ajoutée ici)
            int index = ScalarFieldUtility.CoordToIndex(gridIndex, gridSize);
            
            if (index >= 0 && index < scalarField.Length)
            {
                return scalarField[index].Value;
            }

            return 0f;
        }
    }
}

