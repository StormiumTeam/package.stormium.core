using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            public JobHandle JobHandle;
            public DataBufferWriter Data;
            public SnapshotRuntime Runtime;

            public void Dispose()
            {
                Runtime.Dispose();
                Data.Dispose();
            }
        }
        
        private ComponentDataFromEntity<NetworkInstanceData> m_NetworkInstanceFromEntity;
        private ComponentGroup m_NetworkClientGroup;
        private ComponentGroup m_GenerateSnapshotEntityGroup;

        protected override void OnCreateManager()
        {                        
            m_NetworkInstanceFromEntity = GetComponentDataFromEntity<NetworkInstanceData>();
            m_NetworkClientGroup = GetComponentGroup(typeof(StormiumClient), typeof(ClientToNetworkInstance));
            m_GenerateSnapshotEntityGroup = GetComponentGroup(typeof(GenerateEntitySnapshot));
        }

        private List<SnapshotGeneration> m_Generations = new List<SnapshotGeneration>();

        protected override void OnUpdate()
        {
            var entityLength = m_GenerateSnapshotEntityGroup.CalculateLength();
            if (entityLength < 0) return;

            m_Generations.Clear();

            var entities = TransformEntityArray(m_GenerateSnapshotEntityGroup.GetEntityArray());
            for (int i = 0; i != 100; i++)
            {
                Profiler.BeginSample("Generation#" + i);
                Profiler.BeginSample("StartGenerating()");
                var receiver = new SnapshotReceiver(default, false);
                m_Generations.Add(StartGenerating(receiver, entities));
                Profiler.EndSample();
                
                Profiler.BeginSample("CompleteGeneration()");
                CompleteGeneration(m_Generations[m_Generations.Count - 1]);
                Profiler.EndSample();
                Profiler.EndSample();
            }
            entities.Dispose();

            foreach (var generation in m_Generations)
            {
                //CompleteGeneration(generation);
            }

            /*var clientLength = m_NetworkClientGroup.CalculateLength();
            if (clientLength < 0) return;

            var entityArray = m_NetworkClientGroup.GetEntityArray();
            var clientToNetworkArray = m_NetworkClientGroup.GetComponentDataArray<ClientToNetworkInstance>();
            for (int i = 0; i != clientLength; i++)
            {
                // A threaded job for each client snapshot could be possible?

                var entity          = entityArray[i];
                var networkInstance = m_NetworkInstanceFromEntity[clientToNetworkArray[i].Target];
                var netCmd          = networkInstance.Commands;
                var jobHandle       = default(JobHandle);

                DataBufferWriter dataBuffer;
                using (dataBuffer = new DataBufferWriter(Allocator.Temp, 2048))
                {
                    GenerateNetworkClientSnapshot(entity, ref LocalSnapshot, ref dataBuffer, ref jobHandle);
                }
            }*/
        }

        // STRUCTURE:
        // Write System Data
        // Write Entity Data from Systems
        private List<DataBufferWriter> m_BufferList = new List<DataBufferWriter>(8);
        
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

        public NativeArray<Entity> TransformEntityArray(EntityArray entityArray)
        {
            Profiler.BeginSample("Transform EntityArray into NativeArray");
            var entityLength = entityArray.Length;
            var entities     = new NativeArray<Entity>(entityLength, Allocator.TempJob);
            new TransformEntityArrayJob
            {
                EntityArray = entityArray,
                Entities    = entities
            }.Run(entityLength);
            Profiler.EndSample();

            return entities;
        }

        public SnapshotGeneration StartGenerating(SnapshotReceiver receiver, NativeArray<Entity> entities)
        {
            JobHandle jobHandle = default;
            
            Profiler.BeginSample("Vars");
            var buffer = new DataBufferWriter(Allocator.TempJob, 1024);
            var data = new SnapshotData(buffer, entities);
            var runtime = new SnapshotRuntime(data, Allocator.TempJob);
            Profiler.EndSample();
            
            Profiler.BeginSample("Subscribe");
            foreach (var system in AppEvent<ISnapshotSubscribe>.GetObjEvents())
            {
                system.SubscribeSystem();
            }
            Profiler.EndSample();
            
            m_BufferList.Clear();
            
            Profiler.BeginSample("ISnapshotManageForClient");
            foreach (var system in AppEvent<ISnapshotManageForClient>.GetObjEvents())
            {
                m_BufferList.Add(system.WriteData(receiver, runtime, ref jobHandle));
            }
            Profiler.EndSample();

            return new SnapshotGeneration()
            {
                Data      = buffer,
                JobHandle = jobHandle,
                Runtime   = runtime
            };
        }

        public void CompleteGeneration(SnapshotGeneration generation)
        {
            Profiler.BeginSample("Complete Jobs");
            generation.JobHandle.Complete();
            Profiler.EndSample();
            
            Profiler.BeginSample("Join buffers");
            foreach (var systemBuffer in m_BufferList)
            {
                //buffer.WriteStatic(systemBuffer);
                
                systemBuffer.Dispose();
            }
            Profiler.EndSample();
            
            //Debug.Log("Final length: " + buffer.Length + ", Expected: " + (UnsafeUtility.SizeOf<SnapshotEntityDataTransformSystem.State>() * 2 + UnsafeUtility.SizeOf<SnapshotEntityDataVelocitySystem.State>() * 2));

            generation.Dispose();
        }
    }
}