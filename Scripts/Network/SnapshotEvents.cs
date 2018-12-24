using package.stormiumteam.shared;
using Unity.Collections;
using Unity.Entities;

namespace Scripts.Network
{
    public interface INetworkDeltaSnapshotPrepareEntityData : IAppEvent
    {
        EntityComponentSnapshotStreamer PrepareEntityData();
    }

    public interface INetworkDeltaSnapshotWriteData : IAppEvent
    {
        
    }
}