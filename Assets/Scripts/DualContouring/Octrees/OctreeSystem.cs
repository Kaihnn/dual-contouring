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
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach ((DynamicBuffer<ScalarFieldItem> scalarFieldBuffer, DynamicBuffer<OctreeNode> octreeBuffer, RefRO<ScalarFieldInfos> scalarFieldInfos,
                         RefRW<OctreeNodeInfos> octreeNodeInfos) in SystemAPI
                         .Query<
                             DynamicBuffer<ScalarFieldItem>,
                             DynamicBuffer<OctreeNode>,
                             RefRO<ScalarFieldInfos>,
                             RefRW<OctreeNodeInfos>>())
            {
                octreeBuffer.Clear();

                if (scalarFieldBuffer.Length == 0)
                {
                    continue;
                }

                int3 gridSize = scalarFieldInfos.ValueRO.GridSize;
                int maxDepth = CalculateMaxDepth(gridSize);

                octreeNodeInfos.ValueRW.OctreeOffset = scalarFieldInfos.ValueRO.ScalarFieldOffset;
                octreeNodeInfos.ValueRW.MaxDepth = maxDepth;
                octreeNodeInfos.ValueRW.MinNodeSize = scalarFieldInfos.ValueRO.CellSize;
                octreeNodeInfos.ValueRW.MaxNodeSize = math.cmax(scalarFieldInfos.ValueRO.GridSize) * scalarFieldInfos.ValueRO.CellSize;

                int3 rootMin = int3.zero;
                int3 rootMax = scalarFieldInfos.ValueRO.GridSize;
                int rootSize = math.max(math.cmax(gridSize), 1);

                octreeBuffer.Add(new OctreeNode
                {
                    Position = rootMin,
                    Value = 0f,
                    ChildIndex = -1
                });

                SubdivideNode(octreeBuffer, scalarFieldBuffer, gridSize, 0, rootMin, rootMax, rootSize, 0, maxDepth);
            }
        }

        private int CalculateMaxDepth(int3 gridSize)
        {
            int maxDimension = math.cmax(gridSize);
            int depth = 0;
            while (maxDimension > 1)
            {
                maxDimension >>= 1;
                depth++;
            }

            return depth;
        }

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
            int firstChildIndex = octreeBuffer.Length;
            OctreeNode parentNode = octreeBuffer[nodeIndex];
            if (HasSignChange(scalarField, gridSize, min, max, out float sampledValue))
            {
                parentNode.ChildIndex = firstChildIndex;

                // Créer d'abord les 8 enfants
                for (int x = 0; x <= 1; x++)
                {
                    for (int y = 0; y <= 1; y++)
                    {
                        for (int z = 0; z <= 1; z++)
                        {
                            int childSize = size / 2;
                            int3 childMin = min + new int3(x * childSize, y * childSize, z * childSize);

                            octreeBuffer.Add(new OctreeNode
                            {
                                Position = childMin,
                                Value = 0f,
                                ChildIndex = -1
                            });
                        }
                    }
                }

                // Ensuite subdiviser récursivement chaque enfant
                int i = 0;
                for (int x = 0; x <= 1; x++)
                {
                    for (int y = 0; y <= 1; y++)
                    {
                        for (int z = 0; z <= 1; z++)
                        {
                            int childSize = size / 2;
                            int3 childMin = min + new int3(x * childSize, y * childSize, z * childSize);
                            int3 childMax = childMin + new int3(childSize, childSize, childSize);

                            SubdivideNode(octreeBuffer, scalarField, gridSize, firstChildIndex + i, childMin, childMax, childSize, depth + 1, maxDepth);
                            i++;
                        }
                    }
                }
            }
            parentNode.Value = sampledValue;
            octreeBuffer[nodeIndex] = parentNode;
        }

        private bool HasSignChange(
            DynamicBuffer<ScalarFieldItem> scalarField,
            int3 gridSize,
            int3 min,
            int3 max,
            out float sampledValue)
        {
            bool hasPositive = false;
            bool hasNegative = false;

            float addedValue = 0f;
            int count = 0;
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

                        addedValue += value;
                        count++;
                        if (value >= 0)
                        {
                            hasPositive = true;
                        }
                        else
                        {
                            hasNegative = true;
                        }
                    }
                }
            }

            sampledValue = count != 0 ? addedValue / count : 0f;
            return hasPositive && hasNegative;
        }
    }
}