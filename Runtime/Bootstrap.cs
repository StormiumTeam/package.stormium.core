using System;
using System.Collections.Generic;
using package.stormiumteam.shared.modding;
using StormiumTeam.GameBase;

namespace Stormium.Core
{
    public class Bootstrap : CModBootstrap
    {
        protected override void OnRegister()
        {
            var commandLineArgs = new List<string>(Environment.GetCommandLineArgs());
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