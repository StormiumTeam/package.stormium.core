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
        DataBufferWriter WriteData(SnapshotReceiver receiver, StSnapshotRuntime runtime, ref JobHandle jobHandle);
        void             ReadData(SnapshotSender    sender,   StSnapshotRuntime runtime, DataBufferReader sysData, ref JobHandle jobHandle);
    }
}