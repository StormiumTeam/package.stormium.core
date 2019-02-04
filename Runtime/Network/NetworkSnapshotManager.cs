using System;
using System.Collections.Generic;
using package.stormiumteam.networking;
using package.stormiumteam.networking.lz4;
using package.stormiumteam.networking.runtime.highlevel;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Runtime;
using StormiumShared.Core.Networking;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Stormium.Core.Networking
{
    public unsafe class NetworkSnapshotManager : ComponentSystem, INativeEventOnGUI
    {
        public struct ClientSnapshotState : ISystemStateComponentData
        {
        }

        private struct SnapshotDataToApply
        {
            public SnapshotSender      Sender;
            public int TotalDataSize;
            public bool IsCompressed;
            public DataBufferReader    Data;
            public PatternBankExchange Exchange;
        }

        private PatternResult m_SnapshotPattern;

        private ComponentGroup m_ClientWithoutState;
        private ComponentGroup m_DestroyedClientWithState;
        private ComponentGroup m_EntitiesToGenerate;

        private StSnapshotRuntime m_CurrentRuntime;

        private Dictionary<Entity, StSnapshotRuntime> m_ClientRuntimes;
        private List<SnapshotDataToApply>             m_SnapshotDataToApply;

        private float m_AvgSnapshotSize;
        private float m_AvgSnapshotSizeCompressed;
        private int   m_ReceivedSnapshotOnFrame;

        protected override void OnCreateManager()
        {
            m_ClientRuntimes      = new Dictionary<Entity, StSnapshotRuntime>(16);
            m_SnapshotDataToApply = new List<SnapshotDataToApply>();
        }

        protected override void OnStartRunning()
        {
            m_SnapshotPattern = World.GetExistingManager<NetPatternSystem>()
                                     .GetLocalBank()
                                     .Register(new PatternIdent("SyncSnapshot"));

            m_ClientWithoutState       = GetComponentGroup(typeof(NetworkClient), ComponentType.Subtractive<ClientSnapshotState>());
            m_DestroyedClientWithState = GetComponentGroup(typeof(ClientSnapshotState), ComponentType.Subtractive<NetworkClient>());
            m_EntitiesToGenerate       = GetComponentGroup(typeof(GenerateEntitySnapshot));

            m_CurrentRuntime = new StSnapshotRuntime(default, Allocator.Persistent);

            World.GetExistingManager<AppEventSystem>().SubscribeToAll(this);
        }

        protected override void OnUpdate()
        {
            // We need to complete all jobs that are writing to entity components.
            EntityManager.CompleteAllJobs();
            m_SnapshotDataToApply.Clear();

            var gameTime         = World.GetExistingManager<StGameTimeManager>().GetTimeFromSingleton();
            var snapshotMgr      = World.GetExistingManager<SnapshotManager>();
            var networkMgr       = World.GetExistingManager<NetworkManager>();
            var gameMgr          = World.GetExistingManager<StormiumGameManager>();
            var netPatternSystem = World.GetExistingManager<NetPatternSystem>();

            var localClient = gameMgr.Client;

            // Receive data from server
            ForEach((DynamicBuffer<EventBuffer> eventBuffer, ref NetworkInstanceData data) =>
            {
                var exchange = netPatternSystem.GetLocalExchange(data.Id);
                var bank     = netPatternSystem.GetBank(data.Id);

                for (int i = 0; i != eventBuffer.Length; i++)
                {
                    var ev = eventBuffer[i].Event;

                    if (ev.Type != NetworkEventType.DataReceived)
                        continue;

                    var reader  = new DataBufferReader(ev.Data, ev.DataLength);
                    var msgType = reader.ReadValue<MessageType>();

                    if (msgType != MessageType.MessagePattern)
                        continue;

                    var patternId = reader.ReadValue<int>();
                    if (m_SnapshotPattern != exchange.GetOriginId(patternId))
                        continue;

                    var size = reader.ReadValue<int>();
                    var isCompressed = reader.ReadValue<bool>();
                    
                    //Debug.Log($"[{Time.frameCount}] Received from {data.ParentId} (s={size}, c={isCompressed})");

                    m_SnapshotDataToApply.Add(new SnapshotDataToApply
                    {
                        Sender        = new SnapshotSender {Client = networkMgr.GetNetworkInstanceEntity(ev.Invoker.Id), Flags = SnapshotFlags.None},
                        TotalDataSize = size,
                        IsCompressed = isCompressed,
                        Data          = new DataBufferReader(reader, reader.CurrReadIndex, reader.Length),
                        Exchange      = exchange
                    });
                }
            });

            m_ReceivedSnapshotOnFrame = 0;

            foreach (var value in m_SnapshotDataToApply)
            {
                var dataSize       = value.TotalDataSize;
                var compressedData = value.Data;
                var isCompressed = value.IsCompressed;

                DataBufferReader data = default;
                
                // Decompress data...
                if (!isCompressed)
                {
                    data = value.Data;

                    m_CurrentRuntime = snapshotMgr.ApplySnapshotFromData(value.Sender, ref data, ref m_CurrentRuntime, value.Exchange);
                    
                    m_AvgSnapshotSize = Mathf.Lerp(m_AvgSnapshotSize, data.Length, 0.5f);
                    m_ReceivedSnapshotOnFrame++;
                    
                    continue;
                }
                
                using (var decompressed = new UnsafeAllocationLength<byte>(Allocator.Temp, dataSize))
                {
                    var decompressedLength = Lz4Wrapper.Decompress(compressedData.DataPtr, (byte*) decompressed.Data, compressedData.Length, dataSize);
                    /*if (decompressedLength != dataSize)
                        throw new Exception($"{decompressedLength} != {dataSize}");*/
                    
                    data = new DataBufferReader((byte*) decompressed.Data, dataSize);

                    m_CurrentRuntime = snapshotMgr.ApplySnapshotFromData(value.Sender, ref data, ref m_CurrentRuntime, value.Exchange);

                    m_AvgSnapshotSize = Mathf.Lerp(m_AvgSnapshotSize, dataSize, 0.5f);
                    m_AvgSnapshotSizeCompressed = Mathf.Lerp(m_AvgSnapshotSizeCompressed, compressedData.Length, 0.5f);
                    m_ReceivedSnapshotOnFrame++;

                }
            }

            using (var entityArray = m_ClientWithoutState.ToEntityArray(Allocator.TempJob))
            {
                foreach (var e in entityArray)
                {
                    m_ClientRuntimes[e] = new StSnapshotRuntime(default, Allocator.Persistent);

                    EntityManager.AddComponent(e, ComponentType.Create<ClientSnapshotState>());
                }
            }

            using (var entityArray = m_DestroyedClientWithState.ToEntityArray(Allocator.TempJob))
            {
                foreach (var e in entityArray)
                {
                    m_ClientRuntimes.Remove(e);

                    EntityManager.RemoveComponent(e, ComponentType.Create<ClientSnapshotState>());
                }
            }

            using (var entities = m_EntitiesToGenerate.ToEntityArray(Allocator.TempJob))
            {
                // Send data to clients
                ForEach((ref NetworkInstanceData networkInstanceData, ref NetworkInstanceToClient networkToClient) =>
                {
                    if (networkInstanceData.InstanceType != InstanceType.Client)
                        return;

                    var clientEntity  = networkToClient.Target;
                    var clientRuntime = m_ClientRuntimes[clientEntity];

                    var data = new DataBufferWriter(Allocator.TempJob);

                    data.CpyWrite(MessageType.MessagePattern);
                    data.CpyWrite(m_SnapshotPattern.Id);

                    var genData = new DataBufferWriter(Allocator.Persistent);
                    var generation = snapshotMgr.GenerateForConnection(localClient, clientEntity, entities, true, gameTime, Allocator.Persistent, ref genData, ref clientRuntime);

                    // Write the length of uncompressed data.
                    data.CpyWrite(generation.Data.Length);

                    var isCompressed = generation.Data.Length > 128;
                    
                    // Write an information about if the data is compressed or not
                    data.Write(ref isCompressed);

                    var compressedLength = 0;
                    // If it should not be compressed, we write the data directly
                    if (!isCompressed)
                    {
                        data.WriteStatic(generation.Data);
                    }
                    // If it is, we compress the data
                    else
                    {
                        using (var compressed = new UnsafeAllocationLength<byte>(Allocator.Temp, generation.Data.Length))
                        {
                            var uncompressedData = (byte*) generation.Data.GetSafePtr();
                            compressedLength = Lz4Wrapper.Compress(uncompressedData, generation.Data.Length, (byte*) compressed.Data);
                            data.WriteDataSafe((byte*) compressed.Data, compressedLength, default);
                        }
                    }

                    networkInstanceData.Commands.Send(data, default, Delivery.Reliable);

                    m_AvgSnapshotSize = Mathf.Lerp(m_AvgSnapshotSize, generation.Data.Length, 0.5f);
                    m_AvgSnapshotSizeCompressed = Mathf.Lerp(m_AvgSnapshotSizeCompressed, compressedLength, 0.5f);
                    
                    genData.Dispose();
                    data.Dispose();

                    m_ClientRuntimes[clientEntity] = clientRuntime;
                });
            }
        }

        public void NativeOnGUI()
        {
            using (new GUILayout.VerticalScope())
            {
                GUI.color = Color.black;
                GUILayout.Label("Snapshot System:");
                GUILayout.Space(1);
                GUILayout.Label($"Avg Snapshot Size={m_AvgSnapshotSize:F0}B (c={m_AvgSnapshotSizeCompressed:F0}B)");
                GUILayout.Label($"Frame Snapshot Count={m_ReceivedSnapshotOnFrame}");
            }
        }
    }
}