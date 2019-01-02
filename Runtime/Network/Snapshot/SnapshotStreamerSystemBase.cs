using System;
using System.Security.Cryptography.X509Certificates;
using package.stormiumteam.networking;
using package.stormiumteam.networking.extensions.NetEcs;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Stormium.Core.Networking
{
    public interface IStateData
    {
    }

    public abstract class SnapshotEntityDataStreamer<TState> : JobComponentSystem, ISnapshotSubscribe, ISnapshotLocalManageEntityData, ISnapshotNetworkManageEntityData
        where TState : struct, IStateData, IComponentData
    {
        private PatternResult m_SystemPattern;
        
        protected ComponentDataFromEntity<TState> States;

        public PatternResult SystemPattern => m_SystemPattern;
        
        protected override void OnCreateManager()
        {
            World.GetOrCreateManager<AppEventSystem>().SubscribeToAll(this);

            m_SystemPattern = RegisterPattern();
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
            UpdateInjectedComponentGroups();
            return;
        }

        public virtual void LocalWriteData(Entity worldTarget, Entity snapshotTarget, DataBufferWriter data)
        {
            data.CpyWrite(EntityManager.GetComponentData<TState>(worldTarget));
        }

        public virtual void LocalReadData(Entity worldTarget, Entity snapshotTarget, DataBufferReader data)
        {
            EntityManager.SetComponentData(worldTarget, data.ReadValue<TState>());
        }

        public virtual bool NetworkWriteFromLocalData()
        {
            return true;
        }

        public virtual void NetworkWriteData(Entity worldTarget, Entity snapshotTarget, DataBufferWriter data)
        {
            throw new NotImplementedException("Not implemented as NetworkWriteFromLocalData is true.");
        }

        public virtual void NetworkReadData(Entity worldTarget, Entity snapshotTarget, DataBufferReader data)
        {
            throw new NotImplementedException("Not implemented as NetworkWriteFromLocalData is true.");
        }
    }
}