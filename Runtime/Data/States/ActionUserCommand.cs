using System;
using System.IO;
using package.stormiumteam.networking;
using package.stormiumteam.networking.runtime.highlevel;
using package.stormiumteam.networking.runtime.lowlevel;
using UnityEngine;
using StormiumShared.Core.Networking;
using StormiumTeam.GameBase;
using StormiumTeam.GameBase.Systems;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace Stormium.Core
{
	public struct ActionUserCommand : IStateData, IBufferElementData
	{
		public bool IsActive;
		
		public ActionUserCommand(bool isActive)
		{
			IsActive = isActive;
		}
		
		public class Streamer : SnapshotEntityComponentStatusStreamerBuffer<ActionUserCommand>
		{}
	}

	[UpdateInGroup(typeof(STUpdateOrder.UO_Input))]
	public class UpdateActionUserCommand : SyncInputSystem
	{
		private const int MaxAction = 2;

		private PatternResult m_SyncMessage;
		private InputAction[] m_Actions;
		private bool[]        m_InputStates;

		protected override void OnCreate()
		{
			base.OnCreate();

			m_Actions     = new InputAction[MaxAction];
			m_InputStates = new bool[MaxAction];

			var file = File.ReadAllText(Application.streamingAssetsPath + "/action_input.json");

			var asset = ScriptableObject.CreateInstance<InputActionAsset>();
			asset.LoadFromJson(file);

			Refresh(asset);

			m_SyncMessage = AddMessage(GetInputFromClient);
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
			
			Asset.Disable();
		}

		protected override void OnAssetRefresh()
		{
			var actionMap = Asset.GetActionMap("Map");
			for (var i = 0; i != 2; i++)
			{
				var action = actionMap.GetAction("Slot" + i);
				m_Actions[i] = action;

				action.performed += OnInputPerformed;
			}
		}

		private void OnInputPerformed(InputAction.CallbackContext cc)
		{
			var index = Array.IndexOf(m_Actions, cc.action);

			m_InputStates[index] = cc.ReadValue<float>() > 0.5f;
		}

		private void GetInputFromClient(NetworkInstanceData networkInstance, Entity client, DataBufferReader data)
		{
			var playerEntity = EntityManager.GetComponentData<NetworkClientToGamePlayer>(client).Target;
			var inputBuffer  = EntityManager.GetBuffer<ActionUserCommand>(playerEntity);

			inputBuffer.ResizeUninitialized(MaxAction);

			for (var i = 0; i != MaxAction; i++)
			{
				inputBuffer[i] = new ActionUserCommand(data.ReadValue<bool>());
			}
		}

		protected override void OnUpdate()
		{
			base.OnUpdate();
			
			Entities.ForEach((Entity e, DynamicBuffer<ActionUserCommand> commands, ref GamePlayer player) =>
			{
				if (!player.IsSelf)
					return;

				var newCommandBuffer = PostUpdateCommands.SetBuffer<ActionUserCommand>(e);
				newCommandBuffer.ResizeUninitialized(MaxAction);
				
				for (var i = 0; i != newCommandBuffer.Length; i++)
				{
					newCommandBuffer[i] = new ActionUserCommand(m_InputStates[i]);
				}
			});

			using (var msg = new DataBufferWriter(sizeof(bool) * MaxAction, Allocator.Temp))
			{
				for (var i = 0; i != MaxAction; i++)
				{
					msg.WriteUnmanaged(m_InputStates[i]);
				}

				SyncToServer(m_SyncMessage, msg);
			}
		}
	}
}