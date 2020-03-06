using Aevien.Utilities;
using Barebones.Logging;
using Barebones.Networking;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Automatically connects to master server
    /// </summary>
    public class ConnectionToMaster : Singleton<ConnectionToMaster>
    {
        private int currentAttemptToConnect = 0;
        private Logging.Logger logger;

        [SerializeField]
        private HelpBox header = new HelpBox()
        {
            Text = "This script automatically connects to master server. Is is just a helper",
            Type = HelpBoxType.Info
        };

        [Tooltip("Log level of this script"), SerializeField]
        private LogLevel logLevel = LogLevel.Info;

        [Tooltip("If true, ip and port will be read from cmd args"), SerializeField]
        private bool readMasterServerAddressFromCmd = true;

        [Tooltip("Address to the server"), SerializeField]
        private string serverIp = "127.0.0.1";

        [Tooltip("Port of the server"), SerializeField]
        private int serverPort = 5000;

        [Header("Automation"), Tooltip("If true, will try to connect on the Start()"), SerializeField]
        private bool connectOnStart = false;

        [Header("Advanced"), SerializeField]
        private float minTimeToConnect = 0.5f;
        [SerializeField]
        private float maxTimeToConnect = 4f;
        [SerializeField]
        private float timeToConnect = 0.5f;
        [SerializeField]
        private int maxAttemptsToConnect = 5;

        [Header("Events")]
        /// <summary>
        /// Triggers when connected to master server
        /// </summary>
        public UnityEvent OnConnectedEvent;

        /// <summary>
        /// triggers when disconnected from master server
        /// </summary>
        public UnityEvent OnDisconnectedEvent;

        /// <summary>
        /// Main connection to master server
        /// </summary>
        public IClientSocket Connection => Msf.Connection;

        protected override void Awake()
        {
            base.Awake();

            logger = Msf.Create.Logger(typeof(ConnectionToMaster).Name);
            logger.LogLevel = logLevel;

            // In case this object is not at the root level of hierarchy
            // move it there, so that it won't be destroyed
            if (transform.parent != null)
            {
                transform.SetParent(null, false);
            }

            if (readMasterServerAddressFromCmd)
            {
                // If master IP is provided via cmd arguments
                if (Msf.Args.IsProvided(Msf.Args.Names.MasterIp))
                {
                    serverIp = Msf.Args.MasterIp;
                }

                // If master port is provided via cmd arguments
                if (Msf.Args.IsProvided(Msf.Args.Names.MasterPort))
                {
                    serverPort = Msf.Args.MasterPort;
                }
            }

            if (Msf.Args.AutoConnectClient)
            {
                connectOnStart = true;
            }
        }

        private void Start()
        {
            if (connectOnStart)
            {
                StartConnection();
            }
        }

        public void SetIpAddress(string serverIp)
        {
            this.serverIp = serverIp;
        }

        public void SetPort(int serverPort)
        {
            this.serverPort = serverPort;
        }

        public void StartConnection()
        {
            StartCoroutine(StartConnectionProcess(serverIp, serverPort, maxAttemptsToConnect));
        }

        public void StartConnection(int numberOfAttempts)
        {
            StartCoroutine(StartConnectionProcess(serverIp, serverPort, numberOfAttempts));
        }

        public void StartConnection(string serverIp, int serverPort, int numberOfAttempts = 5)
        {
            StartCoroutine(StartConnectionProcess(serverIp, serverPort, numberOfAttempts));
        }

        private IEnumerator StartConnectionProcess(string serverIp, int serverPort, int numberOfAttempts)
        {
            currentAttemptToConnect = 0;
            maxAttemptsToConnect = numberOfAttempts;

            // Wait a fraction of a second, in case we're also starting a master server at the same time
            yield return new WaitForSeconds(0.2f);

            if (!Connection.IsConnected)
                logger.Info($"Starting MSF Client... {Msf.Version}. Multithreading is: {(Msf.Runtime.SupportsThreads ? "On" : "Off")}");

            Connection.AddConnectionListener(OnConnectedEventHandler);

            while (true)
            {
                // If is already connected break cycle
                if (Connection.IsConnected)
                {
                    logger.Debug("Client is already connected. Stop connection process...");
                    yield break;
                }

                // If currentAttemptToConnect of attemts is equals maxAttemptsToConnect stop connection
                if (currentAttemptToConnect == maxAttemptsToConnect)
                {
                    logger.Info($"Client cannot to connect to MSF server at: {serverIp}:{serverPort}");
                    Connection.Disconnect();
                    yield break;
                }

                // If we got here, we're not connected
                if (Connection.IsConnecting)
                {
                    if (maxAttemptsToConnect > 0)
                    {
                        currentAttemptToConnect++;
                    }

                    logger.Info($"Retrying to connect to MSF server at: {serverIp}:{serverPort}");
                }
                else
                {
                    logger.Info($"Connecting to MSF server at: {serverIp}:{serverPort}");
                }

                if (!Connection.IsConnected)
                {
                    Connection.Connect(serverIp, serverPort);
                }

                // Give a few seconds to try and connect
                yield return new WaitForSeconds(timeToConnect);

                // If we're still not connected
                if (!Connection.IsConnected)
                {
                    timeToConnect = Mathf.Min(timeToConnect * 2, maxTimeToConnect);
                }
            }
        }

        private void OnDisconnectedEventHandler()
        {
            logger.Info($"Disconnected from MSF server");

            timeToConnect = minTimeToConnect;

            Connection.RemoveDisconnectionListener(OnDisconnectedEventHandler);

            OnDisconnectedEvent?.Invoke();
        }

        private void OnConnectedEventHandler()
        {
            logger.Info($"Connected to MSF server at: {serverIp}:{serverPort}");

            timeToConnect = minTimeToConnect;

            Connection.RemoveConnectionListener(OnConnectedEventHandler);
            Connection.AddDisconnectionListener(OnDisconnectedEventHandler);

            OnConnectedEvent?.Invoke();
        }

        private void OnApplicationQuit()
        {
            if (Connection != null)
            {
                Connection.Disconnect();
            }
        }
    }
}