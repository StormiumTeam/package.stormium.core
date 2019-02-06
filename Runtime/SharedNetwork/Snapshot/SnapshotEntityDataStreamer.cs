using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

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

        protected ComponentDataFromEntity<TState>              States;
        protected ComponentDataFromEntity<DataChanged<TState>> Changed;

        private ComponentDataFromEntityBurstExtensions.CallExistsAsBurst m_StateExistsBurst;
        private ComponentDataFromEntityBurstExtensions.CallExistsAsBurst m_ChangedStateExistsBurst;

        static DataBufferMarker WriteDataSafe(ref DataBufferWriter writer, int val)
        {
            return default;
        }
        
        protected override unsafe void OnCreateManager()
        {
            base.OnCreateManager();

            StateType   = ComponentType.Create<TState>();
            ChangedType = ComponentType.Create<DataChanged<TState>>();

            m_StateExistsBurst        = GetExistsCall<TState>();
            m_ChangedStateExistsBurst = GetExistsCall<DataChanged<TState>>();

            m_EntityVersion = -1;

            UpdateComponentDataFromEntity();
        }

        public ComponentDataFromEntityBurstExtensions.CallExistsAsBurst GetExistsCall<T>()
            where T : struct, IComponentData
        {
            return ComponentDataFromEntityBurstExtensions.CreateCall<T>.Exists();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool StateExists(Entity entity)
        {
            return States.CallExists(m_StateExistsBurst, entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ChangedStateExists(Entity entity)
        {
            return Changed.CallExists(m_ChangedStateExistsBurst, entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void UpdateComponentDataFromEntity()
        {
            m_EntityVersion = EntityManager.Version;

            Profiler.BeginSample("Update GetComponentDataFromEntity");
            States  = GetComponentDataFromEntity<TState>();
            Changed = GetComponentDataFromEntity<DataChanged<TState>>();
            Profiler.EndSample();
        }
    }

    static unsafe class StreamerBurst
    {
        public struct WriteDataPayload<T>
            where T : struct, IComponentData
        {
            public DataBufferWriter Buffer;
            public SnapshotReceiver Receiver;
            public StSnapshotRuntime Runtime;
            public int EntityLength;
            public ComponentDataFromEntity<T> States;
            public ComponentDataFromEntity<DataChanged<T>> Changes;
        }

        public static class CreateCall<T> where T : struct, IComponentData
        {
            public static CallWriteDataAsBurst WriteData()
            {
                return BurstCompiler.CompileDelegate<CallWriteDataAsBurst>(InternalWriteData);
            }

            private static void InternalWriteData(void* payloadPtr)
            {
                UnsafeUtility.CopyPtrToStructure(payloadPtr, out WriteDataPayload<T> payload);

                ref var entityLength = ref payload.EntityLength;
                ref var buffer       = ref payload.Buffer;
                ref var receiver     = ref payload.Receiver;
                ref var runtime      = ref payload.Runtime;
                ref var states       = ref payload.States;
                ref var changes      = ref payload.Changes;

                for (var i = 0; i != entityLength; i++)
                {
                    var entity = runtime.Entities[i].Source;
                    if (!states.Exists(entity))
                    {
                        buffer.WriteValue(StreamerSkipReason.NoComponent);
                        continue;
                    }

                    var state  = states[entity];
                    var change = new DataChanged<T> {IsDirty = 1};
                    if (changes.Exists(entity))
                        change = changes[entity];

                    if (SnapshotOutputUtils.ShouldSkip(receiver, change))
                    {
                        buffer.WriteValue(StreamerSkipReason.Delta);
                        continue;
                    }

                    buffer.WriteValue(StreamerSkipReason.NoSkip);
                    buffer.WriteRef(ref state);
                }
            }
        }

        public static void CallWriteData<T>(CallWriteDataAsBurst                    call,
                                            DataBufferWriter                        buffer, SnapshotReceiver receiver, StSnapshotRuntime runtime, int entityLength,
                                            ComponentDataFromEntity<T>              states,
                                            ComponentDataFromEntity<DataChanged<T>> changes)
            where T : struct, IComponentData
        {
            var payload = new WriteDataPayload<T>
            {
                Buffer       = buffer,
                Receiver     = receiver,
                Runtime      = runtime,
                EntityLength = entityLength,
                States       = states,
                Changes      = changes
            };

            call(UnsafeUtility.AddressOf(ref payload));
        }

        public delegate void CallWriteDataAsBurst(void* payload);
    }

    public abstract class SnapshotEntityDataAutomaticStreamer<TState> : SnapshotEntityDataStreamerBase<TState>
        where TState : struct, IComponentData
    {
        private StreamerBurst.CallWriteDataAsBurst m_WriteDataBurst;

        protected override unsafe void OnCreateManager()
        {
            base.OnCreateManager();

            Debug.Log("Will start compiling...");
            m_WriteDataBurst = StreamerBurst.CreateCall<TState>.WriteData();
        }

        public override DataBufferWriter WriteData(SnapshotReceiver receiver, StSnapshotRuntime runtime, ref JobHandle jobHandle)
        {
            Profiler.BeginSample("Init Variables");
            GetDataAndEntityLength(runtime, out var buffer, out var entityLength);
            UpdateComponentDataFromEntity();
            Profiler.EndSample();
            
            StreamerBurst.CallWriteData(m_WriteDataBurst, buffer, receiver, runtime, entityLength, States, Changed);
            
            /*Profiler.BeginSample("Foreach");
            for (var i = 0; i != entityLength; i++)
            {
                Profiler.BeginSample("Get Entity");
                var entity = runtime.Entities[i].Source;
                Profiler.EndSample();
                Profiler.BeginSample("Check if state exists");
                if (!StateExists(entity))
                {
                    Profiler.EndSample();
                    buffer.WriteValue(StreamerSkipReason.NoComponent);
                    continue;
                }
                Profiler.EndSample();

                Profiler.BeginSample("Get states and changes");
                var state  = EntityManager.GetComponentData<TState>(entity);
                var change = new DataChanged<TState> {IsDirty = 1};
                if (ChangedStateExists(entity))
                    change = EntityManager.GetComponentData<DataChanged<TState>>(entity);
                Profiler.EndSample();
                
                Profiler.BeginSample("Should skip data?");
                if (SnapshotOutputUtils.ShouldSkip(receiver, change))
                {
                    buffer.WriteValue(StreamerSkipReason.Delta);
                    Profiler.EndSample();
                    continue;
                }
                Profiler.EndSample();

                Profiler.BeginSample("Write Data");
                buffer.WriteValue(StreamerSkipReason.NoSkip);
                buffer.WriteRef(ref state);
                Profiler.EndSample();
            }
            Profiler.EndSample();*/

            return buffer;
        }

        public override void ReadData(SnapshotSender sender, StSnapshotRuntime runtime, DataBufferReader sysData, ref JobHandle jobHandle)
        {
            GetEntityLength(runtime, out var length);
            UpdateComponentDataFromEntity();

            for (var index = 0; index != length; index++)
            {
                var worldEntity = runtime.GetWorldEntityFromGlobal(index);
                var skip        = sysData.ReadValue<StreamerSkipReason>();

                if (skip != StreamerSkipReason.NoSkip)
                {
                    // If the component don't exist in the snapshot, also remove it from our world.
                    if (skip == StreamerSkipReason.NoComponent
                        && StateExists(worldEntity))
                    {
                        EntityManager.RemoveComponent<TState>(worldEntity);
                        // If for some weird reason, it also have the 'DataChanged<T>' component, removed it
                        if (ChangedStateExists(worldEntity))
                        {
                            EntityManager.RemoveComponent<DataChanged<TState>>(worldEntity);
                        }

                        UpdateComponentDataFromEntity();
                    }

                    continue; // skip
                }

                if (!StateExists(worldEntity))
                {
                    EntityManager.AddComponent(worldEntity, typeof(TState));
                    UpdateComponentDataFromEntity();
                }

                var state = sysData.ReadValue<TState>();
                EntityManager.SetComponentData(worldEntity, state);
            }
        }
    }

    public abstract class SnapshotEntityDataManualStreamer<TState> : SnapshotEntityDataStreamerBase<TState>
        where TState : struct, IComponentData
    {
        protected abstract void WriteDataForEntity(int index, Entity entity, ref DataBufferWriter data, SnapshotReceiver receiver, StSnapshotRuntime runtime);
        protected abstract void ReadDataForEntity(int  index, Entity entity, ref DataBufferReader data, SnapshotSender   sender,   StSnapshotRuntime runtime);

        public override DataBufferWriter WriteData(SnapshotReceiver receiver, StSnapshotRuntime runtime, ref JobHandle jobHandle)
        {
            GetDataAndEntityLength(runtime, out var buffer, out var entityLength);
            UpdateComponentDataFromEntity();

            for (var i = 0; i != entityLength; i++)
            {
                var entity = runtime.Entities[i].Source;
                if (!StateExists(entity))
                {
                    buffer.WriteValue(StreamerSkipReason.NoComponent);
                    continue;
                }

                var change = new DataChanged<TState> {IsDirty = 1};
                if (ChangedStateExists(entity))
                    change = EntityManager.GetComponentData<DataChanged<TState>>(entity);

                if (SnapshotOutputUtils.ShouldSkip(receiver, change))
                {
                    buffer.WriteValue(StreamerSkipReason.Delta);
                    continue;
                }

                buffer.WriteValue(StreamerSkipReason.NoSkip);
                WriteDataForEntity(i, entity, ref buffer, receiver, runtime);
            }

            return buffer;
        }

        public override void ReadData(SnapshotSender sender, StSnapshotRuntime runtime, DataBufferReader sysData, ref JobHandle jobHandle)
        {
            GetEntityLength(runtime, out var length);
            UpdateComponentDataFromEntity();

            for (var index = 0; index != length; index++)
            {
                var worldEntity = runtime.GetWorldEntityFromGlobal(index);
                var skip        = sysData.ReadValue<StreamerSkipReason>();

                if (skip != StreamerSkipReason.NoSkip)
                {
                    // If the component don't exist in the snapshot, also remove it from our world.
                    if (skip == StreamerSkipReason.NoComponent
                        && StateExists(worldEntity))
                    {
                        EntityManager.RemoveComponent<TState>(worldEntity);
                        // If for some weird reason, it also have the 'DataChanged<T>' component, removed it
                        if (ChangedStateExists(worldEntity))
                        {
                            EntityManager.RemoveComponent<DataChanged<TState>>(worldEntity);
                        }

                        UpdateComponentDataFromEntity();
                    }

                    continue; // skip
                }

                if (!StateExists(worldEntity))
                {
                    EntityManager.AddComponent(worldEntity, typeof(TState));
                    UpdateComponentDataFromEntity();
                }

                ReadDataForEntity(index, worldEntity, ref sysData, sender, runtime);
            }
        }
    }
}