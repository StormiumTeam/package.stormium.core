using package.stormiumteam.shared;
using UnityEngine;

namespace Stormium.Core
{
    public static class Constants
    {
        public const int CollisionMask = (1 << SolidShape.HitLayer) | (1 << CustomShape.HitLayer);
    }
}