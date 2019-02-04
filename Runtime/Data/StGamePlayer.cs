using System;
using package.stormiumteam.networking.runtime.lowlevel;
using StormiumShared.Core.Networking;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Runtime.Data
{
    public struct StGamePlayer : IComponentData
    {
        public ulong MasterServerId;
        public byte  IsSelf;

        public StGamePlayer(ulong masterServerId, bool isSelf)
        {
            MasterServerId = masterServerId;
            IsSelf        = isSelf ? (byte) 1 : (byte) 0;
        }
    }

    public struct StGamePlayerToNetworkClient : IComponentData
    {
        /// <summary>
        /// This variable should not be synced between connections and need to be assigned locally.
        /// This hold a target to the server client entity.
        /// </summary>
        public Entity Target;

        public StGamePlayerToNetworkClient(Entity target)
        {
            Target = target;
        }
    }

    public struct StNetworkClientToGamePlayer : IComponentData
    {
        public Entity Target;

        public StNetworkClientToGamePlayer(Entity target)
        {
            Target = target;
        }
    }

    public class StGamePlayerStreamer : SnapshotDataStreamerBase
    {
        private ComponentDataFromEntity<StGamePlayer> m_PlayerArray;
        private ComponentDataFromEntity<StGamePlayerToNetworkClient> m_PlayerToNClientArray;

        public override void SubscribeSystem()
        {
            m_PlayerArray = GetComponentDataFromEntity<StGamePlayer>();
            m_PlayerToNClientArray = GetComponentDataFromEntity<StGamePlayerToNetworkClient>();
        }

        public override DataBufferWriter WriteData(SnapshotReceiver receiver, StSnapshotRuntime runtime, ref JobHandle jobHandle)
        {
            GetDataAndEntityLength(runtime, out var data, out var length);

            for (var i = 0; i != length; i++)
            {
                var entity = runtime.Entities[i].Source;
                if (!m_PlayerArray.Exists(entity))
                    continue;

                data.CpyWrite(m_PlayerArray[entity]);

                if (m_PlayerToNClientArray.Exists(entity))
                    // Is the user owned from the same client? (1 = yes, 0 = no)
                    data.CpyWrite(m_PlayerToNClientArray[entity].Target == receiver.Client ? (byte) 1 : (byte) 0);
                else
                    data.CpyWrite((byte) 0);
            }

            return data;
        }

        public override void ReadData(SnapshotSender sender, StSnapshotRuntime runtime, DataBufferReader sysData, ref JobHandle jobHandle)
        {
            GetEntityLength(runtime, out var length);

            for (var i = 0; i != length; i++)
            {
                var entity = runtime.GetWorldEntityFromGlobal(i);
                if (!m_PlayerArray.Exists(entity))
                    continue;

                var state = sysData.ReadValue<StGamePlayer>();
                state.IsSelf = sysData.ReadValue<byte>();

                m_PlayerArray[entity] = state;
            }
        }
    }

    public class StGamePlayerProvider : SystemProvider
    {
        public override Entity SpawnEntity(Entity origin, StSnapshotRuntime snapshotRuntime)
        {
            return EntityManager.CreateEntity
            (
                ComponentType.Create<StGamePlayer>(),
                ComponentType.Create<ModelIdent>(),
                ComponentType.Create<GenerateEntitySnapshot>()
            );
        }

        public override void DestroyEntity(Entity worldEntity)
        {
            // should we also destroy attached modules?
            EntityManager.DestroyEntity(worldEntity);
        }
    }
}