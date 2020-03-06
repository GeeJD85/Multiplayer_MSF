using Barebones.Logging;
using Barebones.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Barebones.MasterServer
{
    public class BaseLobby : ILobby
    {
        private LobbyState _state;
        private string _statusText = "";
        private LobbyMember _gameMaster;

        public event Action<LobbyMember> OnPlayerAddedEvent;
        public event Action<LobbyMember> OnPlayerRemovedEvent;

        public event Action<ILobby> OnDestroyedEvent;

        public Logger Logger = Msf.Create.Logger(typeof(BaseLobby).Name);

        protected Dictionary<string, LobbyMember> membersList;
        protected Dictionary<int, LobbyMember> membersByPeerIdList;
        protected Dictionary<string, string> propertiesList;
        protected Dictionary<string, LobbyTeam> teamsList;
        protected HashSet<IPeer> subscribersList;

        protected List<LobbyPropertyData> Controls;

        protected SpawnTask GameSpawnTask;
        protected RegisteredRoom Room;

        public BaseLobby(int lobbyId, IEnumerable<LobbyTeam> teams,
            LobbiesModule module, LobbyConfig config)
        {
            Id = lobbyId;
            Module = module;
            GameIp = "";
            GamePort = -1;

            Config = config;

            Controls = new List<LobbyPropertyData>();
            membersList = new Dictionary<string, LobbyMember>();
            membersByPeerIdList = new Dictionary<int, LobbyMember>();
            propertiesList = new Dictionary<string, string>();
            teamsList = teams.ToDictionary(t => t.Name, t => t);
            subscribersList = new HashSet<IPeer>();

            MaxPlayers = teamsList.Values.Sum(t => t.MaxPlayers);
            MinPlayers = teamsList.Values.Sum(t => t.MinPlayers);
        }

        public string Name { get; set; }
        public int PlayerCount { get { return membersList.Count; } }

        public int Id { get; private set; }
        protected LobbiesModule Module { get; private set; }

        public bool IsDestroyed { get; private set; }

        public LobbyConfig Config { get; private set; }

        public int MaxPlayers { get; protected set; }
        public int MinPlayers { get; protected set; }

        public string Type { get; set; }
        public string GameIp { get; protected set; }
        public int GamePort { get; protected set; }

        public LobbyState State
        {
            get
            {
                return _state;
            }
            protected set
            {
                if (_state == value)
                {
                    return;
                }

                _state = value;
                OnLobbyStateChange(value);
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            protected set
            {
                if (_statusText == value)
                {
                    return;
                }

                OnStatusTextChange(value);
            }
        }

        protected LobbyMember GameMaster
        {
            get { return _gameMaster; }
            set
            {
                if (!Config.EnableGameMasters)
                {
                    return;
                }

                _gameMaster = value;
                OnGameMasterChange();
            }
        }

        public bool AddPlayer(LobbyUserPeerExtension playerExt, out string error)
        {
            error = null;

            if (playerExt.CurrentLobby != null)
            {
                error = "You're already in a lobby";
                return false;
            }

            var username = TryGetUsername(playerExt.Peer);

            if (username == null)
            {
                error = "Invalid username";
                return false;
            }

            if (membersList.ContainsKey(username))
            {
                error = "Already in the lobby";
                return false;
            }

            if (IsDestroyed)
            {
                error = "Lobby is destroyed";
                return false;
            }

            if (!IsPlayerAllowed(username, playerExt))
            {
                error = "You're not allowed";
                return false;
            }

            if (membersList.Values.Count >= MaxPlayers)
            {
                error = "Lobby is full";
                return false;
            }

            if (!Config.AllowJoiningWhenGameIsLive && State != LobbyState.Preparations)
            {
                error = "Game is already in progress";
                return false;
            }

            // Create an "instance" of the member
            var member = CreateMember(username, playerExt);

            // Add it to a team
            var team = PickTeamForPlayer(member);

            if (team == null)
            {
                error = "Invalid lobby team";
                return false;
            }

            if (!team.AddMember(member))
            {
                error = "Not allowed to join a team";
                return false;
            }

            membersList[member.Username] = member;
            membersByPeerIdList[playerExt.Peer.Id] = member;

            // Set this lobby as player's current lobby
            playerExt.CurrentLobby = this;

            if (GameMaster == null)
            {
                PickNewGameMaster(false);
            }

            Subscribe(playerExt.Peer);

            playerExt.Peer.OnPeerDisconnectedEvent += OnPeerDisconnected;

            OnPlayerAdded(member);

            if (OnPlayerAddedEvent != null)
            {
                OnPlayerAddedEvent.Invoke(member);
            }

            return true;
        }

        public void RemovePlayer(LobbyUserPeerExtension playerExt)
        {
            var username = TryGetUsername(playerExt.Peer);

            LobbyMember member;
            membersList.TryGetValue(username, out member);

            // If this player was never in the lobby
            if (member == null)
            {
                return;
            }

            membersList.Remove(username);
            membersByPeerIdList.Remove(playerExt.Peer.Id);

            if (playerExt.CurrentLobby == this)
            {
                playerExt.CurrentLobby = null;
            }

            // Remove member from it's current team
            if (member.Team != null)
            {
                member.Team.RemoveMember(member);
            }

            // Change the game master
            if (GameMaster == member)
            {
                PickNewGameMaster();
            }


            // Unsubscribe
            playerExt.Peer.OnPeerDisconnectedEvent -= OnPeerDisconnected;
            Unsubscribe(playerExt.Peer);

            // Notify player himself that he's removed
            playerExt.Peer.SendMessage((short)MsfMessageCodes.LeftLobby, Id);

            OnPlayerRemoved(member);

            if (OnPlayerRemovedEvent != null)
            {
                OnPlayerRemovedEvent.Invoke(member);
            }
        }

        public virtual bool SetProperty(LobbyUserPeerExtension setter, string key, string value)
        {
            if (!Config.AllowPlayersChangeLobbyProperties)
            {
                return false;
            }

            if (Config.EnableGameMasters)
            {
                LobbyMember member;
                membersByPeerIdList.TryGetValue(setter.Peer.Id, out member);

                if (GameMaster != member)
                {
                    return false;
                }
            }

            return SetProperty(key, value);
        }

        public bool SetProperty(string key, string value)
        {
            if (propertiesList.ContainsKey(key))
            {
                propertiesList[key] = value;
            }
            else
            {
                propertiesList.Add(key, value);
            }

            OnLobbyPropertyChange(key);
            return true;
        }

        public LobbyMember GetMember(LobbyUserPeerExtension playerExt)
        {
            LobbyMember member;
            membersByPeerIdList.TryGetValue(playerExt.Peer.Id, out member);

            return member;
        }

        public LobbyMember GetMember(string username)
        {
            LobbyMember member;
            membersList.TryGetValue(username, out member);

            return member;
        }

        public LobbyMember GetMemberByPeerId(int peerId)
        {
            LobbyMember member;
            membersByPeerIdList.TryGetValue(peerId, out member);

            return member;
        }

        public bool SetPlayerProperty(LobbyMember player, string key, string value)
        {
            // Invalid property
            if (key == null)
            {
                return false;
            }

            // Check if player is allowed to change this property
            if (!IsPlayerPropertyChangeable(player, key, value))
            {
                return false;
            }

            player.SetProperty(key, value);

            OnPlayerPropertyChange(player, key);

            return true;
        }

        public void SetLobbyProperties(Dictionary<string, string> properties)
        {
            foreach (var property in properties)
            {
                propertiesList[property.Key] = property.Value;
            }
        }

        public void SetReadyState(LobbyMember member, bool state)
        {
            if (!membersList.ContainsKey(member.Username))
            {
                return;
            }

            member.IsReady = state;

            OnPlayerReadyStatusChange(member);

            if (membersList.Values.All(m => m.IsReady))
            {
                OnAllPlayersReady();
            }
        }

        public void AddControl(LobbyPropertyData propertyData, string defaultValue)
        {
            SetProperty(propertyData.PropertyKey, defaultValue);
            Controls.Add(propertyData);
        }

        public void AddControl(LobbyPropertyData propertyData)
        {
            var defaultValue = "";

            if (propertyData.Options != null && propertyData.Options.Count > 0)
            {
                defaultValue = propertyData.Options.First();
            }

            SetProperty(propertyData.PropertyKey, defaultValue);
            Controls.Add(propertyData);
        }

        public bool TryJoinTeam(string teamName, LobbyMember member)
        {
            if (!Config.EnableTeamSwitching)
            {
                return false;
            }

            var currentTeam = member.Team;
            var newTeam = teamsList[teamName];

            // Ignore, if any of the teams is invalid
            if (currentTeam == null || newTeam == null)
            {
                return false;
            }

            if (newTeam.PlayerCount >= newTeam.MaxPlayers)
            {
                SendChatMessage(member, "Team is full", true);
                return false;
            }

            // Try to add the member
            if (!newTeam.AddMember(member))
            {
                return false;
            }

            // Remove member from previous team
            currentTeam.RemoveMember(member);

            OnPlayerTeamChanged(member, newTeam);

            return true;
        }

        protected virtual LobbyMember CreateMember(string username, LobbyUserPeerExtension extension)
        {
            return new LobbyMember(username, extension);
        }

        protected virtual void PickNewGameMaster(bool broadcastChange = true)
        {
            if (!Config.EnableGameMasters)
            {
                return;
            }

            GameMaster = membersList.Values.FirstOrDefault();
        }

        public virtual LobbyTeam PickTeamForPlayer(LobbyMember member)
        {
            return teamsList.Values
                .Where(t => t.CanAddPlayer(member))
                .OrderBy(t => t.PlayerCount).FirstOrDefault();
        }

        /// <summary>
        /// Extracts username of the peer.
        /// By default, uses user extension <see cref="IUserPeerExtension"/>
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        protected virtual string TryGetUsername(IPeer peer)
        {
            var userExt = peer.GetExtension<IUserPeerExtension>();

            if (userExt == null)
            {
                return null;
            }

            return userExt.Username;
        }

        /// <summary>
        /// This will be called before adding a player to lobby.
        /// Override it to add custom checks for bans and etc.
        /// </summary>
        protected virtual bool IsPlayerAllowed(string username, LobbyUserPeerExtension user)
        {
            return true;
        }

        protected virtual bool IsPlayerPropertyChangeable(LobbyMember member, string key, string value)
        {
            return true;
        }

        public void Subscribe(IPeer peer)
        {
            subscribersList.Add(peer);
        }

        public void Unsubscribe(IPeer peer)
        {
            subscribersList.Remove(peer);
        }

        public virtual bool StartGame()
        {
            if (IsDestroyed)
            {
                return false;
            }

            var region = "";

            propertiesList[MsfDictKeys.isPublic] = "false";

            // Extract the region if available
            if (propertiesList.ContainsKey(MsfDictKeys.region))
            {
                region = propertiesList[MsfDictKeys.region];
            }

            var task = Module.SpawnersModule.Spawn(propertiesList, region, GenerateCmdArgs());

            if (task == null)
            {
                BroadcastChatMessage("Servers are busy", true);
                return false;
            }

            State = LobbyState.StartingGameServer;

            SetGameSpawnTask(task);

            return true;
        }

        public void Destroy()
        {
            if (IsDestroyed)
            {
                return;
            }

            IsDestroyed = true;

            // Remove players
            foreach (var member in membersList.Values.ToList())
            {
                RemovePlayer(member.Extension);
            }

            if (GameSpawnTask != null)
            {
                GameSpawnTask.OnStatusChangedEvent -= OnSpawnServerStatusChanged;
                GameSpawnTask.KillSpawnedProcess();
            }

            if (OnDestroyedEvent != null)
            {
                OnDestroyedEvent.Invoke(this);
            }
        }

        protected virtual string GenerateCmdArgs()
        {
            return Msf.Args.Names.LobbyId + " " + Id;
        }

        public void SetGameSpawnTask(SpawnTask task)
        {
            if (task == null)
            {
                return;
            }

            if (GameSpawnTask == task)
            {
                return;
            }

            if (GameSpawnTask != null)
            {
                // Unsubscribe from previous game
                GameSpawnTask.OnStatusChangedEvent -= OnSpawnServerStatusChanged;
                GameSpawnTask.Abort();
            }

            GameSpawnTask = task;

            task.OnStatusChangedEvent += OnSpawnServerStatusChanged;
        }

        protected virtual void OnSpawnServerStatusChanged(SpawnStatus status)
        {
            var isStarting = status > SpawnStatus.None && status < SpawnStatus.Finalized;

            // If the game is currently starting
            if (isStarting && State != LobbyState.StartingGameServer)
            {
                State = LobbyState.StartingGameServer;
                return;
            }

            // If game is running
            if (status == SpawnStatus.Finalized)
            {
                State = LobbyState.GameInProgress;
                OnGameServerFinalized();
            }

            // If game is aborted / closed
            if (status < SpawnStatus.None)
            {
                // If game was open before
                if (State == LobbyState.StartingGameServer)
                {
                    State = Config.PlayAgainEnabled ? LobbyState.Preparations : LobbyState.FailedToStart;
                    BroadcastChatMessage("Failed to start a game server", true);
                }
                else
                {
                    State = Config.PlayAgainEnabled ? LobbyState.Preparations : LobbyState.GameOver;
                }
            }
        }

        protected virtual void OnGameServerFinalized()
        {
            if (GameSpawnTask.FinalizationPacket == null)
            {
                return;
            }

            var data = GameSpawnTask.FinalizationPacket.FinalizationData;

            if (!data.ContainsKey(MsfDictKeys.roomId))
            {
                BroadcastChatMessage("Game server finalized, but room ID cannot be found", true);
                return;
            }

            // Get room id from finalization data
            var roomId = int.Parse(data[MsfDictKeys.roomId]);
            var room = Module.RoomsModule.GetRoom(roomId);

            if (room == null)
            {
                return;
            }

            Room = room;

            GameIp = room.Options.RoomIp;
            GamePort = room.Options.RoomPort;

            room.OnDestroyedEvent += OnRoomDestroyed;
        }

        public void OnRoomDestroyed(RegisteredRoom room)
        {
            room.OnDestroyedEvent -= OnRoomDestroyed;

            GameIp = "";
            GamePort = -1;
            Room = null;

            GameSpawnTask = null;

            State = Config.PlayAgainEnabled ? LobbyState.Preparations : LobbyState.GameOver;
        }

        public Dictionary<string, string> GetPublicProperties(IPeer peer)
        {
            return propertiesList;
        }

        #region Packet generators

        public LobbyDataPacket GenerateLobbyData()
        {
            var info = new LobbyDataPacket
            {
                LobbyType = Type ?? "",
                GameMaster = GameMaster != null ? GameMaster.Username : "",
                LobbyName = Name,
                LobbyId = Id,
                LobbyProperties = propertiesList,
                Players = membersList.Values
                    .ToDictionary(m => m.Username, GenerateMemberData),
                Teams = teamsList.Values.ToDictionary(t => t.Name, t => t.GenerateData()),
                Controls = Controls,
                LobbyState = State,
                MaxPlayers = MaxPlayers,
                EnableTeamSwitching = Config.EnableTeamSwitching,
                EnableReadySystem = Config.EnableReadySystem,
                EnableManualStart = Config.EnableManualStart,
                CurrentUserUsername = ""
            };

            return info;
        }

        public LobbyDataPacket GenerateLobbyData(LobbyUserPeerExtension user)
        {
            var info = new LobbyDataPacket
            {
                LobbyType = Type ?? "",
                GameMaster = GameMaster != null ? GameMaster.Username : "",
                LobbyName = Name,
                LobbyId = Id,
                LobbyProperties = propertiesList,
                Players = membersList.Values
                    .ToDictionary(m => m.Username, GenerateMemberData),
                Teams = teamsList.Values.ToDictionary(t => t.Name, t => t.GenerateData()),
                Controls = Controls,
                LobbyState = State,
                MaxPlayers = MaxPlayers,
                EnableTeamSwitching = Config.EnableTeamSwitching,
                EnableReadySystem = Config.EnableReadySystem,
                EnableManualStart = Config.EnableManualStart,
                CurrentUserUsername = TryGetUsername(user.Peer)
            };

            return info;
        }

        public void HandleChatMessage(LobbyMember member, IIncommingMessage message)
        {
            var text = message.AsString();

            var messagePacket = new LobbyChatPacket()
            {
                Message = text,
                Sender = member.Username
            };

            var msg = MessageHelper.Create((short)MsfMessageCodes.LobbyChatMessage, messagePacket.ToBytes());

            Broadcast(msg);
        }

        public void HandleGameAccessRequest(IIncommingMessage message)
        {
            if (Room == null)
            {
                message.Respond("Game is not running", ResponseStatus.Failed);
                return;
            }

            var requestData = new Dictionary<string, string>().FromBytes(message.AsBytes());

            Room.GetAccess(message.Peer, requestData, (access, error) =>
            {
                if (access == null)
                {
                    message.Respond(error ?? "Failed to get access to game", ResponseStatus.Failed);
                    return;
                }

                // Send back the access
                message.Respond(access, ResponseStatus.Success);
            });
        }

        public virtual bool StartGameManually(LobbyUserPeerExtension user)
        {
            var member = GetMember(user);

            if (!Config.EnableManualStart)
            {
                SendChatMessage(member, "You cannot start the game manually", true);
                return false;
            }

            // If not game maester
            if (GameMaster != member)
            {
                SendChatMessage(member, "You're not the master of this game", true);
                return false;
            }

            if (State != LobbyState.Preparations)
            {
                SendChatMessage(member, "Invalid lobby state", true);
                return false;
            }

            if (IsDestroyed)
            {
                SendChatMessage(member, "Lobby is destroyed", true);
                return false;
            }

            if (membersList.Values.Any(m => !m.IsReady && m != _gameMaster))
            {
                SendChatMessage(member, "Not all players are ready", true);
                return false;
            }

            if (membersList.Count < MinPlayers)
            {
                SendChatMessage(
                    member,
                    string.Format("Not enough players. Need {0} more ", (MinPlayers - membersList.Count)),
                    true);
                return false;
            }

            var lackingTeam = teamsList.Values.FirstOrDefault(t => t.MinPlayers > t.PlayerCount);

            if (lackingTeam != null)
            {
                var msg = string.Format("Team {0} does not have enough players", lackingTeam.Name);
                SendChatMessage(member, msg, true);
                return false;
            }

            return StartGame();
        }

        public virtual LobbyMemberData GenerateMemberData(LobbyMember member)
        {
            return member.GenerateDataPacket();
        }

        #endregion

        #region Broadcasting

        public void Broadcast(IMessage message)
        {
            foreach (var peer in subscribersList)
            {
                peer.SendMessage(message, DeliveryMethod.Reliable);
            }
        }

        public void Broadcast(IMessage message, Func<IPeer, bool> condition)
        {
            foreach (var peer in subscribersList)
            {
                if (!condition(peer))
                {
                    continue;
                }

                peer.SendMessage(message, DeliveryMethod.Reliable);
            }
        }

        public void BroadcastChatMessage(string message, bool isError = false,
            string sender = "System")
        {
            var msg = new LobbyChatPacket()
            {
                Message = message,
                Sender = sender,
                IsError = isError
            };

            Broadcast(MessageHelper.Create((short)MsfMessageCodes.LobbyChatMessage, msg.ToBytes()));
        }

        public void SendChatMessage(LobbyMember member, string message, bool isError = false,
            string sender = "System")
        {
            var packet = new LobbyChatPacket()
            {
                Message = message,
                Sender = sender,
                IsError = isError
            };

            var msg = MessageHelper.Create((short)MsfMessageCodes.LobbyChatMessage, packet.ToBytes());

            member.Extension.Peer.SendMessage(msg, DeliveryMethod.Reliable);
        }

        #endregion

        #region On... Stuff

        protected virtual void OnPlayerAdded(LobbyMember member)
        {
            // Notify others about the new user
            var msg = MessageHelper.Create((short)MsfMessageCodes.LobbyMemberJoined, member.GenerateDataPacket().ToBytes());

            // Don't send to the person who just joined
            Broadcast(msg, p => p != member.Extension.Peer);
        }

        protected virtual void OnPlayerRemoved(LobbyMember member)
        {
            // Destroy lobby if last member left
            if (!Config.KeepAliveWithZeroPlayers && membersList.Count == 0)
            {
                Destroy();
                Logger.Log(LogLevel.Info, string.Format("Lobby \"{0}\" destroyed due to last player leaving.", Name));
            }

            // Notify others about the user who left
            Broadcast(MessageHelper.Create((short)MsfMessageCodes.LobbyMemberLeft, member.Username));
        }

        protected virtual void OnLobbyStateChange(LobbyState state)
        {
            switch (state)
            {
                case LobbyState.FailedToStart:
                    StatusText = "Failed to start server";
                    break;
                case LobbyState.Preparations:
                    StatusText = "Failed to start server";
                    break;
                case LobbyState.StartingGameServer:
                    StatusText = "Starting game server";
                    break;
                case LobbyState.GameInProgress:
                    StatusText = "Game in progress";
                    break;
                case LobbyState.GameOver:
                    StatusText = "Game is over";
                    break;
                default:
                    StatusText = "Unknown lobby state";
                    break;
            }

            // Disable ready states
            foreach (var lobbyMember in membersList.Values)
            {
                SetReadyState(lobbyMember, false);
            }

            var msg = MessageHelper.Create((short)MsfMessageCodes.LobbyStateChange, (int)state);
            Broadcast(msg);
        }

        private void OnStatusTextChange(string text)
        {
            var msg = MessageHelper.Create((short)MsfMessageCodes.LobbyStatusTextChange, text);
            Broadcast(msg);
        }

        protected virtual void OnLobbyPropertyChange(string propertyKey)
        {
            var packet = new StringPairPacket()
            {
                A = propertyKey,
                B = propertiesList[propertyKey]
            };

            // Broadcast new properties
            Broadcast(MessageHelper.Create((short)MsfMessageCodes.LobbyPropertyChanged, packet.ToBytes()));
        }

        protected virtual void OnPlayerPropertyChange(LobbyMember member, string propertyKey)
        {
            // Broadcast the changes
            var changesPacket = new LobbyMemberPropChangePacket()
            {
                LobbyId = Id,
                Username = member.Username,
                Property = propertyKey,
                Value = member.GetProperty(propertyKey)
            };

            Broadcast(MessageHelper.Create((short)MsfMessageCodes.LobbyMemberPropertyChanged, changesPacket.ToBytes()));
        }

        protected virtual void OnPlayerTeamChanged(LobbyMember member, LobbyTeam newTeam)
        {
            var packet = new StringPairPacket()
            {
                A = member.Username,
                B = newTeam.Name
            };

            // Broadcast the change
            var msg = MessageHelper.Create((short)MsfMessageCodes.LobbyMemberChangedTeam, packet.ToBytes());
            Broadcast(msg);
        }

        /// <summary>
        /// Invoked when one of the members disconnects
        /// </summary>
        /// <param name="session"></param>
        protected virtual void OnPeerDisconnected(IPeer peer)
        {
            RemovePlayer(peer.GetExtension<LobbyUserPeerExtension>());
        }

        protected virtual void OnPlayerReadyStatusChange(LobbyMember member)
        {
            // Broadcast the new status
            var packet = new StringPairPacket()
            {
                A = member.Username,
                B = member.IsReady.ToString()
            };

            Broadcast(MessageHelper.Create((short)MsfMessageCodes.LobbyMemberReadyStatusChange, packet.ToBytes()));
        }

        protected virtual void OnGameMasterChange()
        {
            var masterUsername = GameMaster != null ? GameMaster.Username : "";
            var msg = MessageHelper.Create((short)MsfMessageCodes.LobbyMasterChange, masterUsername);
            Broadcast(msg);
        }

        protected virtual void OnAllPlayersReady()
        {
            if (!Config.StartGameWhenAllReady)
            {
                return;
            }

            if (teamsList.Values.Any(t => t.PlayerCount < t.MinPlayers))
            {
                return;
            }

            StartGame();
        }

        #endregion
    }
}