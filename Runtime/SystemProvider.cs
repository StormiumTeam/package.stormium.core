using package.stormiumteam.networking;
using StormiumShared.Core.Networking;
using Unity.Entities;

namespace Runtime
{
    public abstract class SystemProvider : ComponentSystem
    {
        private EntityModelManager m_ModelManager;
        private ModelIdent m_ModelIdent;

        protected override void OnCreateManager()
        {
            GetManager();
        }

        protected override void OnUpdate()
        {
            
        }

        public EntityModelManager GetManager()
        {
            if (m_ModelManager == null)
            {
                m_ModelManager = World.GetOrCreateManager<EntityModelManager>();
                m_ModelIdent = m_ModelManager.Register($"EntityProvider.{GetType().Name}", SpawnEntity, DestroyEntity);
            }

            return m_ModelManager;
        }

        public ModelIdent GetModelIdent()
        {
            return m_ModelIdent;
        }

        public abstract Entity SpawnEntity(Entity origin, StSnapshotRuntime snapshotRuntime);

        public abstract void DestroyEntity(Entity worldEntity);
    }
}