using System;
using System.IO;
using package.stormiumteam.networking;
using package.stormiumteam.networking.runtime.highlevel;
using package.stormiumteam.networking.runtime.lowlevel;
using package.stormiumteam.shared;
using Runtime.Data;
using Runtime.Systems;
using UnityEngine;
using StormiumShared.Core.Networking;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.AddressableAssets;
using UnityEngine.Experimental.Input;

namespace Stormium.Core
{
	public struct ActionUserCommand : IStateData, IBufferElementData
	{
		private readonly byte m_ActiveFlags;

		public bool IsActive => Convert.ToBoolean(m_ActiveFlags);

		public ActionUserCommand(bool isActive)
		{
			m_ActiveFlags = Convert.ToByte(isActive);
		}
		
		public class Streamer : SnapshotEntityComponentStatusStreamer<ActionUserCommand>
		{}
	}

	[UpdateInGroup(typeof(STUpdateOrder.UO_Input))]
	public class UpdateActionUserCommand : SyncInputSystem
	{
		private const int MaxAction = 2;

		private PatternResult m_SyncMessage;
		private InputAction[] m_Actions;
		private bool[]        m_InputStates;

		protected override void OnCreateManager()
		{
			base.OnCreateManager();

			m_Actions     = new InputAction[MaxAction];
			m_InputStates = new bool[MaxAction];

			var file = File.ReadAllText(Application.streamingAssetsPath + "/action_input.json");

			var asset = ScriptableObject.CreateInstance<InputActionAsset>();
			asset.LoadFromJson(file);

			Refresh(asset);

			m_SyncMessage = AddMessage(GetInputFromClient);
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
			var playerEntity = EntityManager.GetComponentData<StNetworkClientToGamePlayer>(client).Target;
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
			
			ForEach((Entity e, DynamicBuffer<ActionUserCommand> commands, ref StGamePlayer player) =>
			{
				if (player.IsSelf == 0)
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