using Barebones.MasterServer;
using Barebones.Networking;
using UnityEngine;
using TMPro;
using System.Text.RegularExpressions;
using Aevien.UI;

namespace GW.MasterServer
{
    public class Chat_Manager : MonoBehaviour
    {
        enum ChatChannel { Global, Private, Channel }
        [SerializeField]
        ChatChannel chatChannel;

        [SerializeField]
        private TMP_InputField chatInputField;
        [SerializeField]
        private Transform chatPanel;
        [SerializeField]
        private GameObject chatMessage;
        [SerializeField]
        private TMP_Text chatChannelLabel;

        private ProfileSettings_View profileSettingsView;

        bool chatIsActive;
        int chatType;

        private void Awake()
        {
            profileSettingsView = ViewsManager.GetView<ProfileSettings_View>("ProfileSettingsView");
            Msf.Client.Chat.OnMessageReceivedEvent += OnMessageReceived;
            chatChannelLabel.text = chatChannel.ToString();
            chatChannel = ChatChannel.Global;
        }

        public void JoinChat()
        {
            MsfTimer.WaitForEndOfFrame(() =>
            {
                Msf.Client.Chat.JoinChannel("Global", (successful, error) =>
                {
                    if (!successful)
                    {
                        Msf.Events.Invoke(Event_Keys.showLoadingInfo, error);
                        Debug.Log(error);
                    }
                });

                Msf.Client.Chat.SetDefaultChannel("Global", (successful, error) =>
                {
                    if (!successful)
                    {
                        Msf.Events.Invoke(Event_Keys.showLoadingInfo, error);
                        Debug.Log(error);
                    }                        
                });
            });
        }

        private void Update()
        {
            if(chatIsActive && Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleChatChannel();
            }
        }

        public void ToggleChatChannel()
        {
            chatType = (int)chatChannel;
            //Set chatType == X to highest value of enum
            chatType = chatType == 1 ? 0 : chatType + 1; 
            chatChannel = (ChatChannel)chatType;
            chatChannelLabel.text = chatChannel.ToString();
        }

        //Takes input from the chatInputField to detect what type of chat to send
        public void SelectChatType()
        {
            switch (chatChannel)
            {
                case ChatChannel.Global:
                    SendChat();
                    break;
                case ChatChannel.Private:
                    DetectPlayernameForPrivateMessage();
                    break;
                case ChatChannel.Channel:
                    //Create a method to take channel name here
                    break;
            }
        }

        void SendChat()
        {
            string message = chatInputField.text;

            // Send a message to default channel
            if (message != "" && chatChannel == ChatChannel.Global)            
                Msf.Client.Chat.SendToDefaultChannel(message, (successful, error) => { });
        }

        #region PrivateMessage
        void DetectPlayernameForPrivateMessage()
        {
            string message = chatInputField.text;

            //Detect text input between quotations
            var reg = new Regex("\".*?\"");
            var username = reg.Match(message);
            int removeText = username.ToString().Length;

            //Remove "Playername" plus whitepace from start of message
            string messageToSend = message.Substring(removeText + 1);

            SendPrivateMessage(username.ToString().Trim('"'), messageToSend);
        }

        void SendPrivateMessage(string username, string message)
        {
            // Send a private message
            if (message != "" && chatChannel == ChatChannel.Private)
                Msf.Client.Chat.SendPrivateMessage(username, message, (successful, error) => 
                {
                    if (!successful)
                        UserNotOnlineMessage(username);
                });
        }

        void UserNotOnlineMessage(string username)
        {
            GameObject chat = Instantiate(chatMessage, chatPanel);
            TMP_Text tChat = chat.GetComponentInChildren<TMP_Text>();

            tChat.text =  "*** \"" + username + "\"" + " not found or user is offline! ***";
            tChat.color = Color.red;

            ClearTextField();
        }
        #endregion PrivateMessage

        private void OnMessageReceived(ChatMessagePacket message)
        {
            GameObject chat = Instantiate(chatMessage, chatPanel);
            TMP_Text tChat = chat.GetComponentInChildren<TMP_Text>();

            switch (message.MessageType)
            {
                // Received a private message
                case ChatMessageType.PrivateMessage:
                    string messageToDisplay = string.Format("From [{0}]: {1}",
                        message.Sender, // User name
                        message.Message);  
                    if(message.Sender != profileSettingsView.DisplayName)
                    {
                        tChat.text = messageToDisplay;
                        tChat.color = Color.magenta;
                    }                    
                    ClearTextField();
                    break;

                //Received a channel message
                case ChatMessageType.ChannelMessage:                    
                    messageToDisplay = string.Format("[{0}] [{1}]: {2}",
                        message.Receiver, //Channel name
                        message.Sender, //User name
                        message.Message);
                    tChat.text = messageToDisplay;
                    ClearTextField();
                    break;
            }
        }
        
        public void SetChatActive()
        {
            chatIsActive = true;
        }

        public void SetChatInactive()
        {
            chatIsActive = false;
        }

        void ClearTextField()
        {
            chatInputField.text = "";
        }

        void OnDestroy()
        {
            Msf.Client.Chat.OnMessageReceivedEvent -= OnMessageReceived;
        }
    }
}