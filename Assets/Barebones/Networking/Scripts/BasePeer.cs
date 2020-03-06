﻿using Barebones.Logging;
using Barebones.MasterServer;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Barebones.Networking
{
    /// <summary>
    /// This is an abstract implementation of <see cref="IPeer" /> interface,
    /// which handles acknowledgements and SendMessage overloads.
    /// Extend this, if you want to implement custom protocols
    /// </summary>
    public abstract class BasePeer : IPeer
    {
        private readonly Dictionary<int, ResponseCallback> _acks;
        protected readonly List<long[]> _ackTimeoutQueue;
        private readonly Dictionary<int, object> _data;
        private int _id = -1;
        private int _nextAckId = 1;
        private IIncommingMessage _timeoutMessage;
        private Dictionary<Type, IPeerExtension> extensionsList;
        private static readonly object _idGenerationLock = new object();
        private static int _peerIdGenerator;

        /// <summary>
        /// Default timeout, after which response callback is invoked with
        /// timeout status.
        /// </summary>
        public static int DefaultTimeoutSecs = 60;
        public static bool DontCatchExceptionsInEditor = true;

        /// <summary>
        /// True, if connection is stil valid
        /// </summary>
        public abstract bool IsConnected { get; }

        protected BasePeer()
        {
            _data = new Dictionary<int, object>();
            _acks = new Dictionary<int, ResponseCallback>(30);
            _ackTimeoutQueue = new List<long[]>();
            extensionsList = new Dictionary<Type, IPeerExtension>();

            MsfTimer.Instance.OnTickEvent += HandleAckDisposalTick;

            _timeoutMessage = new IncommingMessage(-1, 0, "Time out".ToBytes(), DeliveryMethod.Reliable, this)
            {
                Status = ResponseStatus.Timeout
            };
        }

        /// <summary>
        /// Fires when peer received message
        /// </summary>
        public event Action<IIncommingMessage> OnMessageReceivedEvent;

        /// <summary>
        /// Fires when peer disconnects
        /// </summary>
        public event PeerActionHandler OnPeerDisconnectedEvent;

        /// <summary>
        /// Current peer info
        /// </summary>
        public IPeer Peer { get; private set; }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        public void SendMessage(short opCode)
        {
            SendMessage(MessageHelper.Create(opCode), DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        public void SendMessage(short opCode, ISerializablePacket packet)
        {
            SendMessage(MessageHelper.Create(opCode, packet), DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        /// <param name="method"></param>
        public void SendMessage(short opCode, ISerializablePacket packet, DeliveryMethod method)
        {
            SendMessage(MessageHelper.Create(opCode, packet), method);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        /// <param name="responseCallback"></param>
        public void SendMessage(short opCode, ISerializablePacket packet, ResponseCallback responseCallback)
        {
            var message = MessageHelper.Create(opCode, packet.ToBytes());
            SendMessage(message, responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="packet"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        public void SendMessage(short opCode, ISerializablePacket packet, ResponseCallback responseCallback, int timeoutSecs)
        {
            var message = MessageHelper.Create(opCode, packet.ToBytes());
            SendMessage(message, responseCallback, timeoutSecs, DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="responseCallback"></param>
        public void SendMessage(short opCode, ResponseCallback responseCallback)
        {
            SendMessage(MessageHelper.Create(opCode), responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        public void SendMessage(short opCode, byte[] data)
        {
            SendMessage(MessageHelper.Create(opCode, data), DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="ackCallback"></param>
        public void SendMessage(short opCode, byte[] data, ResponseCallback ackCallback)
        {
            var message = MessageHelper.Create(opCode, data);
            SendMessage(message, ackCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        public void SendMessage(short opCode, byte[] data, ResponseCallback responseCallback, int timeoutSecs)
        {
            var message = MessageHelper.Create(opCode, data);
            SendMessage(message, responseCallback, timeoutSecs);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        public void SendMessage(short opCode, string data)
        {
            SendMessage(MessageHelper.Create(opCode, data), DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        public void SendMessage(short opCode, string data, ResponseCallback responseCallback)
        {
            var message = MessageHelper.Create(opCode, data);
            SendMessage(message, responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        public void SendMessage(short opCode, string data, ResponseCallback responseCallback, int timeoutSecs)
        {
            var message = MessageHelper.Create(opCode, data);
            SendMessage(message, responseCallback, timeoutSecs);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        public void SendMessage(short opCode, int data)
        {
            SendMessage(MessageHelper.Create(opCode, data), DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        public void SendMessage(short opCode, int data, ResponseCallback responseCallback)
        {
            var message = MessageHelper.Create(opCode, data);
            SendMessage(message, responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        public void SendMessage(short opCode, int data, ResponseCallback responseCallback, int timeoutSecs)
        {
            var message = MessageHelper.Create(opCode, data);
            SendMessage(message, responseCallback, timeoutSecs);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message"></param>
        public void SendMessage(IMessage message)
        {
            SendMessage(message, DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <returns></returns>
        public int SendMessage(IMessage message, ResponseCallback responseCallback)
        {
            return SendMessage(message, responseCallback, DefaultTimeoutSecs);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <param name="timeoutSecs">If peer fails to respons within this time frame, callback will be invoked with timeout status</param>
        /// <returns></returns>
        public int SendMessage(IMessage message, ResponseCallback responseCallback, int timeoutSecs)
        {
            return SendMessage(message, responseCallback, timeoutSecs, DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <param name="timeoutSecs">If peer fails to respons within this time frame, callback will be invoked with timeout status</param>
        /// <param name="deliveryMethod">Delivery method</param>
        /// <returns></returns>
        public int SendMessage(IMessage message, ResponseCallback responseCallback, int timeoutSecs, DeliveryMethod deliveryMethod)
        {
            if (!IsConnected)
            {
                responseCallback.Invoke(ResponseStatus.NotConnected, null);
                return -1;
            }

            var id = RegisterAck(message, responseCallback, timeoutSecs);
            SendMessage(message, deliveryMethod);
            return id;
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="deliveryMethod">Delivery method</param>
        /// <returns></returns>
        public abstract void SendMessage(IMessage message, DeliveryMethod deliveryMethod);

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseCallback"></param>
        void IMsgDispatcher<IPeer>.SendMessage(IMessage message, ResponseCallback responseCallback)
        {
            SendMessage(message, responseCallback);
        }

        /// <summary>
        /// Sends a message to peer
        /// </summary>
        /// <param name="message"></param>
        /// <param name="responseCallback"></param>
        /// <param name="timeoutSecs"></param>
        void IMsgDispatcher<IPeer>.SendMessage(IMessage message, ResponseCallback responseCallback, int timeoutSecs)
        {
            SendMessage(message, responseCallback, timeoutSecs);
        }

        /// <summary>
        /// Saves data into peer
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public void SetProperty(int id, object data)
        {
            if (_data.ContainsKey(id))
            {
                _data[id] = data;
            }
            else
            {
                _data.Add(id, data);
            }
        }

        /// <summary>
        /// Retrieves data from peer, which was stored with <see cref="SetProperty" />
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public object GetProperty(int id)
        {
            _data.TryGetValue(id, out object value);
            return value;
        }

        /// <summary>
        /// Retrieves data from peer, which was stored with <see cref="SetProperty" />
        /// </summary>
        /// <param name="id"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public object GetProperty(int id, object defaultValue)
        {
            var obj = GetProperty(id);
            return obj ?? defaultValue;
        }

        /// <summary>
        /// Add any <see cref="IPeerExtension"/> to peer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="extension"></param>
        /// <returns></returns>
        public T AddExtension<T>(T extension) where T : IPeerExtension
        {
            extensionsList[typeof(T)] = extension;
            return extension;
        }

        /// <summary>
        /// Get any <see cref="IPeerExtension"/> from peer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetExtension<T>() where T : IPeerExtension
        {
            if (HasExtension<T>())
            {
                return (T)extensionsList[typeof(T)];
            }
            else
            {
                return default;
            }
        }

        /// <summary>
        /// Check if peer has <see cref="IPeerExtension"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool HasExtension<T>()
        {
            return extensionsList.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Destroy peer
        /// </summary>
        public void Dispose()
        {
            MsfTimer.Instance.OnTickEvent -= HandleAckDisposalTick;
        }

        /// <summary>
        ///     Force disconnect
        /// </summary>
        /// <param name="reason"></param>
        public abstract void Disconnect(string reason);

        public void NotifyDisconnectEvent()
        {
            OnPeerDisconnectedEvent?.Invoke(this);
        }

        protected void NotifyMessageEvent(IIncommingMessage message)
        {
            OnMessageReceivedEvent?.Invoke(message);
        }

        protected int RegisterAck(IMessage message, ResponseCallback responseCallback,
            int timeoutSecs)
        {
            int id;

            lock (_acks)
            {
                id = _nextAckId++;
                _acks.Add(id, responseCallback);
            }

            message.AckRequestId = id;

            StartAckTimeout(id, timeoutSecs);
            return id;
        }

        protected void TriggerAck(int ackId, ResponseStatus statusCode, IIncommingMessage message)
        {
            ResponseCallback ackCallback;
            lock (_acks)
            {
                _acks.TryGetValue(ackId, out ackCallback);

                if (ackCallback == null)
                {
                    return;
                }

                _acks.Remove(ackId);
            }
            ackCallback(statusCode, message);
        }

        private void StartAckTimeout(int ackId, int timeoutSecs)
        {
            // +1, because it might be about to tick in a few miliseconds
            _ackTimeoutQueue.Add(new[] { ackId, MsfTimer.Instance.CurrentTick + timeoutSecs + 1 });
        }

        public virtual void HandleMessage(IIncommingMessage message)
        {
            OnMessageReceivedEvent?.Invoke(message);
        }

        public void HandleDataReceived(byte[] buffer, int start)
        {
            IIncommingMessage message = null;

            try
            {
                message = MessageHelper.FromBytes(buffer, start, this);

                if (message.AckRequestId.HasValue)
                {
                    // We received a message which is a response to our ack request
                    TriggerAck(message.AckRequestId.Value, message.Status, message);
                    return;
                }
            }
            catch (Exception e)
            {
#if UNITY_EDITOR
                if (DontCatchExceptionsInEditor)
                {
                    throw e;
                }
#endif

                Debug.LogError("Failed parsing an incomming message: " + e);

                return;
            }

            HandleMessage(message);
        }

        #region Ack Disposal Stuff

        /// <summary>
        ///     Unique id
        /// </summary>
        public int Id
        {
            get
            {
                if (_id < 0)
                {
                    lock (_idGenerationLock)
                    {
                        if (_id < 0)
                        {
                            _id = _peerIdGenerator++;
                        }
                    }
                }

                return _id;
            }
        }

        /// <summary>
        ///     Called when ack disposal thread ticks
        /// </summary>
        private void HandleAckDisposalTick(long currentTick)
        {
            // TODO test with ordered queue, might be more performant
            _ackTimeoutQueue.RemoveAll(a =>
            {
                if (a[1] > currentTick)
                {
                    return false;
                }

                try
                {
                    CancelAck((int)a[0], ResponseStatus.Timeout);
                }
                catch (Exception e)
                {
                    Logs.Error(e);
                }

                return true;
            });
        }

        private void CancelAck(int ackId, ResponseStatus responseCode)
        {
            ResponseCallback ackCallback;
            lock (_acks)
            {
                _acks.TryGetValue(ackId, out ackCallback);

                if (ackCallback == null)
                {
                    return;
                }

                _acks.Remove(ackId);
            }
            ackCallback(responseCode, _timeoutMessage);
        }

        #endregion
    }
}