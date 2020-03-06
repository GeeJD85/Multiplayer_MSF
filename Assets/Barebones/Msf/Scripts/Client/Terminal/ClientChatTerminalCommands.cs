using Barebones.Logging;
using Barebones.MasterServer;
using CommandTerminal;
using System;
using System.Text;
using UnityEngine;

namespace Barebones.Client.Utilities
{
    public static class ClientChatTerminalCommands
    {
        static string tempMessage = string.Empty;

        [RegisterCommand(Name = "client.chat.msg", Help = "Send the chat message to all clients", MinArgCount = 2)]
        static void SendPrivateMessage(CommandArg[] args)
        {
            tempMessage = BuildMessage(args, 1);

            Msf.Client.Chat.SendPrivateMessage(args[0].String, tempMessage, OnSuccess);
        }

        private static void OnSuccess(bool isSuccessful, string error)
        {
            if (isSuccessful)
            {
                Logs.Info($"Message: {tempMessage}");
            }
        }

        static string BuildMessage(CommandArg[] args, int from)
        {
            StringBuilder sb = new StringBuilder();

            for(int i = from; i < args.Length; i++)
            {
                sb.Append($"{args[i].String.Trim()} ");
            }

            return sb.ToString();
        }
    }
}