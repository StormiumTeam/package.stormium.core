using StormiumTeam.GameBase;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Stormium.Core.Data
{
	[InternalBufferCapacity(16)]
	public struct TransformHistory : IBufferElementData
	{
		public float3     Position;
		public quaternion Rotation;
	}

	[UpdateInGroup(typeof(OrderGroup.Simulation.Initialization))]
	public class TransformHistorySystem : JobComponentSystem
	{
		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			return Entities.ForEach((ref DynamicBuffer<TransformHistory> history, in LocalToWorld ltw) =>
			{
				if (history.Length >= history.Capacity)
					history.RemoveAt(history.Length - 1);

				history.Insert(0, new TransformHistory {Position = ltw.Position, Rotation = ltw.Rotation});
			}).Schedule(inputDeps);
		}
	}
}