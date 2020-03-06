using Barebones.Logging;
using Barebones.Networking;
using System;
using UnityEngine.SceneManagement;

namespace Barebones.MasterServer
{
    public delegate void RoomAccessProviderCallback(RoomAccessPacket access, string error);
    public delegate void RoomAccessProvider(UsernameAndPeerIdPacket requester, RoomAccessProviderCallback giveAccess);

    /// <summary>
    /// Instance of this class will be created when room registration is completed.
    /// It acts as a helpful way to manage a registered room.
    /// </summary>
    public class RoomController
    {
        private RoomAccessProvider _accessProvider;

        public IClientSocket Connection { get; private set; }
        public int RoomId { get; private set; }
        public RoomOptions Options { get; private set; }
        public static Logger Logger { get; private set; }

        public RoomController(int roomId, IClientSocket connection, RoomOptions options)
        {
            Logger = Msf.Create.Logger(typeof(RoomController).Name, LogLevel.Warn);

            Connection = connection;
            RoomId = roomId;
            Options = options;

            // Add handlers
            connection.SetHandler((short)MsfMessageCodes.ProvideRoomAccessCheck, HandleProvideRoomAccessCheck);
        }

        /// <summary>
        /// Destroys and unregisters the room
        /// </summary>
        public void Destroy()
        {
            Destroy((successful, error) =>
            {
                if (!successful)
                {
                    Logger.Error(error);
                }
                else
                {
                    Logger.Debug("Unregistered room successfully: " + RoomId);
                }
            });
        }

        /// <summary>
        /// Destroys and unregisters the room
        /// </summary>
        public void Destroy(SuccessCallback callback)
        {
            Msf.Server.Rooms.DestroyRoom(RoomId, callback, Connection);
        }

        /// <summary>
        /// Send's current options to master server
        /// </summary>
        public void SaveOptions()
        {
            SaveOptions(Options);
        }

        /// <summary>
        /// Send's new options to master server
        /// </summary>
        public void SaveOptions(RoomOptions options)
        {
            SaveOptions(options, (successful, error) =>
            {
                if (!successful)
                {
                    Logger.Error(error);
                }
                else
                {
                    Logger.Debug("Room " + RoomId + " options changed successfully");
                    Options = options;
                }
            });
        }

        /// <summary>
        /// Send's new options to master server
        /// </summary>
        public void SaveOptions(RoomOptions options, SuccessCallback callback)
        {
            Msf.Server.Rooms.SaveOptions(RoomId, options, (successful, error) =>
            {
                if (successful)
                {
                    Options = options;
                }

                callback.Invoke(successful, error);

            }, Connection);
        }

        /// <summary>
        /// Call this, if you want to manually check if peer should receive an access
        /// </summary>
        /// <param name="provider"></param>
        public void SetAccessProvider(RoomAccessProvider provider)
        {
            _accessProvider = provider;
        }

        /// <summary>
        /// Sends the token to "master" server to see if it's valid. If it is -
        /// callback will be invoked with peer id of the user, whos access was confirmed.
        /// This peer id can be used to retrieve users data from master server
        /// </summary>
        /// <param name="token"></param>
        /// <param name="callback"></param>
        public void ValidateAccess(string token, RoomAccessValidateCallback callback)
        {
            Msf.Server.Rooms.ValidateAccess(RoomId, token, callback, Connection);
        }
        
        /// <summary>
        /// Call this method when one of the players left current room
        /// </summary>
        /// <param name="peerId"></param>
        public void NotifyPlayerLeft(int peerId)
        {
            Msf.Server.Rooms.NotifyPlayerLeft(RoomId, peerId, (successful, error) =>
            {
                if (!successful)
                {
                    Logger.Error(error);
                }

                Logger.Info($"Player {peerId} left room");
            });
        }

        /// <summary>
        /// Default access provider, which always confirms access requests
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="callback"></param>
        public void DefaultAccessProvider(UsernameAndPeerIdPacket requester, RoomAccessProviderCallback callback)
        {
            callback.Invoke(new RoomAccessPacket()
            {
                RoomIp = Options.RoomIp,
                RoomPort = Options.RoomPort,
                Properties = Options.Properties,
                RoomId = RoomId,
                Token = Msf.Helper.CreateGuidString(),
                SceneName = SceneManager.GetActiveScene().name
            }, null);
        }

        /// <summary>
        /// Makes the room public
        /// </summary>
        public void MakePublic()
        {
            Options.IsPublic = true;
            SaveOptions(Options);
        }

        /// <summary>
        /// Makes the room public
        /// </summary>
        public void MakePublic(Action callback)
        {
            Options.IsPublic = true;
            SaveOptions(Options, (successful, error) =>
            {
                callback.Invoke();
            });
        }

        #region Message handlers

        private void HandleProvideRoomAccessCheck(IIncommingMessage message)
        {
            var data = message.Deserialize(new RoomAccessProvideCheckPacket());

            var roomController = Msf.Server.Rooms.GetRoomController(data.RoomId);

            if (roomController == null)
            {
                message.Respond("There's no room controller with room id " + data.RoomId, ResponseStatus.NotHandled);
                return;
            }

            var accessProvider = roomController._accessProvider ?? DefaultAccessProvider;
            var isProviderDone = false;

            var requester = new UsernameAndPeerIdPacket()
            {
                PeerId = data.PeerId,
                Username = data.Username
            };

            // Invoke the access provider
            accessProvider.Invoke(requester, (access, error) =>
            {
                // In case provider timed out
                if (isProviderDone)
                {
                    return;
                }

                isProviderDone = true;

                if (access == null)
                {
                    // If access is not provided
                    message.Respond(error ?? "", ResponseStatus.Failed);
                    return;
                }

                message.Respond(access, ResponseStatus.Success);

                if (Logger.IsLogging(LogLevel.Trace))
                {
                    Logger.Trace("Room controller gave address to peer " + data.PeerId + ":" + access);
                }
            });

            // Timeout the access provider
            MsfTimer.WaitForSeconds(Msf.Server.Rooms.AccessProviderTimeout, () =>
            {
                if (!isProviderDone)
                {
                    isProviderDone = true;
                    message.Respond("Timed out", ResponseStatus.Timeout);
                    Logger.Error("Access provider took longer than " + Msf.Server.Rooms.AccessProviderTimeout + " seconds to provide access. " +
                               "If it's intended, increase the threshold at Msf.Server.Rooms.AccessProviderTimeout");
                }
            });
        }

        #endregion
    }
}