using package.stormiumteam.networking;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Jobs;

namespace Stormium.Core.Networking.SnapshotDataMgr
{
    public interface ISystemDataMgr
    {
        void Read(SnapshotSender sender, ref DataBufferReader data);
        void Write(SnapshotReceiver receiver, ref DataBufferWriter data);
    }
    
    public struct DefaultSystemDataManager : ISystemDataMgr
    {
        public PatternBank PatternBank;

        public DefaultSystemDataManager(PatternBank patternBank)
        {
            PatternBank = patternBank;
        }

        public void Read(SnapshotSender sender, ref DataBufferReader data)
        {
            
        }

        public void Write(SnapshotReceiver receiver, ref DataBufferWriter data)
        {
            var preInitSystems = AppEvent<ISnapshotSubscribe>.GetObjEvents();
            foreach (var obj in preInitSystems)
            {
                obj.SubscribeSystem();
            }

            JobHandle jobHandle = default;
            
            var systemsMfc = AppEvent<ISnapshotManageForClient>.GetObjEvents();
            foreach (var obj in systemsMfc)
            {
                var pattern = obj.GetSystemPattern();
                var sysBuffer = obj.WriteData(receiver, default, ref jobHandle);

                data.Write(data.Length + sysBuffer.Length);
                data.Write(pattern.Id);
                data.WriteStatic(sysBuffer);
                
                jobHandle.Complete();
            }
        }
    }
}