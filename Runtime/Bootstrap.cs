using package.stormium.core;
using package.stormiumteam.networking.runtime.highlevel;
using package.stormiumteam.shared;
using package.stormiumteam.shared.modding;
using Scripts;
using Stormium.Core.Networking;
using Unity.Entities;

namespace Stormium.Core
{
    public class Bootstrap : CModBootstrap
    {
        protected override void OnRegister()
        {
            var mainWorld = World.Active;
            var em = mainWorld.GetOrCreateManager<EntityManager>();
            
            var physicSettings = mainWorld.GetOrCreateManager<CPhysicSettings>();
            
            KnowPhysicGroups.Initialize(physicSettings);
            
            // Create a local client
            em.CreateEntity(typeof(ClientTag), typeof(StormiumClient), typeof(SimulateEntity));
            
            // Create a timer
            em.CreateEntity(typeof(GameTimeComponent), typeof(SimulateEntity));
        }

        protected override void OnUnregister()
        {
            
        }
    }
}