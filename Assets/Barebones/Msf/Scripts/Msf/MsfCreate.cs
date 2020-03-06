﻿using Barebones.Logging;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    public class MsfCreate
    {
        public IServerSocket ServerSocket()
        {
            var serverSocket = Msf.Advanced.ServerSocketFactory();
            return serverSocket;
        }

        /// <summary>
        /// Creates a logger of the given name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Logger Logger(string name)
        {
            return LogManager.GetLogger(name);
        }

        /// <summary>
        /// Creates a logger of the given name, and sets its defualt log level
        /// </summary>
        /// <param name="name"></param>
        /// <param name="defaulLogLevel"></param>
        /// <returns></returns>
        public Logger Logger(string name, LogLevel defaulLogLevel)
        {
            var logger = LogManager.GetLogger(name);
            logger.LogLevel = defaulLogLevel;
            return logger;
        }

        /// <summary>
        /// Creates a generic success callback (for lazy people)
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="unknownErrorMsg"></param>
        /// <returns></returns>
        public ResponseCallback SuccessCallback(SuccessCallback callback, string unknownErrorMsg = "Unknown Error")
        {
            return (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString(unknownErrorMsg));
                    return;
                }

                callback.Invoke(true, null);
            };
        }

        #region Message Creation

        /// <summary>
        /// Creates an empty message
        /// </summary>
        /// <param name="opCode"></param>
        /// <returns></returns>
        public IMessage Message(short opCode)
        {
            return MessageHelper.Create(opCode);
        }

        /// <summary>
        /// Creates a message with string content
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public IMessage Message(short opCode, string message)
        {
            return MessageHelper.Create(opCode, message);
        }

        /// <summary>
        /// Creates a message with int content
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public IMessage Message(short opCode, int data)
        {
            return MessageHelper.Create(opCode, data);
        }

        /// <summary>
        /// Creates a message with binary data
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public IMessage Message(short opCode, byte[] data)
        {
            return MessageHelper.Create(opCode, data);
        }

        /// <summary>
        /// Creates a message by serializing a packet
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        public IMessage Message(short opCode, ISerializablePacket packet)
        {
            return MessageHelper.Create(opCode, packet.ToBytes());
        }

        #endregion
    }
}