using package.stormiumteam.shared;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine.Experimental.PlayerLoop;

namespace Runtime
{
	[UpdateAfter(typeof(PreLateUpdate)), UpdateBefore(typeof(CopyTransformToGameObjectSystem))]
	public class ForceInternalSystemAtLateUpdate : ComponentSystem
	{
		protected override void OnUpdate()
		{
			
		} 
	}
}