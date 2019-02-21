using package.stormiumteam.networking.runtime.highlevel;
using Unity.Entities;
using UnityEngine.Experimental.PlayerLoop;

namespace Stormium.Core
{
    public static class STUpdateOrder
    {
	    [UpdateAfter(typeof(UpdateLoop.IntEnd))]
	    public class UO_EventManager : BarrierSystem
	    {
	    }
	    
	    [UpdateAfter(typeof(UO_EventManager))]
	    public class UO_Input : BarrierSystem
	    {}
	    
	    [UpdateAfter(typeof(UO_Input))]
	    public class UO_ActionGrabInputs : BarrierSystem
	    {}

	    [UpdateAfter(typeof(UO_ActionGrabInputs))]
        public class UO_BeginData : BarrierSystem
        {}
        
        [UpdateAfter(typeof(UO_BeginData))]
        public class UO_CharacterBehavior : BarrierSystem
        {}
        
        [UpdateAfter(typeof(UO_CharacterBehavior))]
        public class UO_ActionBehavior : BarrierSystem
        {}

        [UpdateAfter(typeof(UO_ActionBehavior))]
        public class UO_ProjectileBehavior : BarrierSystem
        {
        }
        
        [UpdateAfter(typeof(UO_ProjectileBehavior))]
        public class UO_FinalizeData : BarrierSystem
        {}
        
        [UpdateAfter(typeof(UO_FinalizeData))]
        public class UO_GameMode : BarrierSystem
        {}
    }
}
