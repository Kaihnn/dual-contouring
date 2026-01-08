using DualContouring.ScalarField;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.Octrees
{
    /// <summary>
    /// Système qui construit un octree à partir d'une grille de voxels
    /// La grille est un cube de taille puissance de 2
    /// </summary>
    public partial struct OctreeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScalarFieldItem>();
            state.RequireForUpdate<OctreeNode>();
            state.RequireForUpdate<ScalarFieldOrigin>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (scalarFieldBuffer, octreeBuffer, gridSize, origin) in SystemAPI.Query<
                         DynamicBuffer<ScalarFieldItem>,
                         DynamicBuffer<OctreeNode>,
                         RefRO<ScalarFieldGridSize>,
                         RefRO<ScalarFieldOrigin>>())
            {
                octreeBuffer.Clear();

                if (scalarFieldBuffer.Length == 0)
                    continue;

                int3 grid = gridSize.ValueRO.Value;
                
                // Le champ scalaire est un cube de voxels (grid.x == grid.y == grid.z)
                // gridSize est le nombre de voxels (et aussi le nombre de points)
                int voxelDim = grid.x;
                
                // Vérifier que c'est une puissance de 2
                if (!IsPowerOfTwo(voxelDim))
                {
                    UnityEngine.Debug.LogWarning($"La dimension des voxels ({voxelDim}) n'est pas une puissance de 2!");
                    return;
                }

                // Créer le nœud racine (couvre tous les voxels)
                // Position au centre du volume de voxels
                int3 rootMin = int3.zero;
                int rootSize = voxelDim;
                
                octreeBuffer.Add(new OctreeNode
                {
                    Position = VoxelCenterToWorld(rootMin, rootSize, origin.ValueRO),
                    Value = 0f, // La valeur sera calculée lors de la subdivision
                    ChildIndex = -1
                });

                // Subdiviser récursivement l'octree
                int maxDepth = CalculateMaxDepth(voxelDim);
                SubdivideNode(octreeBuffer, scalarFieldBuffer, grid, origin.ValueRO, 0, rootMin, rootSize, 0, maxDepth);
            }
        }

        /// <summary>
        /// Vérifie si un nombre est une puissance de 2
        /// </summary>
        private bool IsPowerOfTwo(int n)
        {
            return n > 0 && (n & (n - 1)) == 0;
        }

        /// <summary>
        /// Calcule la profondeur maximale de l'octree basée sur la dimension des voxels
        /// </summary>
        private int CalculateMaxDepth(int voxelDim)
        {
            int depth = 0;
            int size = voxelDim;
            while (size > 1)
            {
                size /= 2;
                depth++;
            }
            return depth;
        }

        /// <summary>
        /// Convertit la position d'un voxel (coin min + taille) en position monde (centre)
        /// </summary>
        private float3 VoxelCenterToWorld(int3 voxelMin, int voxelSize, ScalarFieldOrigin origin)
        {
            // Le centre du voxel en coordonnées de grille
            float3 voxelCenter = new float3(voxelMin) + voxelSize * 0.5f;
            return origin.Origin + voxelCenter * origin.CellSize;
        }

        /// <summary>
        /// Subdivise récursivement un nœud de l'octree
        /// </summary>
        private void SubdivideNode(
            DynamicBuffer<OctreeNode> octreeBuffer,
            DynamicBuffer<ScalarFieldItem> scalarField,
            int3 gridSize,
            ScalarFieldOrigin origin,
            int nodeIndex,
            int3 voxelMin,
            int voxelSize,
            int depth,
            int maxDepth)
        {
            // Condition d'arrêt: profondeur maximale atteinte ou taille de voxel unitaire
            if (depth >= maxDepth || voxelSize <= 1)
                return;

            // Vérifier si le nœud traverse la surface (changement de signe)
            if (!HasSignChange(scalarField, gridSize, voxelMin, voxelSize))
                return;

            // Subdiviser en 8 enfants
            int childSize = voxelSize / 2;
            int firstChildIndex = octreeBuffer.Length;

            // Mettre à jour le ChildIndex du parent
            OctreeNode parentNode = octreeBuffer[nodeIndex];
            parentNode.ChildIndex = firstChildIndex;
            octreeBuffer[nodeIndex] = parentNode;

            // Créer les 8 octants enfants
            // Ordre: x varie le plus vite, puis z, puis y
            for (int i = 0; i < 8; i++)
            {
                int3 childOffset = new int3(
                    (i & 1) * childSize,      // bit 0 -> X
                    ((i >> 2) & 1) * childSize, // bit 2 -> Y
                    ((i >> 1) & 1) * childSize  // bit 1 -> Z
                );

                int3 childMin = voxelMin + childOffset;
                float3 childWorldCenter = VoxelCenterToWorld(childMin, childSize, origin);
                
                // Calculer la valeur au centre du voxel enfant
                float childValue = SampleVoxelCenter(scalarField, gridSize, childMin, childSize);

                octreeBuffer.Add(new OctreeNode
                {
                    Position = childWorldCenter,
                    Value = childValue,
                    ChildIndex = -1
                });

                // Subdiviser récursivement
                SubdivideNode(octreeBuffer, scalarField, gridSize, origin, firstChildIndex + i, childMin, childSize, depth + 1, maxDepth);
            }
        }

        /// <summary>
        /// Détermine si un voxel contient un changement de signe
        /// Un voxel est défini par son coin min et sa taille
        /// Parcourt TOUS les points contenus dans le voxel
        /// </summary>
        private bool HasSignChange(
            DynamicBuffer<ScalarFieldItem> scalarField,
            int3 gridSize,
            int3 voxelMin,
            int voxelSize)
        {
            bool hasPositive = false;
            bool hasNegative = false;

            // Calculer les limites du voxel
            int3 voxelMax = voxelMin + voxelSize;
            
            // Clamper aux limites de la grille
            int3 clampedMin = math.max(voxelMin, int3.zero);
            int3 clampedMax = math.min(voxelMax, gridSize - 1);

            // Parcourir TOUS les points contenus dans ce voxel
            for (int y = clampedMin.y; y <= clampedMax.y; y++)
            {
                for (int z = clampedMin.z; z <= clampedMax.z; z++)
                {
                    for (int x = clampedMin.x; x <= clampedMax.x; x++)
                    {
                        int index = ScalarFieldUtility.CoordToIndex(x, y, z, gridSize);
                        if (index < 0 || index >= scalarField.Length)
                            continue;

                        float value = scalarField[index].Value;

                        if (value >= 0)
                            hasPositive = true;
                        else
                            hasNegative = true;

                        // Si on a les deux signes, il y a une surface
                        if (hasPositive && hasNegative)
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Échantillonne la valeur au centre d'un voxel
        /// Pour simplifier, on prend la moyenne des 8 coins
        /// </summary>
        private float SampleVoxelCenter(
            DynamicBuffer<ScalarFieldItem> scalarField,
            int3 gridSize,
            int3 voxelMin,
            int voxelSize)
        {
            if (scalarField.Length == 0)
                return 0f;

            float sum = 0f;
            int count = 0;

            // Calculer la moyenne des 8 coins
            for (int i = 0; i < 8; i++)
            {
                int3 corner = voxelMin + new int3(
                    (i & 1) * voxelSize,
                    ((i >> 2) & 1) * voxelSize,
                    ((i >> 1) & 1) * voxelSize
                );

                if (math.any(corner < int3.zero) || math.any(corner >= gridSize))
                    continue;

                int index = ScalarFieldUtility.CoordToIndex(corner, gridSize);
                if (index >= 0 && index < scalarField.Length)
                {
                    sum += scalarField[index].Value;
                    count++;
                }
            }

            return count > 0 ? sum / count : 0f;
        }
    }
}

