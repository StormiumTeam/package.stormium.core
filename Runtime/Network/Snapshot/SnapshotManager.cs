using System;
using System.Collections.Generic;
using package.stormiumteam.networking.runtime.highlevel;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;

namespace Stormium.Core.Networking
{
    public class SnapshotManager : ComponentSystem
    {
        private CompiledSnapshot LocalSnapshot;
        
        private ComponentDataFromEntity<NetworkInstanceData> m_NetworkInstanceFromEntity;
        private ComponentGroup m_NetworkClientGroup;
        private ComponentGroup m_GenerateSnapshotEntityGroup;

        protected override void OnCreateManager()
        {
            LocalSnapshot = new CompiledSnapshot
            {
                EntityData = new FastDictionary<Entity, FastDictionary<int, byte[]>>(),
                SystemData = new FastDictionary<int, byte[]>(),
                SystemIds  = new FastDictionary<string, int>(),
                
                TrackedSystems = new NativeList<int>(24, Allocator.Persistent),
                TrackedEntities = new NativeList<Entity>(128, Allocator.Persistent)
            };
            
            m_NetworkInstanceFromEntity = GetComponentDataFromEntity<NetworkInstanceData>();
            m_NetworkClientGroup = GetComponentGroup(typeof(StormiumClient), typeof(ClientToNetworkInstance));
            m_GenerateSnapshotEntityGroup = GetComponentGroup(typeof(GenerateEntitySnapshot));
        }

        protected override void OnUpdate()
        {
            var entityLength = m_GenerateSnapshotEntityGroup.CalculateLength();
            if (entityLength < 0) return;
            
            GenerateLocalSnapshot(ref LocalSnapshot, m_GenerateSnapshotEntityGroup.GetEntityArray());

            var clientLength = m_NetworkClientGroup.CalculateLength();
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
            }
        }

        // STRUCTURE:
        // Write System Data
        // Write Entity Data from Systems
        public void GenerateLocalSnapshot(ref CompiledSnapshot newSnapshot, EntityArray entities)
        {
            var dataBuffer = new DataBufferWriter(Allocator.Temp, 1024);
            
            newSnapshot.EmptyTracked();

            foreach (var obj in AppEvent<ISnapshotSubscribe>.GetObjEvents())
            {
                obj.SubscribeSystem();
                
                var pattern = obj.GetSystemPattern();
                
                newSnapshot.SetSystemPattern(pattern);
                newSnapshot.TrackSystem(pattern.Id);
            }
            
            // Write System Data
            foreach (var obj in AppEvent<ISnapshotGenerateLocal>.GetObjEvents())
            {
                dataBuffer.Buffer.Clear();
                
                var pattern = obj.GetSystemPattern();
                
                obj.GenerateLocal(dataBuffer);
                
                newSnapshot.SetSystemDataFromNativeBuffer(pattern, dataBuffer);
            }
            
            // Write Entity Data
            var entityLength = entities.Length;
            Profiler.BeginSample("Write Entity Data");
            for (int i = 0; i != entityLength; i++)
            {
                var entity = entities[i];
                newSnapshot.TrackEntity(entity);

                foreach (var obj in AppEvent<ISnapshotLocalManageEntityData>.GetObjEvents())
                {
                    dataBuffer.Buffer.Clear();

                    var pattern = obj.GetSystemPattern();
                    
                    Profiler.BeginSample("LocalWriteData");
                    obj.LocalWriteData(entity, entity, dataBuffer);
                    Profiler.EndSample();
                    Profiler.BeginSample("WriteEntityData");
                    newSnapshot.WriteEntityData(pattern, entity, dataBuffer);
                    Profiler.EndSample();
                }
            }
            Profiler.EndSample();
            
            dataBuffer.Buffer.Clear();

            Profiler.BeginSample("Clear useless data");
            newSnapshot.DeleteNonTracked();
            Profiler.EndSample();
        }

        public void GenerateNetworkClientSnapshot(Entity client, ref CompiledSnapshot snapshot, ref DataBufferWriter dataBuffer, ref JobHandle jobHandle)
        {
            foreach (var obj in AppEvent<ISnapshotNetworkManageEntityData>.GetObjEvents())
            {
            }
        }
    }
}