using StormiumTeam.GameBase;
using Unity.Entities;
using UnityEngine;

namespace Stormium.Core
{
	public struct StActionSlotInput : IComponentData
	{
		public bool IsActive;

		public StActionSlotInput(bool active)
		{
			IsActive = active;
		}
	}

	public class StActionInputFromSlotUpdateSystem : ComponentSystem
	{
		protected override void OnUpdate()
		{
			Entities.ForEach((ref StActionSlotInput data, ref ActionSlot actionSlot, ref Relative<PlayerDescription> player) =>
			{
				var target = player	.Target;
				
				if (!EntityManager.HasComponent<GamePlayerActionCommand>(target))
				{
					Debug.Log("Has no ActionUserCommand: " + target);
					return;
				}

				var fireBuffer = EntityManager.GetBuffer<GamePlayerActionCommand>(target);
				if (actionSlot.Value >= fireBuffer.Length)
					return;
				
				var fireState  = fireBuffer[actionSlot.Value];

				data.IsActive = fireState.IsActive;
			});
		}
	}
}