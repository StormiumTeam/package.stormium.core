using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using package.stormiumteam.networking;
using package.stormiumteam.networking.extensions.NetEcs;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Stormium.Core.Networking
{
    public struct SnapshotDeltaBuffer
    {
        public NativeHashMap<Entity, IntPtr> DataArray;
        public NativeHashMap<Entity, byte> ChangeArray;
    }

    public struct SnapshotDelta
    {
        
    }
}