using System;
using package.stormiumteam.networking.extensions.NetEcs;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Random = UnityEngine.Random;

namespace Scripts.Network
{
    public class SnapshotTransformSystem : JobSnapshotStreamerState<SnapshotTransformSystem.State>
    {
        public struct State : IStateData, IComponentData
        {
            public float3     Position;
            public quaternion Rotation;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            return inputDeps;
        }
    }
}