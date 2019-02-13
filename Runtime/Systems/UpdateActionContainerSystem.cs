using package.stormium.core;
using Unity.Entities;
using UnityEngine;

namespace Stormium.Core
{
	[UpdateInGroup(typeof(STUpdateOrder.UO_FinalizeData))]
	public class UpdateActionContainerSystem : ComponentSystem
	{
		protected override void OnUpdate()
		{
			// First clear buffers...
			ForEach((DynamicBuffer<StActionContainer> buffer) =>
			{
				buffer.Clear();
			});
			
			ForEach((Entity entity, ref StActionTag actionTag, ref StActionOwner owner) =>
			{
				if (!EntityManager.Exists(owner.LivableTarget))
				{
					Debug.LogError("Owner doesn't exist anymore.");
					return;
				}
				
				if (!EntityManager.HasComponent(owner.LivableTarget, typeof(StActionContainer)))
				{
					Debug.LogWarning("Owner don't have an action container.");
					return;
				}

				// TODO: This can corrupt game state.
				var newBuffer = PostUpdateCommands.SetBuffer<StActionContainer>(owner.LivableTarget);
				var oldBuffer = EntityManager.GetBuffer<StActionContainer>(owner.LivableTarget);
				
				newBuffer.CopyFrom(oldBuffer.AsNativeArray());
				newBuffer.Add(new StActionContainer(entity));
			});
		}
	}
}