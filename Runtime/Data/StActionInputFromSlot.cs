using System;
using package.stormium.core;
using Stormium.Core;
using Unity.Entities;
using UnityEngine;

namespace Stormium.Core
{
	public struct StActionInputFromSlot : IComponentData
	{
		private byte m_ActiveFlags;

		public bool IsActive
		{
			get => Convert.ToBoolean(m_ActiveFlags);
			set => m_ActiveFlags = Convert.ToByte(value);
		}

		public StActionInputFromSlot(bool active)
		{
			m_ActiveFlags = Convert.ToByte(active);
		}
	}

	[UpdateInGroup(typeof(STUpdateOrder.UO_ActionGrabInputs))]
	public class StActionInputFromSlotUpdateSystem : ComponentSystem
	{
		protected override void OnUpdate()
		{
			ForEach((ref StActionInputFromSlot data, ref StActionSlot actionSlot, ref StActionOwner owner) =>
			{
				if (!owner.TargetsValid())
					throw new InvalidOperationException($"(invalid) lt={owner.LivableTarget} it={owner.InputTarget}");

				var target = owner.InputTarget;
				
				if (!EntityManager.HasComponent<ActionUserCommand>(target))
				{
					Debug.Log(target);
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