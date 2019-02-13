using package.stormiumteam.networking.runtime.lowlevel;
using StormiumShared.Core.Networking;
using Unity.Entities;

namespace Stormium.Core
{
    public struct OwnerToPlayerState : IStateData, IComponentData
    {
        public struct WritePayload : IWriteEntityDataPayload
        {
            public ComponentDataFromEntity<OwnerToPlayerState> States;
            
            public void Write(int index, Entity entity, DataBufferWriter data, SnapshotReceiver receiver, StSnapshotRuntime runtime)
            {
                var state = States[entity];

                data.WriteRef(ref state.Target);   
            }
        }

        public struct ReadPayload : IReadEntityDataPayload
        {
            public EntityManager EntityManager;
            
            public void Read(int index, Entity entity, ref DataBufferReader data, SnapshotSender sender, StSnapshotRuntime runtime)
            {
                var worldTarget = runtime.EntityToWorld(data.ReadValue<Entity>());

                EntityManager.SetComponentData(entity, new OwnerToPlayerState {Target = worldTarget});
            }
        }
        
        public class Streamer : SnapshotEntityDataManualStreamer<OwnerToPlayerState, WritePayload, ReadPayload>
        {
            protected override void UpdatePayloadW(ref WritePayload current)
            {
                current.States = States;
            }

            protected override void UpdatePayloadR(ref ReadPayload current)
            {
                current.EntityManager = EntityManager;
            }
        }

        public Entity Target;
    }
    
    public struct OwnerToLivableState : IStateData, IComponentData
    {
        public struct WritePayload : IWriteEntityDataPayload
        {
            public ComponentDataFromEntity<OwnerToLivableState> States;
            
            public void Write(int index, Entity entity, DataBufferWriter data, SnapshotReceiver receiver, StSnapshotRuntime runtime)
            {
                var state = States[entity];

                data.WriteRef(ref state.Target);   
            }
        }

        public struct ReadPayload : IReadEntityDataPayload
        {
            public EntityManager EntityManager;
            
            public void Read(int index, Entity entity, ref DataBufferReader data, SnapshotSender sender, StSnapshotRuntime runtime)
            {
                var worldTarget = runtime.EntityToWorld(data.ReadValue<Entity>());

                EntityManager.SetComponentData(entity, new OwnerToLivableState {Target = worldTarget});
            }
        }
        
        public class Streamer : SnapshotEntityDataManualStreamer<OwnerToLivableState, WritePayload, ReadPayload>
        {
            protected override void UpdatePayloadW(ref WritePayload current)
            {
                current.States = States;
            }

            protected override void UpdatePayloadR(ref ReadPayload current)
            {
                current.EntityManager = EntityManager;
            }
        }

        public Entity Target;
    }
    
    public struct OwnerToActionState : IStateData, IComponentData
    {
        public struct WritePayload : IWriteEntityDataPayload
        {
            public ComponentDataFromEntity<OwnerToActionState> States;
            
            public void Write(int index, Entity entity, DataBufferWriter data, SnapshotReceiver receiver, StSnapshotRuntime runtime)
            {
                var state = States[entity];

                data.WriteRef(ref state.Target);   
            }
        }

        public struct ReadPayload : IReadEntityDataPayload
        {
            public EntityManager EntityManager;
            
            public void Read(int index, Entity entity, ref DataBufferReader data, SnapshotSender sender, StSnapshotRuntime runtime)
            {
                var worldTarget = runtime.EntityToWorld(data.ReadValue<Entity>());

                EntityManager.SetComponentData(entity, new OwnerToActionState {Target = worldTarget});
            }
        }
        
        public class Streamer : SnapshotEntityDataManualStreamer<OwnerToActionState, WritePayload, ReadPayload>
        {
            protected override void UpdatePayloadW(ref WritePayload current)
            {
                current.States = States;
            }

            protected override void UpdatePayloadR(ref ReadPayload current)
            {
                current.EntityManager = EntityManager;
            }
        }

        public Entity Target;
    }
}