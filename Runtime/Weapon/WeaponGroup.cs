using Stormium.Default.Mixed;
using StormiumTeam.GameBase;
using Unity.Entities;

[assembly: RegisterGenericComponentType(typeof(Relative<WeaponDescription>))]
[assembly: RegisterGenericComponentType(typeof(Relative<WeaponHolderDescription>))]

namespace Stormium.Default.Mixed
{
	public struct WeaponHolderDescription : IEntityDescription
	{
		public class Sync : RelativeSynchronize<WeaponHolderDescription>
		{
		}
	}
	
	public struct WeaponDescription : IEntityDescription
	{
		public class Sync : RelativeSynchronize<WeaponDescription>
		{
		}
	}

	public struct WeaponContainer : IBufferElementData
	{
		public Entity Target;

		public WeaponContainer(Entity target)
		{
			Target = target;
		}
	}
}