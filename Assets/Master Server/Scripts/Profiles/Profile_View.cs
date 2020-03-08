using Aevien.UI;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Barebones.MasterServer;

namespace GW.MasterServer
{
    public class Profile_View : UIView
    {
        private Image avatarImage;
        private Profiles_Manager profilesManager;
        private UIProperty displayNameUIProperty;
        private UIProperty levelUIProperty;
        private UIProperty xpUIProperty;
        private UIProperty goldUIProperty;

        public string DisplayName
        {
            get
            {
                return displayNameUIProperty ? displayNameUIProperty.Lable : string.Empty;
            }

            set
            {
                if (displayNameUIProperty)
                    displayNameUIProperty.Lable = value;
            }
        }

        public string Level
        {
            get
            {
                return levelUIProperty ? levelUIProperty.Lable : string.Empty;
            }

            set
            {
                if (levelUIProperty)
                    levelUIProperty.Lable = value;
            }
        }

        public string XP
        {
            get
            {
                return xpUIProperty ? xpUIProperty.Lable : string.Empty;
            }

            set
            {
                if (xpUIProperty)
                    xpUIProperty.Lable = value;
            }
        }

        public string Gold
        {
            get
            {
                return goldUIProperty ? goldUIProperty.Lable : string.Empty;
            }

            set
            {
                if (goldUIProperty)
                    goldUIProperty.Lable = value;
            }
        }

        protected override void Start()
        {
            base.Start();

            if (!profilesManager)
            {
                profilesManager = FindObjectOfType<Profiles_Manager>();
            }

            profilesManager.OnPropertyUpdatedEvent += ProfilesManager_OnPropertyUpdatedEvent;

            avatarImage = ChildComponent<Image>("avatarImage");
            displayNameUIProperty = ChildComponent<UIProperty>("displayNameUIProperty");

            levelUIProperty = ChildComponent<UIProperty>("levelUIProperty");
            xpUIProperty = ChildComponent<UIProperty>("xpUIProperty");
            goldUIProperty = ChildComponent<UIProperty>("goldUIProperty");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (profilesManager)
            {
                profilesManager.OnPropertyUpdatedEvent -= ProfilesManager_OnPropertyUpdatedEvent;
            }
        }

        private void ProfilesManager_OnPropertyUpdatedEvent(short key, IObservableProperty property)
        {
            if (key == (short)ObservablePropertiyCodes.DisplayName)
            {
                DisplayName = $"Name: {property.Serialize()}";
            }
            else if (key == (short)ObservablePropertiyCodes.Avatar)
            {
                LoadAvatarImage(property.Serialize());
            }
            else if (key == (short)ObservablePropertiyCodes.Level)
            {
                Level = $"Level: {property.CastTo<ObservableFloat>().GetValue().ToString("F2")}";
            }
            else if (key == (short)ObservablePropertiyCodes.XP)
            {
                XP = $"XP: {property.CastTo<ObservableFloat>().GetValue().ToString("F2")}";
            }
            else if (key == (short)ObservablePropertiyCodes.Gold)
            {
                Gold = $"Gold: {property.CastTo<ObservableFloat>().GetValue().ToString("F2")}";
            }
        }

        private void LoadAvatarImage(string url)
        {
            StartCoroutine(StartLoadAvatarImage(url));
        }

        private IEnumerator StartLoadAvatarImage(string url)
        {
            UnityWebRequest www = UnityWebRequestTexture.GetTexture(url);

            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Debug.Log(www.error);
            }
            else
            {
                var myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
                avatarImage.sprite = null;
                avatarImage.sprite = Sprite.Create(myTexture, new Rect(0f, 0f, myTexture.width, myTexture.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}