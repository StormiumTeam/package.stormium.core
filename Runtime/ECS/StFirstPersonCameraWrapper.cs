using System;
using Unity.Entities;
using UnityEngine;

namespace package.stormium.core
{
    [Serializable]
    public struct StFirstPersonCamera : ISharedComponentData
    {
        public StFirstPersonCameraInputsBehaviour Inputs;
    }

    public class StFirstPersonCameraWrapper : SharedComponentDataWrapper<StFirstPersonCamera>
    {
        
    }
}