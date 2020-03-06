using Barebones.Logging;
using Barebones.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Barebones.MasterServer
{
    public abstract class BaseClientModule : MonoBehaviour
    {
        /// <summary>
        /// Logger connected to this module
        /// </summary>
        protected Logging.Logger logger;

        [Header("Base Module Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;

        public bool IsConnected => Msf.Connection != null && Msf.Connection.IsConnected;

        public IClientSocket Connection => Msf.Connection;

        protected virtual void Awake()
        {
            logger = Msf.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            Msf.Connection.OnStatusChangedEvent += OnConnectionStatusChanged;
        }

        protected virtual void Start()
        {
            Initialize();
            Msf.Connection.AddConnectionListener(ConnectedToMaster);
        }

        protected virtual void OnDestroy()
        {
            Msf.Connection.OnStatusChangedEvent -= OnConnectionStatusChanged;
            Msf.Connection.RemoveConnectionListener(ConnectedToMaster);
        }

        private void ConnectedToMaster()
        {
            Msf.Connection.RemoveConnectionListener(ConnectedToMaster);
            Msf.Connection.AddDisconnectionListener(DisconnectedToMaster);

            OnConnectedToMaster();
        }

        private void DisconnectedToMaster()
        {
            Msf.Connection.AddConnectionListener(ConnectedToMaster);
            Msf.Connection.RemoveDisconnectionListener(DisconnectedToMaster);

            OnDisconnectedFromMaster();
        }

        protected virtual void Initialize() { }

        protected virtual void OnConnectionStatusChanged(ConnectionStatus status) { }

        protected virtual void OnConnectedToMaster() { }

        protected virtual void OnDisconnectedFromMaster() { }
    }
}
