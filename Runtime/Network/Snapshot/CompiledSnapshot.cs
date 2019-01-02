using System;
using System.Collections.Generic;
using package.stormiumteam;
using package.stormiumteam.networking;
using package.stormiumteam.networking.runtime.lowlevel;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Stormium.Core.Networking
{
    public struct NetworkSnapshot
    {
        public int Tick;
        public int NetId;
    }

    public struct RemoveNonTrackedSystemsJob : IJob
    {
        private static class Static<T>
            where T : new()
        {
            public static T Value = new T();
        }
        
        public NativeHandle<FastDictionary<string, int>> SystemIdsHandle;
        public NativeHandle<FastDictionary<int, byte[]>> SystemDataHandle;
        public NativeHandle<FastDictionary<Entity, FastDictionary<int, byte[]>>> EntityDataHandle;
        public NativeList<int> TrackedSystems;

        public bool Contains(int id)
        {
            for (int i = 0; i != TrackedSystems.Length; i++)
            {
                if (TrackedSystems[i] == id) return true;
            }

            return false;
        }
        
        public void Execute()
        {
            var systemIds = SystemIdsHandle.Object;
            var systemData = SystemDataHandle.Object;
            var entityData = EntityDataHandle.Object;

            var systemIdsToDelete = Static<List<string>>.Value;
            systemIdsToDelete.Clear();
            foreach (var systemId in systemIds)
            {
                if (!Contains(systemId.Value))
                {
                    systemIdsToDelete.Add(systemId.Key);
                }
            }

            foreach (var toDelete in systemIdsToDelete)
                systemIds.Remove(toDelete);
        }
    }
    
    public unsafe struct CompiledSnapshot
    {
        public FastDictionary<string, int> SystemIds;
        public FastDictionary<int, byte[]> SystemData;
        public FastDictionary<Entity, FastDictionary<int, byte[]>> EntityData;

        public NativeList<int> TrackedSystems;
        public NativeList<Entity> TrackedEntities;
        
        public void SetSystemPattern(PatternResult patternResult)
        {
            SystemIds[patternResult.InternalIdent.Name] = patternResult.Id;
        }

        public void TrackSystem(int systemId)
        {
            TrackedSystems.Add(systemId);
        }

        public void TrackEntity(Entity entity)
        {
            TrackedEntities.Add(entity);
        }

        public void EmptyTracked()
        {
            TrackedSystems.Clear();
            TrackedEntities.Clear();
        }

        public void DeleteNonTracked()
        {
            for (int i = 0; i != TrackedEntities.Length; i++)
            {
                var entity = TrackedEntities[i];
                if (EntityData.ContainsKey(entity)) 
                    continue;

                var dico = EntityData[entity];
                dico.Clear();

                EntityData.Remove(entity);
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

        public void WriteEntityData(PatternResult pattern, Entity entity, DataBufferWriter dataBuffer)
        {            
            if (!EntityData.FastTryGet(entity, out var systemEntityData))
            {
                EntityData[entity] = new FastDictionary<int, byte[]>();
            }

            var allSystemEntityData = EntityData[entity];

            RewriteBytesFromBufferIntoDictionary(pattern, dataBuffer, ref allSystemEntityData);
        }

        public void SetSystemDataFromNativeBuffer(PatternResult pattern, DataBufferWriter dataBuffer)
        {
            RewriteBytesFromBufferIntoDictionary(pattern, dataBuffer, ref SystemData);
        }

        internal void RewriteBytesFromBufferIntoDictionary(PatternResult pattern, DataBufferWriter dataBuffer, ref FastDictionary<int, byte[]> dictionary)
        {
            if (!dictionary.TryGetValue(pattern.Id, out var bytes))
            {
                dictionary[pattern.Id] = dataBuffer.Buffer.ToArray();
                return;
            }

            if (bytes.Length != dataBuffer.Length)
            {
                dictionary[pattern.Id] = dataBuffer.Buffer.ToArray();
                return;
            }

            fixed (byte* byteBuffer = bytes)
            {
                UnsafeUtility.MemCpy(byteBuffer, (void*) dataBuffer.GetSafePtr(), dataBuffer.Length);
            }
        }
    }
}