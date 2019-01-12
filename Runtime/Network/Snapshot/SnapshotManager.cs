using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using package.stormiumteam.networking;
using package.stormiumteam.networking.runtime.highlevel;
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
    public class SnapshotManager : ComponentSystem
    {
        public struct SnapshotGeneration
        {
            private bool m_Disposed;
            
            public JobHandle JobHandle;
            public DataBufferWriter Data;
            public SnapshotRuntime Runtime;
            public bool IsCreated => !m_Disposed && Runtime.Data.SnapshotType != SnapshotType.Unknown;

            public void Dispose()
            {
                m_Disposed = true;
                
                Runtime.Dispose();
                Data.Dispose();
            }
        }
        
        private ComponentDataFromEntity<NetworkInstanceData> m_NetworkInstanceFromEntity;
        private ComponentGroup m_LocalClientGroup;
        private ComponentGroup m_NetworkClientGroup;
        private ComponentGroup m_GenerateSnapshotEntityGroup;

        protected override void OnCreateManager()
        {
            m_LocalClientGroup = GetComponentGroup(typeof(StormiumClient), typeof(StormiumLocalTag));
            m_NetworkInstanceFromEntity = GetComponentDataFromEntity<NetworkInstanceData>();
            m_NetworkClientGroup = GetComponentGroup(typeof(StormiumClient), typeof(ClientToNetworkInstance));
            m_GenerateSnapshotEntityGroup = GetComponentGroup(typeof(GenerateEntitySnapshot));
        }
        
        protected override void OnUpdate()
        {
            return;
            
            var entityLength = m_GenerateSnapshotEntityGroup.CalculateLength();
            if (entityLength < 0)
                return;

            var entities = TransformEntityArray(m_GenerateSnapshotEntityGroup.GetEntityArray());
            if (!DoLocalGeneration(entities, default, Allocator.TempJob, out _))
            {
                entities.Dispose();
                return;
            }

            var clientLength = m_NetworkClientGroup.CalculateLength();
            if (clientLength < 0)
            {
                entities.Dispose();
                return;
            }

            var entityArray          = m_NetworkClientGroup.GetEntityArray();
            var clientToNetworkArray = m_NetworkClientGroup.GetComponentDataArray<ClientToNetworkInstance>();
            /*for (int i = 0; i != clientLength; i++)
            {
                // A threaded job for each client snapshot could be possible?

                var entity          = entityArray[i];
                var networkInstance = m_NetworkInstanceFromEntity[clientToNetworkArray[i].Target];
                var netCmd          = networkInstance.Commands;
                var receiver        = new SnapshotReceiver(entity, true);

                DataBufferWriter dataBuffer;
                using (dataBuffer = new DataBufferWriter(Allocator.Temp, 2048))
                {
                    var generation = StartGenerating(receiver, default, Allocator.TempJob, entities);
                    CompleteGeneration(generation);

                    dataBuffer.WriteStatic(generation.Data);
                }
            }*/

            entities.Dispose();
        }

        public NativeArray<Entity> GetSnapshotWorldEntities(Allocator allocator)
        {
            return TransformEntityArray(m_GenerateSnapshotEntityGroup.GetEntityArray(), allocator);
        }
        
        public bool MakeACompleteLocalGeneration(out SnapshotGeneration generation, GameTime gt, Allocator allocator)
        {
            var entities = TransformEntityArray(m_GenerateSnapshotEntityGroup.GetEntityArray());
            if (!DoLocalGeneration(entities, gt, allocator, out generation))
            {
                entities.Dispose();
                return false;
            }

            return true;
        }

        [BurstCompile]
        struct TransformEntityArrayJob : IJobParallelFor
        {
            public EntityArray EntityArray;
            public NativeArray<Entity> Entities;
            
            public void Execute(int index)
            {
                Entities[index] = EntityArray[index];
            }
        }

        public NativeArray<Entity> TransformEntityArray(EntityArray entityArray, Allocator allocator)
        {
            var entityLength = entityArray.Length;
            var entities     = new NativeArray<Entity>(entityLength, allocator);
            
            new TransformEntityArrayJob
            {
                EntityArray = entityArray,
                Entities    = entities
            }.Run(entityLength);

            return entities;
        }

        public bool DoLocalGeneration(NativeArray<Entity> entitiesToGenerate, GameTime gt, Allocator allocator, out SnapshotGeneration snapshotGeneration)
        {
            snapshotGeneration = default;
            
            var clientLength = m_LocalClientGroup.CalculateLength();
            if (clientLength <= 0)
                return false;

            var clientEntity = m_LocalClientGroup.GetEntityArray()[0];
            var receiver = new SnapshotReceiver(clientEntity, false);
            snapshotGeneration = GenerateFor(receiver, gt, allocator, entitiesToGenerate);

            return true;
        }
        
        public SnapshotGeneration GenerateFor(SnapshotReceiver receiver, GameTime gt, Allocator allocator, NativeArray<Entity> entities)
        {
            if (gt.Tick < 0) throw new Exception("Tick is inferior to 0");
            
            JobHandle jobHandle = default;
            
            var buffer = new DataBufferWriter(allocator, 1024);
            var data = new SnapshotData(buffer, entities, gt.Tick);
            var runtime = new SnapshotRuntime(data, allocator);

            buffer.Write(entities.Length);
            for (var i = 0; i != entities.Length; i++)
            {
                buffer.CpyWrite(entities[i]);
            }
            
            foreach (var system in AppEvent<ISnapshotSubscribe>.GetObjEvents())
            {
                system.SubscribeSystem();
            }

            var objEvents = AppEvent<ISnapshotManageForClient>.GetObjEvents();
            buffer.Write(objEvents.Length);
            
            // System Loop
            // -----------------
            // Int32 - Index To Next System
            // Int32 - System Pattern Id
            // Data? - System Data
            // -----------------
            foreach (var system in AppEvent<ISnapshotManageForClient>.GetObjEvents())
            {
                var nextDataLength = buffer.CpyWrite(0);
                buffer.Write(system.GetSystemPattern().Id);
                
                var sysBuffer = system.WriteData(receiver, runtime, ref jobHandle);
                
                jobHandle.Complete();
                
                buffer.WriteStatic(sysBuffer);
                sysBuffer.Dispose();
                buffer.Write(buffer.Length, nextDataLength);
            }

            return new SnapshotGeneration()
            {
                Data      = buffer,
                JobHandle = jobHandle,
                Runtime   = runtime
            };
        }

        public SnapshotRuntime ReadApplySnapshot(DataBufferReader reader, Allocator allocator, SnapshotRuntime previousRuntime = default)
        {
            ISnapshotManageForClient GetSystem(int id)
            {
                return AppEvent<ISnapshotManageForClient>.GetObjEvents().FirstOrDefault(system => system.GetSystemPattern().Id == id);
            }
            
            var bank = World.GetExistingManager<NetPatternSystem>();
            var runtime = new SnapshotRuntime();
            
            var tick = reader.ReadValue<int>();
            var entityArray = new NativeArray<Entity>(reader.ReadValue<int>(), allocator);
            for (var i = 0; i != entityArray.Length; i++)
            {
                entityArray[i] = reader.ReadValue<Entity>();
            }

            var sysLength = reader.ReadValue<int>();
            for (var i = 0; i != sysLength; i++)
            {
                var pattern = reader.ReadValue<int>();
                var nextSysDataIndex = reader.ReadValue<int>();
                var sysBuffer = new DataBufferReader(reader, reader.CurrReadIndex, nextSysDataIndex);
                var system = GetSystem(pattern);
                var sender = new SnapshotSender();

                var jobHandle = new JobHandle();
                system.ReadData(sender, runtime, sysBuffer, ref jobHandle);
                
                if (reader.CurrReadIndex != nextSysDataIndex)
                {
                    Debug.LogError("Incoherence?");
                }
                reader.CurrReadIndex = nextSysDataIndex;
            }

            return default;
        }

        public SnapshotRuntime ReadApplySnapshot<TEntityDataMgr, TSystemDataMgr>(DataBufferReader data, TEntityDataMgr entityDataMgr, TSystemDataMgr systemDataMgr)
        {
            
        }
        
        public void ApplySnapshot(SnapshotRuntime runtime)
        {
            ref var reader = ref runtime.Data.Reader;

            // skip ticks
            reader.CurrReadIndex = sizeof(int);
        }
    }
}