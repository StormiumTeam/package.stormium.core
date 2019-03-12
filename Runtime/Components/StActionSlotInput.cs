using System;
using package.StormiumTeam.GameBase;
using StormiumTeam.GameBase;
using Unity.Entities;
using UnityEngine;

namespace Stormium.Core
{
	public struct StActionSlotInput : IComponentData
	{
		public byte Flags;

		public bool IsActive
		{
			get => Convert.ToBoolean(Flags);
			set => Flags = Convert.ToByte(value);
		}

		public StActionSlotInput(bool active)
		{
			Flags = Convert.ToByte(active);
		}
	}

	public class StActionInputFromSlotUpdateSystem : ComponentSystem
	{
		protected override void OnUpdate()
		{
			ForEach((ref StActionSlotInput data, ref ActionSlot actionSlot, ref OwnerState<PlayerDescription> player) =>
			{
				var target = player.Target;
				
				if (!EntityManager.HasComponent<ActionUserCommand>(target))
				{
					Debug.Log("Has no ActionUserCommand: " + target);
					return;
				}

				var fireBuffer = EntityManager.GetBuffer<ActionUserCommand>(target);
				if (actionSlot.Value >= fireBuffer.Length)
					return;
				
				var fireState  = fireBuffer[actionSlot.Value];

				data.IsActive = fireState.IsActive;
			});
		}
	}
}