using DualContouring.ScalarField;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.Octrees
{
    struct NodeToProcess
    {
        public int NodeIndex;
        public int3 Min;
        public int3 Max;
        public int Size;
        public int Depth;
    }

    [BurstCompile]
    partial struct BuildOctreeJob : IJobEntity
    {
        void Execute(
            DynamicBuffer<ScalarFieldItem> scalarFieldBuffer,
            DynamicBuffer<OctreeNode> octreeBuffer,
            in ScalarFieldInfos scalarFieldInfos,
            ref OctreeNodeInfos octreeNodeInfos)
        {
            octreeBuffer.Clear();

            if (scalarFieldBuffer.Length == 0)
            {
                return;
            }

            int3 gridSize = scalarFieldInfos.GridSize;
            int maxDepth = CalculateMaxDepth(in gridSize);

            octreeNodeInfos.OctreeOffset = scalarFieldInfos.ScalarFieldOffset;
            octreeNodeInfos.MaxDepth = maxDepth;
            octreeNodeInfos.MinNodeSize = scalarFieldInfos.CellSize;
            octreeNodeInfos.MaxNodeSize = math.cmax(scalarFieldInfos.GridSize) * scalarFieldInfos.CellSize;

            int3 rootMin = int3.zero;
            int3 rootMax = scalarFieldInfos.GridSize;
            int rootSize = math.max(math.cmax(gridSize), 1);

            octreeBuffer.Add(new OctreeNode
            {
                Position = rootMin,
                Value = 0f,
                ChildIndex = -1
            });

            SubdivideNodeIterative(ref octreeBuffer, in scalarFieldBuffer, in gridSize, 0, in rootMin, in rootMax, rootSize, maxDepth);
        }
        
        [BurstCompile]
        static int CalculateMaxDepth(in int3 gridSize)
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
        
        [BurstCompile]
        static void SubdivideNodeIterative(
            ref DynamicBuffer<OctreeNode> octreeBuffer,
            in DynamicBuffer<ScalarFieldItem> scalarField,
            in int3 gridSize,
            int rootNodeIndex,
            in int3 rootMin,
            in int3 rootMax,
            int rootSize,
            int maxDepth)
        {
            int estimatedCapacity = math.max(64, math.min(2048, maxDepth * maxDepth * 8));
            var nodesToProcess = new NativeList<NodeToProcess>(estimatedCapacity, Allocator.Temp);
            
            nodesToProcess.Add(new NodeToProcess
            {
                NodeIndex = rootNodeIndex,
                Min = rootMin,
                Max = rootMax,
                Size = rootSize,
                Depth = 0
            });
            
            while (nodesToProcess.Length > 0)
            {
                int lastIndex = nodesToProcess.Length - 1;
                NodeToProcess current = nodesToProcess[lastIndex];
                nodesToProcess.RemoveAtSwapBack(lastIndex);
                
                if (current.Depth >= maxDepth)
                {
                    AnalyzeNodeValues(in scalarField, in gridSize, in current.Min, in current.Max, out float leafValue, out _, out _);
                    ref OctreeNode leafNode = ref octreeBuffer.ElementAt(current.NodeIndex);
                    
                    int cornerIndex = ScalarFieldUtility.CoordToIndex(current.Min, gridSize);
                    if (cornerIndex >= 0 && cornerIndex < scalarField.Length)
                    {
                        leafNode.Value = scalarField[cornerIndex].Value;
                    }
                    else
                    {
                        leafNode.Value = leafValue;
                    }
                    continue;
                }
                
                AnalyzeNodeValues(in scalarField, in gridSize, in current.Min, in current.Max, out float sampledValue, out bool hasSignChange, out bool hasVariation);
                
                if (!hasSignChange && !hasVariation)
                {
                    ref OctreeNode node = ref octreeBuffer.ElementAt(current.NodeIndex);
                    node.Value = sampledValue;
                    continue;
                }
                
                int firstChildIndex = octreeBuffer.Length;
                ref OctreeNode parentNode = ref octreeBuffer.ElementAt(current.NodeIndex);
                parentNode.ChildIndex = firstChildIndex;
                parentNode.Value = sampledValue;
                
                int childSize = current.Size / 2;
                int3 childSizeVec = new int3(childSize);
                
                for (int childIdx = 0; childIdx < 8; childIdx++)
                {
                    int3 offset = new int3(
                        (childIdx >> 0) & 1,
                        (childIdx >> 1) & 1,
                        (childIdx >> 2) & 1
                    );
                    
                    int3 childMin = current.Min + offset * childSize;
                    int3 childMax = childMin + childSizeVec;
                    
                    int childIndex = octreeBuffer.Length;
                    
                    octreeBuffer.Add(new OctreeNode
                    {
                        Position = childMin,
                        Value = 0f,
                        ChildIndex = -1
                    });
                    
                    nodesToProcess.Add(new NodeToProcess
                    {
                        NodeIndex = childIndex,
                        Min = childMin,
                        Max = childMax,
                        Size = childSize,
                        Depth = current.Depth + 1
                    });
                }
            }
            
            nodesToProcess.Dispose();
        }
        
        [BurstCompile]
        static void AnalyzeNodeValues(
            in DynamicBuffer<ScalarFieldItem> scalarField,
            in int3 gridSize,
            in int3 min,
            in int3 max,
            out float averageValue,
            out bool hasSignChange,
            out bool hasVariation)
        {
            hasSignChange = false;
            hasVariation = false;
            averageValue = 0f;

            float addedValue = 0f;
            float minValue = float.MaxValue;
            float maxValue = float.MinValue;
            bool hasPositive = false;
            bool hasNegative = false;
            int count = 0;

            for (int y = min.y; y < max.y; y++)
            {
                for (int z = min.z; z < max.z; z++)
                {
                    for (int x = min.x; x < max.x; x++)
                    {
                        int index = ScalarFieldUtility.CoordToIndex(x, y, z, gridSize);
                        if (index < 0 || index >= scalarField.Length)
                        {
                            continue;
                        }

                        float value = scalarField[index].Value;

                        addedValue += value;
                        count++;

                        minValue = math.min(minValue, value);
                        maxValue = math.max(maxValue, value);

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

            if (count > 0)
            {
                averageValue = addedValue / count;
                hasSignChange = hasPositive && hasNegative;
                
                float valueRange = maxValue - minValue;
                float varianceThreshold = 0.01f;
                hasVariation = valueRange > varianceThreshold;
            }
        }
    }

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

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new BuildOctreeJob();
            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }
}