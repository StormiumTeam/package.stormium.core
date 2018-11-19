using Unity.Entities;

namespace package.stormium.core
{
    public struct EntityPhysicLayer : IComponentData
    {
        public int Value;

        public EntityPhysicLayer(int layer)
        {
            Value = layer;
        }
    }
}