using Unity.Entities;

namespace StormiumShared.Core.Networking
{
    public struct AdditionalComponent : IBufferElementData
    {
        public int TypeIndex;
        
        public AdditionalComponent(ComponentType componentType)
        {
            TypeIndex = componentType.TypeIndex;
        }

        public AdditionalComponent(int index)
        {
            TypeIndex = index;
        }

        public ComponentType ToComponentType()
        {
            return ComponentType.FromTypeIndex(TypeIndex);
        }
    }
}