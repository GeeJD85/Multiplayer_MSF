using Barebones.MasterServer;
using CommandTerminal;
using UnityEngine;

namespace Barebones.Client.Utilities
{
    public static class ClientTerminalCommands
    {
        [RegisterCommand(Name = "client.ping", Help = "Send the Ping message to master. No arguments are required", MaxArgCount = 0)]
        static void ClientPing(CommandArg[] args)
        {
            Msf.Client.Connection.SendMessage((short)MsfMessageCodes.Ping, (status, response) =>
            {
                Debug.Log($"Message: {response.AsString()}, Status: {response.Status.ToString()}");
            });
        }

        [RegisterCommand(Name = "client.connect", Help = "Connects the client to master. 1 Server IP address, 2 Server port", MinArgCount = 0, MaxArgCount = 2)]
        static void ClientConnect(CommandArg[] args)
        {
            if(args.Length > 0)
            {
                ConnectionToMaster.Instance.SetIpAddress(args[0].String);
            }

            if (args.Length > 1)
            {
                ConnectionToMaster.Instance.SetPort(Mathf.Clamp(args[0].Int, 0, ushort.MaxValue));
            }

            ConnectionToMaster.Instance.StartConnection();
        }

        [RegisterCommand(Name = "client.disconnect", Help = "Disconnects the client from master", MaxArgCount = 0)]
        static void ClientDisconnect(CommandArg[] args)
        {
            Msf.Connection.Disconnect();
        }
    }
}