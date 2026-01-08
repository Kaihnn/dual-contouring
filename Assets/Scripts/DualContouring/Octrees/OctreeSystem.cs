using DualContouring.ScalarField;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.Octrees
{
    /// <summary>
    ///     Système qui construit un octree à partir d'une grille de voxels
    ///     La grille est un cube de taille puissance de 2
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
            foreach ((DynamicBuffer<ScalarFieldItem> scalarFieldBuffer, DynamicBuffer<OctreeNode> octreeBuffer, RefRO<ScalarFieldGridSize> gridSize, RefRO<ScalarFieldOrigin> origin) in SystemAPI
                         .Query<
                             DynamicBuffer<ScalarFieldItem>,
                             DynamicBuffer<OctreeNode>,
                             RefRO<ScalarFieldGridSize>,
                             RefRO<ScalarFieldOrigin>>())
            {
                octreeBuffer.Clear();

                if (scalarFieldBuffer.Length == 0)
                {
                    continue;
                }

                int3 gridSizeValue = gridSize.ValueRO.Value;
                int maxDepth = CalculateMaxDepth(gridSizeValue);
                int3 rootMin = int3.zero;
                int3 rootMax = gridSize.ValueRO.Value;
                int rootSize = math.max(math.cmax(gridSizeValue), 1);

                octreeBuffer.Add(new OctreeNode
                {
                    Position = rootMin,
                    Value = 0f,
                    ChildIndex = -1
                });

                SubdivideNode(octreeBuffer, scalarFieldBuffer, gridSizeValue, 0, rootMin, rootMax, rootSize, 0, maxDepth);
            }
        }

        private int CalculateMaxDepth(int3 gridSize)
        {
            int maxDimension = math.cmax(gridSize);
            int depth = 0;
            while (maxDimension > 1)
            {
                maxDimension = maxDimension >> 1;
                depth++;
            }

            return depth;
        }

        /// <summary>
        ///     Subdivise récursivement un nœud de l'octree
        /// </summary>
        private void SubdivideNode(
            DynamicBuffer<OctreeNode> octreeBuffer,
            DynamicBuffer<ScalarFieldItem> scalarField,
            int3 gridSize,
            int nodeIndex,
            int3 min,
            int3 max,
            int size,
            int depth,
            int maxDepth)
        {
            if (depth >= maxDepth)
            {
                return;
            }

            if (!HasSignChange(scalarField, gridSize, min, max))
            {
                return;
            }

            int firstChildIndex = octreeBuffer.Length;

            OctreeNode parentNode = octreeBuffer[nodeIndex];
            parentNode.ChildIndex = firstChildIndex;
            octreeBuffer[nodeIndex] = parentNode;

            int i = 0;
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        int childSize = size / 2;
                        int3 childMin = min + new int3(x * childSize, y * childSize, z * childSize);
                        int3 childMax = min + new int3(childSize, childSize, childSize);

                        octreeBuffer.Add(new OctreeNode
                        {
                            Position = childMax,
                            Value = 0f,
                            ChildIndex = -1
                        });

                        // Subdiviser récursivement l'enfant
                        SubdivideNode(octreeBuffer, scalarField, gridSize, firstChildIndex + i, childMin, childMax, childSize, depth + 1, maxDepth);
                        i++;
                    }
                }
            }
        }

        private bool HasSignChange(
            DynamicBuffer<ScalarFieldItem> scalarField,
            int3 gridSize,
            int3 min,
            int3 max)
        {
            bool hasPositive = false;
            bool hasNegative = false;

            for (int y = min.y; y <= max.y; y++)
            {
                for (int z = min.z; z <= max.z; z++)
                {
                    for (int x = min.x; x <= max.x; x++)
                    {
                        int index = ScalarFieldUtility.CoordToIndex(x, y, z, gridSize);
                        if (index < 0 || index >= scalarField.Length)
                        {
                            continue;
                        }

                        float value = scalarField[index].Value;

                        if (value >= 0)
                        {
                            hasPositive = true;
                        }
                        else
                        {
                            hasNegative = true;
                        }

                        // Si on a les deux signes, il y a une surface
                        if (hasPositive && hasNegative)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}