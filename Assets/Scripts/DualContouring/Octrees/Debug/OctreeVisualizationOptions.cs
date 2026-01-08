using Unity.Entities;
using Unity.Mathematics;

namespace DualContouring.Octrees.Debug
{
    public struct OctreeVisualizationOptions : IComponentData
    {
        public bool Enabled;
        public int2 Depth;
    }
}