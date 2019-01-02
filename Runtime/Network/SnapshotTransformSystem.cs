using System;
using package.stormiumteam.networking.extensions.NetEcs;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Random = UnityEngine.Random;

namespace Stormium.Core.Networking
{
    public class SnapshotEntityDataTransformSystem : SnapshotEntityDataStreamer<SnapshotEntityDataTransformSystem.State>
    {
        public struct State : IStateData, IComponentData
        {
            public float3     Position;
            public quaternion Rotation;
        }
    }
    
    public class SnapshotEntityDataVelocitySystem : SnapshotEntityDataStreamer<SnapshotEntityDataVelocitySystem.State>
    {
        public struct State : IStateData, IComponentData
        {
            public float3 velocity;
            public float3 angularVelocity;
            public float3 friction;
            public quaternion time0;
            public quaternion time1;
            public quaternion time2;
            public quaternion time3;
            public float mass;
        }
    }
}