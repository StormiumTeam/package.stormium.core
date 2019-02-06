using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using package.stormiumteam.networking;
using package.stormiumteam.networking.extensions.NetEcs;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace StormiumShared.Core.Networking
{
    public interface IStateData
    {
    }

    public abstract unsafe class SnapshotDataStreamerBase : ComponentSystem, ISnapshotSubscribe, ISnapshotManageForClient
    {
        protected PatternResult PatternResult;
        
        protected override void OnCreateManager()
        {
            World.GetOrCreateManager<AppEventSystem>().SubscribeToAll(this);
            PatternResult = World.GetOrCreateManager<NetPatternSystem>()
                        .GetLocalBank()
                        .Register(new PatternIdent($"auto." + GetType().Namespace + "." + GetType().Name));
        }

        public PatternResult GetSystemPattern()
        {
            return PatternResult;
        }

        public virtual void SubscribeSystem()
        {
        }

        public abstract DataBufferWriter WriteData(SnapshotReceiver receiver, StSnapshotRuntime runtime, ref JobHandle jobHandle);

        public abstract void ReadData(SnapshotSender sender, StSnapshotRuntime runtime, DataBufferReader sysData, ref JobHandle jobHandle);

        protected void GetDataAndEntityLength(StSnapshotRuntime runtime, out DataBufferWriter data, out int entityLength, int desiredDataLength = 0)
        {
            entityLength = runtime.Entities.Length;
            data = new DataBufferWriter(math.max(desiredDataLength, 1024 + entityLength * 4 * sizeof(Entity)), Allocator.TempJob);
        }

        protected void GetEntityLength(StSnapshotRuntime runtime, out int entityLength)
        {
            entityLength = runtime.Entities.Length;
        }
        
        protected override void OnUpdate()
        {
        }
    }
}