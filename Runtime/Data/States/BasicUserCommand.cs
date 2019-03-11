using System;
using System.IO;
using package.stormiumteam.networking;
using package.stormiumteam.networking.runtime.highlevel;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using StormiumShared.Core.Networking;
using StormiumTeam.GameBase;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Input;

namespace Stormium.Core
{
    public struct BasicUserCommand : IStateData, IComponentData
    {
        public struct WritePayload : IWriteEntityDataPayload<BasicUserCommand>
        {
            public ComponentDataFromEntity<GamePlayer>     Players;

            public void Write(int index, Entity entity, ComponentDataFromEntity<BasicUserCommand> stateFromEntity, ComponentDataFromEntity<DataChanged<BasicUserCommand>> changeFromEntity, DataBufferWriter data, SnapshotReceiver receiver, SnapshotRuntime runtime)
            {
                data.WriteUnmanaged(stateFromEntity[entity]);
            }
        }

        public struct ReadPayload : IReadEntityDataPayload<BasicUserCommand>
        {
            public EntityManager EntityManager;

            public void Read(int index, Entity entity, ComponentDataFromEntity<BasicUserCommand> dataFromEntity, ref DataBufferReader data, SnapshotSender sender, SnapshotRuntime runtime)
            {
                var value = data.ReadValue<BasicUserCommand>();
                // If the entity is attached to a player (in all cases) and if it's our own player, we don't set the new data.
                if (EntityManager.HasComponent<GamePlayer>(entity))
                {
                    if (EntityManager.GetComponentData<GamePlayer>(entity).IsSelf == 1)
                        return;
                }

                dataFromEntity[entity] = value;
            }
        }

        public class Streamer : SnapshotEntityDataManualStreamer<BasicUserCommand, WritePayload, ReadPayload>
        {
            protected override void UpdatePayloadW(ref WritePayload current)
            {
                current.Players = GetComponentDataFromEntity<GamePlayer>();
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
            set => MainBit.SetBitAt(ref ControlMask, JumpBitIndex, Convert.ToByte(value));
        }

        public int TapJumpFrame;

        public bool Dodge
        {
            get => (ControlMask & 1 << DodgeBitIndex) != 0;
            set => MainBit.SetBitAt(ref ControlMask, DodgeBitIndex, Convert.ToByte(value));
        }

        public int TapDodgeFrame;

        public BasicUserCommand(IntPtr ctx)
        {
            Move        = float2.zero;
            Look        = float2.zero;
            ControlMask = 0;
            TapJumpFrame = 0;
            TapDodgeFrame = 0;
        }

        public void SetControlMask(bool jump, bool dodge)
        {
            var bJump  = jump ? (byte) 1 : (byte) 0;
            var bDodge = dodge ? (byte) 1 : (byte) 0;

            ControlMask = (byte) (bJump | bDodge << 1);
        }
    }

    public class BasicUserCommandUpdateLocal : BaseComponentSystem
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

            m_SyncCommandId = World.GetExistingManager<NetPatternSystem>().GetLocalBank().Register("000SyncBasicUserCommand");
        }

        protected override unsafe void OnUpdate()
        {
            m_ActualCommand.Look = GetNewAimLook(m_ActualCommand.Look, new float2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")));

            ForEach((ref GamePlayer player, ref BasicUserCommand command) =>
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
                    buffer.WriteValue(m_ActualCommand.ControlMask);
                    buffer.WriteValue(m_ActualCommand.Move);
                    buffer.WriteValue(m_ActualCommand.Look);
                    buffer.WriteValue(m_ActualCommand.TapJumpFrame == Time.frameCount);
                    buffer.WriteValue(m_ActualCommand.TapDodgeFrame == Time.frameCount);

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
                    if (!EntityManager.HasComponent<NetworkClientToGamePlayer>(clientEntity))
                        continue;

                    var playerEntity = EntityManager.GetComponentData<NetworkClientToGamePlayer>(clientEntity).Target;
                    var userCommand = EntityManager.GetComponentData<BasicUserCommand>(playerEntity);
                    userCommand.ControlMask = buffer.ReadValue<byte>();
                    userCommand.Move = buffer.ReadValue<float2>();
                    userCommand.Look = buffer.ReadValue<float2>();
                    var jump = buffer.ReadValue<bool>();
                    var dodge = buffer.ReadValue<bool>();
                    if (jump) 
                        userCommand.TapJumpFrame = Time.frameCount;
                    if (dodge) 
                        userCommand.TapDodgeFrame = Time.frameCount;
                    
                    
                    EntityManager.SetComponentData(playerEntity, userCommand);
                }
            });
        }

        private double m_lastJumpTime, m_lastDodgeTime;
        
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
            m_JumpAction.performed += (cc) =>
            {                
                m_ActualCommand.Jump = cc.ReadValue<float>() > 0.5f;
                
                if (m_ActualCommand.Jump)
                    m_ActualCommand.TapJumpFrame = Time.frameCount;
            };
            m_DodgeAction.performed += (cc) =>
            {                
                m_ActualCommand.Dodge = cc.ReadValue<float>() > 0.5f;

                if (m_ActualCommand.Dodge)
                    m_ActualCommand.TapDodgeFrame = Time.frameCount;
            };
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