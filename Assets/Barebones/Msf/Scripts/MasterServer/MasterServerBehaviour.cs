using Barebones.Logging;
using Barebones.Networking;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Starts the master server
    /// </summary>
    public class MasterServerBehaviour : ServerBehaviour
    {
        [Header("Master Server Settings"), SerializeField]
        private HelpBox hpInfo = new HelpBox()
        {
            Text = "This component is responsible for starting a Master Server and initializing its modules",
            Type = HelpBoxType.Info
        };

        [Header("Editor settings"), SerializeField]
        private bool autoStartInEditor = true;

        [SerializeField]
        private HelpBox hpEditor = new HelpBox()
        {
            Text = "Editor settings are used only while running in editor",
            Type = HelpBoxType.Warning
        };

        /// <summary>
        /// Singleton instance of the master server behaviour
        /// </summary>
        public static MasterServerBehaviour Instance { get; private set; }

        /// <summary>
        /// Invoked when master server started
        /// </summary>
        public static event Action<MasterServerBehaviour> OnMasterStartedEvent;

        /// <summary>
        /// Invoked when master server stopped
        /// </summary>
        public static event Action<MasterServerBehaviour> OnMasterStoppedEvent;

        protected override void Awake()
        {
            base.Awake();

            // If instance of the server is already running
            if (Instance != null)
            {
                // Destroy, if this is not the first instance
                Destroy(gameObject);
                return;
            }

            // Create new instance
            Instance = this;

            // Move to root, so that it won't be destroyed
            // In case this MSF instance is a child of another gameobject
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }

            // Set server behaviour to be able to use in all levels
            DontDestroyOnLoad(gameObject);

            // Check is command line argument '-msfMasterPort' is defined
            if (Msf.Args.IsProvided(Msf.Args.Names.MasterPort))
            {
                port = Msf.Args.MasterPort;
            }
        }

        protected virtual void Start()
        {
            // Start master server at start
            if (Msf.Args.StartMaster || (Msf.Runtime.IsEditor && autoStartInEditor))
            {
                // Start the master server on next frame
                MsfTimer.WaitForEndOfFrame(() => {
                    StartServer(port);
                });
            }
        }

        /// <summary>
        /// Start master server with given <see cref="port"/>
        /// </summary>
        public void StartServer()
        {
            StartServer(port);
        }

        /// <summary>
        /// Start master server with given port
        /// </summary>
        /// <param name="port"></param>
        public override void StartServer(int port)
        {
            // If master is allready running then return function
            if (IsRunning)
            {
                return;
            }

            logger.Info($"Starting Master Server... {Msf.Version}");

            base.StartServer(port);
        }

        protected override void OnStartedServer()
        {
            logger.Info($"Master Server is started and listening port: {port}");

            base.OnStartedServer();

            OnMasterStartedEvent?.Invoke(this);
        }

        protected override void OnStoppedServer()
        {
            logger.Info("Master Server is stopped");

            OnMasterStoppedEvent?.Invoke(this);
        }
    }
}