using Barebones.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Barebones.Networking
{
    /// <summary>
    /// Client for connecting to websocket server.
    /// </summary>
    public class ClientSocketWs : BaseClientSocket<PeerWs>, IClientSocket, IUpdatable
    {
        IPeer IMsgDispatcher<IPeer>.Peer { get; }

        private WebSocket webSocket;
        private ConnectionStatus status;
        private readonly Dictionary<short, IPacketHandler> handlers;
        private static bool rethrowExceptionsInEditor = true;

        public bool IsConnected { get; private set; } = false;
        public bool IsConnecting { get { return status == ConnectionStatus.Connecting; } }
        public string ConnectionIp { get; private set; }
        public int ConnectionPort { get; private set; }

        public event Action OnConnectedEvent;
        public event Action OnDisconnectedEvent;
        public event Action<ConnectionStatus> OnStatusChangedEvent;

        public ClientSocketWs()
        {
            SetStatus(ConnectionStatus.Disconnected);
            handlers = new Dictionary<short, IPacketHandler>();
        }

        /// <summary>
        /// Invokes a callback when connection is established, or after the timeout
        /// (even if failed to connect). If already connected, callback is invoked instantly
        /// </summary>
        /// <param name="connectionCallback"></param>
        /// <param name="timeoutSeconds"></param>
        public void WaitConnection(Action<IClientSocket> connectionCallback, float timeoutSeconds)
        {
            if (IsConnected)
            {
                connectionCallback.Invoke(this);
                return;
            }

            var isConnected = false;
            var timedOut = false;

            // Make local function
            void onConnected()
            {
                OnConnectedEvent -= onConnected;
                isConnected = true;

                if (!timedOut)
                {
                    connectionCallback.Invoke(this);
                }
            }

            // Listen to connection event
            OnConnectedEvent += onConnected;

            // Wait for some seconds
            MsfTimer.WaitForSeconds(timeoutSeconds, () =>
            {
                if (!isConnected)
                {
                    timedOut = true;
                    OnConnectedEvent -= onConnected;
                    connectionCallback.Invoke(this);
                }
            });
        }

        /// <summary>
        /// Invokes a callback when connection is established, or after the timeout
        /// (even if failed to connect). If already connected, callback is invoked instantly
        /// </summary>
        /// <param name="connectionCallback"></param>
        public void WaitConnection(Action<IClientSocket> connectionCallback)
        {
            WaitConnection(connectionCallback, 10);
        }

        /// <summary>
        /// Adds a listener, which is invoked when connection is established,
        /// or instantly, if already connected and  <see cref="invokeInstantlyIfConnected"/> 
        /// is true
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="invokeInstantlyIfConnected"></param>
        public void AddConnectionListener(Action callback, bool invokeInstantlyIfConnected = true)
        {
            // Remove copy of the callback method to prevent double invocation
            OnConnectedEvent -= callback;

            // Asign callback method again
            OnConnectedEvent += callback;

            if (IsConnected && invokeInstantlyIfConnected)
            {
                callback.Invoke();
            }
        }

        /// <summary>
        /// Removes connection listener
        /// </summary>
        /// <param name="callback"></param>
        public void RemoveConnectionListener(Action callback)
        {
            OnConnectedEvent -= callback;
        }

        /// <summary>
        /// Adds a listener, which is invoked when connection is broken,
        /// or instantly, if already disconnected and invokeInstantlyIfDisconnected 
        /// is true
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="invokeInstantlyIfDisconnected"></param>
        public void AddDisconnectionListener(Action callback, bool invokeInstantlyIfDisconnected = true)
        {
            // Remove copy of the callback method to prevent double invocation
            OnDisconnectedEvent -= callback;

            // Asign callback method again
            OnDisconnectedEvent += callback;

            if (!IsConnected && invokeInstantlyIfDisconnected)
            {
                callback.Invoke();
            }
        }

        /// <summary>
        /// Removes disconnection listener
        /// </summary>
        /// <param name="callback"></param>
        public void RemoveDisconnectionListener(Action callback)
        {
            OnDisconnectedEvent -= callback;
        }

        /// <summary>
        /// Adds a packet handler, which will be invoked when a message of
        /// specific operation code is received
        /// </summary>
        public IPacketHandler SetHandler(IPacketHandler handler)
        {
            handlers[handler.OpCode] = handler;
            return handler;
        }

        /// <summary>
        /// Adds a packet handler, which will be invoked when a message of
        /// specific operation code is received
        /// </summary>
        public IPacketHandler SetHandler(short opCode, IncommingMessageHandler handlerMethod)
        {
            var handler = new PacketHandler(opCode, handlerMethod);
            SetHandler(handler);
            return handler;
        }

        /// <summary>
        /// Removes the packet handler, but only if this exact handler
        /// was used
        /// </summary>
        /// <param name="handler"></param>
        public void RemoveHandler(IPacketHandler handler)
        {
            if (handlers.TryGetValue(handler.OpCode, out IPacketHandler previousHandler) && previousHandler != handler)
            {
                return;
            }

            handlers.Remove(handler.OpCode);
        }

        /// <summary>
        /// Disconnects and connects again
        /// </summary>
        public void Reconnect()
        {
            Disconnect();
            Connect(ConnectionIp, ConnectionPort);
        }

        // Update is called once per frame
        public void Update()
        {
            if (webSocket == null)
            {
                return;
            }

            byte[] data = webSocket.Recv();

            while (data != null)
            {
                Peer.HandleDataReceived(data, 0);
                data = webSocket.Recv();
            }

            bool wasConnected = IsConnected;
            IsConnected = webSocket.IsConnected;

            // Check if status changed
            if (wasConnected != IsConnected)
            {
                SetStatus(IsConnected ? ConnectionStatus.Connected : ConnectionStatus.Disconnected);
            }
        }

        /// <summary>
        /// Connection status
        /// </summary>
        public ConnectionStatus Status
        {
            get
            {
                return status;
            }
            set
            {
                if (status != value)
                {
                    status = value;
                    OnStatusChangedEvent?.Invoke(status);
                }
            }
        }

        private void SetStatus(ConnectionStatus status)
        {
            switch (status)
            {
                case ConnectionStatus.Connecting:

                    if (Status != ConnectionStatus.Connecting)
                    {
                        Status = ConnectionStatus.Connecting;
                    }

                    break;
                case ConnectionStatus.Connected:

                    if (Status != ConnectionStatus.Connected)
                    {
                        Status = ConnectionStatus.Connected;
                        MsfTimer.Instance.StartCoroutine(Peer.SendDelayedMessages());
                        OnConnectedEvent?.Invoke();
                    }

                    break;
                case ConnectionStatus.Disconnected:

                    if (Status != ConnectionStatus.Disconnected)
                    {
                        Status = ConnectionStatus.Disconnected;
                        OnDisconnectedEvent?.Invoke();
                    }

                    break;
            }
        }

        /// <summary>
        /// Starts connecting to another socket
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public IClientSocket Connect(string ip, int port)
        {
            return Connect(ip, port, 10000);
        }

        /// <summary>
        /// Starts connecting to another socket
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="timeoutMillis"></param>
        /// <returns></returns>
        public IClientSocket Connect(string ip, int port, int timeoutMillis)
        {
            ConnectionIp = ip;
            ConnectionPort = port;

            if (webSocket != null && webSocket.IsConnected)
            {
                webSocket.Close();
            }

            IsConnected = false;
            SetStatus(ConnectionStatus.Connecting);

            if (Peer != null)
            {
                Peer.OnMessageReceivedEvent -= HandleMessage;
                Peer.Dispose();
            }

            webSocket = new WebSocket(new Uri($"ws://{ip}:{port}/msf"));

            Logs.Debug(webSocket == null);

            Peer = new PeerWs(webSocket);
            Peer.OnMessageReceivedEvent += HandleMessage;

            MsfUpdateRunner.Instance.Add(this);
            MsfUpdateRunner.Instance.StartCoroutine(webSocket.Connect());

            return this;
        }

        public void Disconnect()
        {
            if (webSocket != null)
            {
                webSocket.Close();
            }

            if (Peer != null)
            {
                Peer.Dispose();
            }

            IsConnected = false;
            SetStatus(ConnectionStatus.Disconnected);
        }

        private void HandleMessage(IIncommingMessage message)
        {
            try
            {
                if (handlers.TryGetValue(message.OpCode, out IPacketHandler handler))
                {
                    handler.Handle(message);
                }
                else if (message.IsExpectingResponse)
                {
                    Logs.Error($"Connection is missing a handler. OpCode: {message.OpCode}");
                    message.Respond(ResponseStatus.Error);
                }
            }
            catch (Exception e)
            {

#if UNITY_EDITOR
                if (rethrowExceptionsInEditor)
                {
                    throw;
                }
#endif

                Logs.Error($"Failed to handle a message. OpCode: {message.OpCode}, Error: {e}");

                if (!message.IsExpectingResponse)
                {
                    return;
                }

                try
                {
                    message.Respond(ResponseStatus.Error);
                }
                catch (Exception exception)
                {
                    Logs.Error(exception);
                }
            }
        }
    }
}