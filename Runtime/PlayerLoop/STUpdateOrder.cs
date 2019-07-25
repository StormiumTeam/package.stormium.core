using StormiumTeam.GameBase;
using Unity.Entities;
using Unity.Transforms;

namespace Stormium.Core
{
    public static class STUpdateOrder
    {
	    public class UO_EventManager : ComponentSystemGroup
	    {
	    }
	    
	    [UpdateAfter(typeof(UO_EventManager))]
	    public class UO_Input : ComponentSystemGroup
	    {}
	    
	    [UpdateAfter(typeof(UO_Input))]
	    public class UO_ActionGrabInputs : ComponentSystemGroup
	    {}

	    [UpdateAfter(typeof(UO_ActionGrabInputs))]
        public class UO_BeginData : ComponentSystemGroup
        {}
        
        [UpdateAfter(typeof(UO_BeginData))]
        public class UO_CharacterBehavior : ComponentSystemGroup
        {}
        
        [UpdateAfter(typeof(UO_CharacterBehavior)), UpdateAfter(typeof(TransformSystemGroup)), UpdateAfter(typeof(LivableSystemGroup))]
        public class UO_FinalizeData : ComponentSystemGroup
        {}
        
        [UpdateAfter(typeof(UO_FinalizeData))]
        public class UO_GameMode : ComponentSystemGroup
        {}
    }
}
