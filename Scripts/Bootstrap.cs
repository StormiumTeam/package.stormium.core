using package.stormiumteam.shared;
using package.stormiumteam.shared.modding;
using Scripts;
using Unity.Entities;

namespace package.stormium.core
{
    public class Bootstrap : CModBootstrap
    {
        protected override void OnRegister()
        {
            var mainWorld = World.Active;
            var physicSettings = mainWorld.GetOrCreateManager<CPhysicSettings>();
            
            KnowPhysicGroups.Initialize(physicSettings);
        }

        protected override void OnUnregister()
        {
            
        }
    }
}