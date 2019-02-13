using System;
using System.Collections.Generic;
using package.stormiumteam.networking;
using package.stormiumteam.networking.runtime.highlevel;
using package.stormiumteam.networking.runtime.lowlevel;
using Stormium.Core;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Experimental.Input;

namespace Runtime.Systems
{
	public abstract class SyncInputSystem : GameBaseSystem
	{
		public InputActionAsset Asset { get; private set; }

		protected delegate void OnReceiveMessage(NetworkInstanceData networkInstance, Entity client, DataBufferReader data);
		
		private Dictionary<int, OnReceiveMessage> m_ActionForPattern;

		protected override void OnCreateManager()
		{
			base.OnCreateManager();

			m_ActionForPattern = new Dictionary<int, OnReceiveMessage>();
		}

		protected PatternResult AddMessage(OnReceiveMessage func, byte version = 0)
		{
			var patternName = $"auto.syncInputs.{GetType().Name}.{func.Method.Name}";
			var result = LocalBank.Register(new PatternIdent(patternName, version));

			m_ActionForPattern[result.Id] = func;

			return result;
		}

		protected bool Refresh(InputActionAsset asset)
		{
			Asset = asset;
			Asset.Enable();
			
			OnAssetRefresh();

			return Asset != null;
		}

		protected abstract void OnAssetRefresh();

		protected void SyncToServer(PatternResult result, DataBufferWriter syncData)
		{
			if (ServerMgr.ConnectedServerEntity == default)
				return;

			var instanceData = EntityManager.GetComponentData<NetworkInstanceData>(ServerMgr.ConnectedServerEntity);
			using (var buffer = BufferHelper.CreateFromPattern(result.Id, length: sizeof(byte) + sizeof(int) + syncData.Length))
			{
				buffer.WriteBuffer(syncData);
				
				instanceData.Commands.Send(buffer, default, Delivery.Reliable | Delivery.Unsequenced);
			}
		}
	}
}