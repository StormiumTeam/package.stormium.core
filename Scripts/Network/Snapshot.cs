using System;
using System.Collections.Generic;
using package.stormiumteam.networking.runtime.lowlevel;
using Unity.Entities;

namespace Scripts.Network
{
    public struct NetworkSnapshot
    {
        public int Tick;
        public int NetId;
    }

    public unsafe struct CompiledSnapshot
    {
        public FastDictionary<Type, int> SystemIds;
        public FastDictionary<Entity, Dictionary<int, byte[]>> EntityData;

        public int GetSystemId<TSystem>()
        {
            return SystemIds[typeof(TSystem)];
        }
        
        public DataBufferReader ReadEntityData<TSystem>(Entity entity, TSystem system)
        {
            var dataArray = EntityData[entity][GetSystemId<TSystem>()];
            fixed (byte* buffer = dataArray)
            {
                return new DataBufferReader(buffer, dataArray.Length);
            }
        }

        public DataBufferReader ReadEntityData(Entity entity, int systemId)
        {
            var dataArray = EntityData[entity][systemId];
            fixed (byte* buffer = dataArray)
            {
                return new DataBufferReader(buffer, dataArray.Length);
            }
        }
    }
}