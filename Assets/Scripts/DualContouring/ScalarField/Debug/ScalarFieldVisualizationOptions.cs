using Unity.Entities;

namespace DualContouring.ScalarField.Debug
{
    public struct ScalarFieldVisualizationOptions : IComponentData
    {
        public bool Enabled;
    }
}