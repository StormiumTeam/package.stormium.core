using StormiumShared.Core.Networking;
using Unity.Entities;

namespace Runtime
{
    public enum GameType
    {
        Client = 0,
        Server = 1
    }

    public class StormiumGameManager : ComponentSystem
    {
        public GameType GameType => m_GameType;
        private GameType m_GameType;

        public Entity Client => m_Client;
        private Entity m_Client;

        public EntityModelManager EntityModelManager => m_EntityModelManager;
        private EntityModelManager m_EntityModelManager;

        public StormiumGameServerManager ServerManager => m_ServerManager;
        private StormiumGameServerManager m_ServerManager;
        
        protected override void OnCreateManager()
        {
            m_Client = EntityManager.CreateEntity
            (
                typeof(NetworkClient),
                typeof(NetworkLocalTag)
            );

            m_EntityModelManager = World.GetOrCreateManager<EntityModelManager>();
            m_ServerMgr = World.GetOrCreateManager<StormiumGameServerManager>();
        }

        protected override void OnUpdate()
        {

        }

        public Entity SpawnLocal(ModelIdent ident)
        {
            var fakeLocalRuntime = default(StSnapshotRuntime);
            
            fakeLocalRuntime.Header.Sender = new SnapshotSender(m_Client, SnapshotFlags.Local);
            
            return EntityModelManager.SpawnEntity(ident.Id, default, fakeLocalRuntime);
        }

        public void Unspawn(Entity entity)
        {
            
        }

        public void SetGameAs(GameType gameType)
        {
            m_GameType = gameType;
        }
    }
    
    public class StormiumGameServerManager : ComponentSystem
    {
        protected override void OnUpdate()
        {
            
        }

        public void ConnectToServer()
        {
            
        }
    }
}