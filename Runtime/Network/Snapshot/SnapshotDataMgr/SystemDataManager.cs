using package.stormiumteam.networking;

namespace Stormium.Core.Networking.SnapshotDataMgr
{
    public interface ISystemDataMgr
    {
        void Read();
        void Write();
    }
    
    public struct DefaultSystemDataManager : ISystemDataMgr
    {
        public PatternBank PatternBank;

        public DefaultSystemDataManager(PatternBank patternBank)
        {
            PatternBank = patternBank;
        }
        
        public void Read()
        {
            
        }

        public void Write()
        {
            
        }
    }
}