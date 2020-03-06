using Aevien.UI;
using Barebones.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer.Examples
{
    public class ClientConnectionView : MonoBehaviour, IUIViewComponent
    {
        private TMP_InputField ipAddressInputField;
        private TMP_InputField portInputField;
        private TextMeshProUGUI connectionStatusText;

        private Button connectButton;
        private Button quitButton;

        /// <summary>
        /// Owner of this <see cref="IUIViewComponent"/>
        /// </summary>
        public IUIView Owner { get; set; }

        /// <summary>
        /// Get IP address of master server
        /// </summary>
        public string IpAddress => ipAddressInputField.text;

        /// <summary>
        /// Get connection port to master server
        /// </summary>
        public int Port => Convert.ToInt32(portInputField.text);

        public void OnOwnerAwake()
        {
            ipAddressInputField = Owner.ChildComponent<TMP_InputField>("IpAddressInputField");
            portInputField = Owner.ChildComponent<TMP_InputField>("PortInputField");
            connectionStatusText = Owner.ChildComponent<TextMeshProUGUI>("ConnectionStatus");

            //connectButton = Owner.ChildComponent<Button>

            Msf.Connection.OnStatusChangedEvent += OnStatusChangedEventHandler;
        }

        public void OnOwnerHide(IUIView owner) { }

        public void OnOwnerShow(IUIView owner) { }

        public void OnOwnerStart() { }

        public void StartConnection()
        {
            Debug.Log("Start connection");

            Msf.Connection.Connect(IpAddress, Port);
        }

        private void OnStatusChangedEventHandler(ConnectionStatus status)
        {
            if (connectionStatusText)
            {
                switch (status)
                {
                    case ConnectionStatus.Connecting:
                        connectionStatusText.text = "Connection status: Connecting";
                        break;
                    case ConnectionStatus.Connected:
                        connectionStatusText.text = "Connection status: Connected";
                        break;
                    case ConnectionStatus.Disconnected:
                        connectionStatusText.text = "Connection status: Offline";
                        break;
                    case ConnectionStatus.None:
                    default:
                        connectionStatusText.text = "Connection status: Undefined";
                        break;
                }
            }

            if (ipAddressInputField)
            {
                ipAddressInputField.interactable = !(status == ConnectionStatus.Connecting || status == ConnectionStatus.Connected);
            }

            if (portInputField)
            {
                portInputField.interactable = !(status == ConnectionStatus.Connecting || status == ConnectionStatus.Connected);
            }
        }
    }
}
