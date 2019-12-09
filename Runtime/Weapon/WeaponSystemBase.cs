using Revolution;
using Stormium.Default.Mixed;
using StormiumTeam.GameBase;
using Unity.Entities;

namespace Stormium.Core.Weapon
{
	public interface IWeaponCreate<TComponent>
	{
		Entity Owner { get; set; }
		Entity Player { get; set; }
	}

	public abstract class WeaponProviderBase<TComponent, TCreate> : BaseProviderBatch<TCreate>
		where TComponent : struct, IComponentData
		where TCreate : struct, IWeaponCreate<TComponent>
	{
		public override void GetComponents(out ComponentType[] entityComponents)
		{
			entityComponents = new ComponentType[]
			{
				typeof(WeaponDescription),
				typeof(TComponent),
				typeof(Owner),
				typeof(Relative<PlayerDescription>),
				typeof(GhostEntity)
			};
		}

		public override void SetEntityData(Entity entity, TCreate data)
		{
			EntityManager.ReplaceOwnerData(entity, data.Owner);
			if (EntityManager.HasComponent<PlayerDescription>(data.Owner))
				data.Player = data.Owner;
			if (data.Player != default)
				EntityManager.SetComponentData(entity, new Relative<PlayerDescription>(data.Player));
		}
	}
	
	public abstract class WeaponJobSystemBase<TWeapon> : JobGameBaseSystem 
		where TWeapon : struct
	{
		protected abstract void Register(Entity desc);
	}

	public abstract class WeaponSystemBase<TWeapon> : GameBaseSystem 
		where TWeapon : struct
	{
		protected abstract void Register(Entity desc);
	}
}