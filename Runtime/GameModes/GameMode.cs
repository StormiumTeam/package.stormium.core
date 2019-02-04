using Runtime;
using StormiumShared.Core.Networking;
using Unity.Entities;

namespace package.stormium.core.gamemodes
{
    public abstract class GameModeSystem : ComponentSystem
    {
        public StormiumGameManager GameMgr { get; private set; }
        public StormiumGameServerManager ServerMgr { get; private set; }
        public EntityModelManager EntityModelMgr { get; private set; }

        protected override void OnCreateManager()
        {
            GameMgr = World.GetOrCreateManager<StormiumGameManager>();
            ServerMgr = World.GetOrCreateManager<StormiumGameServerManager>();
            EntityModelMgr = World.GetOrCreateManager<EntityModelManager>();
        }
    }
}