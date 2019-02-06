using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Stormium.Core
{
    public static unsafe class ComponentDataFromEntityBurstExtensions
    {
        public static class CreateCall<T> where T : struct, IComponentData
        {
            public static CallExistsAsBurst Exists()
            {
                return BurstCompiler.CompileDelegate<CallExistsAsBurst>(InternalExists);
            }

            private static bool InternalExists(void* data, Entity entity)
            {
                UnsafeUtility.CopyPtrToStructure(data, out ComponentDataFromEntity<T> dataFromEntity);

                return dataFromEntity.Exists(entity);
            }
        }

        public static bool CallExists<T>(this ComponentDataFromEntity<T> dataFromEntity, CallExistsAsBurst call, Entity entity)
            where T : struct, IComponentData
        {
            return call(UnsafeUtility.AddressOf(ref dataFromEntity), entity);
        }

        public delegate bool CallExistsAsBurst(void* data, Entity entity);
    }

    public static class BurstComponentFromExtensions
    {
        public static Func<ComponentDataFromEntity<T>, Entity, bool> GetExistsCall<T>()
            where T : struct, IComponentData
        {
            return BurstCompiler.CompileDelegate(new Func<ComponentDataFromEntity<T>, Entity, bool>((f, e) => f.Exists(e)));
        }
    }
}