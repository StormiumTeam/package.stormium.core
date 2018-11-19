using package.stormiumteam.shared.online;
using Unity.Entities;

namespace package.stormium.core
{
    public struct ClientDriveData<T> : IComponentData 
        where T : IComponentData
    {
        public ClientDriveData(GamePlayer client)
        {
            
        }
    }
}