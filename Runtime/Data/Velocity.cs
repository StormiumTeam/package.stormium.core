using Unity.Entities;
using Unity.Mathematics;

namespace Stormium.Core
{
    public struct Velocity : IComponentData
    {
        public float3 Value;

        public Velocity(float3 value)
        {
            Value = value;
        }
    }
}