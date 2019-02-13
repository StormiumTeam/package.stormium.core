using UnityEngine;

namespace Stormium.Core
{
	[ExecuteInEditMode]
	public class SolidShape : MonoBehaviour
	{
		public const int HitLayer = 20;
		
		void OnEnable()
		{
			gameObject.layer = HitLayer;
		}
	}
}