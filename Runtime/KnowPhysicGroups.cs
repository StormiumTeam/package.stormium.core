using package.stormiumteam.shared;
using UnityEngine;

namespace Stormium.Core
{
    public static class KnowPhysicGroups
    {
        public static CPhysicGroup CharacterGroup { get; private set; }

        internal static void Initialize(CPhysicSettings physicSettings)
        {
            CharacterGroup = physicSettings.CreateOrGetGroup("Character");
            Debug.Log(CharacterGroup.Id);
        }
    }
}