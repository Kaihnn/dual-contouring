using Unity.Entities;

namespace DualContouring.Octrees.Debug
{
    public struct OctreeVisualizationOptions : IComponentData
    {
        public bool Enabled;
    }
}