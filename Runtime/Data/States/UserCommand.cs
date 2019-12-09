using System;
using System.IO;
using package.stormiumteam.shared;
using Unity.NetCode;
using StormiumTeam.GameBase;
using StormiumTeam.GameBase.Systems;
using Unity.Burst;
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
    [InternalBufferCapacity(32)]
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
        public bool Crouch;

        public bool Reload;

        [NativeDisableUnsafePtrRestriction]
        public unsafe fixed byte Action[MaxActionCount];

        public unsafe ref ActionMask GetAction(int action)
        {
            if (action >= MaxActionCount)
                throw new IndexOutOfRangeException();

            return ref *(ActionMask*) Action[action];
        }

        public unsafe void ReadFrom(DataStreamReader reader, ref DataStreamReader.Context ctx, NetworkCompressionModel compressionModel)
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
            Crouch = MainBit.GetBitAt(mask, maskPos++) == 1;

            for (var ac = 0; ac != MaxActionCount; ac++)
            {
                Action[ac] = reader.ReadByte(ref ctx);
            }
        }

        public unsafe void WriteTo(DataStreamWriter writer, NetworkCompressionModel compressionModel)
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
            MainBit.SetBitAt(ref mask, maskPos++, Crouch);

            writer.Write(mask);

            for (var ac = 0; ac != MaxActionCount; ac++)
            {
                writer.Write(Action[ac]);
            }
        }

        public void ReadFrom(DataStreamReader reader, ref DataStreamReader.Context ctx, UserCommand baseline, NetworkCompressionModel compressionModel)
        {
            ReadFrom(reader, ref ctx, compressionModel);
        }

        public void WriteTo(DataStreamWriter writer, UserCommand baseline, NetworkCompressionModel compressionModel)
        {
            WriteTo(writer, compressionModel);
        }
    }

    public struct GamePlayerUserCommand : IComponentData
    {
        public int Scroll;
        public float2 Move;
        public float2 Look;

        public bool QueueJump;
        public bool IsJumping;
        public bool QueueDodge;
        public bool IsDodging;

        public bool QueueCrouch;
        public bool IsCrouching;

        public bool QueueReload;
        public bool IsReloading;
        
        public uint Tick { get; set; }
    }

    [InternalBufferCapacity(UserCommand.MaxActionCount)]
    public struct GamePlayerActionCommand : IBufferElementData
    {
        public byte Data;
        
        public bool IsActive => MainBit.GetBitAt(Data, 0) == 1;
    }
    
    [BurstCompile]
    public struct RpcPlayerScroll : IRpcCommand
    {
        public class RequestSystem : RpcCommandRequestSystem<RpcPlayerScroll>
        {}
        
        public int Value;
        public void Serialize(DataStreamWriter writer)
        {
            writer.Write(Value);
        }

        public void Deserialize(DataStreamReader reader, ref DataStreamReader.Context ctx)
        {
            Value = reader.ReadInt(ref ctx);
        }
        
        [BurstCompile]
        private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
        {
            RpcExecutor.ExecuteCreateRequestComponent<RpcPlayerScroll>(ref parameters);
        }

        public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
        {
            return new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);
        }
    }


    [UpdateInGroup(typeof(GhostPredictionSystemGroup))]
    public class BasicUserCommandUpdatePredicted : ComponentSystem
    {
        protected override void OnUpdate()
        {
            World.GetExistingSystem<BasicUserCommandUpdateServer>().Update();
        }
    }
    
    [UpdateInGroup(typeof(ClientAndServerSimulationSystemGroup))]
    [UpdateAfter(typeof(CommandReceiveSystem))]
    [UpdateAfter(typeof(CommandSendSystem))]
    public class BasicUserCommandUpdateServer : JobGameBaseSystem
    {
        [BurstCompile]
        [RequireComponentTag(typeof(GamePlayerReadyTag), typeof(UserCommand), typeof(WorldOwnedTag))]
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
                gamePlayerCommand.QueueCrouch = false;
                gamePlayerCommand.QueueReload = false;
                gamePlayerCommand.Tick        = 0;

                if (!userCommandFromEntity[entity].GetDataAtTick(targetTick, out var userCommand))
                {
                    return;
                }

                if (!gamePlayerCommand.IsJumping && userCommand.Jump)
                    gamePlayerCommand.QueueJump = true;

                if (!gamePlayerCommand.IsDodging && userCommand.Dodge)
                    gamePlayerCommand.QueueDodge = true;

                if (!gamePlayerCommand.IsCrouching && userCommand.Crouch)
                    gamePlayerCommand.QueueCrouch = true;

                if (!gamePlayerCommand.IsReloading && userCommand.Reload)
                    gamePlayerCommand.QueueReload = true;

                gamePlayerCommand.Tick        = userCommand.Tick;
                gamePlayerCommand.Scroll      = 0;
                gamePlayerCommand.Look        = userCommand.Look;
                gamePlayerCommand.Move        = userCommand.Move;
                gamePlayerCommand.IsJumping   = userCommand.Jump;
                gamePlayerCommand.IsDodging   = userCommand.Dodge;
                gamePlayerCommand.IsCrouching = userCommand.Crouch;
                gamePlayerCommand.IsReloading = userCommand.Reload;

                var actionCommand = gamePlayerActionCommandFromEntity[entity];
                actionCommand.Clear();

                for (var ac = 0; ac != UserCommand.MaxActionCount; ac++)
                {
                    actionCommand.Add(new GamePlayerActionCommand {Data = userCommand.Action[ac]});
                }
            }
        }

        [BurstCompile]
        private struct GamePlayerSetScroll : IJobForEachWithEntity<RpcPlayerScroll, ReceiveRpcCommandRequestComponent>
        {
            public EntityCommandBuffer.Concurrent Ecb;

            [ReadOnly]
            public ComponentDataFromEntity<CommandTargetComponent> CommandTargetFromEntity;

            public ComponentDataFromEntity<GamePlayerUserCommand> UserCommandFromEntity;

            public void Execute(Entity entity, int index, ref RpcPlayerScroll rpc, [ReadOnly] ref ReceiveRpcCommandRequestComponent receive)
            {
                if (CommandTargetFromEntity.Exists(receive.SourceConnection))
                {
                    var target = CommandTargetFromEntity[receive.SourceConnection].targetEntity;
                    if (!UserCommandFromEntity.Exists(target))
                        return;

                    var cmd = UserCommandFromEntity[target];
                    {
                        cmd.Scroll = rpc.Value;
                    }
                    UserCommandFromEntity[target] = cmd;
                }

                Ecb.DestroyEntity(index, entity);
            }
        }

        private EntityQuery m_Query;
        private EntityQuery m_QueryWithoutCmd;

        private EndSimulationEntityCommandBufferSystem m_EndBarrier;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_Query = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] {typeof(GamePlayer), typeof(GamePlayerUserCommand), typeof(UserCommand)}
            });
            m_QueryWithoutCmd = GetEntityQuery(new EntityQueryDesc
            {
                All  = new ComponentType[] {typeof(GamePlayer), typeof(UserCommand)},
                None = new ComponentType[] {typeof(GamePlayerUserCommand)}
            });

            m_EndBarrier = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (m_QueryWithoutCmd.CalculateEntityCount() > 0)
                using (var entities = m_QueryWithoutCmd.ToEntityArray(Allocator.TempJob))
                    foreach (var entity in entities)
                        EntityManager.AddComponents(entity, new ComponentTypes(typeof(GamePlayerUserCommand), typeof(GamePlayerActionCommand)));

            inputDeps = new JobSetGamePlayerUserCommand
            {
                userCommandFromEntity             = GetBufferFromEntity<UserCommand>(),
                gamePlayerActionCommandFromEntity = GetBufferFromEntity<GamePlayerActionCommand>(),
                targetTick                        = World.GetExistingSystem<GhostPredictionSystemGroup>().PredictingTick
            }.Schedule(m_Query, inputDeps);
            inputDeps = new GamePlayerSetScroll
            {
                Ecb                     = m_EndBarrier.CreateCommandBuffer().ToConcurrent(),
                CommandTargetFromEntity = GetComponentDataFromEntity<CommandTargetComponent>(true),
                UserCommandFromEntity   = GetComponentDataFromEntity<GamePlayerUserCommand>()
            }.ScheduleSingle(this, inputDeps);
            inputDeps.Complete();

            return inputDeps;
        }
    }

    [UpdateInGroup(typeof(ClientSimulationSystemGroup))]
    [UpdateBefore(typeof(CommandSendSystem))]
    public class BasicUserCommandUpdateLocal : JobSyncInputSystem
    {
        public static float sensivity = 1f;

        private InputActionAsset m_Asset;
        private InputAction      m_MoveAction, m_LookAction, m_JumpAction, m_DodgeAction, m_CrouchAction, m_ReloadAction;
        private InputAction[]    m_Actions;

        private UserCommand m_ActualCommand;

        public UserCommand Current => m_ActualCommand;

        private struct JobAddCommand : IJobForEachWithEntity_EB<UserCommand>
        {
            public UserCommand userCommand;

            public void Execute(Entity entity, int index, DynamicBuffer<UserCommand> inputs)
            {
                inputs.AddCommandData(userCommand);
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
            InputEvents.Clear();

            m_ActualCommand.Look = GetNewAimLook(m_ActualCommand.Look, new float2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")));
            m_ActualCommand.Tick = World.GetExistingSystem<ClientSimulationSystemGroup>().ServerTick;

            var currScroll = (int) Input.mouseScrollDelta.y;
            if (currScroll != 0)
            {
                var request = EntityManager.CreateEntity(typeof(RpcPlayerScroll), typeof(SendRpcCommandRequestComponent));
                EntityManager.SetComponentData(request, new RpcPlayerScroll {Value                           = currScroll});
                EntityManager.SetComponentData(request, new SendRpcCommandRequestComponent {TargetConnection = Entity.Null});
            }
            
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

            AddActionEvents(m_MoveAction   = inputMap.FindAction("Move"));
            AddActionEvents(m_LookAction   = inputMap.FindAction("Look"));
            AddActionEvents(m_JumpAction   = inputMap.FindAction("Jump"));
            AddActionEvents(m_DodgeAction  = inputMap.FindAction("Dodge"));
            AddActionEvents(m_CrouchAction = inputMap.FindAction("Crouch"));
            AddActionEvents(m_ReloadAction = inputMap.FindAction("Reload"));

            for (var ac = 0; ac != UserCommand.MaxActionCount; ac++)
            {
                var action = inputMap.FindAction("Action" + ac);
                AddActionEvents(m_Actions[ac] = action);
            }

            m_Asset.Enable();
        }

        private float2 GetNewAimLook(float2 previous, float2 next)
        {
            var input = next * sensivity;

            var newRotation = previous + input;
            newRotation.x = newRotation.x % 360;
            newRotation.y = Mathf.Clamp(newRotation.y, -89f, 89f);

            return newRotation;
        }

        protected override unsafe void InputActionEvent(InputAction.CallbackContext ctx)
        {
            if (ctx.action == m_MoveAction)
                m_ActualCommand.Move = ctx.ReadValue<Vector2>();
            if (ctx.action == m_JumpAction)
                m_ActualCommand.Jump = ctx.ReadValue<float>() > 0.5f;
            if (ctx.action == m_DodgeAction)
                m_ActualCommand.Dodge = ctx.ReadValue<float>() > 0.5f;
            if (ctx.action == m_CrouchAction)
                m_ActualCommand.Crouch = ctx.ReadValue<float>() > 0.5f;
            if (ctx.action == m_ReloadAction)
                m_ActualCommand.Reload = ctx.ReadValue<float>() > 0.5f;
            
            for (var ac = 0; ac != UserCommand.MaxActionCount; ac++)
            {
                if (ctx.action != m_Actions[ac])
                    continue;
                
                MainBit.SetBitAt(ref m_ActualCommand.Action[ac], 0, ctx.ReadValue<float>() > 0.5f);
            }
        }
    }

    public class ServerAddBufferToPlayer : ComponentSystem
    {
        protected override void OnUpdate()
        {
            EntityManager.AddComponent(Entities.WithAll<GamePlayer>().WithNone<UserCommand>().ToEntityQuery(), typeof(UserCommand));
        }
    }
    
    /*[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
    public class ClientAddBufferToPlayer : ComponentSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((ref PlayerConnectedEvent connectedEvent) =>
            {
                connectedEvent.Player
            });
        }
    }*/
}