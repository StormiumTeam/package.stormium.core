using Unity.Entities;

namespace Stormium.Core
{
    // candidate for shared package
    public struct RefTo<T> : ISharedComponentData
        where T : class
    {
        public T Value;

        public bool IsNull => Value == null;
        
        public RefTo(T v)
        {
            Value = v;
        }
    }
}