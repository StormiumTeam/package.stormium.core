using package.stormiumteam.networking;
using Runtime;
using StormiumShared.Core.Networking;
using Unity.Entities;

namespace Stormium.Core
{
	public abstract class GameBaseSystem : ComponentSystem
	{
		public StormiumGameManager       GameMgr        { get; private set; }
		public StormiumGameServerManager ServerMgr      { get; private set; }
		public EntityModelManager        EntityModelMgr { get; private set; }
		public StGameTimeManager         TimeMgr        { get; private set; }
		public NetPatternSystem          PatternSystem  { get; private set; }

		public int Tick      => TimeMgr.GetTimeFromSingleton().Tick;
		public int TickDelta => TimeMgr.GetTimeFromSingleton().DeltaTick;

		public PatternBank LocalBank => PatternSystem.GetLocalBank();

		protected override void OnCreateManager()
		{
			GameMgr        = World.GetOrCreateManager<StormiumGameManager>();
			ServerMgr      = World.GetOrCreateManager<StormiumGameServerManager>();
			EntityModelMgr = World.GetOrCreateManager<EntityModelManager>();
			TimeMgr        = World.GetOrCreateManager<StGameTimeManager>();
			PatternSystem  = World.GetOrCreateManager<NetPatternSystem>();
		}
	}
}