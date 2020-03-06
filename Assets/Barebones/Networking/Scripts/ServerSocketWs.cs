using Barebones.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Barebones.Networking
{
    /// <summary>
    /// Server socket, which accepts websocket connections
    /// </summary>
    public class ServerSocketWs : IServerSocket, IUpdatable
    {
        private WebSocketServer server;
        private Queue<Action> executeOnUpdate;
        private float initialSendMessageDelayTime = 0.2f;

        private event Action OnUpdateEvent;

        /// <summary>
        /// Invoked, when a client connects to this socket
        /// </summary>
        public event PeerActionHandler OnClientConnectedEvent;

        /// <summary>
        /// Invoked, when client disconnects from this socket
        /// </summary>
        public event PeerActionHandler OnClientDisconnectedEvent;

        public ServerSocketWs()
        {
            executeOnUpdate = new Queue<Action>();
        }

        /// <summary>
        /// Opens the socket and starts listening to a given port
        /// </summary>
        /// <param name="port"></param>
        public void Listen(int port)
        {
            // Stop listening when application closes
            MsfTimer.Instance.OnApplicationQuitEvent += Stop;

            server = new WebSocketServer(port);

            SetupService(server);

            server.Stop();
            server.Start();

            MsfUpdateRunner.Instance.Add(this);
        }

        /// <summary>
        /// Stops listening
        /// </summary>
        public void Stop()
        {
            MsfUpdateRunner.Instance.Remove(this);
            server.Stop();
        }

        public void ExecuteOnUpdate(Action action)
        {
            lock (executeOnUpdate)
            {
                executeOnUpdate.Enqueue(action);
            }
        }

        private void SetupService(WebSocketServer server)
        {
            server.AddWebSocketService<WsService>("/msf", (service) =>
            {
                service.IgnoreExtensions = true;
                service.SetServerSocket(this);
                var peer = new PeerWsServer(service);

                service.OnMessageEvent += (data) =>
                {
                    peer.HandleDataReceived(data, 0);
                };

                ExecuteOnUpdate(() =>
                {
                    MsfTimer.Instance.StartCoroutine(peer.SendDelayedMessages(initialSendMessageDelayTime));
                    OnClientConnectedEvent?.Invoke(peer);
                });

                peer.OnPeerDisconnectedEvent += OnClientDisconnectedEvent;

                service.OnCloseEvent += reason =>
                {
                    peer.NotifyDisconnectEvent();
                };

                service.OnErrorEvent += reason =>
                {
                    Logs.Error(reason);
                    peer.NotifyDisconnectEvent();
                };
            });
        }

        public void Update()
        {
            OnUpdateEvent?.Invoke();

            lock (executeOnUpdate)
            {
                while (executeOnUpdate.Count > 0)
                {
                    executeOnUpdate.Dequeue()?.Invoke();
                }
            }
        }

        /// <summary>
        /// Web socket service, designed to work with unitys main thread
        /// </summary>
        public class WsService : WebSocketBehavior
        {
            private ServerSocketWs _serverSocket;

            public event Action OnOpenEvent;
            public event Action<string> OnCloseEvent;
            public event Action<string> OnErrorEvent;
            public event Action<byte[]> OnMessageEvent;

            private Queue<byte[]> _messageQueue;

            public WsService()
            {
                IgnoreExtensions = true;
                _messageQueue = new Queue<byte[]>();
            }

            public WsService(ServerSocketWs serverSocket)
            {
                IgnoreExtensions = true;
                _messageQueue = new Queue<byte[]>();

                _serverSocket = serverSocket;
                _serverSocket.OnUpdateEvent += Update;
            }

            public void SetServerSocket(ServerSocketWs serverSocket)
            {
                if (_serverSocket == null)
                {
                    _serverSocket = serverSocket;
                    _serverSocket.OnUpdateEvent += Update;
                }
            }

            private void Update()
            {
                if (_messageQueue.Count <= 0)
                {
                    return;
                }

                lock (_messageQueue)
                {
                    // Notify about new messages
                    while (_messageQueue.Count > 0)
                    {
                        OnMessageEvent?.Invoke(_messageQueue.Dequeue());
                    }
                }
            }

            protected override void OnOpen()
            {
                _serverSocket.ExecuteOnUpdate(() =>
                {
                    OnOpenEvent?.Invoke();
                });
            }

            protected override void OnClose(CloseEventArgs e)
            {
                _serverSocket.OnUpdateEvent -= Update;

                _serverSocket.ExecuteOnUpdate(() =>
                {
                    OnCloseEvent?.Invoke(e.Reason);
                });
            }

            protected override void OnError(ErrorEventArgs e)
            {
                _serverSocket.OnUpdateEvent -= Update;

                _serverSocket.ExecuteOnUpdate(() =>
                {
                    OnErrorEvent?.Invoke(e.Message);
                });
            }

            protected override void OnMessage(MessageEventArgs e)
            {
                lock (_messageQueue)
                {
                    _messageQueue.Enqueue(e.RawData);
                }
            }

            public void SendData(byte[] data)
            {
                Send(data);
            }

            public void Disconnect()
            {
                Sessions.CloseSession(ID);
            }
        }
    }
}