using StormiumShared.Core.Networking;
using Unity.Entities;
using UnityEngine;

namespace Runtime.Components
{
	public class CharacterModelPresentation : MonoBehaviour
	{		
		/// <summary>
		/// Used for managing camera, shooting projectiles
		/// </summary>
		[Tooltip("Used for managing camera, shooting projectiles")]
		public float EyeHeight = 1.6f;
		/// <summary>
		/// If there are no weapon model, projectile will be spawned based on this position (visual only)
		/// </summary>
		[Tooltip("If there are no weapon model, projectile will be spawned based on this position (visual only)")]
		public Transform DefaultProjectileSpawn;

		private void OnDrawGizmos()
		{
			var pos = transform.position;

			Gizmos.color = Color.red;

			Gizmos.DrawRay(pos, transform.forward * 2);
			Gizmos.DrawRay(pos + transform.TransformDirection(Vector3.up * EyeHeight), transform.forward * 2);
			
			Gizmos.color = Color.blue;
			
			Gizmos.DrawRay(pos, transform.TransformDirection(Vector3.up * EyeHeight));
			Gizmos.DrawWireSphere(DefaultProjectileSpawn.position, 0.1f);
		}
	}
}