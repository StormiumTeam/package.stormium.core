using Unity.Entities;
using Unity.Mathematics;

namespace Stormium.Core.Data
{
	public struct ForceSpawnPosition : IComponentData
	{
		public float3 Value;
	}
}