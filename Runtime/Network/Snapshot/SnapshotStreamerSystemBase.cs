using System;
using System.Security.Cryptography.X509Certificates;
using package.stormiumteam.networking;
using package.stormiumteam.networking.extensions.NetEcs;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;

namespace Stormium.Core.Networking
{
    public interface IStateData
    {
    }

    public abstract class SnapshotEntityDataStreamer<TState> : JobComponentSystem, ISnapshotSubscribe, ISnapshotManageForClient
        where TState : struct, IStateData, IComponentData
    {
        [BurstCompile]
        [RequireComponentTag(typeof(GenerateEntitySnapshot))]
        private struct WriteDataJob : IJobProcessComponentDataWithEntity<TState>
        {
            public SnapshotReceiver Receiver;
            public SnapshotSystemOutput Output;

            [ReadOnly]
            public ComponentDataFromEntity<DataChanged<TState>> ChangeFromEntity;

            public void Execute(Entity entity, int chunkIndex, ref TState state)
            {
                // no linear data :(
                var change = new DataChanged<TState> {IsDirty = 1};
                if (ChangeFromEntity.Exists(entity))
                    change = ChangeFromEntity[entity];

                if (Output.ShouldSkip(Receiver, change))
                {
                    Output.Data.CpyWrite(0);
                    return;
                }

                Output.Data.Write(ref entity);
                Output.Data.Write(ref state);
            }
        }

        private struct ReadDataJob : IJobParallelFor
        {
            public SnapshotSender                  Sender;
            public SnapshotRuntime                 Runtime;
            public SnapshotSystemInput             Input;
            public ComponentDataFromEntity<TState> StateFromEntity;
            public EntityCommandBuffer.Concurrent EntityCommandBuffer;

            [DeallocateOnJobCompletion]
            public NativeArray<int> Cursor;

            [NativeSetThreadIndex]
            public int JobIndex;

            private void UpdateCursor()
            {
                Cursor[0] = Input.Data.Length;
            }

            public void Execute(int index)
            {
                var readMarker    = Input.Data.CreateMarker(Cursor[0]);
                var entityVersion = Input.Data.ReadValue<int>(readMarker);
                if (entityVersion == 0)
                {
                    UpdateCursor();
                    return; // skip
                }

                var entityIndex = Input.Data.ReadValue<int>();

                var worldEntity = Runtime.EntityToWorld(new Entity {Index = entityIndex, Version = entityVersion});
                if (StateFromEntity.Exists(worldEntity))
                {
                    StateFromEntity[worldEntity] = Input.Data.ReadValue<TState>();
                }
                else
                {
                    EntityCommandBuffer.AddComponent(JobIndex, worldEntity, Input.Data.ReadValue<TState>());
                }

                UpdateCursor();
            }
        }

        private PatternResult m_SystemPattern;

        protected ComponentGroup WriteGroup;
        protected ComponentGroup ReadGroup;
        protected ComponentDataFromEntity<DataChanged<TState>> Changed;
        protected ComponentDataFromEntity<TState> States;

        public PatternResult SystemPattern => m_SystemPattern;

        private readonly int m_SizeOfState = UnsafeUtility.SizeOf<TState>();
        private readonly int m_SizeOfEntity = UnsafeUtility.SizeOf<Entity>();
        
        protected override void OnCreateManager()
        {
            World.GetOrCreateManager<AppEventSystem>().SubscribeToAll(this);
            World.CreateManager<DataChangedSystem<TState>>();

            m_SystemPattern = RegisterPattern();

            WriteGroup = GetComponentGroup(typeof(TState));
            ReadGroup = GetComponentGroup(typeof(TState));
            Changed = GetComponentDataFromEntity<DataChanged<TState>>();
            States = GetComponentDataFromEntity<TState>();
        }

        protected virtual PatternResult RegisterPattern()
        {
            return World.GetOrCreateManager<NetPatternSystem>()
                        .GetLocalBank()
                        .Register(new PatternIdent($"auto." + GetType().Namespace + "." + GetType().Name));
        }

        public PatternResult GetSystemPattern()
        {
            return m_SystemPattern;
        }
        
        public void SubscribeSystem()
        {
            Changed = GetComponentDataFromEntity<DataChanged<TState>>();
            States  = GetComponentDataFromEntity<TState>();
        }

        public DataBufferWriter WriteData(SnapshotReceiver receiver, SnapshotRuntime runtime, ref JobHandle jobHandle)
        {
            var length = WriteGroup.CalculateLength();
            var buffer = new DataBufferWriter(Allocator.TempJob, true, length * m_SizeOfState + length);
            var output = new SnapshotSystemOutput(buffer);

            output.Data.WriteDynInteger((ulong) length);
            new WriteDataJob
            {
                Output           = output,
                Receiver         = receiver,
                ChangeFromEntity = Changed
            }.Run(this);
            
            return buffer;
        }

        public void ReadData(SnapshotSender sender, SnapshotRuntime runtime, ref JobHandle jobHandle)
        {
             var input = new SnapshotSystemInput(buffer);

             var length = (int) runtime.Data.Reader.ReadDynInteger();
             new ReadDataJob
             {
                 Cursor = new NativeArray<int>(1, Allocator.TempJob),
                 Sender = sender,
                 Input = input,
                 Runtime = runtime,
                 StateFromEntity = States
             }.Run(length);
        }
    }
}