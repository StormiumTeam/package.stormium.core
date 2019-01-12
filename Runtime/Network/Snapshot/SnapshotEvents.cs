using package.stormiumteam.networking;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Stormium.Core.Networking
{
    public interface ISnapshotEventObject : IAppEvent
    {
        PatternResult GetSystemPattern();
    }

    public interface ISnapshotSubscribe : ISnapshotEventObject
    {
        void SubscribeSystem();
    }

    public interface ISnapshotManageForClient : ISnapshotEventObject
    {
        DataBufferWriter WriteData(SnapshotReceiver receiver, SnapshotRuntime runtime, ref JobHandle jobHandle);
        void             ReadData(SnapshotSender    sender,   SnapshotRuntime runtime, ref JobHandle jobHandle);
    }
}