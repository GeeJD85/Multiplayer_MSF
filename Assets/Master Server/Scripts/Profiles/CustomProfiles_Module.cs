using Barebones.Networking;
using Barebones.MasterServer;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GW.MasterServer
{
    public enum ObservablePropertiyCodes { DisplayName, Avatar, Level, XP, Gold }

    public class CustomProfiles_Module : ProfilesModule
    {
        [Header("Start Values"), SerializeField]
        private float level = 1;
        [SerializeField]
        private float xp = 0;
        [SerializeField]
        private float gold = 0;

        public HelpBox _header = new HelpBox()
        {
            Text = "This script is a custom module, which sets up profiles values for new users"
        };

        public override void Initialize(IServer server)
        {
            base.Initialize(server);

            // Set the new factory in ProfilesModule
            ProfileFactory = CreateProfileInServer;

            server.SetHandler((short)MsfMessageCodes.UpdateDisplayNameRequest, UpdateDisplayNameRequestHandler);
            server.SetHandler((short)MsfMessageCodes.UpdateLevelValue, UpdateLevelValueHandler);
            server.SetHandler((short)MsfMessageCodes.UpdateXPValue, UpdateXPValueHandler);
            server.SetHandler((short)MsfMessageCodes.UpdateGoldValue, UpdateGoldValueHandler);
        }

        private ObservableServerProfile CreateProfileInServer(string username, IPeer clientPeer)
        {
            return new ObservableServerProfile(username, clientPeer)
            {
                new ObservableString((short)ObservablePropertiyCodes.DisplayName, username),
                new ObservableString((short)ObservablePropertiyCodes.Avatar, "https://i.imgur.com/JQ9pRoD.png"),
                new ObservableFloat((short)ObservablePropertiyCodes.Level, level),
                new ObservableFloat((short)ObservablePropertiyCodes.XP, xp),
                new ObservableFloat((short)ObservablePropertiyCodes.Gold, gold)
            };
        }

        //Use to update ALL profiles from server side
        //TODO: Make a function to target an individual profile for recovery
        public void AmendLevelValue(float value)
        {
            foreach (var profile in ProfilesList.Values)
            {
                var levelProperty = profile.GetProperty<ObservableFloat>((short)ObservablePropertiyCodes.Level);
                levelProperty.Add(value);
            }
        }

        public void AmendXPValue(float value)
        {
            foreach (var profile in ProfilesList.Values)
            {
                var xpProperty = profile.GetProperty<ObservableFloat>((short)ObservablePropertiyCodes.XP);
                xpProperty.Add(value);
            }
        }

        public void AmendGoldValue(float value)
        {
            foreach (var profile in ProfilesList.Values)
            {
                var goldProperty = profile.GetProperty<ObservableFloat>((short)ObservablePropertiyCodes.Gold);
                goldProperty.Add(value);
            }
        }

        #region Handlers
        private void UpdateDisplayNameRequestHandler(IIncommingMessage message)
        {
            var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

            if (userExtension == null || userExtension.Account == null)
            {
                message.Respond("Invalid session", ResponseStatus.Unauthorized);
                return;
            }

            var newProfileData = new Dictionary<string, string>().FromBytes(message.AsBytes());

            try
            {
                if (ProfilesList.TryGetValue(userExtension.Username, out ObservableServerProfile profile))
                {
                    profile.GetProperty<ObservableString>((short)ObservablePropertiyCodes.DisplayName).Set(newProfileData["displayName"]);
                    profile.GetProperty<ObservableString>((short)ObservablePropertiyCodes.Avatar).Set(newProfileData["avatarUrl"]);

                    message.Respond(ResponseStatus.Success);
                }
                else
                {
                    message.Respond("Invalid session", ResponseStatus.Unauthorized);
                }
            }
            catch (Exception e)
            {
                message.Respond($"Internal Server Error: {e}", ResponseStatus.Error);
            }
        }

        private void UpdateLevelValueHandler(IIncommingMessage message)
        {
            var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

            if (userExtension == null || userExtension.Account == null)
            {
                message.Respond("Invalid session", ResponseStatus.Unauthorized);
                return;
            }

            var newProfileData = new Dictionary<string, float>().FromBytes(message.AsBytes());

            try
            {
                if (ProfilesList.TryGetValue(userExtension.Username, out ObservableServerProfile profile))
                {
                    var xpProperty = profile.GetProperty<ObservableFloat>((short)ObservablePropertiyCodes.Level);
                    xpProperty.Add(newProfileData["level"]);

                    message.Respond(ResponseStatus.Success);
                }
                else
                {
                    message.Respond("Invalid session", ResponseStatus.Unauthorized);
                }
            }
            catch (Exception e)
            {
                message.Respond($"Internal Server Error: {e}", ResponseStatus.Error);
            }
        }

        private void UpdateXPValueHandler(IIncommingMessage message)
        {
            var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

            if (userExtension == null || userExtension.Account == null)
            {
                message.Respond("Invalid session", ResponseStatus.Unauthorized);
                return;
            }

            var newProfileData = new Dictionary<string, float>().FromBytes(message.AsBytes());

            try
            {
                if (ProfilesList.TryGetValue(userExtension.Username, out ObservableServerProfile profile))
                {
                    var xpProperty = profile.GetProperty<ObservableFloat>((short)ObservablePropertiyCodes.XP);
                    xpProperty.Add(newProfileData["xp"]);

                    message.Respond(ResponseStatus.Success);
                }
                else
                {
                    message.Respond("Invalid session", ResponseStatus.Unauthorized);
                }
            }
            catch (Exception e)
            {
                message.Respond($"Internal Server Error: {e}", ResponseStatus.Error);
            }
        }

        private void UpdateGoldValueHandler(IIncommingMessage message)
        {
            var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

            if (userExtension == null || userExtension.Account == null)
            {
                message.Respond("Invalid session", ResponseStatus.Unauthorized);
                return;
            }

            var newProfileData = new Dictionary<string, float>().FromBytes(message.AsBytes());

            try
            {
                if (ProfilesList.TryGetValue(userExtension.Username, out ObservableServerProfile profile))
                {
                    var goldProperty = profile.GetProperty<ObservableFloat>((short)ObservablePropertiyCodes.Gold);
                    goldProperty.Add(newProfileData["gold"]);

                    message.Respond(ResponseStatus.Success);
                }
                else
                {
                    message.Respond("Invalid session", ResponseStatus.Unauthorized);
                }
            }
            catch (Exception e)
            {
                message.Respond($"Internal Server Error: {e}", ResponseStatus.Error);
            }
        }
        #endregion Hndlers
    }
}