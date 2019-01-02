using package.stormiumteam.shared.online;
using Unity.Entities;

namespace package.stormiumteam.shared
{
    public class GamePlayerSystem : ComponentSystem
    {
        public struct AllPlayers
        {
            public ComponentDataArray<PlayerEntityTag> PlayersTag;
            public EntityArray                         Entities;

            public readonly int Length;
        }
        [Inject] private AllPlayers m_AllPlayers;

        public struct ConnectedPlayers
        {
            public ComponentDataArray<PlayerEntityTag>       PlayersTag;
            public ComponentDataArray<ConnectedPlayerEntity> ConnectedPlayersTag;
            public EntityArray                               Entities;

            public readonly int Length;
        }
        [Inject] private ConnectedPlayers m_ConnectedPlayers;

        protected override void OnUpdate()
        {

        }

        public ConnectedPlayers SlowGetAllConnectedPlayers()
        {
            // It's slow because of this. (but it's not really slow)
            UpdateInjectedComponentGroups();
            
            return m_ConnectedPlayers;
        }

        public AllPlayers SlowGetAllPlayers()
        {
            // It's slow because of this. (but it's not really slow)
            UpdateInjectedComponentGroups();
            
            return m_AllPlayers;
        }
    }
}