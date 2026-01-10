using Unity.Burst;
using Unity.Mathematics;

namespace DualContouring.ScalarField
{
    [BurstCompile]
    public static class ScalarFieldUtility
    {
        [BurstCompile]
        public static int CoordToIndex(int x, int y, int z, in int3 gridSize)
        {
            if (x < 0 || x >= gridSize.x ||
                y < 0 || y >= gridSize.y ||
                z < 0 || z >= gridSize.z)
            {
                return -1;
            }

            return x + z * gridSize.x + y * gridSize.x * gridSize.z;
        }

        [BurstCompile]
        public static int CoordToIndex(in int3 coord, in int3 gridSize)
        {
            return CoordToIndex(coord.x, coord.y, coord.z, gridSize);
        }

        [BurstCompile]
        public static void IndexToCoord(int index, in int3 gridSize, out float3 coordinates)
        {
            int layerSize = gridSize.x * gridSize.z;
            int y = index / layerSize;
            int remainder = index % layerSize;
            int z = remainder / gridSize.x;
            int x = remainder % gridSize.x;

            coordinates = new int3(x, y, z);
        }

        [BurstCompile]
        public static void GetWorldPosition(in int3 coord, in float cellSize, in float3 scalarFieldOffset, out float3 worldPosition)
        {
            worldPosition = scalarFieldOffset + new float3(coord.x * cellSize, coord.y * cellSize, coord.z * cellSize);
        }
        
        [BurstCompile]
        public static bool IsInBounds(in int3 coord, in int3 gridSize)
        {
            return coord.x >= 0 && coord.x < gridSize.x &&
                   coord.y >= 0 && coord.y < gridSize.y &&
                   coord.z >= 0 && coord.z < gridSize.z;
        }
    }
}