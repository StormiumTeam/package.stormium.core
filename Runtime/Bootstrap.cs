using package.stormium.core;
using package.stormiumteam.networking.runtime.highlevel;
using package.stormiumteam.shared;
using package.stormiumteam.shared.modding;
using Scripts;
using StormiumShared.Core.Networking;
using Unity.Entities;

namespace Stormium.Core
{
    public class Bootstrap : CModBootstrap
    {
        protected override void OnRegister()
        {
            var mainWorld = World.Active;
            var em = mainWorld.GetOrCreateManager<EntityManager>();
            
            // Create a local client
            em.CreateEntity(typeof(ClientTag), typeof(NetworkClient), typeof(EntityAuthority));
            
            // Create a timer
            em.CreateEntity(typeof(GameTimeComponent), typeof(EntityAuthority));
        }

        protected override void OnUnregister()
        {
            
        }
    }
}