using package.stormiumteam.networking;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace StormiumShared.Core.Networking
{
    public enum StreamerSkipReason : byte
    {
        NoSkip = 0,
        Delta = 1,
        NoComponent = 2
    }
    
    public abstract class SnapshotEntityDataAutomaticStreamer<TState> : SnapshotDataStreamerBase
        where TState : struct, IStateData, IComponentData
    {
        private PatternResult m_SystemPattern;

        protected ComponentDataFromEntity<DataChanged<TState>> Changed;
        protected ComponentDataFromEntity<TState>              States;

        public override DataBufferWriter WriteData(SnapshotReceiver receiver, StSnapshotRuntime runtime, ref JobHandle jobHandle)
        {
            Changed = GetComponentDataFromEntity<DataChanged<TState>>();
            States  = GetComponentDataFromEntity<TState>();
            
            GetDataAndEntityLength(runtime, out var buffer, out var entityLength);

            for (var i = 0; i != entityLength; i++)
            {
                var entity = runtime.Entities[i].Source;
                if (!States.Exists(entity))
                {
                    buffer.CpyWrite(StreamerSkipReason.NoComponent);
                    continue;
                }

                var state  = States[entity];
                var change = new DataChanged<TState> {IsDirty = 1};
                if (Changed.Exists(entity))
                    change = Changed[entity];

                if (SnapshotOutputUtils.ShouldSkip(receiver, change))
                {
                    buffer.CpyWrite(StreamerSkipReason.Delta);
                    continue;
                }

                buffer.CpyWrite(StreamerSkipReason.NoSkip);
                buffer.Write(ref state);
            }

            return buffer;
        }

        public override void ReadData(SnapshotSender sender, StSnapshotRuntime runtime, DataBufferReader sysData, ref JobHandle jobHandle)
        {
            Changed = GetComponentDataFromEntity<DataChanged<TState>>();
            States  = GetComponentDataFromEntity<TState>();
            
            GetEntityLength(runtime, out var length);
            
            for (var index = 0; index != length; index++)
            {
                var worldEntity = runtime.GetWorldEntityFromGlobal(index);
                var skip = sysData.ReadValue<StreamerSkipReason>();
                
                if (skip != StreamerSkipReason.NoSkip)
                {
                    // If the component don't exist in the snapshot, also remove it from our world.
                    if (skip == StreamerSkipReason.NoComponent
                        && States.Exists(worldEntity))
                    {
                        EntityManager.RemoveComponent<TState>(worldEntity);
                        // If for some weird reason, it also have the 'DataChanged<T>' component, removed it
                        if (EntityManager.HasComponent<DataChanged<TState>>(worldEntity))
                        {
                            EntityManager.RemoveComponent<DataChanged<TState>>(worldEntity);
                        }
                    }

                    continue; // skip
                }

                if (!States.Exists(worldEntity))
                    EntityManager.AddComponent(worldEntity, typeof(TState));

                var state = sysData.ReadValue<TState>();
                States[worldEntity] = state;
            }
        }
    }
    
        public abstract class SnapshotEntityDataManualStreamer<TState> : SnapshotDataStreamerBase
        where TState : struct, IStateData, IComponentData
    {
        private PatternResult m_SystemPattern;

        protected ComponentDataFromEntity<DataChanged<TState>> Changed;
        protected ComponentDataFromEntity<TState>              States;

        protected abstract void WriteDataForEntity(int index, Entity entity, ref DataBufferWriter data, in StSnapshotRuntime runtime);
        protected abstract void ReadDataForEntity(int index, Entity entity, in DataBufferReader data, in StSnapshotRuntime runtime);

        public override DataBufferWriter WriteData(SnapshotReceiver receiver, StSnapshotRuntime runtime, ref JobHandle jobHandle)
        {
            Changed = GetComponentDataFromEntity<DataChanged<TState>>();
            States  = GetComponentDataFromEntity<TState>();
            
            GetDataAndEntityLength(runtime, out var buffer, out var entityLength);

            for (var i = 0; i != entityLength; i++)
            {
                var entity = runtime.Entities[i].Source;
                if (!States.Exists(entity))
                {
                    buffer.CpyWrite(StreamerSkipReason.NoComponent);
                    continue;
                }

                var state  = States[entity];
                var change = new DataChanged<TState> {IsDirty = 1};
                if (Changed.Exists(entity))
                    change = Changed[entity];

                if (SnapshotOutputUtils.ShouldSkip(receiver, change))
                {
                    buffer.CpyWrite(StreamerSkipReason.Delta);
                    continue;
                }

                buffer.CpyWrite(StreamerSkipReason.NoSkip);
                WriteDataForEntity(i, entity, ref buffer, runtime);
            }

            return buffer;
        }

        public override void ReadData(SnapshotSender sender, StSnapshotRuntime runtime, DataBufferReader sysData, ref JobHandle jobHandle)
        {
            Changed = GetComponentDataFromEntity<DataChanged<TState>>();
            States  = GetComponentDataFromEntity<TState>();
            
            GetEntityLength(runtime, out var length);
            
            for (var index = 0; index != length; index++)
            {
                var worldEntity = runtime.GetWorldEntityFromGlobal(index);
                var skip = sysData.ReadValue<StreamerSkipReason>();
                
                if (skip != StreamerSkipReason.NoSkip)
                {
                    // If the component don't exist in the snapshot, also remove it from our world.
                    if (skip == StreamerSkipReason.NoComponent
                        && States.Exists(worldEntity))
                    {
                        EntityManager.RemoveComponent<TState>(worldEntity);
                        // If for some weird reason, it also have the 'DataChanged<T>' component, removed it
                        if (EntityManager.HasComponent<DataChanged<TState>>(worldEntity))
                        {
                            EntityManager.RemoveComponent<DataChanged<TState>>(worldEntity);
                        }
                    }

                    continue; // skip
                }

                if (!States.Exists(worldEntity))
                    EntityManager.AddComponent(worldEntity, typeof(TState));
                
                ReadDataForEntity(index, worldEntity, sysData, runtime);
            }
        }
    }
}