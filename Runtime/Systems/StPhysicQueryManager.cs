using System.Collections.Generic;
using package.stormiumteam.shared;
using Unity.Entities;

namespace Stormium.Core
{
	interface IOnQueryEnableCollisionFor : IAppEvent
	{
		bool EnableCollisionFor(Entity entity);

		void EnableCollision();
		void DisableCollision();
	}
	
	
	
	public class StPhysicQueryManager : ComponentSystem
	{
		private List<IOnQueryEnableCollisionFor> m_ReenableCollisions;

		protected override void OnCreateManager()
		{
			m_ReenableCollisions = new List<IOnQueryEnableCollisionFor>();
		}

		protected override void OnUpdate()
		{
			
		}

		public void EnableCollisionFor(Entity entity)
		{
			ReenableCollisions();
			
			m_ReenableCollisions.Clear();
			
			foreach (var obj in AppEvent<IOnQueryEnableCollisionFor>.GetObjEvents())
			{
				if (!obj.EnableCollisionFor(entity))
				{
					obj.DisableCollision();
					continue;
				}

				obj.EnableCollision();
				m_ReenableCollisions.Add(obj);
			}
		}

		public void ReenableCollisions()
		{
			if (m_ReenableCollisions.Count <= 0)
				return;

			foreach (var obj in m_ReenableCollisions)
			{
				obj.EnableCollision();
			}
		}
	}
}