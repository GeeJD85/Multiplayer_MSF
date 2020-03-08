using Aevien.UI;
using Barebones.MasterServer;
using Barebones.Networking;
using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace GW.MasterServer
{
    public class Profiles_Manager : BaseClientModule
    {
        public ObservableProfile Profile { get; private set; }

        private Profile_View profileView;
        private ProfileSettings_View profileSettingsView;

        public event Action<short, IObservableProperty> OnPropertyUpdatedEvent;
        public UnityEvent OnProfileLoadedEvent;
        public UnityEvent OnProfileSavedEvent;

        protected override void Initialize()
        {
            profileView = ViewsManager.GetView<Profile_View>("ProfileView");
            profileSettingsView = ViewsManager.GetView<ProfileSettings_View>("ProfileSettingsView");

            Profile = new ObservableProfile
            {
                new ObservableString((short)ObservablePropertiyCodes.DisplayName),
                new ObservableString((short)ObservablePropertiyCodes.Avatar),
                new ObservableFloat((short)ObservablePropertiyCodes.Level),
                new ObservableFloat((short)ObservablePropertiyCodes.XP),
                new ObservableFloat((short)ObservablePropertiyCodes.Gold)
            };

            Profile.OnPropertyUpdatedEvent += OnPropertyUpdatedEventHandler;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            Profile.OnPropertyUpdatedEvent -= OnPropertyUpdatedEventHandler;
        }

        private void OnPropertyUpdatedEventHandler(short key, IObservableProperty property)
        {
            OnPropertyUpdatedEvent?.Invoke(key, property);

            logger.Debug($"Property with code: {key} were updated: {property.Serialize()}");
        }

        public void LoadProfile()
        {
            Msf.Events.Invoke(Event_Keys.showLoadingInfo, "Loading profile... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                Msf.Client.Profiles.GetProfileValues(Profile, (isSuccessful, error) =>
                {
                    if (isSuccessful)
                    {
                        Msf.Events.Invoke(Event_Keys.hideLoadingInfo);
                        OnProfileLoadedEvent?.Invoke();
                    }
                    else
                    {
                        Msf.Events.Invoke(Event_Keys.showOkDialogBox,
                            new OkDialogBox_ViewEventMessage($"When requesting profile data an error occurred. [{error}]"));
                    }
                });
            });
        }

        public void UpdateProfile()
        {
            Msf.Events.Invoke(Event_Keys.showLoadingInfo, "Saving profile data... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                var data = new Dictionary<string, string>
                {
                    { "displayName", profileSettingsView.DisplayName },
                    { "avatarUrl", profileSettingsView.AvatarUrl }
                };

                Connection.SendMessage((short)MsfMessageCodes.UpdateDisplayNameRequest, data.ToBytes(), OnSaveProfileResponseCallback);
            });
        }

        //Sends message to CustomProfiles_Module to update a given value
        //TODO: Edit method to take a string and value so method can be used for Level, XP and Gold
        public void UpdateGold(float value)
        {
            Msf.Events.Invoke(Event_Keys.showLoadingInfo, "Saving profile data... Please wait!");

            MsfTimer.WaitForSeconds(1f, () =>
            {
                var data = new Dictionary<string, float>
                {
                    { "gold", value },
                };

                Connection.SendMessage((short)MsfMessageCodes.UpdateGoldRequest, data.ToBytes(), OnSaveProfileResponseCallback);
            });
        }

        private void OnSaveProfileResponseCallback(ResponseStatus status, IIncommingMessage response)
        {
            Msf.Events.Invoke(Event_Keys.hideLoadingInfo);

            if (status == ResponseStatus.Success)
            {
                OnProfileSavedEvent?.Invoke();

                logger.Debug("Your profile is successfuly updated and saved");
            }
            else
            {
                Msf.Events.Invoke(Event_Keys.showOkDialogBox, new OkDialogBox_ViewEventMessage(response.AsString()));
                logger.Error(response.AsString());
            }
        }
    }
}