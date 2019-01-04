using System;
using package.stormiumteam.networking.extensions.NetEcs;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
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
            public unsafe fixed double matrix[2];
        }
    }
}