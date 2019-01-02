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

    public interface ISnapshotGenerateLocal : ISnapshotEventObject
    {
        void GenerateLocal(DataBufferWriter data);
    }

    public interface ISnapshotLocalManageSingleEntityData : ISnapshotEventObject
    {
        void LocalWriteDataSingle(Entity worldTarget, Entity snapshotTarget, DataBufferWriter data);
        void LocalReadDataSingle(Entity worldTarget, Entity snapshotTarget, DataBufferReader data);
    }
    
    public interface ISnapshotLocalManageJobEntityData : ISnapshotEventObject
    {
        void LocalWriteDataJob(NativeArray<Entity> entityArray, ref JobHandle jobHandle);
        void LocalReadDataJob(NativeArray<Entity> entityArray, DataBufferReader data, ref JobHandle jobHandle);
    }

    public interface ISnapshotNetworkManageEntityData : ISnapshotEventObject
    {
        bool NetworkWriteFromLocalData();
        
        void NetworkWriteData(Entity worldTarget, Entity snapshotTarget, DataBufferWriter data);
        void NetworkReadData(Entity worldTarget, Entity snapshotTarget, DataBufferReader data);
    }
}