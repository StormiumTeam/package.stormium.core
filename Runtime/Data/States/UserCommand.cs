using System;
using System.IO;
using package.stormiumteam.shared;
using Revolution.NetCode;
using StormiumTeam.GameBase;
using StormiumTeam.GameBase.Systems;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stormium.Core
{
    public struct UserCommand : ICommandData<UserCommand>
    {
        public struct ActionMask
        {
            private byte m_InternalByte;

            public bool IsActive
            {
                get => MainBit.GetBitAt(m_InternalByte, 0) == 1;
                set => MainBit.SetBitAt(ref m_InternalByte, 0, value);
            }

            public bool FrameUpdate
            {
                get => MainBit.GetBitAt(m_InternalByte, 1) == 1;
                set => MainBit.SetBitAt(ref m_InternalByte, 1, value);
            }

            public bool WasPressed  => IsActive && FrameUpdate;
            public bool WasReleased => !IsActive && FrameUpdate;
        }

        public const int MaxActionCount = 4;

        public uint Tick { get; set; }

        public float2 Move;
        public float2 Look;

        public bool Jump;
        public bool Dodge;

        public bool Reload;

        [NativeDisableUnsafePtrRestriction]
        public unsafe fixed byte Action[MaxActionCount];

        public unsafe ref ActionMask GetAction(int action)
        {
            if (action >= MaxActionCount)
                throw new IndexOutOfRangeException();

            return ref *(ActionMask*) Action[action];
        }

        public unsafe void WriteTo(DataStreamWriter writer)
        {
            writer.Write(Move.x);
            writer.Write(Move.y);
            writer.Write(Look.x);
            writer.Write(Look.y);

            var mask    = new byte();
            var maskPos = 0;
            MainBit.SetBitAt(ref mask, maskPos++, Jump);
            MainBit.SetBitAt(ref mask, maskPos++, Dodge);
            MainBit.SetBitAt(ref mask, maskPos++, Reload);

            writer.Write(mask);

            for (var ac = 0; ac != MaxActionCount; ac++)
            {
                writer.Write(Action[ac]);
            }
        }

        public unsafe void ReadFrom(DataStreamReader reader, ref DataStreamReader.Context ctx)
        {
            Move.x = reader.ReadFloat(ref ctx);
            Move.y = reader.ReadFloat(ref ctx);
            Look.x = reader.ReadFloat(ref ctx);
            Look.y = reader.ReadFloat(ref ctx);

            var mask    = reader.ReadByte(ref ctx);
            var maskPos = 0;
            Jump   = MainBit.GetBitAt(mask, maskPos++) == 1; // jump
            Dodge  = MainBit.GetBitAt(mask, maskPos++) == 1;
            Reload = MainBit.GetBitAt(mask, maskPos++) == 1;

            for (var ac = 0; ac != MaxActionCount; ac++)
            {
                Action[ac] = reader.ReadByte(ref ctx);
            }
        }
    }

    public struct GamePlayerUserCommand : IComponentData
    {
        public float2 Move;
        public float2 Look;

        public bool QueueJump;
        public bool IsJumping;
        public bool QueueDodge;
        public bool IsDodging;

        public bool QueueReload;
        public bool IsReloading;
    }

    [InternalBufferCapacity(UserCommand.MaxActionCount)]
    public struct GamePlayerActionCommand : IBufferElementData
    {
        public byte Data;
        
        public bool IsActive => MainBit.GetBitAt(Data, 0) == 1;
    }
    
    [UpdateInGroup(typeof(ServerSimulationSystemGroup))]
    [UpdateAfter(typeof(CommandSendSystem))]
    public class BasicUserCommandUpdateServer : JobGameBaseSystem
    {
        [RequireComponentTag(typeof(GamePlayerReadyTag), typeof(UserCommand))]
        private struct JobSetGamePlayerUserCommand : IJobForEachWithEntity<GamePlayer, GamePlayerUserCommand>
        {
            [ReadOnly]
            public BufferFromEntity<UserCommand> userCommandFromEntity;

            [NativeDisableContainerSafetyRestriction]
            public BufferFromEntity<GamePlayerActionCommand> gamePlayerActionCommandFromEntity;

            public uint targetTick;

            public unsafe void Execute(Entity entity, int index, ref GamePlayer gp, ref GamePlayerUserCommand gamePlayerCommand)
            {
                gamePlayerCommand.QueueJump   = false;
                gamePlayerCommand.QueueDodge  = false;
                gamePlayerCommand.QueueReload = false;

                if (!userCommandFromEntity[entity].GetDataAtTick(targetTick, out var userCommand))
                    return;

                if (!gamePlayerCommand.IsJumping && userCommand.Jump)
                    gamePlayerCommand.QueueJump = true;

                if (!gamePlayerCommand.IsDodging && userCommand.Dodge)
                    gamePlayerCommand.QueueDodge = true;

                if (!gamePlayerCommand.IsReloading && userCommand.Reload)
                    gamePlayerCommand.QueueReload = true;

                gamePlayerCommand.Look        = userCommand.Look;
                gamePlayerCommand.Move        = userCommand.Move;
                gamePlayerCommand.IsJumping   = userCommand.Jump;
                gamePlayerCommand.IsDodging   = userCommand.Dodge;
                gamePlayerCommand.IsReloading = userCommand.Reload;

                var actionCommand = gamePlayerActionCommandFromEntity[entity];
                actionCommand.Clear();

                for (var ac = 0; ac != UserCommand.MaxActionCount; ac++)
                {
                    actionCommand.Add(new GamePlayerActionCommand {Data = userCommand.Action[ac]});
                }
            }
        }

        private ServerSimulationSystemGroup m_ServerSimulationSystemGroup;
        protected override void OnCreate()
        {
            base.OnCreate();
            
            m_ServerSimulationSystemGroup = World.GetOrCreateSystem<ServerSimulationSystemGroup>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            return new JobSetGamePlayerUserCommand
            {
                userCommandFromEntity             = GetBufferFromEntity<UserCommand>(),
                gamePlayerActionCommandFromEntity = GetBufferFromEntity<GamePlayerActionCommand>(),
                targetTick                        = m_ServerSimulationSystemGroup.ServerTick
            }.Schedule(this, inputDeps);
        }
    }

    [UpdateInGroup(typeof(ClientPresentationSystemGroup))]
    public class BasicUserCommandUpdateLocal : JobSyncInputSystem
    {
        private InputActionAsset m_Asset;
        private InputAction      m_MoveAction, m_LookAction, m_JumpAction, m_DodgeAction;
        private InputAction[]    m_Actions;

        private UserCommand m_ActualCommand;

        [ExcludeComponent(typeof(NetworkStreamDisconnected))]
        private struct JobAddCommand : IJobForEachWithEntity_EBC<UserCommand, CommandTargetComponent>
        {
            public UserCommand userCommand;

            public void Execute(Entity entity, int index, DynamicBuffer<UserCommand> inputs, [ReadOnly] ref CommandTargetComponent state)
            {
                inputs.Add(userCommand);
            }
        }

        protected override void OnCreate()
        {
            var file = File.ReadAllText(Application.streamingAssetsPath + "/basic_input.json");

            m_Actions = new InputAction[UserCommand.MaxActionCount];

            m_Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            m_Asset.LoadFromJson(file);

            Refresh();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            m_Asset.Disable();
        }

        protected override void OnAssetRefresh()
        {
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var networkTimeSystem = World.GetExistingSystem<NetworkTimeSystem>();
            foreach (var ev in InputEvents)
            {
                if (ev.action == m_MoveAction)
                    m_ActualCommand.Move = ev.ReadValue<Vector2>();
                if (ev.action == m_JumpAction)
                    m_ActualCommand.Jump = ev.ReadValue<float>() > 0.5f;
                if (ev.action == m_DodgeAction)
                    m_ActualCommand.Dodge = ev.ReadValue<float>() > 0.5f;

                for (var ac = 0; ac != UserCommand.MaxActionCount; ac++)
                {
                    if (ev.action != m_Actions[ac])
                        continue;

                    var data = 0;
                    MainBit.SetBitAt(ref data, 0, ev.ReadValue<float>() > 0.5f);
                }
            }

            m_ActualCommand.Look = GetNewAimLook(m_ActualCommand.Look, new float2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")));
            m_ActualCommand.Tick = networkTimeSystem.predictTargetTick;

            inputDeps = new JobAddCommand
            {
                userCommand = m_ActualCommand
            }.ScheduleSingle(this, inputDeps);

            return inputDeps;
        }

        private void Refresh()
        {
            var inputMap = m_Asset.FindActionMap("Map");
            if (inputMap == null)
                throw new Exception("InputActionMap 'Map' not found.");

            AddActionEvents(m_MoveAction  = inputMap.FindAction("Move"));
            AddActionEvents(m_LookAction  = inputMap.FindAction("Look"));
            AddActionEvents(m_JumpAction  = inputMap.FindAction("Jump"));
            AddActionEvents(m_DodgeAction = inputMap.FindAction("Dodge"));

            for (var ac = 0; ac != UserCommand.MaxActionCount; ac++)
            {
                var action = inputMap.FindAction("Action" + ac);
                AddActionEvents(m_Actions[ac] = action);
            }
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