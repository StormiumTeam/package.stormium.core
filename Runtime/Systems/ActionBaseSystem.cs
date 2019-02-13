using System.Collections.Generic;
using Stormium.Core;

namespace Runtime.Systems
{
	public abstract class ActionBaseSystem<TSpawnRequest> : GameBaseSystem
		where TSpawnRequest : struct
	{
		protected List<TSpawnRequest> SpawnRequests;
		
		protected override void OnCreateManager()
		{
			base.OnCreateManager();
			
			SpawnRequests = new List<TSpawnRequest>();
		}

		protected abstract void OnActionUpdate();
		protected abstract void FinalizeSpawnRequests();

		protected override void OnUpdate()
		{
			SpawnRequests.Clear();
			
			OnActionUpdate();
			FinalizeSpawnRequests();
		}
	}
}