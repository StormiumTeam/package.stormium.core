using package.stormiumteam.networking.runtime.lowlevel;
using Unity.Collections;
using Unity.Entities;

namespace Stormium.Core.Networking.SnapshotDataMgr
{
    public interface IEntityDataMgr
    {
        NativeArray<Entity> Entities { get; set; }

        void Read(SnapshotSender sender, DataBufferReader data);
        void Write(SnapshotReceiver receiver, DataBufferWriter data);
    }
    
    public struct DefaultEntityDataManager : IEntityDataMgr
    {
        public NativeArray<Entity> Entities { get; set; }

        public DefaultEntityDataManager(NativeArray<Entity> entities)
        {
            Entities = entities;
        }

        public void SetEntities(NativeArray<Entity> entities)
        {
            Entities = entities;
        }
        
        public void Read(SnapshotSender sender, DataBufferReader data)
        {
            
        }

        public void Write(SnapshotReceiver receiver, DataBufferWriter data)
        {
            
        }
    }
}