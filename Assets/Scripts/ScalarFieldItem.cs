using Unity.Entities;
using Unity.Mathematics;

public struct ScalarFieldItem : IBufferElementData
{
    public float Value;
    public float3 Position;
}