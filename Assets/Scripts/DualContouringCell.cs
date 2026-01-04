using Unity.Entities;
using Unity.Mathematics;

public struct DualContouringCell : IBufferElementData
{
    public float3 Position;
    public float Size;
    public bool HasVertex;
    public float3 VertexPosition;
}