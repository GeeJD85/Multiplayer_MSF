using Barebones.Logging;
using Barebones.MasterServer;
using CommandTerminal;
using System.Collections.Generic;

namespace Barebones.Client.Utilities
{
    public class ClientSpawnerTerminalCommands
    {
        [RegisterCommand(Name = "client.spawner.start", Help = "Send request to start room. 1 Room Name, 2 Max Connections", MinArgCount = 1)]
        private static void SendRequestSpawn(CommandArg[] args)
        {
            var settings = new Dictionary<string, string>
            {
                { MsfDictKeys.roomName, args[0].String }
            };

            if(args.Length > 1)
            {
                settings.Add(MsfDictKeys.maxPlayers, args[1].String);
            }

            var customArgs = new Dictionary<string, string>
            {
                { "-myName", "\"John Adams\"" },
                { "-myAge", "45" }
            };

            Msf.Client.Spawners.RequestSpawn(settings);
        }

        [RegisterCommand(Name = "client.spawner.abort", Help = "Send request to start room. 1 Process Id", MinArgCount = 1, MaxArgCount = 1)]
        private static void SendAbortSpawn(CommandArg[] args)
        {
            Msf.Client.Spawners.AbortSpawn(args[0].Int);
        }
    }
}
