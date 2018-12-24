using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using package.stormiumteam.networking.extensions.NetEcs;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

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
        public DataBufferWriter Data;
    }

    public abstract class NetworkEntityDataSnapshotDeltaStreamer : SnapshotStreamerBase
    {
        private FastDictionary<long, byte[]> m_CachedEntityBuffer = new FastDictionary<long, byte[]>();
        private DataBufferWriter m_CachedBuffer = new DataBufferWriter(new NativeList<byte>(Allocator.Persistent));

        public unsafe void InternalPrepareData()
        {
            PrepareData();
        }
        
        public unsafe DeltaChange InternalWriteEntityDelta(Entity entity)
        {
            var hasChanged = false;
            var key = *(long*) &entity;
            
            m_CachedBuffer.Buffer.Clear();
            
            WriteEntity(m_CachedBuffer, entity);

            byte[] dataArray = default;
            if (!m_CachedEntityBuffer.RefFastTryGet(key, ref dataArray))
            {
                dataArray = new byte[m_CachedBuffer.Length];
                m_CachedEntityBuffer[key] = dataArray;
            }
            else if (dataArray.Length != m_CachedBuffer.Length)
            {
                hasChanged = true;
            }
            else fixed (byte* dataBuffer = dataArray)
            {
                var cachedPtr = (byte*) m_CachedBuffer.GetSafePtr();
                hasChanged = UnsafeUtility.MemCmp(dataBuffer, cachedPtr, m_CachedBuffer.Length) != 0;
            }

            return new DeltaChange {HasChanged = hasChanged, Data = m_CachedBuffer};
        }

        /*public unsafe DeltaChange InternalWriteEntityDelta(DataBufferWriter snapshotBuffer, Entity entity)
        {
            byte[] cachedBuffer = default;
            DeltaChange      deltaChange  = default;

            // entity is just ID + Version, we can easily convert this to a long integer.
            var key = *(long*) &entity;
            if (!m_CachedEntityBuffer.RefFastTryGet(key, ref cachedBuffer))
            {
                deltaChange.HasChanged = true;
            }

            var start = snapshotBuffer.Length;
            WriteEntity(snapshotBuffer, entity);
            var deltaLength = snapshotBuffer.Length - start;

            Debug.Log($"{cachedBuffer.Length} != {deltaLength}");
            if (cachedBuffer.Length != deltaLength)
            {
                deltaChange.HasChanged = true;
            }
            else
            {    
                var cachedPtr = (byte*)cachedBuffer.GetSafePtr();
                var snapshotPtr = (byte*)(snapshotBuffer.GetSafePtr() + (start));

                for (int i = 0; i != deltaLength; i++)
                {
                    if (cachedPtr[i] != snapshotPtr[i])
                    {
                        Debug.Log($"{i} -> {cachedPtr[i]} != {snapshotPtr[i]}");   
                    }
                }

                var memCpmResult = UnsafeUtility.MemCmp(cachedPtr, snapshotPtr, deltaLength);
                deltaChange.HasChanged = memCpmResult != 0;
            }

            if (deltaChange.HasChanged)
            {
                var cachedPtr   = (void*)cachedBuffer.GetSafePtr();
                var snapshotPtr = (void*)(snapshotBuffer.GetSafePtr() + (start));
                
                cachedBuffer.TryResize(deltaLength);
                
                m_CachedEntityBuffer[key] = cachedBuffer;
                
                UnsafeUtility.MemCpy(cachedPtr, snapshotPtr, deltaLength);
            }

            return deltaChange;
        }*/

        protected abstract void PrepareData(NativeMultiHashMap<Entity, byte> previousBuffer);

        protected abstract void WriteEntity(DataBufferWriter data, Entity entity);
        protected abstract void ReadEntity(DataBufferReader data, NetworkEntity networkEntity, Entity entity);
        protected abstract bool EntityValid(Entity entity);

        protected override void Dispose(bool disposing)
        {
            m_CachedEntityBuffer.Clear();
            m_CachedEntityBuffer = null;
        }
    }

    public abstract class StReplaySnapshotComponentStreamerBase : SnapshotStreamerBase
    {
        
    }
}