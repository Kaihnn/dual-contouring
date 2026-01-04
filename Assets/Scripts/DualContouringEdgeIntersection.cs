using Unity.Entities;
using Unity.Mathematics;

public struct DualContouringEdgeIntersection : IBufferElementData
{
    public float3 Position;
    public float3 Normal;
    public int CellIndex;
}