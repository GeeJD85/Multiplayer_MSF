using Barebones.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barebones.MasterServer
{
    /// <summary>
    /// This is an instance of the room in master server
    /// </summary>
    public class RegisteredRoom
    {
        public delegate void GetAccessCallback(RoomAccessPacket access, string error);

        /// <summary>
        /// List used accesses
        /// </summary>
        private Dictionary<int, RoomAccessPacket> _accessesInUse;

        /// <summary>
        /// List of unconfirmed access
        /// </summary>
        private Dictionary<string, RoomAccessData> _unconfirmedAccesses;

        /// <summary>
        /// Connected players list
        /// </summary>
        private Dictionary<int, IPeer> _players;

        /// <summary>
        /// 
        /// </summary>
        private HashSet<int> _requestsInProgress;

        /// <summary>
        /// 
        /// </summary>
        public RoomOptions Options { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public int RoomId { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public IPeer Peer { get; private set; }

        public int OnlineCount { get { return _accessesInUse.Count; } }

        /// <summary>
        /// Fires when player joined room 
        /// </summary>
        public event Action<IPeer> OnPlayerJoinedEvent;

        /// <summary>
        /// Fireswhen player left room
        /// </summary>
        public event Action<IPeer> OnPlayerLeftEvent;

        /// <summary>
        /// Fires when room destroyed
        /// </summary>
        public event Action<RegisteredRoom> OnDestroyedEvent;

        public RegisteredRoom(int roomId, IPeer peer, RoomOptions options)
        {
            RoomId = roomId;
            Peer = peer;
            Options = options;

            _unconfirmedAccesses = new Dictionary<string, RoomAccessData>();
            _players = new Dictionary<int, IPeer>();
            _accessesInUse = new Dictionary<int, RoomAccessPacket>();
            _requestsInProgress = new HashSet<int>();

            OverrideOptionsWithProperties();
        }

        public void OverrideOptionsWithProperties()
        {
            if (Options.Properties.ContainsKey(MsfDictKeys.isPublic))
            {
                bool isPublic;
                bool.TryParse(Options.Properties[MsfDictKeys.isPublic], out isPublic);
                Options.IsPublic = isPublic;
            }
        }

        public void ChangeOptions(RoomOptions options)
        {
            Options = options;
        }

        /// <summary>
        /// Sends a request to room, to retrieve an access to it for a specified peer
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="callback"></param>
        public void GetAccess(IPeer peer, GetAccessCallback callback)
        {
            GetAccess(peer, new Dictionary<string, string>(), callback);
        }

        /// <summary>
        /// Sends a request to room, to retrieve an access to it for a specified peer, 
        /// with some extra properties
        /// </summary>
        public void GetAccess(IPeer peer, Dictionary<string, string> properties, GetAccessCallback callback)
        {
            // If request is already pending
            if (_requestsInProgress.Contains(peer.Id))
            {
                callback.Invoke(null, "You've already requested an access to this room");
                return;
            }

            // If player is already in the game
            if (_players.ContainsKey(peer.Id))
            {
                callback.Invoke(null, "You are already in this room");
                return;
            }

            // If player has already received an access and didn't claim it
            // but is requesting again - send him the old one
            var currentAccess = _unconfirmedAccesses.Values.FirstOrDefault(v => v.Peer == peer);
            if (currentAccess != null)
            {
                // Restore the timeout
                currentAccess.Timeout = DateTime.Now.AddSeconds(Options.AccessTimeoutPeriod);

                callback.Invoke(currentAccess.Access, null);
                return;
            }

            // If there's a player limit
            if (Options.MaxConnections != 0)
            {
                var playerSlotsTaken = _requestsInProgress.Count
                                       + _accessesInUse.Count
                                       + _unconfirmedAccesses.Count;

                if (playerSlotsTaken >= Options.MaxConnections)
                {
                    callback.Invoke(null, "Room is already full");
                    return;
                }
            }

            var packet = new RoomAccessProvideCheckPacket()
            {
                PeerId = peer.Id,
                RoomId = RoomId
            };

            // Add the username if available
            var userExt = peer.GetExtension<IUserPeerExtension>();
            if (userExt != null && !string.IsNullOrEmpty(userExt.Username))
            {
                packet.Username = userExt.Username;
            }

            // Add to pending list
            _requestsInProgress.Add(peer.Id);

            Peer.SendMessage((short)MsfMessageCodes.ProvideRoomAccessCheck, packet, (status, response) =>
            {
                // Remove from pending list
                _requestsInProgress.Remove(peer.Id);

                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(null, response.AsString("Unknown Error"));
                    return;
                }

                var accessData = response.Deserialize(new RoomAccessPacket());

                var access = new RoomAccessData()
                {
                    Access = accessData,
                    Peer = peer,
                    Timeout = DateTime.Now.AddSeconds(Options.AccessTimeoutPeriod)
                };

                // Save the access
                _unconfirmedAccesses[access.Access.Token] = access;

                callback.Invoke(access.Access, null);
            });
        }

        /// <summary>
        /// Checks if access token is valid
        /// </summary>
        /// <param name="token"></param>
        /// <param name="peer"></param>
        /// <returns></returns>
        public bool ValidateAccess(string token, out IPeer peer)
        {
            RoomAccessData data;
            _unconfirmedAccesses.TryGetValue(token, out data);

            peer = null;

            // If there's no data
            if (data == null)
            {
                return false;
            }

            // Remove unconfirmed
            _unconfirmedAccesses.Remove(token);

            // If player is no longer connected
            if (!data.Peer.IsConnected)
            {
                return false;
            }

            // Set access as used
            _accessesInUse.Add(data.Peer.Id, data.Access);

            peer = data.Peer;

            // Invoke the event
            if (OnPlayerJoinedEvent != null)
            {
                OnPlayerJoinedEvent.Invoke(peer);
            }

            return true;
        }

        /// <summary>
        /// Clears all of the accesses that have not been confirmed in time
        /// </summary>
        public void ClearTimedOutAccesses()
        {
            var timedOut = _unconfirmedAccesses.Values.Where(u => u.Timeout < DateTime.Now).ToList();

            foreach (var access in timedOut)
            {
                _unconfirmedAccesses.Remove(access.Access.Token);
            }
        }

        private class RoomAccessData
        {
            public RoomAccessPacket Access;
            public IPeer Peer;
            public DateTime Timeout;
        }

        public void OnPlayerLeft(int peerId)
        {
            _accessesInUse.Remove(peerId);

            if (!_players.TryGetValue(peerId, out IPeer playerPeer))
            {
                return;
            }

            OnPlayerLeftEvent?.Invoke(playerPeer);
        }

        public void Destroy()
        {
            if (OnDestroyedEvent != null)
            {
                OnDestroyedEvent.Invoke(this);
            }

            _unconfirmedAccesses.Clear();

            // Clear listeners
            OnPlayerJoinedEvent = null;
            OnPlayerLeftEvent = null;
            OnDestroyedEvent = null;
        }
    }
}