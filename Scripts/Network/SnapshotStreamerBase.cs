using System;
using System.Collections.Generic;
using package.stormiumteam.networking.extensions.NetEcs;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Scripts.Network
{
    public abstract class SnapshotStreamerBase : IDisposable
    {
        protected abstract void Dispose(bool disposing);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    public struct DeltaChange
    {
        public bool HasChanged;
    }

    public abstract class StNetSnapshotComponentStreamerBase : SnapshotStreamerBase
    {
        private FastDictionary<long, DataBufferWriter> m_CachedEntityBuffer;

        internal unsafe DeltaChange InternalWriteEntityDelta(DataBufferWriter snapshotBuffer, Entity entity)
        {
            DataBufferWriter cachedBuffer = default;
            DeltaChange      deltaChange  = default;

            // entity is just ID + Version, we can easily convert this to a long integer.
            var key = *(long*) &entity;
            if (!m_CachedEntityBuffer.RefFastTryGet(key, ref cachedBuffer))
            {
                cachedBuffer = new DataBufferWriter(Allocator.Persistent);

                m_CachedEntityBuffer[key] = cachedBuffer;
            }

            var start = snapshotBuffer.Length;
            WriteEntity(snapshotBuffer, entity);
            var deltaLength = snapshotBuffer.Length - start;

            if (cachedBuffer.Length != deltaLength)
            {
                deltaChange.HasChanged = true;
            }
            else
            {
                var cachedPtr = (void*)cachedBuffer.GetSafePtr();
                var snapshotPtr = (void*)(snapshotBuffer.GetSafePtr() + (start - 1));

                var memCpmResult = UnsafeUtility.MemCmp(cachedPtr, snapshotPtr, deltaLength);
                deltaChange.HasChanged = memCpmResult != 0;
            }

            if (deltaChange.HasChanged)
            {
                var cachedPtr   = (void*)cachedBuffer.GetSafePtr();
                var snapshotPtr = (void*)(snapshotBuffer.GetSafePtr() + (start - 1));
                
                UnsafeUtility.MemCpy(cachedPtr, snapshotPtr, deltaLength);
            }

            return deltaChange;
        }

        protected abstract void WriteEntity(DataBufferWriter data, Entity entity);
        protected abstract void ReadEntity(DataBufferReader data, NetworkEntity networkEntity, Entity entity);

        protected override void Dispose(bool disposing)
        {
            foreach (var entityBuffer in m_CachedEntityBuffer)
            {
                entityBuffer.Value.Dispose();
            }
            
            m_CachedEntityBuffer.Clear();
            m_CachedEntityBuffer = null;
        }
    }

    public abstract class StReplaySnapshotComponentStreamerBase : SnapshotStreamerBase
    {
        
    }
}