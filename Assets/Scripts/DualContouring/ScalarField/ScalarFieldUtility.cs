using Unity.Burst;
using Unity.Mathematics;

namespace DualContouring.ScalarField
{
    [BurstCompile]
    public static class ScalarFieldUtility
    {
        public static int CoordToIndex(int x, int y, int z, int3 gridSize)
        {
            if (x < 0 || x >= gridSize.x ||
                y < 0 || y >= gridSize.y ||
                z < 0 || z >= gridSize.z)
            {
                return -1;
            }

            return x + z * gridSize.x + y * gridSize.x * gridSize.z;
        }

        public static int CoordToIndex(int3 coord, int3 gridSize)
        {
            return CoordToIndex(coord.x, coord.y, coord.z, gridSize);
        }

        public static int3 IndexToCoord(int index, int3 gridSize)
        {
            int layerSize = gridSize.x * gridSize.z;
            int y = index / layerSize;
            int remainder = index % layerSize;
            int z = remainder / gridSize.x;
            int x = remainder % gridSize.x;

            return new int3(x, y, z);
        }

        [BurstCompile]
        public static void GetWorldPosition(in int3 coord, in float cellSize, in float3 scalarFieldOffset, out float3 worldPosition)
        {
            worldPosition = scalarFieldOffset + new float3(coord.x * cellSize, coord.y * cellSize, coord.z * cellSize);
        }

        public static bool IsInBounds(int3 coord, int3 gridSize)
        {
            return coord.x >= 0 && coord.x < gridSize.x &&
                   coord.y >= 0 && coord.y < gridSize.y &&
                   coord.z >= 0 && coord.z < gridSize.z;
        }
    }
}