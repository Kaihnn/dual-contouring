using Unity.Entities;
using Unity.Mathematics;

public struct ScalarFieldValue : IBufferElementData
{
    public float Value;
    public float3 Position;
}