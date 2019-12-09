using Revolution;
using Stormium.Core;
using StormiumTeam.GameBase;
using Unity.Entities;
using Unity.Networking.Transport;

namespace Stormium.Default.Mixed
{
	public struct CurrentWeapon : IReadWriteComponentSnapshot<CurrentWeapon, GhostSetup>
	{
		public struct Exclude : IComponentData
		{
		}

		public Entity Target;

		public void WriteTo(DataStreamWriter writer, ref CurrentWeapon baseline, GhostSetup setup, SerializeClientData jobData)
		{
			writer.WritePackedUInt(setup[Target], jobData.NetworkCompressionModel);
		}

		public void ReadFrom(ref DataStreamReader.Context ctx, DataStreamReader reader, ref CurrentWeapon baseline, DeserializeClientData jobData)
		{
			jobData.GhostToEntityMap.TryGetValue(reader.ReadPackedUInt(ref ctx, jobData.NetworkCompressionModel), out Target);
		}

		public class Sync : MixedComponentSnapshotSystem<CurrentWeapon, GhostSetup>
		{
			public override ComponentType ExcludeComponent => typeof(Exclude);
		}
	}

	public struct WeaponCycle : IComponentData
	{
		public int Index;
	}
	
	[UpdateInGroup(typeof(OrderGroup.Simulation.UpdateEntities.Interaction))]
	public class UpdateWeaponCycle : ComponentSystem
	{
		protected override void OnUpdate()
		{
			Entities.ForEach((ref WeaponCycle cycle, ref Relative<PlayerDescription> relativePlayer) =>
			{
				if (relativePlayer.Target == default)
					return;

				if (!EntityManager.HasComponent<GamePlayerUserCommand>(relativePlayer.Target))
					return;

				var gpUserCommand = EntityManager.GetComponentData<GamePlayerUserCommand>(relativePlayer.Target);
				cycle.Index += gpUserCommand.Scroll;
			});
			
			Entities.ForEach((DynamicBuffer<WeaponContainer> weapons, ref WeaponCycle cycle, ref CurrentWeapon currWeap) =>
			{
				if (weapons.Length == 0)
					return;
				
				if (cycle.Index >= weapons.Length)
					cycle.Index = 0;
				if (cycle.Index < 0)
					cycle.Index = weapons.Length - 1;

				currWeap.Target = weapons[cycle.Index].Target;
			});
		}
	}
}