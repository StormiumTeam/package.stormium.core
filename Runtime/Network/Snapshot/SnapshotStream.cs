using System;
using package.stormiumteam.networking.runtime.lowlevel;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Stormium.Core.Networking
{
    public enum SnapshotType : byte
    {
        Unknown = 0,
        Write = 1,
        Read = 2,
    }

    public enum SkipReason : byte
    {
        None,
        NoDeltaDifference
    }
    
    [BurstCompile]
    public struct JobWriteSystemEntities : IJob
    {
        public DataBufferWriter Data;
        public EntityArray SystemEntities;
        public NativeArray<Entity> FromEntities;
        public int Length;

        public void Execute()
        {
            for (var index = 0; index != Length; index++)
            {
                var compare = ((ushort) FromEntities.IndexOf<Entity, Entity>(SystemEntities[index]));
                Data.CpyWrite(compare);
            }
        }
    }

    public struct SnapshotSystemOutput
    {
        public DataBufferWriter Data;

        public SnapshotSystemOutput(DataBufferWriter data)
        {
            Data = data;
        }
        
        public bool ShouldSkip(SnapshotReceiver receiver, SkipReason skipReason)
        {
            if (receiver.WantFullSnapshot == 1) return false;
            
            return skipReason != SkipReason.None;
        }
        
        public bool Skip(SnapshotReceiver receiver, SkipReason skipReason)
        {
            if (receiver.WantFullSnapshot == 1) skipReason = SkipReason.None;
            
            Data.CpyWrite((byte) skipReason);
            return skipReason != SkipReason.None;
        }

        public bool ShouldSkip<T>(SnapshotReceiver receiver, DataChanged<T> changed)
            where T : struct, IComponentData
        {
            return Skip(receiver, changed.IsDirty == 0 ? SkipReason.NoDeltaDifference : SkipReason.None);
        }

        public bool Skip<T>(SnapshotReceiver receiver, DataChanged<T> changed)
            where T : struct, IComponentData
        {
            return Skip(receiver, changed.IsDirty == 0 ? SkipReason.NoDeltaDifference : SkipReason.None);
        }
        
        public void WriteSystemEntities(NativeArray<Entity> systemEntities, NativeArray<Entity> fromEntities)
        {
            var length = systemEntities.Length;
            Data.Write(ref length);
            for (var i = 0; i != length; i++)
                Data.CpyWrite((ushort) fromEntities.IndexOf<Entity, Entity>(systemEntities[i]));
        }

        public JobWriteSystemEntities WriteSystemEntities(EntityArray systemEntities, NativeArray<Entity> fromEntities)
        {
            var length = systemEntities.Length;
            Data.Write(ref length);
            // If the data is dynamic and we are gonna do a parallelFor job, we need to have no errors when we set a value to a list.
            if (Data.IsDynamic == 1)
                Data.TryResize(Data.Length + length);

            return new JobWriteSystemEntities
            {
                Data           = Data,
                SystemEntities = systemEntities,
                FromEntities   = fromEntities,
                Length         = length
            };
        }
    }
    
    public struct SnapshotSystemInput
    {
        public DataBufferReader Data;

        public SnapshotSystemInput(DataBufferReader data)
        {
            Data = data;
        }
        
        public SkipReason GetSkipReason()
        {
            return (SkipReason) Data.ReadValue<byte>();
        }
        
        public NativeArray<Entity> ReadSystemEntities(NativeList<Entity> fromEntities, Allocator allocator)
        {
            var length = Data.ReadValue<int>();
            var array  = new NativeArray<Entity>(length, allocator);
            
            for (var i = 0; i != length; i++)
            {
                var index = Data.ReadValue<ushort>();
                array[i] = fromEntities[index];
            }

            return array;
        }
        
        public NativeArray<Entity> ReadSystemEntities(NativeArray<Entity> fromEntities, Allocator allocator)
        {
            var length = Data.ReadValue<int>();
            var array = new NativeArray<Entity>(length, allocator);
            
            for (var i = 0; i != length; i++)
            {
                var index = Data.ReadValue<ushort>();
                array[i] = fromEntities[index];
            }

            return array;
        }
    }

    public struct SnapshotRuntime
    {
        public SnapshotData Data;

        public NativeHashMap<Entity, Entity> SnapshotToWorld;
        public NativeHashMap<Entity, Entity> WorldToSnapshot;
        
        public SnapshotRuntime(SnapshotData data, Allocator allocator)
        {
            Data = data;

            SnapshotToWorld = new NativeHashMap<Entity, Entity>(128, allocator);
            WorldToSnapshot = new NativeHashMap<Entity, Entity>(128, allocator);
        }
        
        public Entity EntityToWorld(Entity snapshotEntity)
        {
            SnapshotToWorld.TryGetValue(snapshotEntity, out var worldEntity);

            return worldEntity;
        }

        public Entity EntityToSnapshot(Entity worldEntity)
        {
            WorldToSnapshot.TryGetValue(worldEntity, out var snapshotEntity);

            return snapshotEntity;
        }
        
        public Entity GetWorldEntityFromCustom(NativeArray<Entity> entities, int systemIndex)
        {
            return EntityToWorld(entities[systemIndex]);
        }

        public Entity GetWorldEntityFromGlobal(int index)
        {
            return EntityToWorld(Data.Entities[index]);
        }

        public void Dispose()
        {
            SnapshotToWorld.Dispose();
            WorldToSnapshot.Dispose();
        }
    }

    public struct SnapshotData
    {
        public SnapshotType SnapshotType;
        
        public DataBufferReader Reader;
        public DataBufferWriter Writer;
        
        public NativeArray<Entity> Entities;

        public SnapshotData(NativeArray<Entity> entities)
        {
            SnapshotType = SnapshotType.Unknown;
            Reader = default;
            Writer = default;

            Entities = entities;
        }

        public SnapshotData(DataBufferReader reader, NativeArray<Entity> entities) : this(entities)
        {
            SnapshotType = SnapshotType.Read;
            Reader = reader;
        }

        public SnapshotData(DataBufferWriter writer, NativeArray<Entity> entities) : this(entities)
        {
            SnapshotType = SnapshotType.Write;
            Writer = writer;
        }
    }
}