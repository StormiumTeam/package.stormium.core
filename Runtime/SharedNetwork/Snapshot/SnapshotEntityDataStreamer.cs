using System;
using package.stormiumteam.networking;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Stormium.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;

namespace StormiumShared.Core.Networking
{
    public enum StreamerSkipReason : byte
    {
        NoSkip      = 0,
        Delta       = 1,
        NoComponent = 2
    }

    public abstract class SnapshotEntityDataStreamerBase<TState> : SnapshotDataStreamerBase
        where TState : struct, IComponentData
    {
        private int m_EntityVersion;
        
        public ComponentType StateType;
        public ComponentType ChangedType;
        
        protected ComponentDataFromEntity<TState> States;
        protected ComponentDataFromEntity<DataChanged<TState>> Changed;
        
        private ComponentDataFromEntityBurstExtensions.CallExistsAsBurst m_StateExistsBurst;
        private ComponentDataFromEntityBurstExtensions.CallExistsAsBurst m_ChangedStateExistsBurst;
        
        protected override void OnCreateManager()
        {
            base.OnCreateManager();
            
            StateType   = ComponentType.Create<TState>();
            ChangedType = ComponentType.Create<DataChanged<TState>>();
            
            m_StateExistsBurst = GetExistsCall<TState>();
            m_ChangedStateExistsBurst = GetExistsCall<DataChanged<TState>>();

            UpdateComponentDataFromEntity();
        }

        public ComponentDataFromEntityBurstExtensions.CallExistsAsBurst GetExistsCall<T>()
            where T : struct, IComponentData
        {
            return ComponentDataFromEntityBurstExtensions.CreateCall<T>.Exists();
        }

        public bool StateExists(Entity entity)
        {
            UpdateComponentDataFromEntity();
            
            return States.CallExists(m_StateExistsBurst, entity);
        }

        public bool ChangedStateExists(Entity entity)
        {
            UpdateComponentDataFromEntity();
            
            return Changed.CallExists(m_ChangedStateExistsBurst, entity);
        }

        private void UpdateComponentDataFromEntity()
        {
            if (m_EntityVersion == EntityManager.Version)
                return;

            m_EntityVersion = EntityManager.Version;

            States = GetComponentDataFromEntity<TState>();
            Changed = GetComponentDataFromEntity<DataChanged<TState>>();
        }
    }

    public abstract class SnapshotEntityDataAutomaticStreamer<TState> : SnapshotEntityDataStreamerBase<TState>
        where TState : struct, IComponentData
    {
        private PatternResult m_SystemPattern;

        private ComponentDataFromEntityBurstExtensions.CallExistsAsBurst m_CallExistsAsBurst;

        public override DataBufferWriter WriteData(SnapshotReceiver receiver, StSnapshotRuntime runtime, ref JobHandle jobHandle)
        {
            GetDataAndEntityLength(runtime, out var buffer, out var entityLength);

            for (var i = 0; i != entityLength; i++)
            {
                var entity = runtime.Entities[i].Source;
                if (!EntityManager.HasComponent(entity, StateType))
                {
                    buffer.CpyWrite(StreamerSkipReason.NoComponent);
                    continue;
                }

                var state  = EntityManager.GetComponentData<TState>(entity);
                var change = new DataChanged<TState> {IsDirty = 1};
                if (EntityManager.HasComponent(entity, ChangedType))
                    change = EntityManager.GetComponentData<DataChanged<TState>>(entity);

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
            GetEntityLength(runtime, out var length);

            for (var index = 0; index != length; index++)
            {
                var worldEntity = runtime.GetWorldEntityFromGlobal(index);
                var skip        = sysData.ReadValue<StreamerSkipReason>();

                if (skip != StreamerSkipReason.NoSkip)
                {
                    // If the component don't exist in the snapshot, also remove it from our world.
                    if (skip == StreamerSkipReason.NoComponent
                        && EntityManager.HasComponent(worldEntity, StateType))
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

                if (!EntityManager.HasComponent(worldEntity, StateType))
                    EntityManager.AddComponent(worldEntity, typeof(TState));

                var state = sysData.ReadValue<TState>();
                EntityManager.SetComponentData(worldEntity, state);
            }
        }
    }

    public abstract class SnapshotEntityDataManualStreamer<TState> : SnapshotDataStreamerBase
        where TState : struct, IComponentData
    {
        private PatternResult m_SystemPattern;

        public ComponentType StateType;
        public ComponentType ChangedType;

        protected override void OnCreateManager()
        {
            base.OnCreateManager();

            StateType   = ComponentType.Create<TState>();
            ChangedType = ComponentType.Create<DataChanged<TState>>();
        }

        protected abstract void WriteDataForEntity(int index, Entity entity, ref DataBufferWriter data, SnapshotReceiver receiver, StSnapshotRuntime runtime);
        protected abstract void ReadDataForEntity(int  index, Entity entity, ref DataBufferReader data, SnapshotSender sender, StSnapshotRuntime runtime);

        public override DataBufferWriter WriteData(SnapshotReceiver receiver, StSnapshotRuntime runtime, ref JobHandle jobHandle)
        {
            GetDataAndEntityLength(runtime, out var buffer, out var entityLength);

            for (var i = 0; i != entityLength; i++)
            {
                var entity = runtime.Entities[i].Source;
                if (!EntityManager.HasComponent(entity, StateType))
                {
                    buffer.CpyWrite(StreamerSkipReason.NoComponent);
                    continue;
                }

                var change = new DataChanged<TState> {IsDirty = 1};
                if (EntityManager.HasComponent(entity, ChangedType))
                    change = EntityManager.GetComponentData<DataChanged<TState>>(entity);

                if (SnapshotOutputUtils.ShouldSkip(receiver, change))
                {
                    buffer.CpyWrite(StreamerSkipReason.Delta);
                    continue;
                }

                buffer.CpyWrite(StreamerSkipReason.NoSkip);
                WriteDataForEntity(i, entity, ref buffer, receiver, runtime);
            }

            return buffer;
        }

        public override void ReadData(SnapshotSender sender, StSnapshotRuntime runtime, DataBufferReader sysData, ref JobHandle jobHandle)
        {
            GetEntityLength(runtime, out var length);

            for (var index = 0; index != length; index++)
            {
                var worldEntity = runtime.GetWorldEntityFromGlobal(index);
                var skip        = sysData.ReadValue<StreamerSkipReason>();

                if (skip != StreamerSkipReason.NoSkip)
                {
                    // If the component don't exist in the snapshot, also remove it from our world.
                    if (skip == StreamerSkipReason.NoComponent
                        && EntityManager.HasComponent(worldEntity, StateType))
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

                if (!EntityManager.HasComponent(worldEntity, StateType))
                    EntityManager.AddComponent(worldEntity, typeof(TState));

                ReadDataForEntity(index, worldEntity, ref sysData, sender, runtime);
            }
        }
    }
}