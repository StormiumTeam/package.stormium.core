using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using package.stormiumteam.networking.runtime.lowlevel;
using Stormium.Core;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
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

        public override DataBufferWriter WriteData(SnapshotReceiver receiver, StSnapshotRuntime runtime)
        {
            Profiler.BeginSample("Init Variables");
            GetDataAndEntityLength(runtime, out var buffer, out var entityLength);
            UpdateComponentDataFromEntity();
            Profiler.EndSample();

            StreamerBurst.CallWriteData(m_WriteDataBurst, buffer, receiver, runtime, entityLength, States, Changed);

            return buffer;
        }

        public override void ReadData(SnapshotSender sender, StSnapshotRuntime runtime, DataBufferReader sysData)
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

        public struct WriteDataPayload<T, Tw>
            where T : struct, IComponentData
            where Tw : struct, IWriteEntityDataPayload
        {
            public DataBufferWriter                        Buffer;
            public SnapshotReceiver                        Receiver;
            public StSnapshotRuntime                       Runtime;
            public int                                     EntityLength;
            public ComponentDataFromEntity<T>              States;
            public ComponentDataFromEntity<DataChanged<T>> Changes;
            public FunctionPointer<WriteDataForEntityToBurst> WriteFunction;
            public Tw CustomWritePayload;
        }

        public static class CreateCall<T, Tw, Tr> where T : struct, IComponentData
                                                  where Tw : struct, IWriteEntityDataPayload
                                                  where Tr : struct, IReadEntityDataPayload
        {
            public static CallWriteDataAsBurst WriteData()
            {
                return BurstCompiler.CompileDelegate<CallWriteDataAsBurst>(InternalWriteData);
            }

            private static void InternalWriteData(void* payloadPtr)
            {
                UnsafeUtility.CopyPtrToStructure(payloadPtr, out WriteDataPayload<T, Tw> payload);

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

                    payload.WriteFunction.Invoke(UnsafeUtility.AddressOf(ref wdfePayload), UnsafeUtility.AddressOf(ref payload.CustomWritePayload));
                }
            }
        }

        public static void CallWriteData<T, Tw>(CallWriteDataAsBurst                    call,
                                            DataBufferWriter                        buffer, SnapshotReceiver receiver, StSnapshotRuntime runtime, int entityLength,
                                            ComponentDataFromEntity<T>              states,
                                            ComponentDataFromEntity<DataChanged<T>> changes,
                                            FunctionPointer<WriteDataForEntityToBurst> writeFunction,
                                            Tw customWritePayload)
            where T : struct, IComponentData
            where Tw : struct, IWriteEntityDataPayload
        {
            var payload = new WriteDataPayload<T, Tw>
            {
                Buffer        = buffer,
                Receiver      = receiver,
                Runtime       = runtime,
                EntityLength  = entityLength,
                States        = states,
                Changes       = changes,
                WriteFunction = writeFunction,
                CustomWritePayload = customWritePayload
            };

            call(UnsafeUtility.AddressOf(ref payload));
        }

        public delegate void CallWriteDataAsBurst(void* payload);

        public delegate void wdfe(void* payload);
        public delegate void WriteDataForEntityToBurst(void* payload, void* custom);
    }

    public interface IWriteEntityDataPayload
    {
        void Write(int index, Entity entity, DataBufferWriter data, SnapshotReceiver receiver, StSnapshotRuntime runtime);
    }

    public interface IReadEntityDataPayload
    {
        void Read(int index, Entity entity, ref DataBufferReader data, SnapshotSender sender, StSnapshotRuntime runtime);
    }
    
    public interface IMultiEntityDataPayload : IWriteEntityDataPayload, IReadEntityDataPayload
    {}

    public abstract class SnapshotEntityDataManualStreamer<TState, TMultiEntityPayload> : SnapshotEntityDataManualStreamer<TState, TMultiEntityPayload, TMultiEntityPayload>
        where TState : struct, IComponentData
        where TMultiEntityPayload : struct, IMultiEntityDataPayload
    {
        protected abstract void UpdatePayload(ref TMultiEntityPayload current);
        
        protected override void UpdatePayloadR(ref TMultiEntityPayload current)
        {
            UpdatePayload(ref current);
        }

        protected override void UpdatePayloadW(ref TMultiEntityPayload current)
        {
            UpdatePayload(ref current);
        }
    }
    
    public abstract unsafe class SnapshotEntityDataManualStreamer<TState, TWriteEntityPayload, TReadEntityPayload> : SnapshotEntityDataStreamerBase<TState>
        where TState : struct, IComponentData
        where TWriteEntityPayload : struct, IWriteEntityDataPayload
        where TReadEntityPayload : struct, IReadEntityDataPayload
    {
        internal static SnapshotEntityDataManualStreamer<TState, TWriteEntityPayload, TReadEntityPayload> m_CurrentStreamer;
        internal static IntPtr m_WriteDataForEntityOptimizedPtr;
        
        internal FunctionPointer<ManualStreamerBurst.wdfe> m_FunctionPointerWriteDataForEntity;
        
        internal TWriteEntityPayload m_CurrentWritePayload;
        internal TReadEntityPayload m_CurrentReadPayload;

        protected abstract void UpdatePayloadW(ref TWriteEntityPayload current);
        protected abstract void UpdatePayloadR(ref TReadEntityPayload current);

        private ManualStreamerBurst.CallWriteDataAsBurst m_WriteDataBurst;

        protected override unsafe void OnCreateManager()
        {
            base.OnCreateManager();

            try
            {
                m_WriteDataForEntityOptimizedPtr = Marshal.GetFunctionPointerForDelegate(BurstCompiler.CompileDelegate((ManualStreamerBurst.WriteDataForEntityToBurst) OptimizedWriteDataForEntity));
            }
            catch (Exception e)
            {
                Debug.LogError($"Couldn't burst {typeof(TWriteEntityPayload).FullName}.\n Exception Message:\n{e.Message}");
                m_WriteDataForEntityOptimizedPtr = Marshal.GetFunctionPointerForDelegate((ManualStreamerBurst.WriteDataForEntityToBurst) OptimizedWriteDataForEntity);
            }
            
            m_WriteDataBurst = ManualStreamerBurst.CreateCall<TState, TWriteEntityPayload, TReadEntityPayload>.WriteData();
            
            /*m_FunctionPointerWriteDataForEntity = new FunctionPointer<ManualStreamerBurst.wdfe>
            (
                Marshal.GetFunctionPointerForDelegate(new Action<IntPtr>(ptr =>
                {
                    var writeDataForEntity = new FunctionPointer<ManualStreamerBurst.WriteDataForEntityToBurst>(m_CurrentStreamer.m_WriteDataForEntityOptimizedPtr);
                    writeDataForEntity.Invoke((void*) ptr, UnsafeUtility.AddressOf(ref m_CurrentStreamer.m_CurrentWritePayload));
                }))
            );*/
        }

        private static void OptimizedWriteDataForEntity(void* payloadPtr, void* customPtr)
        {
            UnsafeUtility.CopyPtrToStructure(payloadPtr, out ManualStreamerBurst.WriteDataForEntityPayload payload);
            UnsafeUtility.CopyPtrToStructure(customPtr, out TWriteEntityPayload custom);

            custom.Write(payload.Index, payload.Entity, payload.Data, payload.Receiver, payload.Runtime);
        }

        public override DataBufferWriter WriteData(SnapshotReceiver receiver, StSnapshotRuntime runtime)
        {
            GetDataAndEntityLength(runtime, out var buffer, out var entityLength);
            UpdateComponentDataFromEntity();

            m_CurrentStreamer = this;
            UpdatePayloadW(ref m_CurrentWritePayload);

            var writeFunction = new FunctionPointer<ManualStreamerBurst.WriteDataForEntityToBurst>(m_WriteDataForEntityOptimizedPtr);
            
            ManualStreamerBurst.CallWriteData(m_WriteDataBurst, buffer, receiver, runtime, entityLength, States, Changed, writeFunction, m_CurrentWritePayload);

            return buffer;
        }

        public override void ReadData(SnapshotSender sender, StSnapshotRuntime runtime, DataBufferReader sysData)
        {
            GetEntityLength(runtime, out var length);
            UpdateComponentDataFromEntity();
            UpdatePayloadR(ref m_CurrentReadPayload);

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

                m_CurrentReadPayload.Read(index, worldEntity, ref sysData, sender, runtime);
            }
        }
    }
}