using System;
using Boo.Lang;
using package.stormium.core;
using package.stormiumteam.networking.runtime.highlevel;
using package.stormiumteam.shared;
using package.stormiumteam.shared.modding;
using Runtime;
using Scripts;
using StormiumShared.Core;
using StormiumShared.Core.Networking;
using Unity.Entities;

namespace Stormium.Core
{
    public class Bootstrap : CModBootstrap
    {
        protected override void OnRegister()
        {
            var commandLineArgs = new List<string>(System.Environment.GetCommandLineArgs());
            var m_isHeadless    = commandLineArgs.Contains("-batchmode");
            var logName         = m_isHeadless ? "game_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") : "game";
            GameDebug.Init(".", logName);
        }

        protected override void OnUnregister()
        {

        }

        internal static void register()
        {
            new Bootstrap().OnRegister();
        }
    }
}