using package.stormiumteam.networking.extensions.NetEcs;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Entities;
using UnityEngine;

namespace Scripts.Network
{
    public interface IStateData
    {
    }

    public abstract class SnapshotStreamerState<TState> : ComponentSystem, IStSnapshotProcessComponentEntity
        where TState : struct, IStateData, IComponentData
    {
        private class Streamer : EntityComponentSnapshotStreamer
        {
            public ComponentDataFromEntity<TState> States;
            
            protected override void WriteEntity(DataBufferWriter data, Entity entity)
            {
                data.CpyWrite(States[entity]);
            }

            protected override void ReadEntity(DataBufferReader data, NetworkEntity networkEntity, Entity entity)
            {
                States[entity] = data.ReadValue<TState>();
            }

            protected override bool EntityValid(Entity entity)
            {
                return States.Exists(entity);
            }
        }

        private readonly Streamer m_Streamer;

        protected ComponentDataFromEntity<TState> States;

        protected SnapshotStreamerState()
        {
            m_Streamer = new Streamer();
            World.GetOrCreateManager<AppEventSystem>().SubscribeToAll(this);
        }

        protected override void OnUpdate()
        {
        }

        public EntityComponentSnapshotStreamer Get()
        {
            States = GetComponentDataFromEntity<TState>();

            m_Streamer.States = States;
            
            return m_Streamer;
        }
    }

    public abstract class JobSnapshotStreamerState<TState> : JobComponentSystem, INetworkDeltaSnapshotPrepareEntityData, INetworkDeltaSnapshotWriteData
        where TState : struct, IStateData, IComponentData
    {
        private class Streamer : EntityComponentSnapshotStreamer
        {
            public ComponentDataFromEntity<TState>  States;
            public JobSnapshotStreamerState<TState> Ctx;

            protected override void WriteEntity(DataBufferWriter data, Entity entity)
            {
                var state = States[entity];
                Ctx.NetworkRewrite(entity, ref state);
                data.CpyWrite(States[entity]);
            }

            protected override void ReadEntity(DataBufferReader data, NetworkEntity networkEntity, Entity entity)
            {
                var state = data.ReadValue<TState>();
                Ctx.NetworkReread(networkEntity, entity, ref state);
                States[entity] = state;
            }

            protected override bool EntityValid(Entity entity)
            {
                return States.Exists(entity);
            }
        }

        private Streamer m_Streamer;

        protected ComponentDataFromEntity<TState> States;

        protected override void OnCreateManager()
        {
            m_Streamer = new Streamer();
            World.GetOrCreateManager<AppEventSystem>().SubscribeToAll(this);
        }

        public EntityComponentSnapshotStreamer PrepareEntityData()
        {
            States = GetComponentDataFromEntity<TState>();

            m_Streamer.States = States;
            m_Streamer.Ctx    = this;

            return m_Streamer;
        }

        public virtual bool NetworkRewrite(Entity target, ref TState state) => false;

        public virtual bool NetworkReread(NetworkEntity source, Entity target, ref TState state) => false;
    }
}