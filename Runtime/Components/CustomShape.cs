using UnityEngine;

namespace Stormium.Core
{
	[ExecuteInEditMode]
	public class CustomShape : MonoBehaviour
	{
		public const int HitLayer = 21;
		
		public void OnEnable()
		{
			gameObject.layer = HitLayer;
		}
	}
}