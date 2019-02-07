using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ICSharpCode.NRefactory.Ast;
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
            public DataBufferWriter                        Buffer;
            public SnapshotReceiver                        Receiver;
            public StSnapshotRuntime                       Runtime;
            public int                                     EntityLength;
            public ComponentDataFromEntity<T>              States;
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

    public unsafe class ManualStreamerBurst
    {
        public struct WriteDataForEntityPayload
        {
            public int               Index;
            public Entity            Entity;
            public DataBufferWriter  Data;
            public SnapshotReceiver  Receiver;
            public StSnapshotRuntime Runtime;
        }

        public struct WriteDataPayload<T>
            where T : struct, IComponentData
        {
            public DataBufferWriter                        Buffer;
            public SnapshotReceiver                        Receiver;
            public StSnapshotRuntime                       Runtime;
            public int                                     EntityLength;
            public ComponentDataFromEntity<T>              States;
            public ComponentDataFromEntity<DataChanged<T>> Changes;
            public FunctionPointer<wdfe>                   FunctionWriteDataForEntity;
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

                    var change = new DataChanged<T> {IsDirty = 1};
                    if (changes.Exists(entity))
                        change = changes[entity];

                    if (SnapshotOutputUtils.ShouldSkip(receiver, change))
                    {
                        buffer.WriteValue(StreamerSkipReason.Delta);
                        continue;
                    }

                    buffer.WriteValue(StreamerSkipReason.NoSkip);

                    var wdfePayload = new WriteDataForEntityPayload
                    {
                        Index    = i,
                        Entity   = entity,
                        Data     = buffer,
                        Receiver = receiver,
                        Runtime  = runtime
                    };

                    payload.FunctionWriteDataForEntity.Invoke(UnsafeUtility.AddressOf(ref wdfePayload));
                }
            }
        }

        public static void CallWriteData<T>(CallWriteDataAsBurst                    call,
                                            DataBufferWriter                        buffer, SnapshotReceiver receiver, StSnapshotRuntime runtime, int entityLength,
                                            ComponentDataFromEntity<T>              states,
                                            ComponentDataFromEntity<DataChanged<T>> changes,
                                            FunctionPointer<wdfe>                   functionWriteDataForEntity)
            where T : struct, IComponentData
        {
            var payload = new WriteDataPayload<T>
            {
                Buffer                     = buffer,
                Receiver                   = receiver,
                Runtime                    = runtime,
                EntityLength               = entityLength,
                States                     = states,
                Changes                    = changes,
                FunctionWriteDataForEntity = functionWriteDataForEntity
            };

            call(UnsafeUtility.AddressOf(ref payload));
        }

        public delegate void CallWriteDataAsBurst(void* payload);

        public delegate void wdfe(void* payload);
        internal delegate void WriteDataForEntityToBurst(void* payload, void* custom);
    }

    public interface IEntityDataPayload
    {
        void Write(int index, Entity entity, DataBufferWriter data, SnapshotReceiver receiver, StSnapshotRuntime runtime);
    }

    public abstract unsafe class SnapshotEntityDataManualStreamer<TState, TWriteEntityPayload> : SnapshotEntityDataStreamerBase<TState>
        where TState : struct, IComponentData
        where TWriteEntityPayload : struct, IEntityDataPayload
    {
        private static SnapshotEntityDataManualStreamer<TState, TWriteEntityPayload> m_CurrentStreamer;
        private TWriteEntityPayload m_CurrentPayload;

        protected abstract void WriteDataForEntity(int index, Entity entity, DataBufferWriter data, SnapshotReceiver receiver, StSnapshotRuntime runtime);
        protected abstract void UpdatePayload(ref TWriteEntityPayload current);

        protected abstract void ReadDataForEntity(int  index, Entity entity, ref DataBufferReader data, SnapshotSender   sender,   StSnapshotRuntime runtime);

        private ManualStreamerBurst.CallWriteDataAsBurst m_WriteDataBurst;

        private FunctionPointer<ManualStreamerBurst.wdfe> m_FunctionPointerWriteDataForEntity;

        private IntPtr m_WriteDataForEntityOptimizedPtr;

        protected override unsafe void OnCreateManager()
        {
            base.OnCreateManager();

            m_WriteDataBurst = ManualStreamerBurst.CreateCall<TState>.WriteData();

            m_FunctionPointerWriteDataForEntity = new FunctionPointer<ManualStreamerBurst.wdfe>
            (
                Marshal.GetFunctionPointerForDelegate(new Action<IntPtr>(ptr =>
                {
                    var writeDataForEntity = new FunctionPointer<ManualStreamerBurst.WriteDataForEntityToBurst>(m_CurrentStreamer.m_WriteDataForEntityOptimizedPtr);
                    writeDataForEntity.Invoke((void*) ptr, UnsafeUtility.AddressOf(ref m_CurrentStreamer.m_CurrentPayload));
                }))
            );

            ManualStreamerBurst.WriteDataForEntityToBurst optimizedWriteDataForEntity = (payloadPtr, customPtr) =>
            {
                UnsafeUtility.CopyPtrToStructure(payloadPtr, out ManualStreamerBurst.WriteDataForEntityPayload payload);
                UnsafeUtility.CopyPtrToStructure(customPtr, out TWriteEntityPayload custom);

                custom.Write(payload.Index, payload.Entity, payload.Data, payload.Receiver, payload.Runtime);
            };

            try
            {
                m_WriteDataForEntityOptimizedPtr = Marshal.GetFunctionPointerForDelegate(BurstCompiler.CompileDelegate(optimizedWriteDataForEntity));
            }
            catch (Exception e)
            {
                Debug.LogError($"Couldn't burst {typeof(TWriteEntityPayload).FullName}.\n Exception Message:\n{e.Message}");
                throw;
            }
        }

        public override DataBufferWriter WriteData(SnapshotReceiver receiver, StSnapshotRuntime runtime, ref JobHandle jobHandle)
        {
            GetDataAndEntityLength(runtime, out var buffer, out var entityLength);
            UpdateComponentDataFromEntity();

            m_CurrentStreamer = this;
            UpdatePayload(ref m_CurrentPayload);

            ManualStreamerBurst.CallWriteData(m_WriteDataBurst, buffer, receiver, runtime, entityLength, States, Changed, m_FunctionPointerWriteDataForEntity);

            /*for (var i = 0; i != entityLength; i++)
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
            }*/

            return buffer;
        }

        public override void ReadData(SnapshotSender sender, StSnapshotRuntime runtime, DataBufferReader sysData, ref JobHandle jobHandle)
        {
            GetEntityLength(runtime, out var length);
            UpdateComponentDataFromEntity();
            UpdatePayload(ref m_CurrentPayload);

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