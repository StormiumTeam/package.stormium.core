using System;
using System.IO;
using package.stormiumteam.networking;
using package.stormiumteam.networking.runtime.highlevel;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Runtime.Data;
using StormiumShared.Core.Networking;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Experimental.Input;

namespace Stormium.Core
{
    public struct BasicUserCommand : IStateData, IComponentData
    {
        public struct WritePayload : IWriteEntityDataPayload
        {
            public ComponentDataFromEntity<BasicUserCommand> States;
            public ComponentDataFromEntity<StGamePlayer>     Players;

            public void Write(int index, Entity entity, DataBufferWriter data, SnapshotReceiver receiver, StSnapshotRuntime runtime)
            {
                data.WriteValue(States[entity]);
            }
        }

        public struct ReadPayload : IReadEntityDataPayload
        {
            public EntityManager EntityManager;

            public void Read(int index, Entity entity, ref DataBufferReader data, SnapshotSender sender, StSnapshotRuntime runtime)
            {
                var value = data.ReadValue<BasicUserCommand>();
                // If the entity is attached to a player (in all cases) and if it's our own player, we don't set the new data.
                if (EntityManager.HasComponent<StGamePlayer>(entity))
                {
                    if (EntityManager.GetComponentData<StGamePlayer>(entity).IsSelf == 1)
                        return;
                }

                EntityManager.SetComponentData(entity, value);
            }
        }

        public class Streamer : SnapshotEntityDataManualStreamer<BasicUserCommand, WritePayload, ReadPayload>
        {
            protected override void UpdatePayloadW(ref WritePayload current)
            {
                current.States  = States;
                current.Players = GetComponentDataFromEntity<StGamePlayer>();
            }

            protected override void UpdatePayloadR(ref ReadPayload current)
            {
                current.EntityManager = EntityManager;
            }
        }

        private const int JumpBitIndex  = 0;
        private const int DodgeBitIndex = 1;

        public float2 Move;
        public float2 Look;
        public byte   ControlMask;

        public bool Jump
        {
            get => (ControlMask & 1 << JumpBitIndex) != 0;
            set => ControlMask = MainBit.SetBitAt(ControlMask, JumpBitIndex, Convert.ToByte(value));
        }

        public bool Dodge
        {
            get => (ControlMask & 1 << DodgeBitIndex) != 0;
            set => ControlMask = MainBit.SetBitAt(ControlMask, DodgeBitIndex, Convert.ToByte(value));
        }

        public BasicUserCommand(IntPtr ctx)
        {
            Move        = float2.zero;
            Look        = float2.zero;
            ControlMask = 0;
        }

        public void SetControlMask(bool jump, bool dodge)
        {
            var bJump  = jump ? (byte) 1 : (byte) 0;
            var bDodge = dodge ? (byte) 1 : (byte) 0;

            ControlMask = (byte) (bJump | bDodge << 1);
        }
    }

    public class BasicUserCommandUpdateLocal : ComponentSystem
    {
        private InputActionAsset m_Asset;
        private InputActionMap   m_InputMap;
        private InputAction      m_MoveAction, m_LookAction, m_JumpAction, m_DodgeAction;

        private BasicUserCommand m_ActualCommand;

        private PatternResult m_SyncCommandId;

        protected override void OnCreateManager()
        {
            var file = File.ReadAllText(Application.streamingAssetsPath + "/basic_input.json");

            m_Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            m_Asset.LoadFromJson(file);

            Refresh();

            m_SyncCommandId = World.GetOrCreateManager<NetPatternSystem>().GetLocalBank().Register("000SyncBasicUserCommand");
        }

        protected override unsafe void OnUpdate()
        {
            m_ActualCommand.Look = GetNewAimLook(m_ActualCommand.Look, new float2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")));

            ForEach((ref StGamePlayer player, ref BasicUserCommand command) =>
            {
                if (player.IsSelf == 0)
                    return;

                command = m_ActualCommand;
            });

            // Send or receive the inputs
            ForEach((Entity entity, ref NetworkInstanceData data) =>
            {
                var patternSystem = World.GetExistingManager<NetPatternSystem>();
                var networkMgr    = World.GetExistingManager<NetworkManager>();

                if (data.InstanceType == InstanceType.Server)
                {
                    var buffer = BufferHelper.CreateFromPattern(m_SyncCommandId.Id);
                    buffer.WriteRef(ref m_ActualCommand);

                    data.Commands.Send(buffer, default, Delivery.Unreliable);
                    buffer.Dispose();
                }

                if (!data.IsLocal())
                    return;

                var evBuffer = EntityManager.GetBuffer<EventBuffer>(entity);

                for (var i = 0; i != evBuffer.Length; i++)
                {
                    var ev = evBuffer[i].Event;
                    if (ev.Type != NetworkEventType.DataReceived)
                        continue;

                    var foreignEntity = networkMgr.GetNetworkInstanceEntity(ev.Invoker.Id);
                    var exchange      = patternSystem.GetLocalExchange(ev.Invoker.Id);
                    var buffer        = BufferHelper.ReadEventAndGetPattern(ev, exchange, out var patternId);

                    if (patternId != m_SyncCommandId.Id
                        || !EntityManager.HasComponent<NetworkInstanceToClient>(foreignEntity))
                        continue;

                    var clientEntity = EntityManager.GetComponentData<NetworkInstanceToClient>(foreignEntity).Target;
                    if (!EntityManager.HasComponent<StNetworkClientToGamePlayer>(clientEntity))
                        continue;

                    var userCommand  = buffer.ReadValue<BasicUserCommand>();
                    var playerEntity = EntityManager.GetComponentData<StNetworkClientToGamePlayer>(clientEntity).Target;

                    EntityManager.SetComponentData(playerEntity, userCommand);
                }
            });
        }

        private void Refresh()
        {
            m_InputMap = m_Asset.TryGetActionMap("Map");
            if (m_InputMap == null)
                throw new Exception("InputActionMap 'Map' not found.");

            m_MoveAction = m_InputMap.TryGetAction("Move");
            if (m_MoveAction == null)
                throw new Exception("InputAction 'Move' not found");

            m_LookAction = m_InputMap.TryGetAction("Look");
            if (m_MoveAction == null)
                throw new Exception("InputAction 'Look' not found");

            m_JumpAction  = m_InputMap.TryGetAction("Jump");
            m_DodgeAction = m_InputMap.TryGetAction("Dodge");

            m_Asset.Enable();

            m_MoveAction.performed += (cc) => { m_ActualCommand.Move = cc.ReadValue<Vector2>(); };
            m_LookAction.performed += (cc) =>
            {
                // weird af in builds
                //m_ActualCommand.Look = GetNewAimLook(m_ActualCommand.Look, cc.ReadValue<Vector2>());
            };
            m_JumpAction.performed  += (cc) => { m_ActualCommand.Jump  = cc.ReadValue<float>() > 0.5f; };
            m_DodgeAction.performed += (cc) => { m_ActualCommand.Dodge = cc.ReadValue<float>() > 0.5f; };
        }

        private float2 GetNewAimLook(float2 previous, float2 next)
        {
            var input = next * 1.5f;

            var newRotation = previous + input;
            newRotation.x = newRotation.x % 360;
            newRotation.y = Mathf.Clamp(newRotation.y, -89f, 89f);

            return newRotation;
        }
    }
}